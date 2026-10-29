using MTGDeckbuilder.Domain;

namespace MTGDeckbuilder.Application;

public static class PaperDeckRules
{
    public static readonly DateTime CheckedUtc = new(2026, 10, 27, 0, 0, 0, DateTimeKind.Utc);
    public static PaperDeckReport Evaluate(PaperDeckInfo deck, Guid version, IReadOnlyList<PaperDeckLine> lines, DateTime now)
    {
        var problems = new List<string>();
        var commanderMode = deck.Profile == "commander";
        var main = lines.Where(x => x.Zone == DeckZone.Mainboard).Sum(x => x.Quantity);
        var commanders = lines.Where(x => x.Zone == DeckZone.Commander).ToArray();
        PaperCard? leader = commanders.Length == 1 && commanders[0].Quantity == 1 ? commanders[0].Card : null;
        if (commanderMode)
        {
            if (lines.Sum(x => x.Quantity) != 100 || main != 99) problems.Add($"Commander vereist exact 100 kaarten inclusief één commander en 99 librarykaarten; nu {lines.Sum(x => x.Quantity)}/{main}.");
            if (leader is null) problems.Add("Kies precies één commander met aantal één. Partner-/background-paren worden niet ondersteund.");
            else if (!leader.IsCommanderCandidate) problems.Add("De gekozen kaart is geen automatisch ondersteunde commander; controleer type en kaartgebonden uitzonderingen.");
            if (lines.Any(x => x.Zone == DeckZone.Sideboard)) problems.Add("Commander heeft geen sideboard.");
        }
        else
        {
            if (main < 60) problems.Add($"Mainboard {main}/60. Historische Conflux-introdecks bevatten 41 kaarten; geen automatische aanvulling.");
            if (lines.Where(x => x.Zone == DeckZone.Sideboard).Sum(x => x.Quantity) > 15) problems.Add("Sideboard groter dan 15.");
            if (commanders.Length > 0) problems.Add("Dit 60-kaartprofiel ondersteunt geen commander-zone.");
        }
        if ((commanderMode || deck.Profile == "standard") && (now < CheckedUtc || now - CheckedUtc > TimeSpan.FromDays(14)))
            problems.Add("Papieren regelcontrole ouder dan veertien dagen; opnieuw onderhoud nodig.");
        foreach (var group in lines.GroupBy(x => x.Card.OracleId))
        {
            var c = group.First().Card; var quantity = group.Sum(x => x.Quantity); var owned = group.Max(x => x.Owned);
            if (quantity > owned) problems.Add($"{c.Name}: nodig {quantity}, geregistreerd bezit {owned}, tekort {quantity - owned}. Andere decks reserveren geen bezit.");
            var basic = c.IsBasicLand;
            if (!basic && c.OracleText.Contains("A deck can have", StringComparison.OrdinalIgnoreCase))
                problems.Add($"{c.Name}: kaartgebonden deckbouwuitzondering vereist handmatige controle; geen geslaagd oordeel.");
            else if (!basic && quantity > (commanderMode ? 1 : 4)) problems.Add($"{c.Name}: {quantity} exemplaren over printings/zones; {(commanderMode ? "singleton, maximaal één" : "maximaal vier")}.");
            if (commanderMode && leader is not null)
            {
                foreach (var printing in group.Select(x => x.Card))
                    if (!ColorsFit(printing, leader)) problems.Add($"{c.Name}: kleuridentiteit of basislandtype valt buiten {leader.Name} ({leader.Colors}).");
            }
            if (deck.Profile == "standard" || commanderMode)
            {
                foreach (var printing in group.Select(x => x.Card))
                {
                    if (now - printing.FetchedUtc > TimeSpan.FromDays(1) || printing.FetchedUtc > now.AddMinutes(5)) problems.Add($"{c.Name}: Scryfall-legaliteitsgegevens niet actueel; kaart vernieuwen.");
                    var legality = commanderMode ? printing.CommanderLegality : printing.Standard;
                    if (legality != "legal") problems.Add($"{c.Name}: {(commanderMode ? "Commander" : "Standard (papier)")} = {legality}; controle niet geslaagd.");
                }
            }
        }
        var nonlands = lines.Where(x => x.Zone == DeckZone.Mainboard && !x.Card.TypeLine.Contains("Land", StringComparison.Ordinal)).ToArray();
        var count = nonlands.Sum(x => x.Quantity);
        var synergy=SynergyEngine.Assess(lines);
        var commanderPlan=leader is null?"":"\n"+new CommanderBuildPlan(SynergyProfiles.Analyze(leader)).Describe(nonlands.Select(x=>x.Card).DistinctBy(c=>c.OracleId).ToArray());
        var domain = lines.Where(x => SynergyProfiles.Analyze(x.Card).Has(SynergyFeature.DomainPayoff)).Sum(x => x.Quantity);
        var exalted = lines.Where(x => SynergyProfiles.Analyze(x.Card).Has(SynergyFeature.Exalted)).Sum(x => x.Quantity);
        var landTypes = synergy.BasicTypes;
        var curve = string.Join(" · ", nonlands.GroupBy(x => Math.Min(7, (int)x.Card.ManaValue)).OrderBy(x => x.Key).Select(x => $"{x.Key}{(x.Key == 7 ? "+" : "")} mana: {x.Sum(c => c.Quantity)}"));
        var analysis = $"Profiel: {(commanderMode ? "Commander, één commander; geen brackets-/powerlevelclaim" : deck.Profile == "standard" ? "Standard fysiek" : "Casual: alleen 60-kaartstructuur, geen toernooilegaliteitsclaim")}\n" +
            $"Gemiddelde gedrukte niet-land-MV: {(count == 0 ? 0 : nonlands.Sum(x => x.Quantity * x.Card.ManaValue) / count):0.00}\n{curve}\n" +
            $"Mechaniekvermeldingen: domain {domain}, exalted {exalted}. Basislandtypen in deck: {string.Join(", ", landTypes)}.\n" +
            "Dit beschrijft kaarttekst en deckinhoud. Landtypen in het deck zijn niet gegarandeerd op het slagveld; exalted beloont alleen aanvallen, domain telt aanwezige basislandtypen. Geen spel-/winkanssimulatie of bewezen synergie-optimalisatie.";
        return new(deck, version, lines, problems.Distinct().ToArray(), analysis+commanderPlan, synergy);
    }
    public static bool ColorsFit(PaperCard card, PaperCard commander)
    {
        if (!card.Colors.All(commander.Colors.Contains)) return false;
        var types = new[] { "Plains", "Island", "Swamp", "Mountain", "Forest" }; var colors = "WUBRG";
        var words = card.TypeLine.Split([" ", "—", "//"], StringSplitOptions.RemoveEmptyEntries);
        return !card.TypeLine.Contains("Land", StringComparison.Ordinal) || !types.Where((t, i) => words.Contains(t) && !commander.Colors.Contains(colors[i])).Any();
    }
}
