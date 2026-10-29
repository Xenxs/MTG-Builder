using MTGDeckbuilder.Application;
using MTGDeckbuilder.Domain;

namespace MTGDeckbuilder.Infrastructure;

// Microsoft.Data.Sqlite executes even its async I/O synchronously. Keep that work off
// the UI thread. Each inner operation creates and disposes its own DbContext.
internal sealed class BackgroundDeckbuilderStore(IDeckbuilderStore inner, DatabaseWriteLock writes) : IDeckbuilderStore
{
    public Task<DashboardSummary> GetSummaryAsync(CancellationToken ct = default)
        => Task.Run(() => inner.GetSummaryAsync(ct), ct);
    public Task<IReadOnlyList<CollectionItem>> GetCollectionAsync(CancellationToken ct = default)
        => Task.Run(() => inner.GetCollectionAsync(ct), ct);
    public Task SaveCollectionAsync(CollectionInput input, CancellationToken ct = default)
        => Task.Run(() => writes.RunAsync(() => inner.SaveCollectionAsync(input, ct), ct), ct);
    public Task SetCollectionQuantityAsync(Guid printingId, int quantity, CancellationToken ct = default)
        => Task.Run(() => writes.RunAsync(() => inner.SetCollectionQuantityAsync(printingId, quantity, ct), ct), ct);
    public Task<IReadOnlyList<DeckItem>> GetDecksAsync(CancellationToken ct = default)
        => Task.Run(() => inner.GetDecksAsync(ct), ct);
    public Task<Guid> CreateDeckAsync(string name, string format, CancellationToken ct = default)
        => Task.Run(async () =>
        {
            Guid id = default;
            await writes.RunAsync(async () => id = await inner.CreateDeckAsync(name, format, ct), ct);
            return id;
        }, ct);
    public Task DeleteDeckAsync(Guid id, CancellationToken ct = default)
        => Task.Run(() => writes.RunAsync(() => inner.DeleteDeckAsync(id, ct), ct), ct);
    public Task<IReadOnlyList<DeckCardItem>> GetDeckCardsAsync(Guid deckId, CancellationToken ct = default)
        => Task.Run(() => inner.GetDeckCardsAsync(deckId, ct), ct);
    public Task SetDeckCardAsync(Guid deckId, Guid printingId, DeckZone zone, int quantity, CancellationToken ct = default)
        => Task.Run(() => writes.RunAsync(() => inner.SetDeckCardAsync(deckId, printingId, zone, quantity, ct), ct), ct);
    public Task<string> GetPlayerNameAsync(CancellationToken ct = default)
        => Task.Run(() => inner.GetPlayerNameAsync(ct), ct);
    public Task SavePlayerNameAsync(string name, CancellationToken ct = default)
        => Task.Run(() => writes.RunAsync(() => inner.SavePlayerNameAsync(name, ct), ct), ct);
}
