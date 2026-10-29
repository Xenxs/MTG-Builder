using MTGDeckbuilder.Domain;

namespace MTGDeckbuilder.Application;

public sealed record AnalysisCard(Guid PrintingId, string? OracleId, string Name, DeckZone Zone,
    int Quantity, string? TypeLine, double? ManaValue, string ColorIdentity);
public sealed record OwnedCard(Guid PrintingId, string? OracleId, int Quantity);

public static class DeckAnalyzer
{
    private static string Identity(Guid printingId, string? oracleId) => oracleId is null ? $"P:{printingId}" : $"O:{oracleId}";
    public static DeckAnalysis Analyze(IReadOnlyList<AnalysisCard> cards, IReadOnlyList<OwnedCard> owned)
    {
        var main = cards.Where(x => x.Zone == DeckZone.Mainboard).ToList();
        var known = main.Where(x => x.OracleId is not null && !string.IsNullOrWhiteSpace(x.TypeLine) &&
            x.ManaValue is { } value && double.IsFinite(value) && value >= 0).ToList();
        string FrontType(AnalysisCard card) => card.TypeLine!.Split(" // ", StringSplitOptions.None)[0].Split('—')[0];
        bool HasType(AnalysisCard card, string type) => FrontType(card).Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(type, StringComparer.Ordinal);
        var nonlands = known.Where(x => !HasType(x, "Land")).ToList();
        var needByPrinting = cards.GroupBy(x => x.PrintingId).ToDictionary(x => x.Key, x => x.Sum(c => c.Quantity));
        var ownedByPrinting = owned.ToDictionary(x => x.PrintingId, x => x.Quantity);
        var ownedByIdentity = owned.GroupBy(x => Identity(x.PrintingId, x.OracleId)).ToDictionary(x => x.Key, x => x.Sum(c => c.Quantity));
        var needs = cards.GroupBy(x => Identity(x.PrintingId, x.OracleId)).Select(group =>
        {
            var required = group.Sum(x => x.Quantity);
            var available = ownedByIdentity.GetValueOrDefault(group.Key);
            var exactMissing = group.Select(x => x.PrintingId).Distinct().Sum(id => Math.Max(0, needByPrinting[id] - ownedByPrinting.GetValueOrDefault(id)));
            return new DeckNeed(group.First().Name, required, available, Math.Max(0, required - available), exactMissing, group.First().OracleId is not null);
        }).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
        var curve = Enumerable.Range(0, 8).Select(i => new DeckMetric(i == 7 ? "7+" : $"{i}–<{i + 1}",
            nonlands.Where(x => Math.Min(7, Math.Floor(x.ManaValue!.Value)) == i).Sum(x => x.Quantity))).ToList();
        var max = Math.Max(1, curve.Max(x => x.Count));
        curve = curve.Select(x => x with { BarWidth = 180d * x.Count / max }).ToList();
        var types = new[] { ("Creature", "Creatures"), ("Instant", "Instants"), ("Sorcery", "Sorceries"),
            ("Artifact", "Artifacts"), ("Enchantment", "Enchantments"), ("Planeswalker", "Planeswalkers"), ("Battle", "Battles"), ("Land", "Landen") }
            .Select(type => new DeckMetric(type.Item2, known.Where(x => HasType(x, type.Item1)).Sum(x => x.Quantity))).ToList();
        var colors = new[] { ("W", "Wit"), ("U", "Blauw"), ("B", "Zwart"), ("R", "Rood"), ("G", "Groen"), ("", "Kleurloos") }
            .Select(color => new DeckMetric(color.Item2, known.Where(x => color.Item1.Length == 0 ? x.ColorIdentity.Length == 0 : x.ColorIdentity.Contains(color.Item1)).Sum(x => x.Quantity))).ToList();
        var nonlandCount = nonlands.Sum(x => x.Quantity);
        return new(main.Sum(x => x.Quantity), cards.Where(x => x.Zone == DeckZone.Sideboard).Sum(x => x.Quantity),
            cards.Where(x => x.Zone == DeckZone.Commander).Sum(x => x.Quantity), known.Where(x => HasType(x, "Land")).Sum(x => x.Quantity),
            nonlandCount, main.Sum(x => x.Quantity) - known.Sum(x => x.Quantity),
            nonlandCount == 0 ? null : nonlands.Sum(x => x.ManaValue!.Value * x.Quantity) / nonlandCount,
            known.Where(x => x.TypeLine!.Contains(" // ")).Sum(x => x.Quantity), needs, curve, types, colors);
    }
}
