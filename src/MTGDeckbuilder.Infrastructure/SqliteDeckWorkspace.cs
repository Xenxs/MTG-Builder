using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MTGDeckbuilder.Application;
using MTGDeckbuilder.Domain;

namespace MTGDeckbuilder.Infrastructure;

public sealed class SqliteDeckWorkspace(IDbContextFactory<DeckbuilderDbContext> factory,
    DatabaseWriteLock writes, ICollectionTransfer choices, ILogger<SqliteDeckWorkspace> logger) : IDeckWorkspace
{
    private sealed class Target
    {
        public Guid Id { get; init; }
        public Guid CardId { get; init; }
        public string? OracleId { get; init; }
        public string? ScryfallId { get; init; }
        public string Name { get; init; } = "";
        public string SetCode { get; init; } = "";
        public string CollectorNumber { get; init; } = "";
        public string Language { get; init; } = "";
        public bool Active { get; init; }
        public int Owned { get; init; }
        public CollectionTarget Dto => new(Id, Name, SetCode, CollectorNumber, Language, ScryfallId is null, Owned);
        public string Signature => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
            new { Id, CardId, OracleId, ScryfallId, Name, SetCode, CollectorNumber, Language }))));
    }
    private static IQueryable<Target> Targets(DeckbuilderDbContext db) =>
        from p in db.Printings.AsNoTracking()
        join c in db.Cards.AsNoTracking() on p.CardId equals c.Id
        join e in db.CollectionEntries.AsNoTracking() on p.Id equals e.PrintingId into owned
        from e in owned.DefaultIfEmpty()
        select new Target { Id = p.Id, CardId = c.Id, OracleId = c.OracleId, ScryfallId = p.ScryfallId,
            Name = c.Name, SetCode = p.SetCode, CollectorNumber = p.CollectorNumber, Language = p.Language,
            Active = p.IsInCatalog, Owned = e == null ? 0 : e.Quantity };

    private async Task<T> WriteAsync<T>(Func<DeckbuilderDbContext, Task<T>> action, CancellationToken ct) => await Task.Run(async () =>
    {
        await writes.Gate.WaitAsync(ct);
        try
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var result = await action(db);
            ct.ThrowIfCancellationRequested(); await db.SaveChangesAsync(ct); ct.ThrowIfCancellationRequested();
            await transaction.CommitAsync(ct); return result;
        }
        catch (DbUpdateConcurrencyException ex) { throw new ArgumentException("De deckvoorbereiding is gewijzigd. Open deze opnieuw.", ex); }
        finally { writes.Gate.Release(); }
    }, ct);

    private static async Task<DeckImportBatch> Batch(DeckbuilderDbContext db, Guid id, Guid? version, CancellationToken ct)
    {
        var batch = await db.DeckImports.FindAsync([Rules.Id(id)], ct) ?? throw new ArgumentException("Deckimport niet gevonden.");
        if (version is not null && batch.Version != version) throw new ArgumentException("De voorbereiding is gewijzigd. Open de actuele voorbereiding.");
        return batch;
    }
    private static string FrontName(string name) => name.Split(" // ", StringSplitOptions.None)[0];
    private static bool SameName(string input, string name) => string.Equals(input, name, StringComparison.OrdinalIgnoreCase) ||
        name.Contains(" // ") && string.Equals(input, FrontName(name), StringComparison.OrdinalIgnoreCase);
    private static void Bind(DeckImportRow row, Target target, string reason)
    { row.PrintingId = target.Id; row.PrintingSignature = target.Signature; row.Reason = reason; }

    public async Task<Guid> PrepareAsync(string text, string name, string format, CancellationToken ct = default)
    {
        var parsed = await Task.Run(() => ArenaDeckText.Parse(text), ct);
        var normalizedName = ArenaDeckText.SafeText(string.IsNullOrWhiteSpace(name) ? parsed.Name ?? "Ingevoerd deck" : name, "Decknaam", 120);
        format = ArenaDeckText.SafeText(format, "Formaat", 40);
        return await WriteAsync(async db =>
        {
            var batch = new DeckImportBatch(normalizedName, format, text);
            db.DeckImports.Add(batch);
            var targets = await Targets(db).ToListAsync(ct);
            var byName = targets.SelectMany(t => t.Name.Contains(" // ") ? new[] { (Name: t.Name, Target: t), (Name: FrontName(t.Name), Target: t) } : [(Name: t.Name, Target: t)])
                .ToLookup(x => x.Name, x => x.Target, StringComparer.OrdinalIgnoreCase);
            foreach (var line in parsed.Lines)
            {
                ct.ThrowIfCancellationRequested();
                var candidates = byName[line.Name].Where(t => line.SetCode is null ||
                    t.SetCode == line.SetCode && t.CollectorNumber == line.CollectorNumber).DistinctBy(t => t.Id).ToList();
                var row = new DeckImportRow
                {
                    BatchId = batch.Id, Number = line.Number, Name = line.Name, SetCode = line.SetCode,
                    CollectorNumber = line.CollectorNumber, Quantity = line.Quantity, Zone = line.Zone,
                    Reason = candidates.Count == 0 ? "Onbekende kaart/printing. Kies expliciet een bestaande printing of pas de bron aan." : "Meerdere mogelijke identiteiten. Kies expliciet een printing."
                };
                if (candidates.Count == 1) Bind(row, candidates[0], "Eenduidige koppeling");
                else if (line.SetCode is null && candidates.Count > 1 && candidates.All(x => x.OracleId is not null) &&
                    candidates.Select(x => x.OracleId).Distinct().Count() == 1)
                {
                    var preferred = candidates.OrderByDescending(x => x.Owned).ThenByDescending(x => x.Language == "en")
                        .ThenByDescending(x => x.Active).ThenBy(x => x.SetCode, StringComparer.Ordinal).ThenBy(x => x.CollectorNumber, StringComparer.Ordinal).ThenBy(x => x.Id).First();
                    Bind(row, preferred, "Op kaartnaam: één Oracle-identiteit; uitgave met meest lokaal bezit heeft voorrang");
                }
                db.DeckImportRows.Add(row);
            }
            logger.LogInformation("Deck import {BatchId} prepared with {Count} rows", batch.Id, parsed.Lines.Count);
            return batch.Id;
        }, ct);
    }

    public Task<DeckImportPreview> GetPreviewAsync(Guid id, int page = 1, CancellationToken ct = default) => Task.Run(async () =>
    {
        if (page < 1) throw new ArgumentException("Ongeldige pagina.");
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var batch = await Batch(db, id, null, ct);
        var query = db.DeckImportRows.AsNoTracking().Where(x => x.BatchId == id);
        var total = await query.CountAsync(ct); page = Math.Min(page, Math.Max(1, (total + 49) / 50));
        var rows = await query.OrderBy(x => x.Number).Skip((page - 1) * 50).Take(50).ToListAsync(ct);
        var ids = rows.Where(x => x.PrintingId is not null).Select(x => x.PrintingId!.Value).ToList();
        var targets = (await Targets(db).Where(x => ids.Contains(x.Id)).ToListAsync(ct)).ToDictionary(x => x.Id);
        return new DeckImportPreview(batch.Id, batch.Version, batch.Name, batch.Format, batch.SourceText, batch.State, batch.DeckId,
            total, await query.CountAsync(x => x.PrintingId == null, ct), await query.SumAsync(x => x.Quantity, ct),
            await query.Where(x => x.PrintingId != null).GroupBy(x => new { x.PrintingId, x.Zone }).CountAsync(x => x.Sum(r => r.Quantity) > 9999, ct), page,
            rows.Select(x => new DeckImportPreviewRow(x.Number, x.Name, x.Quantity, x.Zone, x.PrintingId,
                x.PrintingId is { } pid && targets.TryGetValue(pid, out var target) ? target.Dto.IdentityLabel : "", x.Reason)).ToList());
    }, ct);

    public Task<IReadOnlyList<CollectionTarget>> GetCandidatesAsync(Guid id, int number, CancellationToken ct = default) => Task.Run(async () =>
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var row = await db.DeckImportRows.AsNoTracking().SingleOrDefaultAsync(x => x.BatchId == id && x.Number == number, ct) ?? throw new ArgumentException("Deckregel niet gevonden.");
        var targets = await Targets(db).ToListAsync(ct);
        return (IReadOnlyList<CollectionTarget>)targets.Where(x => SameName(row.Name, x.Name) && (row.SetCode is null ||
            x.SetCode == row.SetCode && x.CollectorNumber == row.CollectorNumber)).OrderByDescending(x => x.Owned).ThenBy(x => x.Id).Take(50).Select(x => x.Dto).ToList();
    }, ct);
    public Task<IReadOnlyList<CollectionTarget>> SearchTargetsAsync(string search, CancellationToken ct = default) => choices.SearchTargetsAsync(search, ct);

    public async Task ResolveAsync(Guid id, Guid version, int number, Guid targetId, CancellationToken ct = default)
        => await WriteAsync(async db =>
        {
            var batch = await Batch(db, id, version, ct); batch.Touch();
            var row = await db.DeckImportRows.FindAsync([id, number], ct) ?? throw new ArgumentException("Deckregel niet gevonden.");
            var target = await Targets(db).SingleOrDefaultAsync(x => x.Id == targetId, ct) ?? throw new ArgumentException("Printing niet gevonden.");
            Bind(row, target, "Expliciet gekoppeld door gebruiker"); return true;
        }, ct);

    public Task<Guid> ApplyAsync(Guid id, Guid version, CancellationToken ct = default) => WriteAsync(async db =>
    {
        var batch = await Batch(db, id, version, ct); batch.RequirePrepared();
        var rows = await db.DeckImportRows.Where(x => x.BatchId == id).ToListAsync(ct);
        if (rows.Count == 0 || rows.Any(x => x.PrintingId is null)) throw new ArgumentException("Koppel eerst alle deckregels; er wordt niets overgeslagen.");
        var ids = rows.Select(x => x.PrintingId!.Value).Distinct().ToList();
        var targets = (await Targets(db).Where(x => ids.Contains(x.Id)).ToListAsync(ct)).ToDictionary(x => x.Id);
        foreach (var row in rows)
            if (!targets.TryGetValue(row.PrintingId!.Value, out var target) || target.Signature != row.PrintingSignature)
                throw new ArgumentException($"Regel {row.Number}: de kaartidentiteit is gewijzigd. Bereid het deck opnieuw voor.");
        var grouped = rows.GroupBy(x => new { Printing = x.PrintingId!.Value, x.Zone }).Select(x => new { x.Key.Printing, x.Key.Zone, Quantity = x.Sum(r => r.Quantity) }).ToList();
        if (grouped.Any(x => x.Quantity > 9999)) throw new ArgumentException("De samengevoegde regels overschrijden 9999 exemplaren voor één printing/zone.");
        var deck = new Deck(batch.Name, batch.Format); db.Decks.Add(deck);
        foreach (var group in grouped) db.DeckCards.Add(new DeckCard(deck.Id, group.Printing, group.Zone, group.Quantity));
        batch.Apply(deck.Id);
        logger.LogInformation("Deck import {BatchId} creates deck {DeckId} with {Rows} rows", id, deck.Id, grouped.Count);
        return deck.Id;
    }, ct);

    public async Task DiscardAsync(Guid id, Guid version, CancellationToken ct = default)
        => await WriteAsync(async db => { (await Batch(db, id, version, ct)).Discard(); return true; }, ct);
    public Task<IReadOnlyList<DeckImportHistory>> GetHistoryAsync(int page = 1, CancellationToken ct = default) => Task.Run(async () =>
    {
        if (page is < 1 or > 1000000) throw new ArgumentException("Ongeldige historiepagina.");
        await using var db = await factory.CreateDbContextAsync(ct);
        return (IReadOnlyList<DeckImportHistory>)await db.DeckImports.AsNoTracking().OrderByDescending(x => x.CreatedUtc).ThenBy(x => x.Id)
            .Skip((page - 1) * 50).Take(50).Select(x => new DeckImportHistory(x.Id, x.Name, x.Format, x.CreatedUtc, x.State, x.DeckId)).ToListAsync(ct);
    }, ct);

    public Task<string> ExportAsync(Guid deckId, CancellationToken ct = default) => Task.Run(async () =>
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var deck = await db.Decks.AsNoTracking().SingleOrDefaultAsync(x => x.Id == deckId, ct) ?? throw new ArgumentException("Deck niet gevonden.");
        var cards = await (from d in db.DeckCards.AsNoTracking() join p in db.Printings.AsNoTracking() on d.PrintingId equals p.Id
            join c in db.Cards.AsNoTracking() on p.CardId equals c.Id where d.DeckId == deckId
            select new { d.Zone, d.Quantity, c.Name, p.SetCode, p.CollectorNumber }).ToListAsync(ct);
        if (cards.Count == 0) throw new ArgumentException("Een leeg deck bevat niets om te exporteren.");
        var output = new StringBuilder();
        output.AppendLine("About").Append("Name ").AppendLine(ArenaDeckText.SafeText(deck.Name, "Decknaam", 120)).AppendLine();
        foreach (var zone in new[] { DeckZone.Commander, DeckZone.Mainboard, DeckZone.Sideboard })
        {
            var group = cards.Where(x => x.Zone == zone).OrderBy(x => x.Name, StringComparer.Ordinal).ThenBy(x => x.SetCode, StringComparer.Ordinal).ThenBy(x => x.CollectorNumber, StringComparer.Ordinal).ToList();
            if (group.Count == 0) continue;
            output.AppendLine(zone == DeckZone.Mainboard ? "Deck" : zone.ToString());
            foreach (var card in group)
            {
                var set = ArenaDeckText.SafeText(card.SetCode, "Setcode", 12);
                var collector = ArenaDeckText.SafeText(card.CollectorNumber, "Collectornummer", 20);
                var line = $"{card.Quantity} {ArenaDeckText.SafeText(card.Name, "Kaartnaam", 200)} ({set}) {collector}";
                var parsed = ArenaDeckText.Parse(line).Lines.Single();
                if (parsed.SetCode != card.SetCode || parsed.CollectorNumber != card.CollectorNumber || parsed.Name != card.Name)
                    throw new ArgumentException("Een handmatige printing bevat een setcode/nummer dat niet naar Arena-tekst kan worden geëxporteerd.");
                output.AppendLine(line);
            }
            output.AppendLine();
        }
        var text = output.ToString().TrimEnd() + Environment.NewLine;
        ArenaDeckText.Parse(text); // Guarantee this export stays within the supported input limits.
        return text;
    }, ct);

    public Task<DeckAnalysis> AnalyzeAsync(Guid deckId, CancellationToken ct = default) => Task.Run(async () =>
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (!await db.Decks.AnyAsync(x => x.Id == deckId, ct)) throw new ArgumentException("Deck niet gevonden.");
        var cards = await (from d in db.DeckCards.AsNoTracking() join p in db.Printings.AsNoTracking() on d.PrintingId equals p.Id
            join c in db.Cards.AsNoTracking() on p.CardId equals c.Id where d.DeckId == deckId
            select new AnalysisCard(p.Id, c.OracleId, c.Name, d.Zone, d.Quantity, c.TypeLine, c.OracleId == null ? null : c.ManaValue, c.ColorIdentity)).ToListAsync(ct);
        var owned = await (from e in db.CollectionEntries.AsNoTracking() join p in db.Printings.AsNoTracking() on e.PrintingId equals p.Id
            join c in db.Cards.AsNoTracking() on p.CardId equals c.Id select new OwnedCard(p.Id, c.OracleId, e.Quantity)).ToListAsync(ct);
        return DeckAnalyzer.Analyze(cards, owned);
    }, ct);
}
