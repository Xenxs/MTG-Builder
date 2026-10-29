using Microsoft.EntityFrameworkCore;
using MTGDeckbuilder.Application;
using MTGDeckbuilder.Domain;

namespace MTGDeckbuilder.Infrastructure;

public sealed class SqliteDeckValidationStore(IDbContextFactory<DeckbuilderDbContext> factory,
    DatabaseWriteLock writes, TimeProvider clock) : IDeckValidationStore
{
    public Task<DeckRuleProfile> GetProfileAsync(Guid deckId, CancellationToken ct = default) => Task.Run(async () =>
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return (await db.Decks.AsNoTracking().SingleOrDefaultAsync(x => x.Id == deckId, ct)
            ?? throw new ArgumentException("Deck niet gevonden.")).ValidationProfile;
    }, ct);

    public Task SetProfileAsync(Guid deckId, DeckRuleProfile profile, CancellationToken ct = default) => Task.Run(() => writes.RunAsync(async () =>
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var deck = await db.Decks.SingleOrDefaultAsync(x => x.Id == deckId, ct) ?? throw new ArgumentException("Deck niet gevonden.");
        deck.SetValidationProfile(profile); await db.SaveChangesAsync(ct);
    }, ct), ct);

    public Task<DeckValidation> ValidateAsync(Guid deckId, CancellationToken ct = default) => Task.Run(async () =>
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var deck = await db.Decks.AsNoTracking().SingleOrDefaultAsync(x => x.Id == deckId, ct) ?? throw new ArgumentException("Deck niet gevonden.");
        var run = await db.CatalogImports.AsNoTracking().Where(x => x.State == CatalogImportState.Completed)
            .OrderByDescending(x => x.CompletedUtc).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        var snapshot = run is null ? new LegalitySnapshot(null, "Geen catalogus", "—", null, null, 0)
            : new LegalitySnapshot(run.Id, run.Source, run.SourceVersion,
                run.SourceUpdatedUtc is { } updated ? DateTime.SpecifyKind(updated, DateTimeKind.Utc) : null,
                run.CompletedUtc is { } imported ? DateTime.SpecifyKind(imported, DateTimeKind.Utc) : null, run.MetadataVersion);
        var rows = await (from d in db.DeckCards.AsNoTracking() join p in db.Printings.AsNoTracking() on d.PrintingId equals p.Id
            join c in db.Cards.AsNoTracking() on p.CardId equals c.Id where d.DeckId == deckId
            select new { p.Id, p.CardId, c.OracleId, c.Name, c.TypeLine, c.OracleText, p.IsInCatalog, p.LastSeenImportId, d.Zone, d.Quantity }).ToListAsync(ct);
        var cardIds = rows.Select(x => x.CardId).Distinct().ToArray();
        var legalities = await db.CardLegalities.AsNoTracking().Where(x => cardIds.Contains(x.CardId)).ToDictionaryAsync(x => x.CardId, ct);
        var cards = rows.Select(x => new ValidationCard(x.Id, x.OracleId, x.Name, x.TypeLine, x.OracleText,
            legalities.TryGetValue(x.CardId, out var status) && status.RunId == run?.Id ? status.Standard : CardLegality.Unknown,
            x.Zone, x.Quantity, x.IsInCatalog && x.LastSeenImportId == run?.Id)).ToArray();
        return StandardDeckValidator.Validate(deck.ValidationProfile, cards, snapshot, clock.GetUtcNow().UtcDateTime);
    }, ct);
}
