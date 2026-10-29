using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MTGDeckbuilder.Application;
using MTGDeckbuilder.Domain;

namespace MTGDeckbuilder.Infrastructure;

public sealed class SqliteCollectionTransfer(IDbContextFactory<DeckbuilderDbContext> factory,
    DatabaseWriteLock writes, ILogger<SqliteCollectionTransfer> logger) : ICollectionTransfer
{
    private const int PageSize = 50;
    private sealed class Target
    {
        public Guid Id { get; init; }
        public Guid CardId { get; init; }
        public string Name { get; init; } = "";
        public string SearchName { get; init; } = "";
        public string SetCode { get; init; } = "";
        public string CollectorNumber { get; init; } = "";
        public string Language { get; init; } = "";
        public string? ScryfallId { get; init; }
        public int? ArenaId { get; init; }
        public int? Quantity { get; init; }
        public Guid? Version { get; init; }
        public CollectionTarget Dto => new(Id, Name, SetCode, CollectorNumber, Language, ScryfallId is null, Quantity ?? 0);
        public string Signature => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
            new { Id, CardId, Name, SetCode, CollectorNumber, Language, ScryfallId, ArenaId }))));
    }
    private static IQueryable<Target> Targets(DeckbuilderDbContext db) =>
        from p in db.Printings.AsNoTracking()
        join c in db.Cards.AsNoTracking() on p.CardId equals c.Id
        join e in db.CollectionEntries.AsNoTracking() on p.Id equals e.PrintingId into entries
        from e in entries.DefaultIfEmpty()
        select new Target { Id = p.Id, CardId = p.CardId, Name = c.Name, SearchName = c.SearchName, SetCode = p.SetCode,
            CollectorNumber = p.CollectorNumber, Language = p.Language, ScryfallId = p.ScryfallId,
            ArenaId = p.ArenaId, Quantity = e == null ? null : e.Quantity, Version = e == null ? null : e.Version };

    private static List<Target> Match(CollectionSource source, IReadOnlyList<Target> targets) => targets.Where(t =>
        (source.ScryfallId is not null ? t.ScryfallId == source.ScryfallId : source.ArenaId is not null ? t.ArenaId == source.ArenaId :
            t.SetCode == source.SetCode && t.CollectorNumber == source.CollectorNumber && t.Language == source.Language) &&
        (source.Name is null || string.Equals(source.Name, t.Name, StringComparison.OrdinalIgnoreCase))).ToList();

    private async Task<T> WriteAsync<T>(Func<DeckbuilderDbContext, Task<T>> action, CancellationToken ct)
    {
        return await Task.Run(async () =>
        {
            await writes.Gate.WaitAsync(ct);
            try
            {
                await using var db = await factory.CreateDbContextAsync(ct);
                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                var result = await action(db);
                ct.ThrowIfCancellationRequested();
                await db.SaveChangesAsync(ct);
                ct.ThrowIfCancellationRequested();
                await transaction.CommitAsync(ct);
                return result;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                throw new ArgumentException("De gegevens zijn inmiddels gewijzigd. Open of bereid de import opnieuw voor.", ex);
            }
            finally { writes.Gate.Release(); }
        }, ct);
    }

    public async Task<Guid> PrepareAsync(string path, CancellationToken ct = default)
    {
        var parsed = await Task.Run(() => CollectionFile.ReadAsync(path, ct), ct);
        return await WriteAsync(async db =>
        {
            // Index by source identity so large collections do not scan the full catalog per row.
            var targets = await Targets(db).ToListAsync(ct);
            var byScryfall = targets.Where(x => x.ScryfallId is not null).ToLookup(x => x.ScryfallId!);
            var byArena = targets.Where(x => x.ArenaId is not null).ToLookup(x => x.ArenaId!.Value);
            var byNatural = targets.ToLookup(x => (x.SetCode, x.CollectorNumber, x.Language));
            var batch = new CollectionImportBatch(Path.GetFileName(path), parsed.Hash);
            db.CollectionImports.Add(batch);
            int number = 0;
            foreach (var source in parsed.Cards)
            {
                ct.ThrowIfCancellationRequested();
                IEnumerable<Target> candidates = source.ScryfallId is not null ? byScryfall[source.ScryfallId] :
                    source.ArenaId is { } arena ? byArena[arena] : byNatural[(source.SetCode!, source.CollectorNumber!, source.Language!)];
                var matches = Match(source, candidates.ToList());
                var row = new CollectionImportRow
                {
                    BatchId = batch.Id, Number = ++number, SourceData = JsonSerializer.Serialize(source),
                    SourceLabel = source.Label, Quantity = source.Quantity,
                    MatchReason = matches.Count == 0 ? "Geen eenduidige koppeling gevonden. Kies een bestaande printing of sla deze regel over." :
                        matches.Count > 1 ? $"{matches.Count} mogelijke printings. Kies expliciet een printing." : "Eenduidig gekoppeld"
                };
                if (matches.Count == 1) Bind(row, matches[0]);
                db.CollectionImportRows.Add(row);
            }
            logger.LogInformation("Collection preview {BatchId} prepared with {Count} rows", batch.Id, number);
            return batch.Id;
        }, ct);
    }

    private static void Bind(CollectionImportRow row, Target target)
    {
        row.PrintingId = target.Id; row.BeforeQuantity = target.Quantity; row.BeforeVersion = target.Version;
        row.PrintingSignature = target.Signature; row.Skipped = false;
    }
    private static async Task<CollectionImportBatch> BatchAsync(DeckbuilderDbContext db, Guid id, Guid? expected, CancellationToken ct)
    {
        var batch = await db.CollectionImports.FindAsync([Rules.Id(id)], ct) ?? throw new ArgumentException("Import niet gevonden.");
        if (expected is { } version && batch.Version != version)
            throw new ArgumentException("De voorbereiding is gewijzigd. Open deze opnieuw voordat je verdergaat.");
        return batch;
    }

    public Task<CollectionPreview> GetPreviewAsync(Guid batchId, int page = 1, CancellationToken ct = default) => Task.Run(async () =>
    {
        if (page < 1) throw new ArgumentException("Ongeldige pagina.");
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var batch = await BatchAsync(db, batchId, null, ct);
        var query = db.CollectionImportRows.AsNoTracking().Where(x => x.BatchId == batchId);
        var total = await query.CountAsync(ct);
        page = Math.Min(page, Math.Max(1, (total + PageSize - 1) / PageSize));
        var duplicates = await query.Where(x => !x.Skipped && x.PrintingId != null).GroupBy(x => x.PrintingId)
            .Where(x => x.Count() > 1).Select(x => x.Key!.Value).ToListAsync(ct);
        var rows = await query.OrderBy(x => x.Number).Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(ct);
        var ids = rows.Where(x => x.PrintingId != null).Select(x => x.PrintingId!.Value).ToList();
        var targets = (await Targets(db).Where(x => ids.Contains(x.Id)).ToListAsync(ct)).ToDictionary(x => x.Id);
        var dtos = rows.Select(r =>
        {
            var target = r.PrintingId is { } id ? targets.GetValueOrDefault(id) : null;
            var problem = r.PrintingId is null ? r.MatchReason : duplicates.Contains(r.PrintingId.Value) ?
                "Meerdere bronregels wijzen naar deze printing. Sla de dubbele regel over of wijzig de koppeling." : "";
            return new CollectionPreviewRow(r.Number, r.SourceLabel, r.Quantity, r.PrintingId,
                target?.Dto.IdentityLabel ?? "", r.BeforeQuantity, r.Skipped, problem, r.ExplicitChoice);
        }).ToList();
        return new CollectionPreview(batch.Id, batch.Version, batch.FileName, batch.State, total,
            await query.CountAsync(x => !x.Skipped && x.PrintingId == null, ct), duplicates.Count,
            await query.CountAsync(x => x.Skipped, ct),
            await query.CountAsync(x => !x.Skipped && x.PrintingId != null && (x.BeforeQuantity == null || x.BeforeQuantity != x.Quantity), ct), page, dtos);
    }, ct);

    public Task<IReadOnlyList<CollectionTarget>> GetCandidatesAsync(Guid batchId, int number, CancellationToken ct = default) => Task.Run(async () =>
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var row = await db.CollectionImportRows.AsNoTracking().SingleOrDefaultAsync(x => x.BatchId == batchId && x.Number == number, ct)
            ?? throw new ArgumentException("Importregel niet gevonden.");
        var source = JsonSerializer.Deserialize<CollectionSource>(row.SourceData)!;
        var query = Targets(db);
        // Filter by identity in SQL; a supplied name mismatch remains visible for explicit repair.
        if (source.ScryfallId is not null) query = query.Where(x => x.ScryfallId == source.ScryfallId);
        else if (source.ArenaId is not null) query = query.Where(x => x.ArenaId == source.ArenaId);
        else query = query.Where(x => x.SetCode == source.SetCode && x.CollectorNumber == source.CollectorNumber && x.Language == source.Language);
        var targets = await query.OrderBy(x => x.Name).ThenBy(x => x.Id).Take(50).ToListAsync(ct);
        return (IReadOnlyList<CollectionTarget>)targets.Select(x => x.Dto).ToList();
    }, ct);

    public Task<IReadOnlyList<CollectionTarget>> SearchTargetsAsync(string search, CancellationToken ct = default) => Task.Run(async () =>
    {
        search = search.Trim().ToUpperInvariant();
        if (search.Length < 2 || search.Length > 200) throw new ArgumentException("Zoek met 2 tot 200 tekens op naam, set of collectornummer.");
        await using var db = await factory.CreateDbContextAsync(ct);
        var targets = await Targets(db).Where(x => x.SearchName.Contains(search) || x.SetCode == search || x.CollectorNumber == search)
            .OrderBy(x => x.Name).ThenBy(x => x.SetCode).ThenBy(x => x.CollectorNumber).ThenBy(x => x.Id).Take(50).ToListAsync(ct);
        return (IReadOnlyList<CollectionTarget>)targets.Select(x => x.Dto).ToList();
    }, ct);

    public async Task ResolveAsync(Guid batchId, Guid expectedVersion, int number, Guid? targetId, bool skip, CancellationToken ct = default)
    {
        await WriteAsync(async db =>
        {
            var batch = await BatchAsync(db, batchId, expectedVersion, ct);
            batch.Touch();
            var row = await db.CollectionImportRows.FindAsync([batchId, number], ct) ?? throw new ArgumentException("Importregel niet gevonden.");
            if (skip) row.Skipped = true;
            else
            {
                if (targetId is null) throw new ArgumentException("Kies eerst een doelprinting.");
                var target = await Targets(db).SingleOrDefaultAsync(x => x.Id == targetId, ct) ?? throw new ArgumentException("Printing niet gevonden.");
                Bind(row, target); row.ExplicitChoice = true; row.MatchReason = "Handmatig gekoppeld";
            }
            return true;
        }, ct);
    }

    public async Task ApplyAsync(Guid batchId, Guid expectedVersion, CancellationToken ct = default)
    {
        await WriteAsync(async db =>
        {
            var batch = await BatchAsync(db, batchId, expectedVersion, ct);
            batch.RequirePrepared();
            var all = await db.CollectionImportRows.Where(x => x.BatchId == batchId).ToListAsync(ct);
            var rows = all.Where(x => !x.Skipped).ToList();
            if (rows.Count == 0 || rows.Any(x => x.PrintingId is null) || rows.Select(x => x.PrintingId).Distinct().Count() != rows.Count)
                throw new ArgumentException("Los alle ongekoppelde en dubbele regels op; er moet minstens één regel overblijven.");
            var targets = (await Targets(db).ToListAsync(ct)).ToDictionary(x => x.Id);
            var owned = await db.CollectionEntries.ToDictionaryAsync(x => x.PrintingId, ct);
            // Validate every row before making any in-memory changes, then commit one transaction.
            foreach (var row in rows)
            {
                ct.ThrowIfCancellationRequested();
                if (!targets.TryGetValue(row.PrintingId!.Value, out var target) || target.Signature != row.PrintingSignature ||
                    target.Version != row.BeforeVersion || target.Quantity != row.BeforeQuantity)
                    throw new ArgumentException($"Regel {row.Number}: bezit of kaartkoppeling is gewijzigd na de voorbereiding. Bereid de import opnieuw voor.");
            }
            int changed = 0;
            foreach (var row in rows)
            {
                ct.ThrowIfCancellationRequested();
                if (row.BeforeQuantity == row.Quantity) continue;
                var id = row.PrintingId!.Value;
                if (!owned.TryGetValue(id, out var entry))
                {
                    entry = new CollectionEntry(id, row.Quantity); db.CollectionEntries.Add(entry);
                }
                else entry.SetQuantity(row.Quantity);
                row.Changed = true; row.AppliedVersion = entry.Version; changed++;
            }
            batch.Apply(changed, all.Count - rows.Count);
            logger.LogInformation("Collection import {BatchId} applies {Changed} changes", batch.Id, changed);
            return true;
        }, ct);
    }

    public async Task DiscardAsync(Guid batchId, Guid expectedVersion, CancellationToken ct = default)
        => await WriteAsync(async db => { (await BatchAsync(db, batchId, expectedVersion, ct)).Discard(); return true; }, ct);

    public async Task UndoAsync(Guid batchId, CancellationToken ct = default)
    {
        await WriteAsync(async db =>
        {
            var batch = await BatchAsync(db, batchId, null, ct);
            if (batch.State != CollectionImportState.Applied) throw new ArgumentException("Deze import kan niet meer worden hersteld.");
            var rows = await db.CollectionImportRows.Where(x => x.BatchId == batchId && x.Changed).ToListAsync(ct);
            var owned = await db.CollectionEntries.ToDictionaryAsync(x => x.PrintingId, ct);
            foreach (var row in rows)
            {
                ct.ThrowIfCancellationRequested();
                if (!owned.TryGetValue(row.PrintingId!.Value, out var entry) || entry.Version != row.AppliedVersion || entry.Quantity != row.Quantity)
                    throw new ArgumentException($"Herstel geblokkeerd: regel {row.Number} is later gewijzigd. Er is niets teruggedraaid.");
            }
            foreach (var row in rows)
            {
                var entry = owned[row.PrintingId!.Value];
                if (row.BeforeQuantity is { } before) entry.SetQuantity(before);
                else db.CollectionEntries.Remove(entry);
            }
            batch.Undo();
            logger.LogInformation("Collection import {BatchId} undone with {Count} changes", batchId, rows.Count);
            return true;
        }, ct);
    }

    public Task<IReadOnlyList<CollectionImportHistory>> GetHistoryAsync(int page = 1, CancellationToken ct = default) => Task.Run(async () =>
    {
        if (page < 1 || page > 1000000) throw new ArgumentException("Ongeldige historiepagina.");
        await using var db = await factory.CreateDbContextAsync(ct);
        return (IReadOnlyList<CollectionImportHistory>)await db.CollectionImports.AsNoTracking().OrderByDescending(x => x.CreatedUtc).ThenByDescending(x => x.Id)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .Select(x => new CollectionImportHistory(x.Id, x.FileName, x.CreatedUtc, x.State, x.ChangedCount, x.SkippedCount)).ToListAsync(ct);
    }, ct);

    public Task ExportAsync(string path, CancellationToken ct = default) => Task.Run(async () =>
    {
        if (!string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Kies een exportbestand met de extensie .json.");
        await using var db = await factory.CreateDbContextAsync(ct);
        var targets = await Targets(db).Where(x => x.Quantity != null).OrderBy(x => x.Name).ThenBy(x => x.Id).ToListAsync(ct);
        if (targets.Count == 0) throw new ArgumentException("De collectie is leeg. Voeg eerst kaarten toe voordat je exporteert.");
        if (targets.Count > CollectionFile.MaxRows) throw new ArgumentException("De collectie overschrijdt de exportlimiet van 50.000 regels.");
        var cards = targets.Select(t => t.ScryfallId is not null ? new CollectionSource(t.ScryfallId, null, null, null, null, t.Name, t.Quantity!.Value) :
            new CollectionSource(null, null, t.SetCode, t.CollectorNumber, t.Language, t.Name, t.Quantity!.Value)).ToList();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 1, cards }, CollectionFile.JsonOptions);
        if (bytes.Length > CollectionFile.MaxBytes) throw new ArgumentException("De export overschrijdt de limiet van 16 MiB.");
        var full = Path.GetFullPath(path);
        var temp = full + $".{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllBytesAsync(temp, bytes, ct);
            ct.ThrowIfCancellationRequested();
            File.Move(temp, full, overwrite: true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }, ct);
}
