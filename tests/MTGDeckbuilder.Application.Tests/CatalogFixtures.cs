using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace MTGDeckbuilder.Application.Tests;

internal static class CatalogFixtures
{
    public static Dictionary<string, object?> Card(string name = "Llanowar Elves", string set = "fdn", string collector = "227",
        string? id = null, string? oracle = null, string[]? colors = null, double mana = 1, string type = "Creature — Elf Druid",
        string rarity = "common", string language = "en", bool arena = true)
        => new()
        {
            ["object"] = "card", ["id"] = id ?? Guid.NewGuid().ToString(), ["oracle_id"] = oracle ?? Guid.NewGuid().ToString(),
            ["arena_id"] = 93940, ["name"] = name, ["set"] = set, ["set_name"] = "Test Set " + set,
            ["collector_number"] = collector, ["lang"] = language, ["games"] = arena ? new[] { "paper", "arena" } : new[] { "paper" },
            ["color_identity"] = colors ?? ["G"], ["cmc"] = mana, ["mana_cost"] = "{G}", ["type_line"] = type,
            ["oracle_text"] = "{T}: Add {G}.", ["rarity"] = rarity
        };

    public static async Task<string> WriteAsync(TestDatabase test, IEnumerable<Dictionary<string, object?>> cards,
        string format, CancellationToken ct)
    {
        Directory.CreateDirectory(test.Paths.DataDirectory);
        var path = Path.Combine(test.Paths.DataDirectory, Guid.NewGuid().ToString("N") + "." + format);
        var text = format.StartsWith("jsonl", StringComparison.Ordinal)
            ? string.Join('\n', cards.Select(x => JsonSerializer.Serialize(x))) : JsonSerializer.Serialize(cards);
        if (format.EndsWith("gz", StringComparison.Ordinal))
        {
            await using var output = File.Create(path);
            await using var gzip = new GZipStream(output, CompressionLevel.Fastest);
            await gzip.WriteAsync(Encoding.UTF8.GetBytes(text), ct);
        }
        else await File.WriteAllTextAsync(path, text, ct);
        return path;
    }

    public static byte[] Gzip(IEnumerable<Dictionary<string, object?>> cards)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
            gzip.Write(Encoding.UTF8.GetBytes(string.Join('\n', cards.Select(x => JsonSerializer.Serialize(x)))));
        return output.ToArray();
    }
}
