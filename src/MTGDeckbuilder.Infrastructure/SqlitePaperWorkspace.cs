using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MTGDeckbuilder.Application;
using MTGDeckbuilder.Domain;

namespace MTGDeckbuilder.Infrastructure;

public sealed class SqlitePaperWorkspace(IDbContextFactory<DeckbuilderDbContext> factory, DatabaseWriteLock writes, IPaperCatalog catalog, TimeProvider clock) : IPaperWorkspace
{
    private const string DemoKey = "conflux-intro-v1";
    private static readonly string[] DemoKeys = [DemoKey, "final-fantasy-starter-v1"];
    private sealed record Payload(PaperImportRow[] Rows, PaperStock[] Before, PaperStock[] After);
    private static PaperStock Stock(PaperHolding h) => new(h.Id, h.Key, h.PrintingId, h.Language, h.Finish, h.Condition, h.Binder, h.Quantity, h.IsDemo, h.Version);
    private static string Hash(IEnumerable<PaperHolding> rows) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(rows.OrderBy(x => x.Id).Select(Stock))));
    private static Payload Read(PaperImportPlan p) => JsonSerializer.Deserialize<Payload>(p.Payload) ?? throw new ArgumentException("Ongeldig importvoorbeeld.");
    private static PaperImportPreview Preview(PaperImportPlan p) => new(p.Id, p.Version, p.FileName, p.State, Read(p).Rows);
    private Task<T> Query<T>(Func<DeckbuilderDbContext, Task<T>> action, CancellationToken ct) => Task.Run(async () =>
    { await using var db = await factory.CreateDbContextAsync(ct); await using var tx = await db.Database.BeginTransactionAsync(ct); return await action(db); }, ct);
    private Task<T> Write<T>(Func<DeckbuilderDbContext, Task<T>> action, CancellationToken ct) => Task.Run(async () =>
    {
        T? result = default;
        await writes.RunAsync(async () =>
        {
            await using var db = await factory.CreateDbContextAsync(ct); await using var tx = await db.Database.BeginTransactionAsync(ct);
            result = await action(db); await db.SaveChangesAsync(ct); ct.ThrowIfCancellationRequested(); await tx.CommitAsync(ct);
        }, ct); return result!;
    }, ct);
    public Task InitializeExampleAsync(CancellationToken ct = default) => Write(async db =>
    {
        if (await db.Settings.AnyAsync(x => !x.ExamplesEnabled, ct)) return false;
        var changed = false;
        foreach (var (keyName, resource, label) in new[] { (DemoKey, "conflux-paper-v1.json", "Conflux"), ("final-fantasy-starter-v1", "final-fantasy-starter-v1.json", "FINAL FANTASY Starter Kit") })
        {
        if (await db.PaperImports.AnyAsync(x => x.SourceHash == keyName, ct)) continue;
        using var stream = typeof(PaperCard).Assembly.GetManifestResourceStream("MTGDeckbuilder.Application.Resources." + resource)!;
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct); var root = doc.RootElement; var fetched = root.GetProperty("FetchedUtc").GetDateTime();
        foreach (var data in root.GetProperty("Cards").EnumerateArray())
        {
            var card = ScryfallPaperCatalog.Parse(data.GetRawText(), fetched);
            if (!await db.PaperPrintings.AnyAsync(x => x.Id == card.Id, ct)) db.PaperPrintings.Add(new(card.Id, data.GetRawText(), fetched));
        }
        var quantities = new Dictionary<string, PaperHolding>();
        foreach (var deckData in root.GetProperty("Decks").EnumerateArray())
        {
            var deck = new PaperDeck(deckData.GetProperty("Name").GetString()!, true); db.PaperDecks.Add(deck);
            foreach (var group in deckData.GetProperty("Lines").EnumerateArray().GroupBy(x => x.GetProperty("ScryfallId").GetGuid()))
            {
                db.PaperDeckCards.Add(new(deck.Id, group.Key, DeckZone.Mainboard, group.Sum(x => x.GetProperty("Quantity").GetInt32())));
                foreach (var line in group)
                {
                    var finish = line.GetProperty("Finish").GetString()!; var key = PaperIdentity.Key(group.Key, "en", finish, "unknown", deck.Name);
                    if (quantities.TryGetValue(key, out var stock)) stock.SetQuantity(stock.Quantity + line.GetProperty("Quantity").GetInt32());
                    else quantities[key] = new(key, group.Key, "en", finish, "unknown", deck.Name, line.GetProperty("Quantity").GetInt32(), true);
                }
            }
        }
        db.PaperHoldings.AddRange(quantities.Values);
        var marker = new PaperImportPlan(label + "-testbezit — aangenomen, geen scan", keyName, "", JsonSerializer.Serialize(new Payload([], [], []))); marker.MarkApplied(); db.PaperImports.Add(marker); changed = true;
        }
        return changed;
    }, ct);
    private static async Task<(Dictionary<Guid, PaperCard> Cards, PaperHolding[] Stock)> Load(DeckbuilderDbContext db, CancellationToken ct)
    {
        var cards = (await db.PaperPrintings.AsNoTracking().ToArrayAsync(ct)).ToDictionary(x => x.Id, x => ScryfallPaperCatalog.Parse(x.MetadataJson, ScryfallPaperCatalog.Utc(x.FetchedUtc)));
        var all = await db.PaperHoldings.AsNoTracking().ToArrayAsync(ct); var real = all.Any(x => !x.IsDemo);
        return (cards, all.Where(x => x.IsDemo != real).ToArray());
    }
    public Task<PaperSummary> SummaryAsync(CancellationToken ct = default) => Query(async db =>
    {
        var (_, stock) = await Load(db, ct); return new PaperSummary(stock.Where(x => x.Quantity > 0).Select(x => x.PrintingId).Distinct().Count(), stock.Sum(x => x.Quantity), await db.PaperDecks.CountAsync(ct), stock.Any(x => x.IsDemo));
    }, ct);
    public Task<IReadOnlyList<PaperOwned>> CollectionAsync(string search = "", CancellationToken ct = default) => Query<IReadOnlyList<PaperOwned>>(async db =>
    {
        var (cards, stock) = await Load(db, ct);
        return stock.GroupBy(x => x.PrintingId).Select(g => new PaperOwned(cards[g.Key], g.Sum(x => x.Quantity),
                string.Join("\n", g.OrderBy(x => x.Binder).Select(x => $"{x.Quantity}× · {x.Language} · {x.Finish} · {x.Condition} · map {x.Binder}"))))
            .Where(x => x.Quantity > 0 && x.Card.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase)).OrderBy(x => x.Card.Name, StringComparer.Ordinal).ToArray();
    }, ct);
    public Task SetOwnedAsync(Guid printing, int quantity, CancellationToken ct = default) => Write(async db =>
    {
        var metadata = await db.PaperPrintings.FindAsync([printing], ct) ?? throw new ArgumentException("Kies een opgehaalde fysieke kaart.");
        var card = ScryfallPaperCatalog.Parse(metadata.MetadataJson, metadata.FetchedUtc); var key = PaperIdentity.Key(printing, card.Language, "unknown", "unknown", "Handmatig");
        var row = await db.PaperHoldings.SingleOrDefaultAsync(x => x.Key == key && !x.IsDemo, ct);
        if (row is null) db.PaperHoldings.Add(new(key, printing, card.Language, "unknown", "unknown", "Handmatig", quantity, false)); else row.SetQuantity(quantity);
        return true;
    }, ct);
    public async Task<PaperImportPreview> PrepareCsvAsync(string path, CancellationToken ct = default)
    {
        var info = new FileInfo(path); if (!info.Exists || info.Length is <= 0 or > 16 * 1024 * 1024) throw new ArgumentException("Kies een CSV van maximaal 16 MiB.");
        var bytes = await File.ReadAllBytesAsync(path, ct); if (bytes.Length > 16 * 1024 * 1024) throw new ArgumentException("CSV te groot.");
        var sourceHash = Convert.ToHexString(SHA256.HashData(bytes));
        var rows = await Task.Run(() => ManaBoxCsv.Parse(bytes), ct); var result = new List<PaperImportRow>();
        foreach (var source in rows)
        {
            ct.ThrowIfCancellationRequested();
            if (source.IsProxy || source.IsList) { result.Add(new(source, null, true, "Proxy of deck/list is geen geregistreerd echt bezit.")); continue; }
            try
            {
                var card = source.ScryfallId is { } id ? await catalog.GetAsync(id, ct: ct) : await catalog.GetAsync(source.Set, source.Collector, source.Language, ct);
                if (source.Language.Length > 0 && source.Language != card.Language)
                {
                    var variant = await catalog.GetAsync(card.Set, card.Number, source.Language, ct);
                    if (variant.OracleId != card.OracleId) throw new ArgumentException("Taalvariant verwijst naar een andere kaart."); card = variant;
                }
                var problem = source.Set.Length > 0 && !string.Equals(source.Set, card.Set, StringComparison.OrdinalIgnoreCase) || source.Collector.Length > 0 && source.Collector != card.Number ? "Scryfall-ID en set/nummer spreken elkaar tegen; controleer expliciet." : "";
                if (source.Finish != "unknown" && !card.Finishes.Contains(source.Finish)) problem = "Deze uitvoering is niet bevestigd voor de printing.";
                if (source.Name.Length > 0 && !string.Equals(source.Name, card.Name, StringComparison.OrdinalIgnoreCase)) problem = "Naam wijkt af (bijvoorbeeld vertaling); controleer de printing expliciet.";
                result.Add(new(source with { Language = card.Language }, card.Id, false, problem));
            }
            catch (ArgumentException ex) { result.Add(new(source, null, false, ex.Message)); }
        }
        return await Write(async db =>
        {
            var stock = await db.PaperHoldings.AsNoTracking().ToArrayAsync(ct);
            var plan = new PaperImportPlan(Path.GetFileName(path), sourceHash, Hash(stock), JsonSerializer.Serialize(new Payload(result.ToArray(), [], [])));
            db.PaperImports.Add(plan); return Preview(plan);
        }, ct);
    }
    public Task<PaperImportPreview> PreviewAsync(Guid id, CancellationToken ct = default) => Query(async db => Preview(await db.PaperImports.SingleAsync(x => x.Id == id, ct)), ct);
    public Task<PaperImportPreview> ResolveAsync(Guid id, Guid version, int row, Guid? target, bool skip, CancellationToken ct = default) => Write(async db =>
    {
        var plan = await db.PaperImports.SingleAsync(x => x.Id == id, ct); if (plan.State != 0 || plan.Version != version) throw new ArgumentException("Voorbeeld gewijzigd; open opnieuw.");
        var payload = Read(plan); var index = Array.FindIndex(payload.Rows, x => x.Source.Number == row); if (index < 0) throw new ArgumentException("Regel ontbreekt.");
        var original = payload.Rows[index];
        if (skip) payload.Rows[index] = original with { Skipped = true, Explicit = true };
        else
        {
            var p = await db.PaperPrintings.FindAsync([target ?? Guid.Empty], ct) ?? throw new ArgumentException("Zoek en selecteer eerst een fysieke doelprinting.");
            var c = ScryfallPaperCatalog.Parse(p.MetadataJson, p.FetchedUtc);
            if (original.Source.IsList || original.Source.IsProxy) throw new ArgumentException("Proxy/deck/list wordt niet als echt bezit geïmporteerd.");
            if (original.Source.Finish != "unknown" && !c.Finishes.Contains(original.Source.Finish)) throw new ArgumentException("Uitvoering niet beschikbaar; corrigeer de bron.");
            payload.Rows[index] = original with { PrintingId = c.Id, Source = original.Source with { Language = c.Language }, Problem = "", Skipped = false, Explicit = true };
        }
        plan.Update(JsonSerializer.Serialize(payload)); return Preview(plan);
    }, ct);
    public Task ApplyAsync(Guid id, Guid version, CancellationToken ct = default) => Write(async db =>
    {
        var plan = await db.PaperImports.SingleAsync(x => x.Id == id, ct); if (plan.State == 1) return false;
        if (plan.Version != version || !Preview(plan).CanApply) throw new ArgumentException("Importvoorbeeld gewijzigd of onvolledig.");
        var stock = await db.PaperHoldings.ToArrayAsync(ct); if (Hash(stock) != plan.StockHash) throw new ArgumentException("Fysiek bezit gewijzigd sinds voorbereiding; bereid opnieuw voor.");
        var payload = Read(plan); var before = new List<PaperStock>(); var after = new List<PaperStock>();
        foreach (var group in payload.Rows.Where(x => !x.Skipped).GroupBy(x => PaperIdentity.Key(x.PrintingId!.Value, x.Source.Language, x.Source.Finish, x.Source.Condition, x.Source.Binder)))
        {
            var first = group.First(); var quantity = group.Sum(x => x.Source.Quantity); Rules.Quantity(quantity);
            var printing = await db.PaperPrintings.FindAsync([first.PrintingId!.Value], ct) ?? throw new ArgumentException("Doelprinting ontbreekt; bereid opnieuw voor.");
            var card = ScryfallPaperCatalog.Parse(printing.MetadataJson, printing.FetchedUtc);
            if (card.Language != first.Source.Language || first.Source.Finish != "unknown" && !card.Finishes.Contains(first.Source.Finish))
                throw new ArgumentException("Kaartgegevens gewijzigd sinds voorbereiding; bereid opnieuw voor.");
            var old = stock.SingleOrDefault(x => x.Key == group.Key && !x.IsDemo);
            if (old is null)
            {
                old = new(group.Key, first.PrintingId!.Value, first.Source.Language, first.Source.Finish, first.Source.Condition, first.Source.Binder, quantity, false); db.PaperHoldings.Add(old);
            }
            else { before.Add(Stock(old)); if (old.Quantity != quantity) old.SetQuantity(quantity); }
            after.Add(Stock(old));
        }
        plan.Update(JsonSerializer.Serialize(payload with { Before = before.ToArray(), After = after.ToArray() })); plan.MarkApplied(); return true;
    }, ct);
    public Task UndoAsync(Guid id, CancellationToken ct = default) => Write(async db =>
    {
        var plan = await db.PaperImports.SingleAsync(x => x.Id == id, ct); var payload = Read(plan);
        if (plan.State != 1 || DemoKeys.Contains(plan.SourceHash)) throw new ArgumentException("Deze import kan niet worden hersteld.");
        var targets = new List<PaperHolding>();
        foreach (var expected in payload.After)
        {
            var actual = await db.PaperHoldings.FindAsync([expected.Id], ct);
            if (actual is null || Stock(actual) != expected) throw new ArgumentException("Bezitsaantal/versie na import gewijzigd; herstel geblokkeerd."); targets.Add(actual);
        }
        foreach (var target in targets)
        {
            var original = payload.Before.SingleOrDefault(x => x.Id == target.Id);
            if (original is null) db.PaperHoldings.Remove(target); else if (target.Quantity != original.Quantity) target.SetQuantity(original.Quantity);
        }
        plan.MarkUndone(); return true;
    }, ct);
    public Task<IReadOnlyList<PaperImportHistory>> HistoryAsync(CancellationToken ct = default) => Query<IReadOnlyList<PaperImportHistory>>(async db => await db.PaperImports.AsNoTracking().Where(x => !DemoKeys.Contains(x.SourceHash)).OrderByDescending(x => x.CreatedUtc).Take(100).Select(x => new PaperImportHistory(x.Id, x.FileName, x.State, x.CreatedUtc)).ToArrayAsync(ct), ct);
    public Task<IReadOnlyList<PaperDeckInfo>> DecksAsync(CancellationToken ct = default) => Query<IReadOnlyList<PaperDeckInfo>>(async db => await db.PaperDecks.AsNoTracking().OrderBy(x => x.Name).Select(x => new PaperDeckInfo(x.Id, x.Name, x.Profile, x.IsDemo)).ToArrayAsync(ct), ct);
    public Task<Guid> CreateDeckAsync(string name, CancellationToken ct = default) => Write(db => { var deck = new PaperDeck(name); db.PaperDecks.Add(deck); return Task.FromResult(deck.Id); }, ct);
    public Task DeleteDeckAsync(Guid id, CancellationToken ct = default) => Write(async db => { var deck = await db.PaperDecks.SingleAsync(x => x.Id == id, ct); db.PaperDecks.Remove(deck); return true; }, ct);
    public Task SetProfileAsync(Guid id, string profile, CancellationToken ct = default) => Write(async db => { (await db.PaperDecks.SingleAsync(x => x.Id == id, ct)).ChangeProfile(profile); return true; }, ct);
    public Task SetDeckCardAsync(Guid deck, Guid printing, DeckZone zone, int quantity, CancellationToken ct = default) => Write(async db =>
    {
        Rules.Quantity(quantity); var d = await db.PaperDecks.SingleAsync(x => x.Id == deck, ct); var p = await db.PaperPrintings.FindAsync([printing], ct) ?? throw new ArgumentException("Printing ontbreekt.");
        if (d.IsDemo) throw new ArgumentException("Maak eerst een bewerkbare kopie van het productvoorbeeld. Het oorspronkelijke productdeck blijft behouden.");
        _ = ScryfallPaperCatalog.Parse(p.MetadataJson, p.FetchedUtc); d.Touch();
        var row = await db.PaperDeckCards.FindAsync([deck, printing, zone], ct);
        if (quantity == 0) { if (row is not null) db.PaperDeckCards.Remove(row); }
        else if (row is null) db.PaperDeckCards.Add(new(deck, printing, zone, quantity)); else row.SetQuantity(quantity);
        return true;
    }, ct);
    private async Task<PaperDeckReport> Report(DeckbuilderDbContext db, Guid id, CancellationToken ct)
    {
        var deck = await db.PaperDecks.SingleAsync(x => x.Id == id, ct); var (cards, stock) = await Load(db, ct);
        var owned = stock.GroupBy(x => cards[x.PrintingId].OracleId).ToDictionary(x => x.Key, x => x.Sum(s => s.Quantity));
        var rows = await db.PaperDeckCards.Where(x => x.DeckId == id).OrderBy(x => x.Zone).ThenBy(x => x.PrintingId).ToArrayAsync(ct);
        return PaperDeckRules.Evaluate(new(deck.Id, deck.Name, deck.Profile, deck.IsDemo), deck.Version,
            rows.Select(x => new PaperDeckLine(cards[x.PrintingId], x.Zone, x.Quantity, owned.GetValueOrDefault(cards[x.PrintingId].OracleId))).ToArray(), clock.GetUtcNow().UtcDateTime);
    }
    public Task<PaperDeckReport> DeckAsync(Guid id, CancellationToken ct = default) => Query(db => Report(db, id, ct), ct);
    public Task<string> ExportDeckAsync(Guid id, CancellationToken ct = default) => Query(async db =>
    {
        var report = await Report(db, id, ct); return "About\nName " + report.Deck.Name + "\n\n" + string.Join("\n\n", report.Lines.GroupBy(x => x.Zone).Select(g =>
            (g.Key == DeckZone.Mainboard ? "Deck" : g.Key.ToString()) + "\n" + string.Join("\n", g.Select(x => $"{x.Quantity} {x.Card.Name} ({x.Card.Set.ToUpperInvariant()}) {x.Card.Number}"))));
    }, ct);
    private static string CardHash(IEnumerable<PaperCard> cards) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(cards.DistinctBy(x => x.Id).OrderBy(x => x.Id))));
    private async Task RefreshStaleOwnedSources(CancellationToken ct)
    {
        var now=clock.GetUtcNow().UtcDateTime;
        var ids=await Query(async db=>
        {
            var stock=await db.PaperHoldings.AsNoTracking().ToArrayAsync(ct);var real=stock.Any(x=>!x.IsDemo);
            var active=stock.Where(x=>x.IsDemo!=real&&x.Quantity>0).Select(x=>x.PrintingId).ToHashSet();
            var stamps=await db.PaperPrintings.AsNoTracking().Select(x=>new{x.Id,x.FetchedUtc}).ToArrayAsync(ct);
            return stamps.Where(x=>active.Contains(x.Id)&&(now-x.FetchedUtc>=TimeSpan.FromDays(1)||x.FetchedUtc>now.AddMinutes(5))).Select(x=>x.Id).ToArray();
        },ct);
        foreach(var id in ids)await catalog.GetAsync(id,true,ct);
    }
    public async Task<PaperDeckDraft> PrepareWizardAsync(DeckWizardRequest request,CancellationToken ct=default)
    {
        request.Validate();PaperCard? commander=null;
        if(request.Format=="commander")
        {commander=await catalog.GetAsync(request.Commander!.Value,ct:ct);if(SynergyProfiles.ComplexCard(commander))throw new ArgumentException("Deze commander heeft meerdere kanten/modi; kies een enkelzijdige commander voor automatische generatie.");if(!request.Allows(commander))throw new ArgumentException("De commander valt buiten je gekozen kleuridentiteit.");}
        if(request.Format!="casual60")await RefreshStaleOwnedSources(ct);
        var effective=commander is null?request:request with{Colors=commander.Colors,ColorMode=commander.Colors.Length==0?"colorless":"selected"};
        if(request.Planning)await catalog.WizardPoolAsync(effective,ct);
        return await Query(async db=>
        {
            var (cards,stock)=await Load(db,ct);var now=clock.GetUtcNow().UtcDateTime;
            var known=cards.Values.Where(effective.Allows).Where(c=>!c.OracleText.Contains("A deck can have",StringComparison.OrdinalIgnoreCase))
                .Where(c=>request.Format!="standard"||c.Standard=="legal"&&c.FetchedUtc<=now.AddMinutes(5)&&now-c.FetchedUtc<=TimeSpan.FromDays(1)).ToArray();
            var owned=stock.GroupBy(x=>x.PrintingId).Select(g=>new PaperOwned(cards[g.Key],g.Sum(x=>x.Quantity))).ToArray();
            if(request.Theme!="any")
            {
                var available=owned.Where(x=>x.Quantity>0).Select(x=>x.Card.OracleId).ToHashSet();
                if(!known.Any(c=>(request.Planning||available.Contains(c.OracleId))&&SynergyProfiles.AutomaticSelectionIssue(c)is null&&CardThemes.Matches(c,request.Theme)))
                    throw new ArgumentException("Geen kaarten met een ondersteunde themarol in de actuele beschikbare pool voor deze kleuren en dit formaat. Kies een ander thema of breid de passende collectie uit; planning kan formaatlegaliteit niet omzeilen.");
            }
            var rows=request.Format=="commander"?CommanderDeckPlanner.Create(cards[request.Commander!.Value],known,owned,request.Planning,now,request):PhysicalDeckPlanner.Create(known,owned,known,request.Planning,request);
            var draft=await Draft(db,request.Name,request.Format,rows,ct);
            if(draft.Report.Problems.Any(p=>!request.Planning||!p.Contains("tekort",StringComparison.Ordinal)))throw new ArgumentException("De gekozen combinatie voldoet niet aan de voorraad-/formaatcontrole. "+string.Join(" ",draft.Report.Problems.Take(3)));
            var themes=rows.Where(x=>!x.Card.TypeLine.Contains("Land",StringComparison.Ordinal)&&CardThemes.Matches(x.Card,request.Theme)).ToArray();
            if(request.Theme!="any"&&themes.Length==0)throw new ArgumentException("Geen kaarten met aanwijzingen voor dit thema gevonden. Kies een ander thema of bewust planning.");
            var explanation=$"\nWizard: {DeckWizardRequest.Formats.Single(x=>x.Key==request.Format).Label} · {DeckWizardRequest.Styles.Single(x=>x.Key==request.Style).Label}\n"+
                $"Toegestane kleuridentiteit: {(effective.AllowedColors.Length==0?"kleurloos":effective.AllowedColors)} · {(request.Planning?"PLANNING; tekorten zijn geen bezit":"BEZITMODUS; alleen actieve eigen voorraad")}\n"+
                $"Thema: {CardThemes.Choices.Single(x=>x.Key==request.Theme).Label} · {themes.Select(x=>x.Card.OracleId).Distinct().Count()} verschillende niet-landen met een ondersteunde themarol.\n"+
                "Selectie kijkt na iedere keuze naar ondersteuning, ontbrekende dragers, thema/stijl, curve en kaartkopieën. Geen kleurvoorkeur geeft een voorkeur aan minder verschillende castingkleuren; Domain mag breder gaan. Kleuren, formaat en voorraad blijven harde grenzen. Geen combo-, castingkans- of speelsterktegarantie. Vast plan: 24 basislanden bij 60 kaarten; 37 basislanden bij Commander. Scryfall-aanvulling gebruikt een beperkte pool op Commander-populariteit; dat bewijst geen geschiktheid of speelsterkte voor dit formaat.";
            return draft with{Report=draft.Report with{Analysis=draft.Report.Analysis+explanation,Synergy=SynergyEngine.ForTheme(draft.Report.Synergy!,request.Theme)}};
        },ct);
    }
    public async Task<PaperDeckDraft> PrepareCommanderAsync(string name, Guid commander, bool planning, CancellationToken ct = default)
    {
        var leader = await catalog.GetAsync(commander, ct: ct);
        if(SynergyProfiles.ComplexCard(leader))throw new ArgumentException("Deze commander heeft meerdere kanten/modi; automatische generatie vereist nu een enkelzijdige commander.");
        if (!leader.IsCommanderCandidate) throw new ArgumentException("Selecteer een ondersteunde commander; één legendary creature/Vehicle of geschikte Spacecraft, of expliciete kaartuitzondering.");
        await RefreshStaleOwnedSources(ct);
        if (planning) await catalog.CommanderPoolAsync(leader.Colors, ct);
        return await Query(async db =>
        {
            var (cards, stock) = await Load(db, ct); var currentLeader = cards[commander];
            var owned = stock.GroupBy(x => x.PrintingId).Select(g => new PaperOwned(cards[g.Key], g.Sum(x => x.Quantity))).ToArray();
            var rows = CommanderDeckPlanner.Create(currentLeader, cards.Values.ToArray(), owned, planning, clock.GetUtcNow().UtcDateTime);
            var draft = await Draft(db, name, "commander", rows, ct);
            if (draft.Report.Problems.Any(x => !planning || !x.Contains("tekort", StringComparison.Ordinal))) throw new ArgumentException("Commander-voorstel voldoet niet aan de ondersteunde regels; er is niets opgeslagen. " + string.Join(" ", draft.Report.Problems.Take(3)));
            return draft with { Report = draft.Report with { Analysis = draft.Report.Analysis + "\nCommander-constructievoorbeeld · " + (planning ? "PLANNING met zichtbare tekorten; Scryfall-aanvulling is geen bezit." : "BEZITMODUS; uitsluitend actieve fysieke voorraad.") +
                "\nVast plan: één commander + 62 singleton niet-landen + 37 basislanden. Bezit eerst, daarna beperkte tekstaanwijzingen en een vaste gedrukte-MV-curve; landen volgen een eenvoudige pipverhouding. Geen bewezen synergie, optimale manabase, bracket, cEDH- of winstclaim. Partner-/background-paren en variabele commander-kleurkeuze worden niet ondersteund." } };
        }, ct);
    }
    public Task<PaperDeckDraft> PrepareProposalAsync(string name, bool planning, CancellationToken ct = default) => Query(async db =>
    {
        var (cards, stock) = await Load(db, ct);
        using var stream = typeof(PaperCard).Assembly.GetManifestResourceStream("MTGDeckbuilder.Application.Resources.conflux-paper-v1.json")!;
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct); var fetched = document.RootElement.GetProperty("FetchedUtc").GetDateTime();
        var reference = document.RootElement.GetProperty("Cards").EnumerateArray().Select(c => ScryfallPaperCatalog.Parse(c.GetRawText(), fetched)).ToArray();
        var owned = stock.GroupBy(x => x.PrintingId).Select(g => new PaperOwned(cards[g.Key], g.Sum(x => x.Quantity))).ToArray();
        var lines = PhysicalDeckPlanner.Create(cards.Values.ToArray(), owned, reference, planning);
        var draft = await Draft(db, name, "casual60", lines, ct);
        if (!planning && draft.Report.Problems.Count > 0) throw new ArgumentException("Het bezitvoorstel voldoet niet aan de ondersteunde controles; er is niets opgeslagen.");
        var explanation = "\nConflux-constructievoorbeeld · " + (planning ? "PLANNING: niet alle kaarten hoeven in bezit te zijn; bekijk alle tekorten." : "BEZITMODUS: uitsluitend actieve fysieke voorraad; andere decks reserveren geen kaarten.") +
            "\nVast plan: 36 niet-landen, 24 basislanden. Selectie op gedrukte manawaarde/naam uit de bevroren Conflux-productpool; basislanden op aanwezige typen en verhouding van enkelkleurige manasymbolen. Geen mana-/castingkanssimulatie, brede synergie- of speelsterkteoptimalisatie. Casual60, geen Standard-voorstel.";
        return draft with { Report = draft.Report with { Analysis = draft.Report.Analysis + explanation } };
    }, ct);
    private async Task<PaperDeckDraft> Draft(DeckbuilderDbContext db, string name, string profile, IEnumerable<PaperDeckLine> lines, CancellationToken ct)
    {
        var d = new PaperDeck(name); d.ChangeProfile(profile); var (cards, stock) = await Load(db, ct);
        var own = stock.GroupBy(x => cards[x.PrintingId].OracleId).ToDictionary(x => x.Key, x => x.Sum(s => s.Quantity));
        var normalized = lines.GroupBy(x => (x.Card.Id, x.Zone)).Select(g => new PaperDeckLine(cards[g.Key.Id], g.Key.Zone, Rules.Quantity(g.Sum(x => x.Quantity), false), own.GetValueOrDefault(cards[g.Key.Id].OracleId))).ToArray();
        var all = await db.PaperHoldings.AsNoTracking().ToArrayAsync(ct);
        return new(d.Id, d.Name, profile, normalized, Hash(all), CardHash(cards.Values), PaperDeckRules.Evaluate(new(d.Id, d.Name, profile, false), d.Version, normalized, clock.GetUtcNow().UtcDateTime));
    }
    public async Task<PaperDeckDraft> PrepareDeckTextAsync(string text, string name, string language, CancellationToken ct = default)
    {
        var parsed = await Task.Run(() => ArenaDeckText.Parse(text, preserveCollectorCase: true), ct); language = ManaBoxCsv.Language(language);
        if (string.IsNullOrWhiteSpace(language)) throw new ArgumentException("Kies expliciet de taal voor decktekst-uitgaven.");
        var rows = new List<PaperDeckLine>();
        foreach (var row in parsed.Lines)
        {
            if (row.SetCode is null || row.CollectorNumber is null) throw new ArgumentException($"Regel {row.Number}: setcode en collectornummer ontbreken; geen automatische keuze op naam.");
            var card = await catalog.GetAsync(row.SetCode, row.CollectorNumber, language, ct);
            if (!string.Equals(card.Name, row.Name, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException($"Regel {row.Number}: kaartnaam en printing verschillen; corrigeer de decktekst.");
            rows.Add(new(card, row.Zone, row.Quantity, 0));
        }
        return await Query(db => Draft(db, name.Length > 0 ? name : parsed.Name ?? "Geïmporteerd fysiek deck", rows.Any(x => x.Zone == DeckZone.Commander) ? "commander" : "casual60", rows, ct), ct);
    }
    public Task<PaperDeckDraft> PrepareDeckCopyAsync(Guid deck, string name, Guid? source = null, Guid? replacement = null, int quantity = 0, CancellationToken ct = default) => Query(async db =>
    {
        var report = await Report(db, deck, ct); var rows = report.Lines.ToList();
        if (source is not null || replacement is not null)
        {
            if (source is null || replacement is null || source == replacement || quantity < 1) throw new ArgumentException("Kies bron, andere doelkaart en een positief aantal.");
            var sourceLine = rows.SingleOrDefault(x => x.Card.Id == source && x.Zone == DeckZone.Mainboard) ?? throw new ArgumentException("Vervanging ondersteunt een geselecteerde mainboardprinting.");
            if (quantity > sourceLine.Quantity) throw new ArgumentException("Aantal groter dan de bronregel.");
            var (cards, _) = await Load(db, ct); if (!cards.TryGetValue(replacement.Value, out var card)) throw new ArgumentException("Doelprinting ontbreekt.");
            rows.Remove(sourceLine); if (quantity < sourceLine.Quantity) rows.Add(sourceLine with { Quantity = sourceLine.Quantity - quantity }); rows.Add(new(card, DeckZone.Mainboard, quantity, 0));
        }
        var draft = await Draft(db, name, report.Deck.Profile, rows, ct);
        if (replacement is not null && draft.Report.Problems.Any(x => x.Contains("tekort", StringComparison.Ordinal) || x.Contains("maximaal vier", StringComparison.Ordinal) || x.Contains("Standard", StringComparison.Ordinal) || x.Contains("niet actueel", StringComparison.Ordinal) || x.Contains("regelcontrole", StringComparison.Ordinal)))
            throw new ArgumentException("Vervanging heeft tekorten, kopielimieten of ongeldige/verouderde formaatbronnen; bekijk het hele deck.");
        return draft;
    }, ct);
    public Task<Guid> SaveDeckDraftAsync(PaperDeckDraft draft, CancellationToken ct = default) => Write(async db =>
    {
        if (await db.PaperDecks.AnyAsync(x => x.Id == draft.Id, ct)) return draft.Id;
        var all = await db.PaperHoldings.AsNoTracking().ToArrayAsync(ct); if (Hash(all) != draft.StockFingerprint) throw new ArgumentException("Voorraad gewijzigd; bereid het deckvoorbeeld opnieuw voor.");
        var (cards, _) = await Load(db, ct);
        if (draft.Lines.Any(x => !cards.ContainsKey(x.Card.Id)) || CardHash(cards.Values) != draft.CardsFingerprint)
            throw new ArgumentException("Kaartgegevens gewijzigd; bereid het deckvoorbeeld opnieuw voor.");
        if (draft.Profile is "commander" or "standard")
        {
            var check = PaperDeckRules.Evaluate(new(draft.Id, draft.Name, draft.Profile, false), Guid.NewGuid(), draft.Lines.Select(x => x with { Card = cards[x.Card.Id] }).ToArray(), clock.GetUtcNow().UtcDateTime);
            if (check.Problems.Any(x => !x.Contains("tekort", StringComparison.Ordinal))) throw new ArgumentException("Formaatregels/bronnen niet meer geldig; bereid opnieuw voor.");
        }
        var deck = new PaperDeck(draft.Name, id: draft.Id); deck.ChangeProfile(draft.Profile); db.PaperDecks.Add(deck);
        foreach (var group in draft.Lines.GroupBy(x => (x.Card.Id, x.Zone))) db.PaperDeckCards.Add(new(deck.Id, group.Key.Id, group.Key.Zone, group.Sum(x => x.Quantity)));
        return deck.Id;
    }, ct);
    public Task<IReadOnlyList<PaperAlternative>> AlternativesAsync(Guid deck, Guid printing, CancellationToken ct = default) => Query<IReadOnlyList<PaperAlternative>>(async db =>
    {
        var report = await Report(db, deck, ct); var (cards, stock) = await Load(db, ct); var source = cards[printing]; var available = stock.GroupBy(x => cards[x.PrintingId].OracleId).ToDictionary(x => x.Key, x => x.Sum(s => s.Quantity));
        var now = clock.GetUtcNow().UtcDateTime;
        var commanderMode = report.Deck.Profile == "commander"; var limit = commanderMode ? 1 : 4;
        var leaders = report.Lines.Where(x => x.Zone == DeckZone.Commander).ToArray();
        var leader = leaders.Length == 1 && leaders[0].Quantity == 1 ? leaders[0].Card : null;
        if (!report.Lines.Any(x => x.Card.Id == printing)) throw new ArgumentException("Selecteer een kaart uit dit deck.");
        if (report.Deck.Profile == "standard" && (now < PaperDeckRules.CheckedUtc || now - PaperDeckRules.CheckedUtc > TimeSpan.FromDays(14) || source.Standard != "legal" || now - source.FetchedUtc > TimeSpan.FromDays(1) || source.FetchedUtc > now.AddMinutes(5)))
            throw new ArgumentException("Bronkaart of papieren regels niet geldig/actueel; geen Standard-alternatiefadvies.");
        if (commanderMode && (leader is null || !leader.IsCommanderCandidate || !CommanderDeckPlanner.FreshLegal(source, now) || now < PaperDeckRules.CheckedUtc || now - PaperDeckRules.CheckedUtc > TimeSpan.FromDays(14) || report.Lines.Any(x => x.Card.Id == printing && x.Zone == DeckZone.Commander)))
            throw new ArgumentException("Commander/bron niet geldig of actueel, of commander-zone geselecteerd; kies een librarykaart en herstel eerst het profiel.");
        var used = report.Lines.GroupBy(x => x.Card.OracleId).ToDictionary(x => x.Key, x => x.Sum(l => l.Quantity));
        return stock.Select(x => x.PrintingId).Distinct().Where(x => x != printing).Select(x => cards[x]).Where(c =>
                available.GetValueOrDefault(c.OracleId) - used.GetValueOrDefault(c.OracleId) > 0 && (c.IsBasicLand || used.GetValueOrDefault(c.OracleId) < limit) &&
                (!commanderMode || CommanderDeckPlanner.FreshLegal(c, now) && PaperDeckRules.ColorsFit(c, leader!)) &&
                (report.Deck.Profile != "standard" || c.Standard == "legal" && now - c.FetchedUtc < TimeSpan.FromDays(1) && c.FetchedUtc <= now.AddMinutes(5)))
            .Select(c => new { Card = c, Match = CardRoleClassifier.Compare(source.AsAdvice(), c.AsAdvice()),Synergy=SynergyReplacement.Compare(source,c,report.Lines) })
            .Where(x => x.Card.OracleId == source.OracleId || SynergyProfiles.AutomaticSelectionIssue(source) is null&&SynergyProfiles.AutomaticSelectionIssue(x.Card) is null&&(x.Match.Fit == RoleFit.Matching && x.Match.Source.Complete && x.Match.Candidate.Complete||x.Synergy.ThematicRole) && x.Card.Colors.All(source.Colors.Contains))
            .OrderBy(x => x.Card.OracleId == source.OracleId ? 0 : 1).ThenByDescending(x=>x.Synergy.ContextChange).ThenBy(x => Math.Abs(x.Card.ManaValue - source.ManaValue)).ThenBy(x => x.Card.Name, StringComparer.Ordinal)
            .Take(20).Select(x => new PaperAlternative(x.Card, Math.Max(0, Math.Min(available[x.Card.OracleId] - used.GetValueOrDefault(x.Card.OracleId), x.Card.IsBasicLand ? int.MaxValue : limit - used.GetValueOrDefault(x.Card.OracleId))), x.Card.OracleId == source.OracleId ? "Andere printing van dezelfde Oracle-kaart" : x.Synergy.ThematicRole?x.Synergy.Explanation:x.Match.Label + "; controleer voorwaarden en hele deck; geen bewezen strategische gelijkwaardigheid")).ToArray();
    }, ct);
}
