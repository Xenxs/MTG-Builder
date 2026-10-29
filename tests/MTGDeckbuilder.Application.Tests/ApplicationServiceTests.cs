using MTGDeckbuilder.Application;
using MTGDeckbuilder.Domain;
using Xunit;

namespace MTGDeckbuilder.Application.Tests;

public sealed class ApplicationServiceTests
{
    [Fact]
    public async Task NormalizesUserInputBeforePassingItToStorage()
    {
        var store = new RecordingStore();
        var service = new DeckbuilderService(store);
        await service.SaveCollectionAsync(new(" Negate ", " fdn ", " 12a ", 4), TestContext.Current.CancellationToken);
        Assert.Equal(new CollectionInput("Negate", "FDN", "12A", 4), store.LastInput);
    }

    [Fact]
    public async Task RejectsInvalidInputBeforeStorageIsCalled()
    {
        var store = new RecordingStore();
        var service = new DeckbuilderService(store);
        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveCollectionAsync(new(" ", "FDN", "1", 1), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SaveCollectionAsync(new("Negate", "FDN", "1", -1), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => service.SetDeckCardAsync(Guid.Empty, Guid.NewGuid(), DeckZone.Mainboard, 1, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => service.SetDeckCardAsync(Guid.NewGuid(), Guid.NewGuid(), (DeckZone)20, 1, TestContext.Current.CancellationToken));
        Assert.Null(store.LastInput);
    }

    private sealed class RecordingStore : IDeckbuilderStore
    {
        public CollectionInput? LastInput { get; private set; }
        public Task SaveCollectionAsync(CollectionInput input, CancellationToken ct = default) { LastInput = input; return Task.CompletedTask; }
        public Task<DashboardSummary> GetSummaryAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<CollectionItem>> GetCollectionAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task SetCollectionQuantityAsync(Guid id, int quantity, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<DeckItem>> GetDecksAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Guid> CreateDeckAsync(string name, string format, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteDeckAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<DeckCardItem>> GetDeckCardsAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SetDeckCardAsync(Guid deck, Guid printing, DeckZone zone, int quantity, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<string> GetPlayerNameAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task SavePlayerNameAsync(string name, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
