using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MTGDeckbuilder.Application;

/// <summary>A reproducible construction exercise from a frozen product pool, not a strength optimizer.</summary>
public static partial class PhysicalDeckPlanner
{
    [GeneratedRegex(@"\{([WUBRG])\}")]
    private static partial Regex ColoredPips();
    public static string Signature(PaperCard c) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
        new { c.OracleId, c.Name, c.ManaCost, c.ManaValue, c.TypeLine, c.OracleText, c.Colors })));

    public static IReadOnlyList<PaperDeckLine> Create(IReadOnlyList<PaperCard> known, IReadOnlyList<PaperOwned> stock,
        IReadOnlyList<PaperCard> reference, bool planning, DeckWizardRequest? preferences = null)
    {
        var pool = reference.GroupBy(x => x.OracleId).ToDictionary(g => g.Key, g => Signature(g.First()));
        var changed = known.Where(c => pool.TryGetValue(c.OracleId, out var signature) && signature != Signature(c)).Select(x => x.OracleId).ToHashSet();
        var owned = stock.GroupBy(x => x.Card.OracleId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
        var exact = stock.ToDictionary(x => x.Card.Id, x => x.Quantity);
        var candidates = known.Where(c => SynergyProfiles.AutomaticSelectionIssue(c) is null&&pool.TryGetValue(c.OracleId, out var signature) && signature == Signature(c) && !changed.Contains(c.OracleId)).ToArray();
        var groups = candidates.GroupBy(x => x.OracleId).ToDictionary(g => g.Key, g => g.OrderByDescending(c => exact.GetValueOrDefault(c.Id)).ThenBy(c => c.Id).ToArray());
        var remaining = groups.Keys.ToDictionary(id => id, id => planning ? 9999 : owned.GetValueOrDefault(id));
        var used = new Dictionary<string, int>(); var picked = new Dictionary<Guid, int>();
        void Pick(string oracle)
        {
            var card = groups[oracle].FirstOrDefault(c => exact.GetValueOrDefault(c.Id) > picked.GetValueOrDefault(c.Id)) ?? groups[oracle][0];
            picked[card.Id] = picked.GetValueOrDefault(card.Id) + 1; used[oracle] = used.GetValueOrDefault(oracle) + 1; remaining[oracle]--;
        }
        var spells = groups.Values.Select(g => g[0]).Where(c => !c.TypeLine.Contains("Land", StringComparison.Ordinal))
            .OrderByDescending(c=>preferences is null?0:DeckWizardRequest.ThemeScore(c,preferences.Theme))
            .ThenByDescending(c=>preferences is null?0:DeckWizardRequest.StyleScore(c,preferences.Style))
            .ThenBy(c => preferences?.Style=="control"?Math.Abs(c.ManaValue-4):preferences?.Style=="balanced"?Math.Abs(c.ManaValue-3):c.ManaValue)
            .ThenBy(c => c.Name, StringComparer.Ordinal).ThenBy(c => c.OracleId, StringComparer.Ordinal).ToArray();
        var selection=preferences is null?null:new SynergySelection(candidates.Where(c=>remaining[c.OracleId]>0),36,preferences,capacity:remaining);
        for (var i = 0; i < 36; i++)
        {
            var available=spells.Where(c => remaining[c.OracleId] > 0 && used.GetValueOrDefault(c.OracleId) < 4);
            var c = selection is null?available.FirstOrDefault():available.OrderByDescending(selection.Score).ThenBy(c=>c.ManaValue).ThenBy(c=>c.Name,StringComparer.Ordinal).ThenBy(c=>c.OracleId,StringComparer.Ordinal).FirstOrDefault();
            if (c is null) throw new ArgumentException("Onvoldoende geschikte niet-landen voor het vaste plan: 36 niet-landen en 24 basislanden. Kies bewust planning of registreer meer fysiek bezit.");
            Pick(c.OracleId);
            selection?.Pick(c);
        }
        var palette=preferences is null?"WUBRG":"WUBRGC";var pips = palette.ToDictionary(x => x, _ => 0);
        foreach (var c in candidates.Where(c => picked.ContainsKey(c.Id)))
            foreach (Match m in ColoredPips().Matches(c.ManaCost)) pips[m.Groups[1].Value[0]] += picked[c.Id];
        if(preferences is not null)pips=DeckManaHeuristics.Demand(candidates.Where(c=>picked.ContainsKey(c.Id)).Select(c=>(c,picked[c.Id])));
        string[] types = ["Plains", "Island", "Swamp", "Mountain", "Forest"]; var colors = palette;
        var lands = groups.Values.Select(g => g[0]).Where(c => c.IsBasicLand && (types.Any(t => c.TypeLine.EndsWith(t, StringComparison.Ordinal))||preferences is not null&&c.Colors.Length==0))
            .OrderBy(c => c.Name, StringComparer.Ordinal).ToArray();
        char Color(PaperCard c) {var index=Array.FindIndex(types,t=>c.TypeLine.EndsWith(t,StringComparison.Ordinal));return index<0?'C':colors[index];}
        if(preferences is not null&&preferences.Theme!="domain"&&pips.Values.Any(n=>n>0))
        {
            var needed=lands.Where(c=>pips[Color(c)]>0).ToArray();
            if(needed.Length>0)lands=needed;
        }
        var chosen = colors.ToDictionary(x => x, _ => 0);
        // One of each available basic type supports the example's domain theme; it does not guarantee battlefield domain.
        foreach (var color in colors)
        {
            var c = lands.FirstOrDefault(c => Color(c) == color && remaining[c.OracleId] > 0);
            if (c is not null) { Pick(c.OracleId); chosen[color]++; }
        }
        for (var i = chosen.Values.Sum(); i < 24; i++)
        {
            var c = lands.Where(c => remaining[c.OracleId] > 0).OrderByDescending(c => (pips[Color(c)] + 1d) / (chosen[Color(c)] + 1d)).ThenBy(c => c.Name, StringComparer.Ordinal).FirstOrDefault();
            if (c is null) throw new ArgumentException("Onvoldoende geschikte basislanden voor het vaste 24-landplan. Er worden geen extra exemplaren als bezit aangenomen.");
            Pick(c.OracleId); chosen[Color(c)]++;
        }
        var index = candidates.ToDictionary(c => c.Id);
        return picked.OrderBy(x => index[x.Key].Name, StringComparer.Ordinal).ThenBy(x => x.Key).Select(x => new PaperDeckLine(index[x.Key], Domain.DeckZone.Mainboard, x.Value, owned.GetValueOrDefault(index[x.Key].OracleId))).ToArray();
    }
}
