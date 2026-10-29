using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MTGDeckbuilder.Application;
using MTGDeckbuilder.Domain;

namespace MTGDeckbuilder.Infrastructure;

public sealed class CatalogImporter(IDbContextFactory<DeckbuilderDbContext> factory, AppPaths paths,
    ScryfallClient source, DatabaseWriteLock writes, ILogger<CatalogImporter> logger) : ICatalogImporter
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private const int CheckpointSize = 500;

    public Task<ImportResult> ImportOnlineAsync(IProgress<ImportProgress>? progress = null, CancellationToken ct = default)
        => Task.Run(() => ExclusiveAsync(async () =>
        {
            progress?.Report(new ImportProgress("Bron controleren"));
            var manifest = await source.GetManifestAsync(ct);
            await using var db = await factory.CreateDbContextAsync(ct);
            var current = await db.CatalogImports.AsNoTracking()
                .Where(x => x.State == CatalogImportState.Completed)
                .OrderByDescending(x => x.CompletedUtc).FirstOrDefaultAsync(ct);
            if (current?.Source == "Scryfall Default Cards" && current.SourceVersion == manifest.Version && current.MetadataVersion >= 1)
                return new ImportResult(current.Id, 0, 0, current.ProcessedRecords, current.StagedRecords,
                    AlreadyCurrent: true, UnlinkedManualPrintings: current.UnlinkedManualPrintings);
            var run = new CatalogImportRun("Scryfall Default Cards", manifest.Version, manifest.DownloadUri.ToString(), manifest.UpdatedUtc);
            await SaveRunAsync(run, ct);
            return await TrackAsync(run.Id, async () =>
            {
                var cached = paths.CatalogCachePath(run.Id);
                var download = await source.DownloadAsync(manifest.DownloadUri, cached + ".part", progress, manifest.Size, ct);
                File.Move(cached + ".part", cached, overwrite: true);
                run.SetCache(download.Hash, download.Size, useHashAsVersion: false);
                await SaveRunAsync(run, ct);
                return await ProcessAsync(run, progress, ct);
            }, ct);
        }, ct), ct);

    public Task<ImportResult> ImportFileAsync(string path, IProgress<ImportProgress>? progress = null, CancellationToken ct = default)
        => Task.Run(() => ExclusiveAsync(async () =>
        {
            var file = new FileInfo(Path.GetFullPath(path));
            if (!file.Exists || file.Length <= 0 || file.Length > ScryfallClient.MaxFileBytes)
                throw new ArgumentException("Kies een bestaand Scryfall-bestand van maximaal 512 MB.");
            var run = new CatalogImportRun("Bestand: " + Path.GetFileName(path)[..Math.Min(Path.GetFileName(path).Length, 180)], "bestand-pending");
            await SaveRunAsync(run, ct);
            return await TrackAsync(run.Id, async () =>
            {
                var cached = paths.CatalogCachePath(run.Id);
                await using (var input = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, useAsync: true))
                {
                    var copied = await ScryfallClient.CopyAsync(input, cached + ".part", "Bestand kopiëren", progress, file.Length, ct);
                    File.Move(cached + ".part", cached, overwrite: true);
                    run.SetCache(copied.Hash, copied.Size, useHashAsVersion: true);
                }
                await SaveRunAsync(run, ct);
                return await ProcessAsync(run, progress, ct);
            }, ct);
        }, ct), ct);

    public Task<ImportResult> ResumeAsync(Guid runId, IProgress<ImportProgress>? progress = null, CancellationToken ct = default)
        => Task.Run(() => ExclusiveAsync(async () =>
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            var run = await db.CatalogImports.AsNoTracking().SingleOrDefaultAsync(x => x.Id == runId, ct)
                ?? throw new ArgumentException("Deze import bestaat niet meer.");
            if (!run.CacheReady || run.State is not (CatalogImportState.Cancelled or CatalogImportState.Interrupted or CatalogImportState.Failed))
                throw new ArgumentException("Deze import kan niet worden hervat. Start de import opnieuw.");
            return await TrackAsync(run.Id, async () =>
            {
                progress?.Report(new ImportProgress("Cache controleren"));
                var cached = paths.CatalogCachePath(run.Id);
                if (!File.Exists(cached)) throw new ArgumentException("Het cachebestand ontbreekt. Start een nieuwe import.");
                await using (var stream = File.OpenRead(cached))
                {
                    var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct));
                    if (hash != run.Sha256 || stream.Length != run.FileSize)
                        throw new InvalidDataException("De importcache is beschadigd. Start een nieuwe import.");
                }
                run.SetState(CatalogImportState.Processing);
                await SaveRunAsync(run, ct);
                return await ProcessAsync(run, progress, ct);
            }, ct);
        }, ct), ct);

    public Task RecoverInterruptedAsync(CancellationToken ct = default) => Task.Run(async () =>
    {
        Directory.CreateDirectory(paths.DataDirectory);
        FileStream fileLock;
        try { fileLock = new FileStream(paths.ImportLockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException) { return; } // Another process still owns the import; do not alter its run.
        await using (fileLock)
        {
            await writes.RunAsync(async () =>
            {
                await using var db = await factory.CreateDbContextAsync(ct);
                var runs = await db.CatalogImports.Where(x => x.State == CatalogImportState.Downloading ||
                    x.State == CatalogImportState.Processing || x.State == CatalogImportState.Applying).ToListAsync(ct);
                foreach (var run in runs) run.SetState(CatalogImportState.Interrupted,
                    "De vorige uitvoering is onderbroken. Hervat vanuit de cache of start opnieuw.");
                await db.SaveChangesAsync(ct);
            }, ct);
        }
    }, ct);

    private async Task<ImportResult> ExclusiveAsync(Func<Task<ImportResult>> action, CancellationToken ct)
    {
        if (!await _gate.WaitAsync(0, ct)) throw new ArgumentException("Er draait al een catalogusimport.");
        try
        {
            Directory.CreateDirectory(paths.CatalogCacheDirectory);
            FileStream fileLock;
            try { fileLock = new FileStream(paths.ImportLockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException ex) { throw new ArgumentException("Een andere appinstantie voert een catalogusimport uit.", ex); }
            await using (fileLock) { return await action(); }
        }
        finally { _gate.Release(); }
    }

    private async Task<ImportResult> TrackAsync(Guid id, Func<Task<ImportResult>> action, CancellationToken ct)
    {
        try
        {
            var result = await action();
            logger.LogInformation("Catalog import {ImportId} completed: {Added} new, {Updated} updated, {Unlinked} manual printings kept separate",
                id, result.Added, result.Updated, result.UnlinkedManualPrintings);
            CleanupCompletedCaches();
            return result;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await MarkStoppedAsync(id, CatalogImportState.Cancelled, "Import gepauzeerd. Met volledige cache is hervatten mogelijk.");
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Catalog import {ImportId} failed; visible catalog remains unchanged", id);
            var message = ex is ArgumentException or InvalidDataException or JsonException or FormatException
                ? ex.Message : "Import mislukt. Controleer verbinding, bestand en logboek.";
            await MarkStoppedAsync(id, CatalogImportState.Failed, message);
            throw;
        }
        finally
        {
            var partial = paths.CatalogCachePath(id) + ".part";
            try { if (File.Exists(partial)) File.Delete(partial); }
            catch (IOException ex) { logger.LogWarning(ex, "Could not remove partial import download"); }
        }
    }

    private Task SaveRunAsync(CatalogImportRun run, CancellationToken ct) => writes.RunAsync(async () =>
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        if (await db.CatalogImports.AnyAsync(x => x.Id == run.Id, ct)) db.CatalogImports.Update(run);
        else db.CatalogImports.Add(run);
        await db.SaveChangesAsync(ct);
    }, ct);

    private Task MarkStoppedAsync(Guid id, CatalogImportState state, string error) => writes.RunAsync(async () =>
    {
        // Reload the last COMMITTED checkpoint, never save an uncommitted in-memory counter.
        await using var db = await factory.CreateDbContextAsync();
        var run = await db.CatalogImports.SingleAsync(x => x.Id == id);
        run.SetState(state, error.Length > 1500 ? error[..1500] : error);
        await db.SaveChangesAsync();
    }, CancellationToken.None);

    private async Task<ImportResult> ProcessAsync(CatalogImportRun run, IProgress<ImportProgress>? progress, CancellationToken ct)
    {
        var processed = 0;
        var staged = run.StagedRecords;
        var checkpoint = run.ProcessedRecords;
        var batch = new List<CatalogStagingEntry>(CheckpointSize);
        await foreach (var element in ScryfallRecordReader.ReadAsync(paths.CatalogCachePath(run.Id), ct))
        {
            ct.ThrowIfCancellationRequested();
            processed++;
            if (processed > 2000000) throw new InvalidDataException("Het bronbestand bevat te veel records.");
            if (processed <= checkpoint) continue;
            CatalogRecord? card;
            try { card = ScryfallRecordReader.Parse(element); }
            catch (Exception ex) when (ex is ArgumentException or InvalidDataException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
            { throw new InvalidDataException($"Ongeldig kaartrecord {processed}: {ex.Message}", ex); }
            if (card is not null)
            {
                batch.Add(new CatalogStagingEntry { RunId = run.Id, ScryfallId = card.ScryfallId, Data = JsonSerializer.Serialize(card) });
                staged++;
                if (staged > 200000) throw new InvalidDataException("Het bestand bevat meer dan 200.000 Arena-printings.");
            }
            if (processed - run.ProcessedRecords >= CheckpointSize)
            {
                await CheckpointAsync(run, batch, processed, staged, ct);
                progress?.Report(new ImportProgress("Valideren", processed, staged));
            }
        }
        if (processed < checkpoint) throw new InvalidDataException("Het cachebestand is korter dan het opgeslagen checkpoint.");
        await CheckpointAsync(run, batch, processed, staged, ct);
        progress?.Report(new ImportProgress("Valideren", processed, staged));
        ct.ThrowIfCancellationRequested();
        if (staged == 0) throw new InvalidDataException("Geen Arena-kaarten gevonden. De bestaande catalogus is behouden.");
        run.SetState(CatalogImportState.Applying);
        await SaveRunAsync(run, ct);
        return await ApplyAsync(run, progress, ct);
    }

    private Task CheckpointAsync(CatalogImportRun run, List<CatalogStagingEntry> batch, int processed, int staged, CancellationToken ct)
        => writes.RunAsync(async () =>
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            db.CatalogStaging.AddRange(batch);
            run.Checkpoint(processed, staged);
            db.CatalogImports.Update(run);
            await db.SaveChangesAsync(ct);
            batch.Clear();
        }, ct);

    private async Task<ImportResult> ApplyAsync(CatalogImportRun run, IProgress<ImportProgress>? progress, CancellationToken ct)
    {
        await writes.Gate.WaitAsync(ct);
        try
        {
            await using var db = await factory.CreateDbContextAsync(ct);
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            db.ChangeTracker.AutoDetectChangesEnabled = false;
            var entries = await db.CatalogStaging.AsNoTracking().Where(x => x.RunId == run.Id).OrderBy(x => x.ScryfallId).ToListAsync(ct);
            if (entries.Count != run.StagedRecords) throw new InvalidDataException("Het importcheckpoint is niet compleet. Start de import opnieuw.");
            var cards = await db.Cards.ToListAsync(ct);
            var printings = await db.Printings.ToListAsync(ct);
            var cardsById = cards.ToDictionary(x => x.Id);
            var cardsByOracle = cards.Where(x => x.OracleId != null).ToDictionary(x => x.OracleId!);
            var printingsBySource = printings.Where(x => x.ScryfallId != null).ToDictionary(x => x.ScryfallId!);
            var printingsByKey = printings.GroupBy(x => Key(x.SetCode, x.CollectorNumber, x.Language)).ToDictionary(x => x.Key, x => x.ToList());
            var unlinked = new HashSet<Guid>();
            var changedCards = new HashSet<Guid>();
            var updatedPrintingIds = new HashSet<Guid>();
            var added = 0;
            var applied = 0;
            var legalities = new Dictionary<Guid, CardLegality>();
            foreach (var entry in entries)
            {
                ct.ThrowIfCancellationRequested();
                var record = JsonSerializer.Deserialize<CatalogRecord>(entry.Data) ?? throw new InvalidDataException("Ongeldige importbuffer.");
                var key = Key(record.SetCode, record.CollectorNumber, record.Language);
                printingsBySource.TryGetValue(record.ScryfallId, out var printing);
                var isNew = false;
                if (printing is not null && Key(printing.SetCode, printing.CollectorNumber, printing.Language) != key)
                    throw new InvalidDataException("Een bekende Scryfall-printing heeft gewijzigde setidentificatie. Geen automatische overschrijving uitgevoerd.");
                if (printing is null && printingsByKey.TryGetValue(key, out var candidates))
                {
                    var manual = candidates.Where(x => x.ScryfallId is null).ToList();
                    if (manual.Count == 1 && candidates.Count == 1 &&
                        string.Equals(cardsById[manual[0].CardId].Name, record.Name, StringComparison.OrdinalIgnoreCase))
                        printing = manual[0];
                    else foreach (var item in manual) unlinked.Add(item.Id);
                }
                if (!cardsByOracle.TryGetValue(record.OracleId, out var card))
                {
                    card = printing is not null && cardsById[printing.CardId].OracleId is null &&
                        string.Equals(cardsById[printing.CardId].Name, record.Name, StringComparison.OrdinalIgnoreCase)
                        ? cardsById[printing.CardId] : new Card(record.Name);
                    if (!cardsById.ContainsKey(card.Id)) { db.Cards.Add(card); cardsById.Add(card.Id, card); }
                    cardsByOracle.Add(record.OracleId, card);
                }
                if (card.UpdateCatalog(record.OracleId, record.Name, record.ManaCost, record.TypeLine, record.OracleText, record.ColorIdentity, record.ManaValue))
                {
                    changedCards.Add(card.Id);
                    if (db.Entry(card).State != EntityState.Added) db.Entry(card).State = EntityState.Modified;
                }
                if (legalities.TryGetValue(card.Id, out var previous) && previous != record.Standard)
                    throw new InvalidDataException($"Tegenstrijdige Standard-legaliteit voor {record.Name}. De catalogus is behouden.");
                legalities[card.Id] = record.Standard;
                if (printing is null)
                {
                    printing = new Printing(card.Id, record.SetCode, record.CollectorNumber);
                    db.Printings.Add(printing); isNew = true; added++;
                    if (!printingsByKey.TryGetValue(key, out var list)) printingsByKey[key] = list = [];
                    list.Add(printing);
                }
                var changed = printing.UpdateCatalog(card.Id, record.ScryfallId, record.ArenaId, record.Language, record.SetName, record.Rarity, run.Id);
                if (!isNew)
                {
                    db.Entry(printing).State = EntityState.Modified;
                    if (changed) updatedPrintingIds.Add(printing.Id);
                }
                printingsBySource[record.ScryfallId] = printing;
                if (printingsByKey.TryGetValue(key, out var remainingCandidates))
                    foreach (var manual in remainingCandidates.Where(x => x.ScryfallId is null)) unlinked.Add(manual.Id);
                applied++;
                if (applied % CheckpointSize == 0)
                {
                    await db.SaveChangesAsync(ct);
                    progress?.Report(new ImportProgress("Toepassen", applied, entries.Count));
                }
            }
            await db.SaveChangesAsync(ct);
            foreach (var printing in printings.Where(x => x.LastSeenImportId == run.Id && changedCards.Contains(x.CardId)))
                updatedPrintingIds.Add(printing.Id);
            var updated = updatedPrintingIds.Count;
            await db.Printings.Where(x => x.IsInCatalog && x.LastSeenImportId != run.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsInCatalog, false), ct);
            await db.CardLegalities.ExecuteDeleteAsync(ct);
            db.CardLegalities.AddRange(legalities.Select(x => new CardFormatLegality(x.Key, run.Id, x.Value)));
            run.Complete(added, updated, unlinked.Count, metadataVersion: 1);
            db.CatalogImports.Update(run);
            await db.CatalogStaging.Where(x => x.RunId == run.Id).ExecuteDeleteAsync(ct);
            await db.SaveChangesAsync(ct);
            ct.ThrowIfCancellationRequested();
            await transaction.CommitAsync(ct);
            progress?.Report(new ImportProgress("Voltooid", run.ProcessedRecords, run.StagedRecords));
            return new ImportResult(run.Id, added, updated, run.ProcessedRecords, run.StagedRecords,
                UnlinkedManualPrintings: unlinked.Count);
        }
        finally { writes.Gate.Release(); }
    }

    private static string Key(string set, string collector, string language) => $"{set}|{collector}|{language}";

    private void CleanupCompletedCaches()
    {
        try
        {
            using var db = factory.CreateDbContext();
            var old = db.CatalogImports.AsNoTracking().Where(x => x.State == CatalogImportState.Completed)
                .OrderByDescending(x => x.CompletedUtc).Skip(2).Select(x => x.Id).ToList();
            foreach (var id in old)
            {
                var path = paths.CatalogCachePath(id);
                if (File.Exists(path)) File.Delete(path);
            }
        }
        catch (Exception ex)
        { logger.LogWarning(ex, "Could not clean older completed import caches"); }
    }
}
