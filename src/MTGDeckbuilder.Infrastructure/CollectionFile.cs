using System.Security.Cryptography;
using System.Text.Json;
using MTGDeckbuilder.Domain;

namespace MTGDeckbuilder.Infrastructure;

internal sealed record CollectionSource(string? ScryfallId, int? ArenaId, string? SetCode,
    string? CollectorNumber, string? Language, string? Name, int Quantity)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string Label => Name ?? (ScryfallId is not null ? $"Scryfall {ScryfallId}" : ArenaId is not null ? $"Arena {ArenaId}" : $"{SetCode} #{CollectorNumber} · {Language}");
}

internal static class CollectionFile
{
    internal const int MaxBytes = 16 * 1024 * 1024;
    internal const int MaxRows = 50000;
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static async Task<(List<CollectionSource> Cards, string Hash)> ReadAsync(string path, CancellationToken ct)
    {
        await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, true);
        if (input.Length > MaxBytes) throw new ArgumentException("Het collectiebestand mag maximaal 16 MiB groot zijn.");
        using var memory = new MemoryStream();
        var buffer = new byte[65536];
        int count;
        while ((count = await input.ReadAsync(buffer, ct)) > 0)
        {
            if (memory.Length + count > MaxBytes) throw new ArgumentException("Het collectiebestand mag maximaal 16 MiB groot zijn.");
            await memory.WriteAsync(buffer.AsMemory(0, count), ct);
        }
        var bytes = memory.ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        try
        {
            ReadOnlyMemory<byte> json = bytes;
            if (bytes.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf })) json = json[3..];
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8 });
            CheckProperties(doc.RootElement, ["schemaVersion", "cards"]);
            if (!doc.RootElement.TryGetProperty("schemaVersion", out var version) || !version.TryGetInt32(out var number) || number != 1)
                throw new ArgumentException("Gebruik collectieformaat schemaVersion 1; een Arena-log is geen collectiebestand.");
            if (!doc.RootElement.TryGetProperty("cards", out var cards) || cards.ValueKind != JsonValueKind.Array || cards.GetArrayLength() is < 1 or > MaxRows)
                throw new ArgumentException("Het collectiebestand moet 1 tot 50.000 kaartregels bevatten.");
            var result = new List<CollectionSource>();
            foreach (var card in cards.EnumerateArray())
            {
                ct.ThrowIfCancellationRequested();
                try { result.Add(ParseRow(card)); }
                catch (ArgumentException ex) { throw new ArgumentException($"Regel {result.Count + 1}: {ex.Message}", ex); }
            }
            return (result, hash);
        }
        catch (JsonException ex) { throw new ArgumentException("Het bestand bevat ongeldig of afgekapt JSON.", ex); }
        catch (InvalidOperationException ex) { throw new ArgumentException("Een veld heeft een verkeerd gegevenstype. Controleer het collectieformaat.", ex); }
    }

    private static CollectionSource ParseRow(JsonElement row)
    {
        CheckProperties(row, ["scryfallId", "arenaId", "setCode", "collectorNumber", "language", "name", "quantity"]);
        if (!row.TryGetProperty("quantity", out var quantity) || quantity.ValueKind != JsonValueKind.Number || !quantity.TryGetInt32(out var qty))
            throw new ArgumentException("quantity is verplicht en moet een geheel getal zijn.");
        Rules.Quantity(qty);
        string? Text(string key, int max)
        {
            if (!row.TryGetProperty(key, out var field) || field.ValueKind == JsonValueKind.Null) return null;
            if (field.ValueKind != JsonValueKind.String) throw new ArgumentException($"{key} moet tekst bevatten.");
            return Rules.Text(field.GetString()!, key, max);
        }
        var scryfall = Text("scryfallId", 36);
        if (scryfall is not null)
        {
            if (!Guid.TryParse(scryfall, out var id) || id == Guid.Empty) throw new ArgumentException("Ongeldig Scryfall-ID.");
            scryfall = id.ToString();
        }
        int? arena = null;
        if (row.TryGetProperty("arenaId", out var arenaField) && arenaField.ValueKind != JsonValueKind.Null)
        {
            if (arenaField.ValueKind != JsonValueKind.Number || !arenaField.TryGetInt32(out var arenaId) || arenaId <= 0)
                throw new ArgumentException("Ongeldig Arena-ID.");
            arena = arenaId;
        }
        var set = Text("setCode", 12)?.ToUpperInvariant();
        var collector = Text("collectorNumber", 20)?.ToUpperInvariant();
        var language = Text("language", 12)?.ToLowerInvariant();
        var name = Text("name", 200);
        var hasNatural = set is not null || collector is not null || language is not null;
        if (hasNatural && (set is null || collector is null || language is null))
            throw new ArgumentException("Geef setCode, collectorNumber en language samen op.");
        if ((scryfall is not null ? 1 : 0) + (arena is not null ? 1 : 0) + (hasNatural ? 1 : 0) != 1)
            throw new ArgumentException("Gebruik precies één identiteit: scryfallId, arenaId of setCode + collectorNumber + language.");
        return new(scryfall, arena, set, collector, language, name, qty);
    }

    private static void CheckProperties(JsonElement value, string[] allowed)
    {
        if (value.ValueKind != JsonValueKind.Object) throw new ArgumentException("Een JSON-object wordt verwacht.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in value.EnumerateObject())
            if (!allowed.Contains(field.Name, StringComparer.Ordinal) || !seen.Add(field.Name))
                throw new ArgumentException($"Onbekend of dubbel veld: {field.Name[..Math.Min(60, field.Name.Length)]}.");
    }
}
