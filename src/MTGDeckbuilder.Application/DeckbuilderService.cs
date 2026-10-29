using MTGDeckbuilder.Domain;

namespace MTGDeckbuilder.Application;

public sealed class DeckbuilderService(IDeckbuilderStore store)
{
    public Task<DashboardSummary> GetSummaryAsync(CancellationToken ct = default) => store.GetSummaryAsync(ct);
    public Task<IReadOnlyList<CollectionItem>> GetCollectionAsync(CancellationToken ct = default) => store.GetCollectionAsync(ct);
    public Task SaveCollectionAsync(CollectionInput input, CancellationToken ct = default)
    {
        var normalized = input with
        {
            Name = Rules.Text(input.Name, "Kaartnaam", 200),
            SetCode = Rules.Text(input.SetCode, "Setcode", 12).ToUpperInvariant(),
            CollectorNumber = Rules.Text(input.CollectorNumber, "Collectornummer", 20).ToUpperInvariant(),
            Quantity = Rules.Quantity(input.Quantity)
        };
        return store.SaveCollectionAsync(normalized, ct);
    }
    public Task SetCollectionQuantityAsync(Guid printingId, int quantity, CancellationToken ct = default)
        => store.SetCollectionQuantityAsync(Rules.Id(printingId), Rules.Quantity(quantity), ct);
    public Task<IReadOnlyList<DeckItem>> GetDecksAsync(CancellationToken ct = default) => store.GetDecksAsync(ct);
    public Task<Guid> CreateDeckAsync(string name, string format, CancellationToken ct = default)
        => store.CreateDeckAsync(Rules.Text(name, "Decknaam", 120), Rules.Text(format, "Formaat", 40), ct);
    public Task DeleteDeckAsync(Guid id, CancellationToken ct = default) => store.DeleteDeckAsync(Rules.Id(id), ct);
    public Task<IReadOnlyList<DeckCardItem>> GetDeckCardsAsync(Guid id, CancellationToken ct = default)
        => store.GetDeckCardsAsync(Rules.Id(id), ct);
    public Task SetDeckCardAsync(Guid deckId, Guid printingId, DeckZone zone, int quantity, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(zone)) throw new ArgumentException("Ongeldige deckzone.");
        return store.SetDeckCardAsync(Rules.Id(deckId), Rules.Id(printingId), zone, Rules.Quantity(quantity), ct);
    }
    public Task<string> GetPlayerNameAsync(CancellationToken ct = default) => store.GetPlayerNameAsync(ct);
    public Task SavePlayerNameAsync(string name, CancellationToken ct = default)
        => store.SavePlayerNameAsync(Rules.Text(name, "Spelersnaam", 80), ct);
}
