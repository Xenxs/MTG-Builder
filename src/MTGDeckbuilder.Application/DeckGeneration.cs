using MTGDeckbuilder.Domain;

namespace MTGDeckbuilder.Application;

public enum GenerationMode { OwnedOnly, Planning }
public sealed record GenerationTarget(string Key, string Label, int Required, IReadOnlyList<AggroSlot> Slots);
public sealed record GenerationGroup(string Label, int Required, int Selected, int Available)
{
    public string LabelText => $"{Label}: {Selected}/{Required} gekozen · {Available} beschikbaar binnen deze modus";
}
public sealed record GeneratedLine(AdvicePrinting Printing, ArchetypePoolEntry Entry, int Quantity, int Owned)
{
    public int Missing => Math.Max(0, Quantity - Owned);
    public string Label => $"{Quantity}× {Printing.Name} ({Printing.SetCode} {Printing.CollectorNumber}) · bezit {Owned} · tekort {Missing}";
    public string Details => $"{Entry.SlotLabel} · {Entry.Purpose}\n{Entry.PreferenceLabel} · gedrukte kosten {Entry.ManaCost}\n" +
        $"Voorwaarden: {Entry.Cautions}\n{Entry.OracleText}\nOracle-ID: {Entry.OracleId}\nPrinting: {Printing.Id}";
}
public sealed record GeneratedDeck(string Fingerprint, GenerationMode Mode, ArchetypePoolReport Pool,
    IReadOnlyList<GeneratedLine> Lines, IReadOnlyList<GenerationGroup> Groups, IReadOnlyList<string> Problems, DeckValidation Validation)
{
    public int Count => Lines.Sum(x => x.Quantity);
    public int Missing => Lines.Sum(x => x.Missing);
    public int OneDrops => Lines.Where(x => x.Entry.Slot != AggroSlot.Land && x.Entry.ManaValue == 1).Sum(x => x.Quantity);
    public double AverageMana => Lines.Where(x => x.Entry.Slot != AggroSlot.Land).Sum(x => x.Quantity * x.Entry.ManaValue) /
        Math.Max(1, Lines.Where(x => x.Entry.Slot != AggroSlot.Land).Sum(x => x.Quantity));
    public bool CanPreview => Problems.Count == 0 && Validation.Passed && Count == 60;
    public string Summary => CanPreview ? $"60 kaarten · Arena Standard BO1 · {Missing} ontbrekende exemplaren" : $"Geen compleet voorstel · {Count}/60 kaarten geselecteerd";
    public string ModeLabel => Mode == GenerationMode.OwnedOnly ? "Alleen geregistreerd bezit" : "PLANNING — mag ontbrekende kaarten bevatten";
    public string Curve => $"22 Mountains als vast manaplan · 20 creatures / 18 overige spreuken bij compleet voorstel\n" +
        $"1 mana: {OneDrops} · 2 mana: {Lines.Where(x => x.Entry.Slot != AggroSlot.Land && x.Entry.ManaValue == 2).Sum(x => x.Quantity)} · " +
        $"3+ mana: {Lines.Where(x => x.Entry.Slot != AggroSlot.Land && x.Entry.ManaValue >= 3).Sum(x => x.Quantity)} · gemiddeld {AverageMana:0.00}\n" +
        "Gedrukte kosten; extra kosten, mulligans, timing en spelsterkte worden niet gesimuleerd.";
    public string Status => string.Join("\n", Problems.Concat(Validation.Issues.Select(x => x.Message)).Distinct());
    public string Shortages => (!CanPreview ? "Onvolledig plan: los eerst de bovenstaande blokkades op. Dit is nog geen compleet deck.\n" : "") +
        (Missing == 0 ? (CanPreview ? "Geen ontbrekende exemplaren binnen dit voorstel. Bezit wordt niet gereserveerd voor andere decks."
            : "De geselecteerde deelvoorraad heeft geen kaarttekorten; ontbrekende functies staan hierboven.")
        : string.Join("\n", Lines.Where(x => x.Missing > 0).Select(x => $"{x.Missing}× {x.Printing.Name} ({x.Printing.SetCode} {x.Printing.CollectorNumber})")));
}
public sealed record GenerationPreview(Guid Id, string Name, GeneratedDeck Deck);
public interface IDeckGenerationStore
{
    Task<GeneratedDeck> CalculateAsync(GenerationMode mode, CancellationToken ct = default);
    Task<GenerationPreview> PreviewAsync(string fingerprint, GenerationMode mode, string name, CancellationToken ct = default);
    Task<Guid> ApplyAsync(Guid previewId, CancellationToken ct = default);
}

public static class DeckGenerationEngine
{
    public const string Version = "Mono-red fixed plan v1";
    public static IReadOnlyList<GenerationTarget> Targets { get; } = Array.AsReadOnly(new GenerationTarget[]
    {
        new("land", "Mountains", 22, [AggroSlot.Land]),
        new("early", "Vroege creatures", 12, [AggroSlot.EarlyThreat]),
        new("attacker", "Spell-aanvallers", 4, [AggroSlot.SpellAttacker]),
        new("ping", "Pingers / enters-burn", 4, [AggroSlot.SpellPinger, AggroSlot.ReachCreature]),
        new("burn", "Burn naar tegenstander", 8, [AggroSlot.BurnReach]),
        new("interaction", "Creature-interactie", 2, [AggroSlot.CreatureInteraction]),
        new("combat", "Combat-spreuken", 6, [AggroSlot.CombatSupport]),
        new("flow", "Hand verversen", 2, [AggroSlot.CardFlow])
    });
    private sealed record Copy(ArchetypePoolCard Card, int Index)
    {
        public int Missing => Index > Card.OwnedEligible ? 1 : 0;
    }
    public static GeneratedDeck Build(ArchetypePoolInput input, string fingerprint, GenerationMode mode, DateTime now)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentException("Onbekende voorstelmodus.");
        var pool = ArchetypePoolEngine.Evaluate(MonoRedPool.Current, input, now);
        var selected = new List<Copy>(); var groups = new List<GenerationGroup>(); var problems = new List<string>();
        var options = new Dictionary<string, Copy[]>();
        foreach (var target in Targets)
        {
            var copies = pool.Cards.Where(x => x.Eligible && target.Slots.Contains(x.Entry.Slot)).SelectMany(x =>
                Enumerable.Range(1, mode == GenerationMode.Planning ? (target.Key == "land" ? 22 : 4)
                    : Math.Min(x.OwnedEligible, target.Key == "land" ? 22 : 4)).Select(i => new Copy(x, i)));
            var ranked = copies.OrderBy(x => x.Missing).ThenBy(x => x.Card.Entry.Preference).ThenBy(x => x.Card.Entry.ManaValue)
                .ThenBy(x => x.Card.Entry.Name, StringComparer.Ordinal).ThenBy(x => x.Card.Entry.OracleId, StringComparer.Ordinal).ThenBy(x => x.Index).ToArray();
            options[target.Key] = ranked; selected.AddRange(ranked.Take(target.Required));
            groups.Add(new(target.Label, target.Required, Math.Min(ranked.Length, target.Required), ranked.Length));
            if (ranked.Length < target.Required) problems.Add($"{target.Label}: {target.Required - ranked.Length} exemplaren te weinig binnen het vaste plan. " +
                (mode == GenerationMode.OwnedOnly ? "Registreer geschikt bezit of kies expliciet planning." : "Controleer pool- en bronstatus."));
        }
        // Repair only the declared curve constraint, within the same function and Oracle copy limit.
        while (selected.Count(x => x.Card.Entry.Slot != AggroSlot.Land && x.Card.Entry.ManaValue == 1) < 12)
        {
            var swaps = Targets.Where(x => x.Key != "land").SelectMany(target =>
            {
                var available = options[target.Key].Where(x => x.Card.Entry.ManaValue == 1 && !selected.Contains(x))
                    .GroupBy(x => x.Card.Entry.OracleId).Select(x => x.MinBy(c => c.Index)!);
                var removable = selected.Where(x => target.Slots.Contains(x.Card.Entry.Slot) && x.Card.Entry.ManaValue != 1)
                    .GroupBy(x => x.Card.Entry.OracleId).Select(x => x.MaxBy(c => c.Index)!);
                return available.SelectMany(add => removable.Select(remove => (Add: add, Remove: remove)));
            }).OrderBy(x => x.Add.Missing - x.Remove.Missing)
                .ThenBy(x => x.Add.Card.Entry.Preference - x.Remove.Card.Entry.Preference)
                .ThenBy(x => x.Add.Card.Entry.Name, StringComparer.Ordinal).ThenBy(x => x.Add.Index)
                .ThenBy(x => x.Remove.Card.Entry.Name, StringComparer.Ordinal).ToArray();
            if (swaps.Length == 0) break;
            selected.Remove(swaps[0].Remove); selected.Add(swaps[0].Add);
        }
        var stock = input.Stock.ToDictionary(x => x.PrintingId, x => Math.Max(0, x.Quantity));
        var lines = new List<GeneratedLine>();
        foreach (var chosen in selected.GroupBy(x => x.Card.Entry.OracleId).OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            var entry = chosen.First().Card.Entry; var remaining = chosen.Count();
            var printings = input.Printings.Where(x => x.AvailableOnArena && entry.Matches(x))
                .OrderByDescending(x => stock.GetValueOrDefault(x.Id)).ThenBy(x => x.SetCode, StringComparer.Ordinal)
                .ThenBy(x => x.CollectorNumber, StringComparer.Ordinal).ThenBy(x => x.Id).ToArray();
            foreach (var printing in printings)
            {
                var quantity = Math.Min(remaining, stock.GetValueOrDefault(printing.Id));
                if (quantity > 0) lines.Add(new(printing, entry, quantity, stock.GetValueOrDefault(printing.Id)));
                remaining -= quantity; if (remaining == 0) break;
            }
            if (remaining > 0)
            {
                var printing = printings[0]; var index = lines.FindIndex(x => x.Printing.Id == printing.Id);
                if (index < 0) lines.Add(new(printing, entry, remaining, stock.GetValueOrDefault(printing.Id)));
                else lines[index] = lines[index] with { Quantity = lines[index].Quantity + remaining };
            }
        }
        var ordered = lines.OrderBy(x => x.Entry.Slot).ThenBy(x => x.Printing.Name, StringComparer.Ordinal).ThenBy(x => x.Printing.Id).ToArray();
        var validation = StandardDeckValidator.Validate(DeckRuleProfile.ArenaStandardBo1,
            ordered.Select(x => x.Printing.Validate(DeckZone.Mainboard, x.Quantity)).ToArray(), input.Source, now);
        var result = new GeneratedDeck(fingerprint, mode, pool, ordered, groups, problems, validation);
        if (result.OneDrops < 12) problems.Add($"Curve: slechts {result.OneDrops}/12 vereiste 1-mana-spreuken binnen deze functies.");
        if (result.AverageMana > 2) problems.Add("Curve: gemiddelde gedrukte niet-land-manawaarde is hoger dan 2.");
        if (mode == GenerationMode.OwnedOnly && result.Missing > 0) problems.Add("Voorraadtoewijzing bevat een tekort; bereken opnieuw.");
        return result;
    }
}
