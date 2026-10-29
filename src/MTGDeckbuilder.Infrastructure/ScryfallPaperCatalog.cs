using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MTGDeckbuilder.Application;
using MTGDeckbuilder.Domain;

namespace MTGDeckbuilder.Infrastructure;

public sealed class ScryfallPaperCatalog(IDbContextFactory<DeckbuilderDbContext> factory, HttpClient client, AppPaths paths, DatabaseWriteLock writes, TimeProvider clock) : IPaperCatalog
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTime _lastRequest;
    public static PaperCard Parse(string json, DateTime fetched)
    {
        using var document = JsonDocument.Parse(json); var c = document.RootElement;
        string Text(string key) => c.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";
        if (Text("object") != "card" || !c.GetProperty("games").EnumerateArray().Any(x => x.GetString() == "paper") ||
            !Guid.TryParse(Text("id"), out var id) || !Guid.TryParse(Text("oracle_id"), out _)) throw new ArgumentException("Geen ondersteunde fysieke Scryfall-printing.");
        var faces = c.TryGetProperty("card_faces", out var f) ? f.EnumerateArray().ToArray() : [];
        string Image(JsonElement e) => e.TryGetProperty("image_uris", out var u) && u.TryGetProperty("normal", out var n) ? n.GetString() ?? "" : "";
        var image = Image(c); if (image.Length == 0 && faces.Length > 0) image = Image(faces[0]);
        var oracleText = Text("oracle_text"); if (oracleText.Length == 0 && faces.Length > 0) oracleText = string.Join("\n//\n", faces.Select(x => x.TryGetProperty("oracle_text", out var t) ? t.GetString() : ""));
        var cost = Text("mana_cost"); if (cost.Length == 0 && faces.Length > 0 && faces[0].TryGetProperty("mana_cost", out var mc)) cost = mc.GetString() ?? "";
        var standard = c.TryGetProperty("legalities", out var l) && l.TryGetProperty("standard", out var s) ? s.GetString() ?? "unknown" : "unknown";
        var commander = c.TryGetProperty("legalities", out var cl) && cl.TryGetProperty("commander", out var cm) ? cm.GetString() ?? "unknown" : "unknown";
        var front = faces.Length > 0 ? faces[0] : c; var hasStats = front.TryGetProperty("power", out _) && front.TryGetProperty("toughness", out _);
        var printedColors=c.TryGetProperty("colors",out var pc)?string.Concat(pc.EnumerateArray().Select(x=>x.GetString())):
            string.Concat(faces.SelectMany(x=>x.TryGetProperty("colors",out var fc)?fc.EnumerateArray().Select(v=>v.GetString()!):[]).Distinct());
        return new(id, Text("oracle_id"), Text("name"), Text("set"), Text("collector_number"), Text("lang"), cost, c.GetProperty("cmc").GetDouble(), Text("type_line"), oracleText,
            string.Concat(c.GetProperty("color_identity").EnumerateArray().Select(x => x.GetString())), standard, image, faces.Length > 1 ? Image(faces[1]) : "", Text("scryfall_uri"),
            c.TryGetProperty("finishes", out var finishes) ? finishes.EnumerateArray().Select(x => x.GetString() == "nonfoil" ? "normal" : x.GetString()!).ToArray() : ["normal"], fetched, commander, hasStats, Text("set_name"), printedColors,
            front.TryGetProperty("power",out var power)?power.GetString()??"":"",front.TryGetProperty("toughness",out var toughness)?toughness.GetString()??"":"",
            Text("border_color")=="silver"||Text("security_stamp")=="acorn"||Text("layout") is "augment" or "host" or "leveler");
    }
    public Task<PaperCard> GetAsync(Guid id, bool refresh = false, CancellationToken ct = default) => Task.Run(async () =>
    {
        if (!refresh)
        {
            await using var db = await factory.CreateDbContextAsync(ct); var cached = await db.PaperPrintings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
            if (cached is not null && cached.FetchedUtc<=clock.GetUtcNow().UtcDateTime.AddMinutes(5)&&clock.GetUtcNow().UtcDateTime - cached.FetchedUtc < TimeSpan.FromDays(1)) return Parse(cached.MetadataJson, Utc(cached.FetchedUtc));
        }
        var json = await FetchAsync("https://api.scryfall.com/cards/" + id, ct); var card = Parse(json, clock.GetUtcNow().UtcDateTime);
        if (card.Id != id) throw new ArgumentException("Scryfall gaf een andere printing terug."); await CacheAsync(card, json, ct); return card;
    }, ct);
    public Task<PaperCard> GetAsync(string set, string number, string language, CancellationToken ct = default) => Task.Run(async () =>
    {
        if (string.IsNullOrWhiteSpace(set) || string.IsNullOrWhiteSpace(number) || string.IsNullOrWhiteSpace(language)) throw new ArgumentException("Setcode, collectornummer en taal zijn vereist zonder Scryfall-ID.");
        var json = await FetchAsync($"https://api.scryfall.com/cards/{Uri.EscapeDataString(set)}/{Uri.EscapeDataString(number)}/{Uri.EscapeDataString(language)}", ct);
        var card = Parse(json, clock.GetUtcNow().UtcDateTime);
        if (!string.Equals(card.Set, set, StringComparison.OrdinalIgnoreCase) || card.Number != number || card.Language != language) throw new ArgumentException("Uitgave/taal komt niet overeen met de bron.");
        await CacheAsync(card, json, ct); return card;
    }, ct);
    public Task<IReadOnlyList<PaperCard>> SearchAsync(string query, CancellationToken ct = default) => Task.Run<IReadOnlyList<PaperCard>>(async () =>
    {
        query = query.Trim(); if (query.Length < 2 || query.Length > 200) throw new ArgumentException("Zoek met 2 tot 200 tekens.");
        var json = await FetchAsync("https://api.scryfall.com/cards/search?unique=prints&order=name&q=" + Uri.EscapeDataString("game:paper (" + query + ")"), ct);
        using var doc = JsonDocument.Parse(json); var result = new List<PaperCard>();
        foreach (var e in doc.RootElement.GetProperty("data").EnumerateArray().Take(40))
        {
            var card = Parse(e.GetRawText(), clock.GetUtcNow().UtcDateTime); await CacheAsync(card, e.GetRawText(), ct); result.Add(card);
        }
        return result;
    }, ct);
    private async Task CacheAsync(PaperCard card, string json, CancellationToken ct) => await writes.RunAsync(async () =>
    {
        await using var db = await factory.CreateDbContextAsync(ct); var entity = await db.PaperPrintings.FindAsync([card.Id], ct);
        if (entity is null) db.PaperPrintings.Add(new(card.Id, json, card.FetchedUtc)); else entity.Update(json, card.FetchedUtc);
        await db.SaveChangesAsync(ct);
    }, ct);
    public Task<IReadOnlyList<PaperCard>> CommanderPoolAsync(string colors, CancellationToken ct = default) => Task.Run<IReadOnlyList<PaperCard>>(async () =>
    {
        if (colors.Length > 5 || colors.Any(c => !"WUBRG".Contains(c)) || colors.Distinct().Count() != colors.Length) throw new ArgumentException("Ongeldige commander-kleuridentiteit.");
        var identity = colors.Length == 0 ? "c" : colors.ToLowerInvariant(); var result = new List<PaperCard>();
        foreach (var (restriction, maximum) in new[] { ("-t:land cmc<=3", 50), ("-t:land cmc>3 cmc<=5", 50), ("-t:land cmc>5 cmc<=8", 50), ("t:basic", 10) })
        {
            var query = "game:paper legal:commander id<=" + identity + " " + restriction;
            var json = await FetchAsync("https://api.scryfall.com/cards/search?unique=cards&order=edhrec&dir=asc&q=" + Uri.EscapeDataString(query), ct);
            using var doc = JsonDocument.Parse(json);
            foreach (var e in doc.RootElement.GetProperty("data").EnumerateArray().Take(maximum))
            {
                var card = Parse(e.GetRawText(), clock.GetUtcNow().UtcDateTime);
                if (card.CommanderLegality != "legal" || !card.Colors.All(colors.Contains)) continue;
                await CacheAsync(card, e.GetRawText(), ct); result.Add(card);
            }
        }
        return result.DistinctBy(c => c.Id).ToArray();
    }, ct);
    public Task<IReadOnlyList<PaperCard>> WizardPoolAsync(DeckWizardRequest request,CancellationToken ct=default)=>Task.Run<IReadOnlyList<PaperCard>>(async()=>
    {
        request.Validate();var colors=request.AllowedColors;var identity=colors.Length==0?"c":colors.ToLowerInvariant();
        var format=request.Format=="standard"?"standard":"commander";var result=new List<PaperCard>();
        var queries=new List<(string Restriction,int Maximum)>{("-t:land cmc<=3",50),("-t:land cmc>3 cmc<=5",50),("-t:land cmc>5 cmc<=8",50),("t:basic",10)};
        var theme=request.Theme switch{"equipment"=>"(t:equipment or o:equipped)","tokens"=>"o:token","graveyard"=>"o:graveyard","sacrifice"=>"o:sacrifice","opponent-death"=>"(o:\"opponent controls dies\" or o:\"destroy target creature\" or o:\"sacrifices a creature\")","domain"=>"o:domain","exalted"=>"o:exalted","draw"=>"o:draw",_=>""};
        if(theme.Length>0)queries.Insert(0,("-t:land "+theme,50));
        foreach(var (restriction,limit) in queries)
        {
            var q="game:paper legal:"+format+" id<="+identity+" "+restriction;
            var json=await FetchAsync("https://api.scryfall.com/cards/search?unique=cards&order=edhrec&dir=asc&q="+Uri.EscapeDataString(q),ct,true);using var doc=JsonDocument.Parse(json);
            foreach(var e in doc.RootElement.GetProperty("data").EnumerateArray().Take(limit))
            {
                var c=Parse(e.GetRawText(),clock.GetUtcNow().UtcDateTime);if(!request.Allows(c)||(request.Format=="standard"?c.Standard:c.CommanderLegality)!="legal")continue;
                await CacheAsync(c,e.GetRawText(),ct);result.Add(c);
            }
        }
        return result.DistinctBy(c=>c.Id).ToArray();
    },ct);
    private async Task<string> FetchAsync(string url, CancellationToken ct, bool allowEmptySearch = false)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var pause = TimeSpan.FromMilliseconds(120) - (DateTime.UtcNow - _lastRequest); if (pause > TimeSpan.Zero) await Task.Delay(pause, ct);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(30));
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token); _lastRequest = DateTime.UtcNow;
            if(allowEmptySearch&&response.StatusCode==System.Net.HttpStatusCode.NotFound)return "{\"data\":[]}";
            if (!response.IsSuccessStatusCode) throw new ArgumentException($"Scryfall: {(int)response.StatusCode}. Controleer identiteit of verbinding en probeer opnieuw; geen kaarten overgeslagen.");
            return System.Text.Encoding.UTF8.GetString(await ReadBoundedAsync(response.Content, 2 * 1024 * 1024, timeout.Token));
        }
        finally { _gate.Release(); }
    }
    public Task<string?> ImageAsync(PaperCard card, bool back, CancellationToken ct = default) => Task.Run<string?>(async () =>
    {
        var url = back ? card.ImageBack : card.ImageFront; if (url.Length == 0) return null;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != "cards.scryfall.io" || !uri.IsDefaultPort || uri.UserInfo.Length > 0)
            throw new ArgumentException("De afbeelding verwijst niet naar de toegestane Scryfall-imagehost.");
        var directory = Path.Combine(paths.DataDirectory, "paper-images"); Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, card.Id.ToString("N") + (back ? "-back" : "-front") + ".img"); if (File.Exists(file)) return file;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(25));
        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, timeout.Token); response.EnsureSuccessStatusCode();
        var bytes = await ReadBoundedAsync(response.Content, 5 * 1024 * 1024, timeout.Token);
        if (bytes.Length < 8 || !(bytes[0] == 0xff && bytes[1] == 0xd8 || bytes[0] == 137 && bytes[1] == 80 && bytes[2] == 78 && bytes[3] == 71)) throw new ArgumentException("Geen ondersteunde kaartafbeelding.");
        var temporary = file + "." + Guid.NewGuid().ToString("N") + ".part";
        try { await File.WriteAllBytesAsync(temporary, bytes, ct); File.Move(temporary, file, overwrite: true); } finally { if (File.Exists(temporary)) File.Delete(temporary); }
        return file;
    }, ct);
    private static async Task<byte[]> ReadBoundedAsync(HttpContent content, int maximum, CancellationToken ct)
    {
        if (content.Headers.ContentLength > maximum) throw new ArgumentException("Scryfall-antwoord te groot.");
        await using var input = await content.ReadAsStreamAsync(ct); using var output = new MemoryStream(); var buffer = new byte[16384];
        int read; while ((read = await input.ReadAsync(buffer, ct)) > 0) { if (output.Length + read > maximum) throw new ArgumentException("Scryfall-antwoord te groot."); output.Write(buffer, 0, read); }
        return output.ToArray();
    }
    internal static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
