using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.VisualBasic.FileIO;
using MTGDeckbuilder.Application;

namespace MTGDeckbuilder.Infrastructure;

public static class ManaBoxCsv
{
    public static string Language(string language) => language.Trim().ToLowerInvariant() switch
    {
        "english" => "en", "german" => "de", "french" => "fr", "italian" => "it", "spanish" => "es", "portuguese" => "pt",
        "japanese" => "ja", "korean" => "ko", "russian" => "ru", "chinese simplified" or "simplified chinese" => "zhs",
        "chinese traditional" or "traditional chinese" => "zht", var value => value
    };
    public static IReadOnlyList<PaperSourceRow> Read(string path)
    {
        var info = new FileInfo(path); if (!info.Exists || info.Length is <= 0 or > 16 * 1024 * 1024) throw new ArgumentException("Kies een CSV van maximaal 16 MiB.");
        return Parse(File.ReadAllBytes(path));
    }
    public static IReadOnlyList<PaperSourceRow> Parse(byte[] bytes)
    {
        if (bytes.Length is <= 0 or > 16 * 1024 * 1024) throw new ArgumentException("CSV te groot of leeg.");
        string text;
        try { text = new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF'); }
        catch (DecoderFallbackException ex) { throw new ArgumentException("Gebruik een UTF-8 CSV-bestand.", ex); }
        var header = text.Split('\n', 2)[0]; var delimiter = header.Contains('\t') ? "\t" : header.Count(x => x == ';') > header.Count(x => x == ',') ? ";" : ",";
        using var parser = new TextFieldParser(new StringReader(text)) { HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = false };
        parser.SetDelimiters(delimiter); string[] names;
        try { names = parser.ReadFields()?.Select(x => x.Trim()).ToArray() ?? []; }
        catch (MalformedLineException ex) { throw new ArgumentException("Ongeldige CSV-kopregel.", ex); }
        if (names.Length == 0 || names.Any(string.IsNullOrWhiteSpace) || names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Length) throw new ArgumentException("CSV-kolommen ontbreken of komen dubbel voor.");
        if (!names.Contains("Quantity", StringComparer.OrdinalIgnoreCase)) throw new ArgumentException("CSV vereist een Quantity-kolom.");
        var rows = new List<PaperSourceRow>();
        try
        {
            while (!parser.EndOfData)
            {
                var values = parser.ReadFields()!; if (values.Length != names.Length) throw new ArgumentException($"CSV-regel {rows.Count + 2} heeft een afwijkend aantal kolommen.");
                var fields = names.Select((n, i) => (n, Value: values[i].Trim())).ToDictionary(x => x.n, x => x.Value, StringComparer.OrdinalIgnoreCase);
                string Get(string name) => fields.GetValueOrDefault(name, "");
                foreach (var (field, maximum) in new[] { ("Name", 200), ("Set code", 12), ("Collector number", 20), ("Condition", 40), ("Binder Name", 200) })
                    if (Get(field).Length > maximum) throw new ArgumentException($"{field} is te lang op regel {rows.Count + 2}.");
                if (!int.TryParse(Get("Quantity"), NumberStyles.None, CultureInfo.InvariantCulture, out var quantity) || quantity > 9999) throw new ArgumentException($"Ongeldig Quantity op regel {rows.Count + 2}.");
                Guid? id = null; if (Get("Scryfall ID").Length > 0) { if (!Guid.TryParse(Get("Scryfall ID"), out var parsed) || parsed == Guid.Empty) throw new ArgumentException("Ongeldige Scryfall ID."); id = parsed; }
                var lang = Language(Get("Language")); if (lang.Length > 0 && !new[] { "en", "de", "fr", "it", "es", "pt", "ja", "ko", "ru", "zhs", "zht", "he", "la", "grc", "ar", "sa", "ph" }.Contains(lang)) throw new ArgumentException("Onbekende kaarttaal; kies de juiste taalcode.");
                var finish = Get("Foil").ToLowerInvariant() switch { "" => "unknown", "false" or "no" or "normal" or "nonfoil" => "normal", "true" or "yes" or "foil" => "foil", "etched" => "etched", _ => throw new ArgumentException("Onbekende Foil-waarde.") };
                bool Bool(string n) => Get(n).ToLowerInvariant() switch { "" or "false" or "no" or "0" => false, "true" or "yes" or "1" => true, _ => throw new ArgumentException($"Ongeldige {n}-waarde.") };
                var type = Get("Binder Type").ToLowerInvariant(); if (type.Length > 0 && type is not ("binder" or "collection" or "deck" or "list")) throw new ArgumentException("Onbekend Binder Type; bezit of deck/list moet duidelijk zijn.");
                rows.Add(new(rows.Count + 1, id, Get("Name"), Get("Set code").ToLowerInvariant(), Get("Collector number"), lang, finish,
                    Get("Condition").Length > 0 ? Get("Condition").ToLowerInvariant() : "unknown", Get("Binder Name").Length > 0 ? Get("Binder Name") : "Collectie", quantity,
                    Bool("Proxy"), type is "deck" or "list", JsonSerializer.Serialize(fields)));
                if (rows.Count > 10000) throw new ArgumentException("Maximaal 10.000 CSV-regels per import.");
            }
        }
        catch (MalformedLineException ex) { throw new ArgumentException("Ongeldige CSV-aanhalingstekens of regelopbouw.", ex); }
        if (rows.Count == 0) throw new ArgumentException("Het CSV-bestand bevat geen kaarten.");
        return rows;
    }
}
