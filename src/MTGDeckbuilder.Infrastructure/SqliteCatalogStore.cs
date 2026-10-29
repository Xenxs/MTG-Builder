using Microsoft.EntityFrameworkCore;
using MTGDeckbuilder.Application;
using MTGDeckbuilder.Domain;

namespace MTGDeckbuilder.Infrastructure;

public sealed class SqliteCatalogStore(IDbContextFactory<DeckbuilderDbContext> factory, DatabaseWriteLock writes) : ICatalogStore
{
    public Task<CatalogPage> SearchAsync(CatalogQuery query, CancellationToken ct = default) => Task.Run(async () =>
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var search = query.Search.ToUpperInvariant();
        var type = query.Type.ToUpperInvariant();
        var rows = from p in db.Printings.AsNoTracking()
                   join c in db.Cards.AsNoTracking() on p.CardId equals c.Id
                   join e in db.CollectionEntries.AsNoTracking() on p.Id equals e.PrintingId into owned
                   from e in owned.DefaultIfEmpty()
                   where p.IsInCatalog
                   select new { P = p, C = c, Owned = e == null ? 0 : e.Quantity };
        if (search.Length > 0) rows = rows.Where(x => x.C.SearchName.Contains(search));
        if (query.SetCode.Length > 0) rows = rows.Where(x => x.P.SetCode == query.SetCode);
        if (query.Rarity.Length > 0) rows = rows.Where(x => x.P.Rarity == query.Rarity);
        if (type.Length > 0) rows = rows.Where(x => x.C.TypeLine != null && x.C.TypeLine.ToUpper().Contains(type));
        if (query.MaxManaValue is { } mana) rows = rows.Where(x => x.C.ManaValue <= mana);
        if (query.OwnedOnly) rows = rows.Where(x => x.Owned > 0);
        if (query.Color == "C") rows = rows.Where(x => x.C.ColorIdentity.Length == 0);
        else if (query.Color == "M") rows = rows.Where(x => x.C.ColorIdentity.Length > 1);
        else if (query.Color.Length > 0) rows = rows.Where(x => x.C.ColorIdentity.Contains(query.Color));
        var total = await rows.CountAsync(ct);
        var page = Math.Min(query.Page, Math.Max(1, (total + query.PageSize - 1) / query.PageSize));
        var items = await rows.OrderBy(x => x.C.SearchName).ThenBy(x => x.P.SetCode).ThenBy(x => x.P.CollectorNumber)
            .ThenBy(x => x.P.Language).ThenBy(x => x.P.ScryfallId)
            .Skip((page - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new CatalogItem(x.P.Id, x.C.Name, x.C.ManaCost ?? "", x.C.TypeLine ?? "",
                x.C.OracleText ?? "", x.C.ColorIdentity, x.C.ManaValue, x.P.SetCode, x.P.SetName,
                x.P.CollectorNumber, x.P.Language, x.P.Rarity, x.Owned, x.P.ScryfallId!, x.P.ArenaId)).ToListAsync(ct);
        return new CatalogPage(items, total, page, query.PageSize);
    }, ct);

    public Task<IReadOnlyList<CatalogSet>> GetSetsAsync(CancellationToken ct = default) => Task.Run(async () =>
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return (IReadOnlyList<CatalogSet>)await db.Printings.AsNoTracking().Where(x => x.IsInCatalog)
            .Select(x => new { x.SetCode, x.SetName }).Distinct().OrderBy(x => x.SetCode)
            .Select(x => new CatalogSet(x.SetCode, x.SetName)).ToListAsync(ct);
    }, ct);

    public Task<CatalogStatus> GetStatusAsync(CancellationToken ct = default) => Task.Run(async () =>
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var latest = await db.CatalogImports.AsNoTracking().Where(x => x.State == CatalogImportState.Completed)
            .OrderByDescending(x => x.CompletedUtc).FirstOrDefaultAsync(ct);
        return new CatalogStatus(await db.Printings.CountAsync(x => x.IsInCatalog, ct),
            await db.Printings.Where(x => x.IsInCatalog).Select(x => x.CardId).Distinct().CountAsync(ct), latest?.CompletedUtc, latest?.SourceVersion);
    }, ct);

    public Task<IReadOnlyList<ImportHistoryItem>> GetHistoryAsync(CancellationToken ct = default) => Task.Run(async () =>
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return (IReadOnlyList<ImportHistoryItem>)await db.CatalogImports.AsNoTracking().OrderByDescending(x => x.StartedUtc).Take(20)
            .Select(x => new ImportHistoryItem(x.Id, x.Source, x.SourceVersion, x.StartedUtc, x.CompletedUtc,
                x.State, x.ProcessedRecords, x.StagedRecords, x.AddedPrintings, x.UpdatedPrintings, x.CacheReady, x.Error, x.UnlinkedManualPrintings)).ToListAsync(ct);
    }, ct);

    public Task SetOwnedQuantityAsync(Guid printingId, int quantity, CancellationToken ct = default) => Task.Run(() => writes.RunAsync(async () =>
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        if (!await db.Printings.AnyAsync(x => x.Id == printingId && x.ScryfallId != null, ct))
            throw new ArgumentException("Deze catalogusprinting bestaat niet meer.");
        var owned = await db.CollectionEntries.FindAsync([printingId], ct);
        if (owned is null) db.CollectionEntries.Add(new CollectionEntry(printingId, quantity));
        else owned.SetQuantity(quantity);
        await db.SaveChangesAsync(ct);
    }, ct), ct);
}
