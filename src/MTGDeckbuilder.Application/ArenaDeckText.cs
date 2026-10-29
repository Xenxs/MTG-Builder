using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MTGDeckbuilder.Domain;

namespace MTGDeckbuilder.Application;

public static partial class ArenaDeckText
{
    public const int MaxBytes = 262144;
    public const int MaxRows = 500;
    public const int MaxCards = 10000;
    [GeneratedRegex("^(?<quantity>[0-9]{1,4})[ \\t]+(?<body>.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex CountLine();
    [GeneratedRegex("^(?<name>.+)[ \\t]+\\((?<set>[A-Za-z0-9]{1,12})\\)[ \\t]+(?<number>[^\\s()]{1,20})$", RegexOptions.CultureInvariant)]
    private static partial Regex PrintingLine();

    public static ParsedDeck Parse(string text, bool preserveCollectorCase = false)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (Encoding.UTF8.GetByteCount(text) > MaxBytes) throw new ArgumentException("Een decktekst mag maximaal 256 KiB groot zijn.");
        var lines = text.TrimStart('\ufeff').Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var result = new List<DeckImportLine>();
        var zone = DeckZone.Mainboard;
        string? name = null;
        bool about = false;
        int total = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0) continue;
            if (line.Length > 256) throw new ArgumentException($"Regel {i + 1}: maximaal 256 tekens per regel.");
            if (line.Equals("About", StringComparison.OrdinalIgnoreCase)) { about = true; continue; }
            if (about && line.StartsWith("Name ", StringComparison.OrdinalIgnoreCase))
            {
                if (name is not null) throw new ArgumentException($"Regel {i + 1}: dubbele decknaam.");
                name = SafeText(line[5..], "Decknaam", 120); continue;
            }
            if (line.Equals("Deck", StringComparison.OrdinalIgnoreCase)) { zone = DeckZone.Mainboard; about = false; continue; }
            if (line.Equals("Sideboard", StringComparison.OrdinalIgnoreCase)) { zone = DeckZone.Sideboard; about = false; continue; }
            if (line.Equals("Commander", StringComparison.OrdinalIgnoreCase)) { zone = DeckZone.Commander; about = false; continue; }
            if (line.Equals("Companion", StringComparison.OrdinalIgnoreCase) || line.Equals("Maybeboard", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"Regel {i + 1}: sectie {line} wordt nog niet ondersteund; er is niets geïmporteerd.");
            if (about) throw new ArgumentException($"Regel {i + 1}: verwacht Name of een decksectie na About.");
            var match = CountLine().Match(line);
            if (!match.Success || !int.TryParse(match.Groups["quantity"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var count) || count < 1)
                throw new ArgumentException($"Regel {i + 1}: gebruik 'aantal Kaartnaam' of 'aantal Kaartnaam (SET) nummer'.");
            var body = match.Groups["body"].Value.Trim();
            var printing = PrintingLine().Match(body);
            string cardName = SafeText(printing.Success ? printing.Groups["name"].Value : body, "Kaartnaam", 200);
            string? set = printing.Success ? printing.Groups["set"].Value.ToUpperInvariant() : null;
            string? collector = printing.Success ? preserveCollectorCase ? printing.Groups["number"].Value : printing.Groups["number"].Value.ToUpperInvariant() : null;
            result.Add(new(i + 1, cardName, set, collector, count, zone));
            total += count;
            if (result.Count > MaxRows || total > MaxCards)
                throw new ArgumentException("Een deckimport mag maximaal 500 regels en 10.000 exemplaren bevatten. Dit is een technische limiet, geen formaatregel.");
        }
        if (result.Count == 0) throw new ArgumentException("De decktekst bevat geen kaartregels.");
        return new(name, result);
    }

    public static string SafeText(string value, string label, int max)
    {
        value = Rules.Text(value, label, max);
        if (value.Any(char.IsControl)) throw new ArgumentException($"{label} mag geen besturingstekens bevatten.");
        return value;
    }
}
