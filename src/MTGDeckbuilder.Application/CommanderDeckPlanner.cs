using MTGDeckbuilder.Domain;

namespace MTGDeckbuilder.Application;

public static class CommanderDeckPlanner
{
    public static bool FreshLegal(PaperCard c, DateTime now) => c.CommanderLegality == "legal" && c.FetchedUtc <= now.AddMinutes(5) && now - c.FetchedUtc <= TimeSpan.FromDays(1);
    public static IReadOnlyList<PaperDeckLine> Create(PaperCard commander, IReadOnlyList<PaperCard> known, IReadOnlyList<PaperOwned> stock, bool planning, DateTime now, DeckWizardRequest? preferences = null)
    {
        if (now < PaperDeckRules.CheckedUtc || now - PaperDeckRules.CheckedUtc > TimeSpan.FromDays(14)) throw new ArgumentException("Commander-regelcontrole verlopen; onderhoud nodig.");
        if(SynergyProfiles.ComplexCard(commander))throw new ArgumentException("Deze commander heeft meerdere kanten/modi; automatische analyse per kant wordt nog niet ondersteund. Kies een enkelzijdige commander.");
        if(SynergyProfiles.AutomaticSelectionIssue(commander) is {} issue)throw new ArgumentException("Commander uitgesloten van automatische selectie: "+issue);
        if (!commander.IsCommanderCandidate || !FreshLegal(commander, now)) throw new ArgumentException("Kies één ondersteunde, legale commander met actuele Scryfall-gegevens. Geen partner/background of variabele kleurkeuze.");
        var owned = stock.GroupBy(x => x.Card.OracleId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
        var exact = stock.ToDictionary(x => x.Card.Id, x => x.Quantity);
        if (!planning && owned.GetValueOrDefault(commander.OracleId) < 1) throw new ArgumentException("Commander niet in actief fysiek bezit; schakel alleen bewust planning in.");
        var eligible = known.Where(c => SynergyProfiles.AutomaticSelectionIssue(c) is null&&FreshLegal(c, now) && PaperDeckRules.ColorsFit(c, commander) && !c.OracleText.Contains("A deck can have", StringComparison.OrdinalIgnoreCase)).ToArray();
        var conflicting=eligible.GroupBy(c=>c.OracleId).Where(g=>g.Select(SynergyProfiles.Fingerprint).Distinct().Count()>1).Select(g=>g.Key).ToHashSet();
        eligible=eligible.Where(c=>!conflicting.Contains(c.OracleId)).ToArray();
        var groups = eligible.GroupBy(c => c.OracleId).ToDictionary(g => g.Key, g => g.OrderByDescending(c => exact.GetValueOrDefault(c.Id)).ThenByDescending(c => c.Id == commander.Id).ThenBy(c => c.Id).ToArray());
        if (!groups.TryGetValue(commander.OracleId, out var leaderPrints)) throw new ArgumentException("Commander ontbreekt in de gecontroleerde catalogus.");
        var leader = leaderPrints.First(); var result = new List<PaperDeckLine> { new(leader, DeckZone.Commander, 1, owned.GetValueOrDefault(leader.OracleId)) };
        var spells = groups.Values.Select(g => g[0]).Where(c => c.OracleId != commander.OracleId && !c.TypeLine.Contains("Land", StringComparison.Ordinal) && (planning || owned.GetValueOrDefault(c.OracleId) > 0)).ToList();
        if (spells.Count < 62) throw new ArgumentException($"Slechts {spells.Count}/62 geschikte verschillende niet-landen binnen de commander-kleuridentiteit. Het vaste plan kan niet uit dit bezit worden gemaakt; planning kan gericht bij Scryfall aanvullen.");
        int[] curve = [2, 3, 4, 2, 3, 4, 5, 6, 1, 7];
        var selection=new SynergySelection(eligible.Where(c=>planning||owned.GetValueOrDefault(c.OracleId)>0),62,preferences,commander);
        for (var i = 0; i < 62; i++)
        {
            var desired = preferences?.Style=="aggro"?2:preferences?.Style=="control"?4:curve[i % curve.Length];
            var card = spells.OrderByDescending(c=>selection.Score(c)+(owned.GetValueOrDefault(c.OracleId)>0?1.5:0))
                .ThenBy(c => Math.Abs(c.ManaValue - desired)).ThenBy(c => c.Name, StringComparer.Ordinal).ThenBy(c => c.OracleId, StringComparer.Ordinal).First();
            spells.Remove(card); result.Add(new(card, DeckZone.Mainboard, 1, owned.GetValueOrDefault(card.OracleId)));
            selection.Pick(card);
        }
        var lands = groups.Values.Where(g => g[0].IsBasicLand && g[0].Colors.Length <= 1).ToArray();
        var used = new Dictionary<Guid, int>(); var usedOracle = new Dictionary<string, int>(); var colors = "WUBRGC";
        var pips = colors.ToDictionary(x => x, _ => 0); var chosen = colors.ToDictionary(x => x, _ => 0);
        pips=DeckManaHeuristics.Demand(result.Select(row=>(row.Card,row.Quantity)));
        if(preferences?.Theme!="domain"&&pips.Values.Any(n=>n>0))
        {
            var needed=lands.Where(g=>pips[g[0].Colors.Length==0?'C':g[0].Colors[0]]>0).ToArray();
            if(needed.Length>0)lands=needed;
        }
        for (var i = 0; i < 37; i++)
        {
            var group = lands.Where(g => planning || owned.GetValueOrDefault(g[0].OracleId) > usedOracle.GetValueOrDefault(g[0].OracleId))
                .OrderByDescending(g => { var color = g[0].Colors.Length == 0 ? 'C' : g[0].Colors[0]; return (pips[color] + 1d) / (chosen[color] + 1d); }).ThenBy(g => g[0].Name, StringComparer.Ordinal).FirstOrDefault();
            if (group is null) throw new ArgumentException("Onvoldoende passende basislanden voor het vaste Commander-plan van 37 landen. Geen fictief bezit toegevoegd.");
            var c = group.FirstOrDefault(c => exact.GetValueOrDefault(c.Id) > used.GetValueOrDefault(c.Id)) ?? group[0];
            used[c.Id] = used.GetValueOrDefault(c.Id) + 1; usedOracle[c.OracleId] = usedOracle.GetValueOrDefault(c.OracleId) + 1; chosen[c.Colors.Length == 0 ? 'C' : c.Colors[0]]++;
        }
        var byId = eligible.ToDictionary(c => c.Id);
        result.AddRange(used.Select(x => new PaperDeckLine(byId[x.Key], DeckZone.Mainboard, x.Value, owned.GetValueOrDefault(byId[x.Key].OracleId))));
        return result.OrderBy(c => c.Zone).ThenBy(c => c.Card.Name, StringComparer.Ordinal).ThenBy(c => c.Card.Id).ToArray();
    }
}
