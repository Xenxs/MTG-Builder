using Microsoft.EntityFrameworkCore;
using MTGDeckbuilder.Application;
using MTGDeckbuilder.Domain;

namespace MTGDeckbuilder.Infrastructure;

public sealed class SqliteArchetypePoolStore(IDbContextFactory<DeckbuilderDbContext> factory, TimeProvider clock) : IArchetypePoolStore
{
    public Task<ArchetypePoolReport> ReadAsync(CancellationToken ct = default) => Task.Run(async () =>
    {
        var definition = MonoRedPool.Current;
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var input = await LoadInputAsync(db, ct);
        ct.ThrowIfCancellationRequested();
        return ArchetypePoolEngine.Evaluate(definition, input, clock.GetUtcNow().UtcDateTime);
    }, ct);

    internal static async Task<ArchetypePoolInput> LoadInputAsync(DeckbuilderDbContext db, CancellationToken ct)
    {
        var ids = MonoRedPool.Current.Cards.Select(x => x.OracleId).ToArray();
        var run = await db.CatalogImports.AsNoTracking().Where(x => x.State == CatalogImportState.Completed)
            .OrderByDescending(x => x.CompletedUtc).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        var source = run is null ? new LegalitySnapshot(null, "Geen catalogus", "—", null, null, 0)
            : new LegalitySnapshot(run.Id, run.Source, run.SourceVersion,
                run.SourceUpdatedUtc is { } u ? DateTime.SpecifyKind(u, DateTimeKind.Utc) : null,
                run.CompletedUtc is { } i ? DateTime.SpecifyKind(i, DateTimeKind.Utc) : null, run.MetadataVersion);
        var raw = await (from p in db.Printings.AsNoTracking() join c in db.Cards.AsNoTracking() on p.CardId equals c.Id
            where c.OracleId != null && ids.Contains(c.OracleId)
            select new { p.Id, p.CardId, c.OracleId, c.Name, p.SetCode, p.CollectorNumber, c.ManaCost, c.TypeLine, c.OracleText,
                c.ColorIdentity, c.ManaValue, p.IsInCatalog, p.LastSeenImportId }).ToArrayAsync(ct);
        var cardIds = raw.Select(x => x.CardId).Distinct().ToArray();
        var legalities = await db.CardLegalities.AsNoTracking().Where(x => cardIds.Contains(x.CardId)).ToDictionaryAsync(x => x.CardId, ct);
        var printingIds = raw.Select(x => x.Id).ToArray();
        var stock = await db.CollectionEntries.AsNoTracking().Where(x => printingIds.Contains(x.PrintingId))
            .Select(x => new AdviceStock(x.PrintingId, x.Quantity, x.Version)).ToArrayAsync(ct);
        var printings = raw.Select(x => new AdvicePrinting(x.Id, x.OracleId, x.Name, x.SetCode, x.CollectorNumber, x.ColorIdentity,
            x.ManaValue, x.TypeLine, x.OracleText,
            legalities.TryGetValue(x.CardId, out var l) && l.RunId == run?.Id ? l.Standard : CardLegality.Unknown,
            x.IsInCatalog && x.LastSeenImportId == run?.Id, x.ManaCost)).ToArray();
        ct.ThrowIfCancellationRequested();
        return new(source, printings.OrderBy(x => x.Id).ToArray(), stock.OrderBy(x => x.PrintingId).ToArray());
    }
}
