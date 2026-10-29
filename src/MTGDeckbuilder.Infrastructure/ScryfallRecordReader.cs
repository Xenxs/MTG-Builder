using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace MTGDeckbuilder.Infrastructure;

internal sealed record CatalogRecord(string ScryfallId, string OracleId, int? ArenaId, string Name,
    string ManaCost, string TypeLine, string OracleText, string ColorIdentity, double ManaValue,
    string SetCode, string SetName, string CollectorNumber, string Language, string Rarity,
    Domain.CardLegality Standard = Domain.CardLegality.Unknown);

internal static class ScryfallRecordReader
{
    public static async IAsyncEnumerable<JsonElement> ReadAsync(string path, [EnumeratorCancellation] CancellationToken ct)
    {
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, useAsync: true);
        var magic = new byte[2];
        var count = await file.ReadAsync(magic, ct);
        file.Position = 0;
        await using Stream content = count == 2 && magic[0] == 0x1f && magic[1] == 0x8b
            ? new GZipStream(file, CompressionMode.Decompress, leaveOpen: true) : file;
        // Strip whitespace/BOM and replay the first token for the streaming JSON parser.
        var first = content.ReadByte();
        if (first == 0xef)
        {
            if (content.ReadByte() != 0xbb || content.ReadByte() != 0xbf) throw new InvalidDataException("Ongeldige UTF-8-invoer.");
            first = content.ReadByte();
        }
        while (first is 9 or 10 or 13 or 32) { ct.ThrowIfCancellationRequested(); first = content.ReadByte(); }
        if (first is not (123 or 91)) throw new InvalidDataException("Verwacht Scryfall JSONL of een JSON-array.");
        await using var replay = new PrefixStream((byte)first, content);
        var options = new JsonSerializerOptions { DefaultBufferSize = 65536, MaxDepth = 64 };
        await foreach (var element in JsonSerializer.DeserializeAsyncEnumerable<JsonElement>(replay,
            topLevelValues: first == 123, options: options, cancellationToken: ct))
            yield return element;
    }

    public static CatalogRecord? Parse(JsonElement element)
    {
        if (Text(element, "object") != "card") throw new InvalidDataException("Het bestand bevat geen Scryfall-kaartobjecten.");
        if (!element.TryGetProperty("games", out var games) || games.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Een kaart mist het games-veld.");
        if (!games.EnumerateArray().Any(x => x.ValueKind == JsonValueKind.String && x.GetString() == "arena")) return null;
        var id = Guid.Parse(Text(element, "id"));
        var oracle = Guid.Parse(Text(element, "oracle_id"));
        if (id == Guid.Empty || oracle == Guid.Empty) throw new InvalidDataException("Een Arena-kaart mist een geldige bronidentiteit.");
        var faces = element.TryGetProperty("card_faces", out var faceArray) && faceArray.ValueKind == JsonValueKind.Array
            ? faceArray.EnumerateArray().ToArray() : [];
        string Combined(string field) => OptionalText(element, field) ?? string.Join(" // ", faces.Select(f => OptionalText(f, field) ?? ""));
        var colors = element.GetProperty("color_identity");
        if (colors.ValueKind != JsonValueKind.Array) throw new InvalidDataException("Ongeldige kleuridentiteit.");
        var colorIdentity = string.Concat(colors.EnumerateArray().Select(x => x.GetString()));
        var mana = element.GetProperty("cmc").GetDouble();
        var arenaId = element.TryGetProperty("arena_id", out var arena) && arena.ValueKind == JsonValueKind.Number ? arena.GetInt32() : (int?)null;
        var record = new CatalogRecord(id.ToString(), oracle.ToString(), arenaId, Text(element, "name"),
            Combined("mana_cost"), Combined("type_line"), Combined("oracle_text"), colorIdentity, mana,
            Text(element, "set").ToUpperInvariant(), Text(element, "set_name"), Text(element, "collector_number").ToUpperInvariant(),
            Text(element, "lang").ToLowerInvariant(), Text(element, "rarity").ToLowerInvariant(), ParseStandard(element));
        // Validate every staged record before changing the visible catalog.
        var card = new Domain.Card(record.Name);
        card.UpdateCatalog(record.OracleId, record.Name, record.ManaCost, record.TypeLine, record.OracleText, record.ColorIdentity, record.ManaValue);
        new Domain.Printing(card.Id, record.SetCode, record.CollectorNumber).UpdateCatalog(card.Id, record.ScryfallId,
            record.ArenaId, record.Language, record.SetName, record.Rarity, Guid.NewGuid());
        return record;
    }

    private static string Text(JsonElement element, string name) => OptionalText(element, name)
        ?? throw new InvalidDataException($"Een kaart mist het verplichte veld {name}.");
    private static string? OptionalText(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static Domain.CardLegality ParseStandard(JsonElement element)
    {
        if (!element.TryGetProperty("legalities", out var legalities)) return Domain.CardLegality.Unknown;
        if (legalities.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Ongeldig legalities-object.");
        if (!legalities.TryGetProperty("standard", out var status)) return Domain.CardLegality.Unknown;
        if (status.ValueKind != JsonValueKind.String) throw new InvalidDataException("Ongeldige Standard-legaliteit.");
        return status.GetString() switch
        {
            "legal" => Domain.CardLegality.Legal, "not_legal" => Domain.CardLegality.NotLegal,
            "banned" => Domain.CardLegality.Banned, "restricted" => Domain.CardLegality.Restricted,
            _ => Domain.CardLegality.Unknown
        };
    }

    private sealed class PrefixStream(byte prefix, Stream inner) : Stream
    {
        private bool _pending = true;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_pending && count > 0) { _pending = false; buffer[offset] = prefix; return 1; }
            return inner.Read(buffer, offset, count);
        }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_pending && buffer.Length > 0) { cancellationToken.ThrowIfCancellationRequested(); _pending = false; buffer.Span[0] = prefix; return ValueTask.FromResult(1); }
            return inner.ReadAsync(buffer, cancellationToken);
        }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
