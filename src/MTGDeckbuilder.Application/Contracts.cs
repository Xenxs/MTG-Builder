using MTGDeckbuilder.Domain;

namespace MTGDeckbuilder.Application;

public sealed record DashboardSummary(int UniquePrintings, int TotalCards, int Decks);
public sealed record CollectionItem(Guid PrintingId, string Name, string SetCode, string CollectorNumber, int Quantity)
{
    public string Label => $"{Name} · {SetCode} #{CollectorNumber} · {Quantity}×";
}
public sealed record DeckItem(Guid Id, string Name, string Format, DeckRuleProfile ValidationProfile = DeckRuleProfile.Unspecified)
{
    public string Label => $"{Name} · {Format}";
}
public sealed record DeckCardItem(Guid PrintingId, string Name, string SetCode, string CollectorNumber, DeckZone Zone, int Quantity)
{
    public string Label => $"{Quantity}× {Name} · {SetCode} #{CollectorNumber} · {Zone}";
}
public sealed record CollectionInput(string Name, string SetCode, string CollectorNumber, int Quantity);

public interface IDeckbuilderStore
{
    Task<DashboardSummary> GetSummaryAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CollectionItem>> GetCollectionAsync(CancellationToken ct = default);
    Task SaveCollectionAsync(CollectionInput input, CancellationToken ct = default);
    Task SetCollectionQuantityAsync(Guid printingId, int quantity, CancellationToken ct = default);
    Task<IReadOnlyList<DeckItem>> GetDecksAsync(CancellationToken ct = default);
    Task<Guid> CreateDeckAsync(string name, string format, CancellationToken ct = default);
    Task DeleteDeckAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<DeckCardItem>> GetDeckCardsAsync(Guid deckId, CancellationToken ct = default);
    Task SetDeckCardAsync(Guid deckId, Guid printingId, DeckZone zone, int quantity, CancellationToken ct = default);
    Task<string> GetPlayerNameAsync(CancellationToken ct = default);
    Task SavePlayerNameAsync(string name, CancellationToken ct = default);
}

public interface IDatabaseInitializer
{
    Task InitializeAsync(CancellationToken ct = default);
}
