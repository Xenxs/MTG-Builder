using MTGDeckbuilder.Domain;

namespace MTGDeckbuilder.Application;

/// <summary>Limited role recognition followed by structural matching; no strategic equivalence claim.</summary>
public static class DeckAdviceEngine
{
    public const string Version = "Role alternatives v4 / " + CardRoleClassifier.Version;
    private static readonly string[] Types = ["Creature", "Instant", "Sorcery", "Artifact", "Enchantment", "Planeswalker", "Battle"];

    public static DeckAdvice Build(AdviceSnapshot snapshot, string fingerprint, IReadOnlyList<AdviceChoice> choices, DateTime now)
    {
        var cards = snapshot.Printings.ToDictionary(x => x.Id);
        DeckValidation Validate(IEnumerable<AdviceLine> lines) => StandardDeckValidator.Validate(snapshot.Profile,
            lines.Select(x => cards[x.PrintingId].Validate(x.Zone, x.Quantity)).ToArray(), snapshot.Source, now);
        var baseline = Validate(snapshot.Lines);
        if (!baseline.Passed)
        {
            if (choices.Count > 0) throw new ArgumentException("Het oorspronkelijke deck is niet volledig goedgekeurd. Ververs het advies en herstel de formaatcontrole.");
            return new(snapshot.DeckId, fingerprint, snapshot.Name, baseline, baseline, [], [], snapshot.Lines);
        }
        var shortages = Needs(snapshot.Lines, snapshot.Stock, cards).ToDictionary(x => (x.PrintingId, x.Zone), x => x.Missing);
        var remaining = snapshot.Stock.ToDictionary(x => x.PrintingId, x => Math.Max(0,
            x.Quantity - snapshot.Lines.Where(l => l.PrintingId == x.PrintingId).Sum(l => l.Quantity)));
        var quantities = snapshot.Lines.ToDictionary(x => (x.PrintingId, x.Zone), x => x.Quantity);
        var changes = new List<AdviceChange>();
        if (choices.Count > 500) throw new ArgumentException("Te veel vervangingen in één voorstel.");
        foreach (var choice in choices)
        {
            if (choice.Quantity <= 0 || choice.FromPrintingId == choice.ToPrintingId || !Enum.IsDefined(choice.Zone) ||
                !cards.TryGetValue(choice.FromPrintingId, out var from) || !cards.TryGetValue(choice.ToPrintingId, out var to) ||
                !Eligible(from, to, choice.Zone)) throw new ArgumentException("Deze vervanging valt buiten de ondersteunde kaartalternatieven.");
            var key = (choice.FromPrintingId, choice.Zone);
            if (shortages.GetValueOrDefault(key) < choice.Quantity)
                throw new ArgumentException("Vervang alleen ontbrekende exemplaren; deze regel is al ingevuld of heeft onvoldoende tekort.");
            if (remaining.GetValueOrDefault(choice.ToPrintingId) < choice.Quantity)
                throw new ArgumentException("Onvoldoende vrije exemplaren in je collectie. Alle deckzones delen dezelfde voorraad.");
            shortages[key] -= choice.Quantity; remaining[choice.ToPrintingId] -= choice.Quantity;
            quantities[key] -= choice.Quantity;
            var target = (choice.ToPrintingId, choice.Zone);
            quantities[target] = quantities.GetValueOrDefault(target) + choice.Quantity;
            changes.Add(new(choice, from.Label, to.Label, CardRoleClassifier.Compare(from, to)));
        }
        var resultLines = quantities.Where(x => x.Value > 0).OrderBy(x => x.Key.Zone).ThenBy(x => x.Key.PrintingId)
            .Select(x => new AdviceLine(x.Key.PrintingId, x.Key.Zone, x.Value)).ToArray();
        var result = Validate(resultLines);
        if (!result.Passed) throw new ArgumentException("De gekozen combinatie doorstaat de formaatcontrole niet: " +
            string.Join(" ", result.Issues.Select(x => x.Card + ": " + x.Message)));
        return new(snapshot.DeckId, fingerprint, snapshot.Name, baseline, result, Needs(resultLines, snapshot.Stock, cards), changes, resultLines);
    }

    public static IReadOnlyList<AdviceCandidate> Candidates(AdviceSnapshot snapshot, string fingerprint,
        IReadOnlyList<AdviceChoice> choices, Guid printingId, DeckZone zone, DateTime now, CancellationToken ct = default)
    {
        var advice = Build(snapshot, fingerprint, choices, now);
        if (!advice.Baseline.Passed) return [];
        var need = advice.Needs.SingleOrDefault(x => x.PrintingId == printingId && x.Zone == zone);
        if (need is null) return [];
        var from = snapshot.Printings.Single(x => x.Id == printingId);
        var stock = snapshot.Stock.ToDictionary(x => x.PrintingId, x => x.Quantity);
        var results = new List<(AdvicePrinting Card, AdviceCandidate Candidate)>();
        foreach (var to in snapshot.Printings.Where(x => x.Id != printingId && Eligible(from, x, zone)))
        {
            ct.ThrowIfCancellationRequested();
            var free = stock.GetValueOrDefault(to.Id) - advice.Lines.Where(x => x.PrintingId == to.Id).Sum(x => x.Quantity);
            var max = Math.Min(need.Missing, free);
            if (max <= 0) continue;
            // Valid counts form a prefix: size is unchanged and only the target copy limit can increase.
            var low = 0; var high = max;
            while (low < high)
            {
                var mid = low + (high - low + 1) / 2;
                try { Build(snapshot, fingerprint, [.. choices, new(printingId, zone, to.Id, mid)], now); low = mid; }
                catch (ArgumentException) { high = mid - 1; }
            }
            if (low == 0) continue;
            var same = SameCard(from, to);
            var role = CardRoleClassifier.Compare(from, to);
            var reason = same ? "Dezelfde geverifieerde Oracle-kaart; alleen de uitgave verandert."
                : $"Kleuridentiteit {Colors(from)} → {Colors(to)}; type {from.TypeLine} → {to.TypeLine}; manawaarde {from.ManaValue:0.##} → {to.ManaValue:0.##}. Overeenkomst bewijst geen gelijkwaardig effect.";
            results.Add((to, new(to.Id, to.Label, low, same, reason,
                $"Origineel: {from.OracleText}\nAlternatief: {to.OracleText}", role)));
        }
        return results.OrderByDescending(x => x.Candidate.SameCard)
            .ThenBy(x => x.Candidate.Role!.Fit)
            .ThenByDescending(x => x.Candidate.Role!.Source.Complete && x.Candidate.Role.Candidate.Complete)
            .ThenByDescending(x => x.Card.ColorIdentity == from.ColorIdentity)
            .ThenBy(x => Math.Abs(x.Card.ManaValue - from.ManaValue))
            .ThenBy(x => CardTypes(from).Union(CardTypes(x.Card)).Count() - CardTypes(from).Intersect(CardTypes(x.Card)).Count())
            .ThenBy(x => x.Card.Name, StringComparer.Ordinal).ThenBy(x => x.Card.SetCode, StringComparer.Ordinal)
            .ThenBy(x => x.Card.CollectorNumber, StringComparer.Ordinal).ThenBy(x => x.Card.Id)
            .Select(x => x.Candidate).ToArray();
    }

    private static bool SameCard(AdvicePrinting a, AdvicePrinting b) => Guid.TryParse(a.OracleId, out var id) && id != Guid.Empty && a.OracleId == b.OracleId;
    private static bool Eligible(AdvicePrinting from, AdvicePrinting to, DeckZone zone)
    {
        if (!to.AvailableOnArena || to.Standard != CardLegality.Legal || !Guid.TryParse(to.OracleId, out var id) || id == Guid.Empty ||
            string.IsNullOrWhiteSpace(to.TypeLine) || to.OracleText is null) return false;
        if (SameCard(from, to)) return true;
        return zone == DeckZone.Mainboard && !IsLand(from) && !IsLand(to) &&
            to.ColorIdentity.All(from.ColorIdentity.Contains) && CardTypes(from).Intersect(CardTypes(to)).Any();
    }
    // Any land face excludes strategic replacement, including modal double-faced lands.
    private static bool IsLand(AdvicePrinting card) => (card.TypeLine ?? "").Split(" // ").Any(x => x.Split('—')[0].Split(' ').Contains("Land"));
    private static IEnumerable<string> CardTypes(AdvicePrinting card) => Types.Intersect((card.TypeLine ?? "").Split(" // ")[0].Split('—')[0].Split(' '));
    private static string Colors(AdvicePrinting card) => card.ColorIdentity.Length == 0 ? "kleurloos" : card.ColorIdentity;
    private static AdviceNeed[] Needs(IEnumerable<AdviceLine> lines, IReadOnlyList<AdviceStock> stock, IReadOnlyDictionary<Guid, AdvicePrinting> cards)
    {
        var left = stock.ToDictionary(x => x.PrintingId, x => x.Quantity);
        var needs = new List<AdviceNeed>();
        // Preserve exact sideboard stock first; missing mainboard cards offer broader replacement options.
        foreach (var line in lines.OrderByDescending(x => x.Zone).ThenBy(x => x.PrintingId))
        {
            var allocated = Math.Min(line.Quantity, left.GetValueOrDefault(line.PrintingId));
            left[line.PrintingId] = left.GetValueOrDefault(line.PrintingId) - allocated;
            if (allocated < line.Quantity) needs.Add(new(line.PrintingId, line.Zone, cards[line.PrintingId].Label, line.Quantity - allocated));
        }
        return needs.OrderBy(x => x.Zone).ThenBy(x => x.Name, StringComparer.Ordinal).ThenBy(x => x.PrintingId).ToArray();
    }
}
