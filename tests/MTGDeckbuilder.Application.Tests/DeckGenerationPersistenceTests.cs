using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using MTGDeckbuilder.Application;
using MTGDeckbuilder.Domain;
using MTGDeckbuilder.Infrastructure;
using Xunit;

namespace MTGDeckbuilder.Application.Tests;

public sealed class DeckGenerationPersistenceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    internal static IDeckGenerationStore Store(TestDatabase test) => test.Provider.GetRequiredService<IDeckGenerationStore>();
    internal static async Task Seed(TestDatabase test, bool stock = true)
    {
        await test.InitializeAsync(); await test.Provider.GetRequiredService<ICatalogImporter>().ImportOnlineAsync(ct: TestContext.Current.CancellationToken);
        if (!stock) return;
        var catalog = test.Provider.GetRequiredService<CatalogService>(); var page = await catalog.SearchAsync(new(PageSize: 100), TestContext.Current.CancellationToken);
        foreach (var card in page.Items) await catalog.SetOwnedQuantityAsync(card.PrintingId, card.Name == "Mountain" ? 22 : 4, TestContext.Current.CancellationToken);
    }
    private static async Task<GenerationPreview> Preview(TestDatabase test, GenerationMode mode = GenerationMode.OwnedOnly)
    {
        var result = await Store(test).CalculateAsync(mode, Ct); Assert.True(result.CanPreview, result.Status);
        return await Store(test).PreviewAsync(result.Fingerprint, mode, "Reviewed mono-red", Ct);
    }
    [Fact]
    public async Task PreviewRestartApplyAndDoubleApplyPreserveStockAndOriginalDeckAndPersistBo1()
    {
        await using var test = DeckValidationPersistenceTests.Database(ArchetypePoolPersistenceTests.Handler()); await Seed(test);
        var original = await test.Service.CreateDeckAsync("Existing", DeckRuleProfiles.Bo3, Ct);
        var collection = await test.Service.GetCollectionAsync(Ct); var preview = await Preview(test);
        Assert.Single(await test.Service.GetDecksAsync(Ct)); await test.ReopenAsync();
        var id = await Store(test).ApplyAsync(preview.Id, Ct); Assert.NotEqual(original, id);
        Assert.Equal(id, await Store(test).ApplyAsync(preview.Id, Ct)); Assert.Equal(2, (await test.Service.GetDecksAsync(Ct)).Count);
        Assert.Equal(60, (await test.Service.GetDeckCardsAsync(id, Ct)).Sum(x => x.Quantity));
        Assert.True((await DeckValidationPersistenceTests.Store(test).ValidateAsync(id, Ct)).Passed);
        Assert.Equal(DeckRuleProfile.ArenaStandardBo1, await DeckValidationPersistenceTests.Store(test).GetProfileAsync(id, Ct));
        Assert.Empty(await test.Service.GetDeckCardsAsync(original, Ct)); Assert.Equal(collection, await test.Service.GetCollectionAsync(Ct));
        await test.ReopenAsync(); Assert.Equal(id, await Store(test).ApplyAsync(preview.Id, Ct));
        await using var db = await test.Provider.GetRequiredService<IDbContextFactory<DeckbuilderDbContext>>().CreateDbContextAsync(Ct);
        Assert.Equal(id, (await db.DeckGenerationPlans.SingleAsync(Ct)).AppliedDeckId); Assert.Empty(await db.DeckAdvicePlans.ToArrayAsync(Ct));
        await db.Decks.Where(x => x.Id == id).ExecuteDeleteAsync(Ct);
        await db.DeckGenerationPlans.Where(x => x.Id == preview.Id).ExecuteUpdateAsync(x => x.SetProperty(p => p.ReviewedJson, "old unsupported receipt"), Ct);
        Assert.Equal(id, await Store(test).ApplyAsync(preview.Id, Ct)); Assert.Single(await test.Service.GetDecksAsync(Ct));
    }
    [Fact]
    public async Task PlanningPersistsMissingCardsWithoutIncreasingOwnershipAndRejectsIncompleteOwnedPreview()
    {
        await using var test = DeckValidationPersistenceTests.Database(ArchetypePoolPersistenceTests.Handler()); await Seed(test, false);
        var owned = await Store(test).CalculateAsync(GenerationMode.OwnedOnly, Ct); Assert.False(owned.CanPreview);
        await Assert.ThrowsAsync<ArgumentException>(() => Store(test).PreviewAsync(owned.Fingerprint, GenerationMode.OwnedOnly, "Blocked", Ct));
        var preview = await Preview(test, GenerationMode.Planning); Assert.Equal(60, preview.Deck.Missing);
        var id = await Store(test).ApplyAsync(preview.Id, Ct); Assert.Equal(60, (await test.Service.GetDeckCardsAsync(id, Ct)).Sum(x => x.Quantity));
        Assert.Empty(await test.Service.GetCollectionAsync(Ct)); Assert.True((await DeckValidationPersistenceTests.Store(test).ValidateAsync(id, Ct)).Passed);
    }
    [Theory]
    [InlineData("stock-back")][InlineData("catalog")][InlineData("metadata")][InlineData("arena")]
    public async Task ChangedInputsRevokeBothStaleCalculationAndPreparedPreview(string change)
    {
        var handler = ArchetypePoolPersistenceTests.Handler(); await using var test = DeckValidationPersistenceTests.Database(handler); await Seed(test);
        var preview = await Preview(test); var catalog = test.Provider.GetRequiredService<CatalogService>();
        var card = (await catalog.SearchAsync(new(Search: "Shock"), Ct)).Items.Single();
        if (change == "stock-back") { await catalog.SetOwnedQuantityAsync(card.PrintingId, 1, Ct); await catalog.SetOwnedQuantityAsync(card.PrintingId, 4, Ct); }
        else if (change == "catalog") { handler.Version++; await test.Provider.GetRequiredService<ICatalogImporter>().ImportOnlineAsync(ct: Ct); }
        else
        {
            await using var db = await test.Provider.GetRequiredService<IDbContextFactory<DeckbuilderDbContext>>().CreateDbContextAsync(Ct);
            if (change == "metadata") await db.Cards.Where(x => x.Name == "Shock").ExecuteUpdateAsync(x => x.SetProperty(p => p.OracleText, "Changed"), Ct);
            else await db.Printings.Where(x => x.Id == card.PrintingId).ExecuteUpdateAsync(x => x.SetProperty(p => p.IsInCatalog, false), Ct);
        }
        await Assert.ThrowsAsync<ArgumentException>(() => Store(test).ApplyAsync(preview.Id, Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => Store(test).PreviewAsync(preview.Deck.Fingerprint, GenerationMode.OwnedOnly, "Stale", Ct));
        Assert.Empty(await test.Service.GetDecksAsync(Ct));
    }
    [Theory]
    [InlineData(2)][InlineData(16)]
    public async Task TimeExpiryWithoutDatabaseMutationBlocksApply(int days)
    {
        var clock = new MutableClock(); await using var test = new TestDatabase(s =>
        { s.AddSingleton(new HttpClient(ArchetypePoolPersistenceTests.Handler())); s.AddSingleton<TimeProvider>(clock); });
        await Seed(test); var preview = await Preview(test); clock.Days = days;
        await Assert.ThrowsAsync<ArgumentException>(() => Store(test).ApplyAsync(preview.Id, Ct)); Assert.Empty(await test.Service.GetDecksAsync(Ct));
    }
    [Theory]
    [InlineData("EngineVersion")][InlineData("PoolVersion")][InlineData("ReviewedLines")]
    public async Task UnsupportedVersionOrDifferentReviewedLinesCannotBeApplied(string field)
    {
        await using var test = DeckValidationPersistenceTests.Database(ArchetypePoolPersistenceTests.Handler()); await Seed(test); var preview = await Preview(test);
        await using var db = await test.Provider.GetRequiredService<IDbContextFactory<DeckbuilderDbContext>>().CreateDbContextAsync(Ct);
        var plan = await db.DeckGenerationPlans.AsNoTracking().SingleAsync(Ct); var payload = JsonNode.Parse(plan.ReviewedJson)!;
        payload[field] = field == "ReviewedLines" ? new JsonArray() : JsonValue.Create("unsupported"); var json = payload.ToJsonString();
        await db.DeckGenerationPlans.Where(x => x.Id == plan.Id).ExecuteUpdateAsync(x => x.SetProperty(p => p.ReviewedJson, json), Ct);
        await Assert.ThrowsAsync<ArgumentException>(() => Store(test).ApplyAsync(preview.Id, Ct)); Assert.Empty(await test.Service.GetDecksAsync(Ct));
    }
    [Fact]
    public async Task ConcurrentApplyFromIndependentServiceProvidersCreatesOnlyOneDeck()
    {
        await using var test = DeckValidationPersistenceTests.Database(ArchetypePoolPersistenceTests.Handler()); await Seed(test); var preview = await Preview(test);
        var services = new ServiceCollection(); services.AddLogging(); services.AddDeckbuilder(test.Paths); services.AddSingleton<TimeProvider>(new DeckValidationPersistenceTests.FixedClock());
        await using var other = services.BuildServiceProvider();
        var ids = await Task.WhenAll(Store(test).ApplyAsync(preview.Id, Ct), other.GetRequiredService<IDeckGenerationStore>().ApplyAsync(preview.Id, Ct));
        Assert.Equal(ids[0], ids[1]); Assert.Single(await test.Service.GetDecksAsync(Ct));
    }
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task FailureOrCancellationAfterSqlWritesRollsBackDeckAndReceipt(bool cancellation)
    {
        var fault = new ApplyFailure(); await using var test = new TestDatabase(s =>
        {
            s.AddSingleton(new HttpClient(ArchetypePoolPersistenceTests.Handler())); s.AddSingleton<TimeProvider>(new DeckValidationPersistenceTests.FixedClock());
            s.AddSingleton<IDbContextFactory<DeckbuilderDbContext>>(sp => new InterceptingFactory(sp.GetRequiredService<AppPaths>(), fault));
        });
        await Seed(test); var preview = await Preview(test); var before = await test.Service.GetCollectionAsync(Ct);
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Ct); fault.Armed = true; fault.Cancel = cancellation ? cancel : null;
        if (cancellation) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Store(test).ApplyAsync(preview.Id, cancel.Token));
        else await Assert.ThrowsAsync<IOException>(() => Store(test).ApplyAsync(preview.Id, Ct));
        Assert.True(fault.Fired); Assert.Empty(await test.Service.GetDecksAsync(Ct)); Assert.Equal(before, await test.Service.GetCollectionAsync(Ct));
        await using (var db = await test.Provider.GetRequiredService<IDbContextFactory<DeckbuilderDbContext>>().CreateDbContextAsync(Ct))
            Assert.Null((await db.DeckGenerationPlans.SingleAsync(Ct)).AppliedDeckId);
        fault.Armed = false; var id = await Store(test).ApplyAsync(preview.Id, Ct); Assert.NotEqual(Guid.Empty, id);
    }
    [Fact]
    public async Task UpgradeFromPhaseTenPreservesFilledRowsAndReceiptAndCreatesOneBackup()
    {
        await using var test = new TestDatabase(); Directory.CreateDirectory(test.Paths.DataDirectory);
        var factory = test.Provider.GetRequiredService<IDbContextFactory<DeckbuilderDbContext>>(); Guid deckId; Guid printingId;
        await using (var db = await factory.CreateDbContextAsync(Ct))
        {
            await db.GetService<IMigrator>().MigrateAsync("20261022090051_AddDeckAdvicePlans", Ct);
            var card = new Card("Preserve"); var printing = new Printing(card.Id, "OLD", "1"); printingId = printing.Id;
            var deck = new Deck("Existing", DeckRuleProfiles.Bo3); deckId = deck.Id;
            db.Cards.Add(card); db.Printings.Add(printing); db.Decks.Add(deck); db.CollectionEntries.Add(new(printingId, 3));
            db.DeckCards.Add(new(deckId, printingId, DeckZone.Sideboard, 2)); db.DeckAdvicePlans.Add(new(deckId, "old fingerprint", "Receipt", "{}")); await db.SaveChangesAsync(Ct);
        }
        await test.InitializeAsync(); await test.InitializeAsync();
        Assert.Equal(3, Assert.Single(await test.Service.GetCollectionAsync(Ct)).Quantity); Assert.Equal(2, Assert.Single(await test.Service.GetDeckCardsAsync(deckId, Ct)).Quantity);
        Assert.Single(Directory.GetFiles(test.Paths.BackupsDirectory, "*.db"));
        await using var after = await factory.CreateDbContextAsync(Ct); Assert.Empty(await after.DeckGenerationPlans.ToArrayAsync(Ct));
        Assert.Single(await after.DeckAdvicePlans.ToArrayAsync(Ct)); Assert.Equal(9, (await after.Database.GetAppliedMigrationsAsync(Ct)).Count());
    }
    [Fact]
    public async Task CancelledCalculationOrPreviewNeverCreatesDeckOrReceipt()
    {
        await using var test = DeckValidationPersistenceTests.Database(ArchetypePoolPersistenceTests.Handler()); await Seed(test);
        var result = await Store(test).CalculateAsync(GenerationMode.OwnedOnly, Ct);
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Store(test).CalculateAsync(GenerationMode.Planning, cancel.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Store(test).PreviewAsync(result.Fingerprint, result.Mode, "Cancelled", cancel.Token));
        await Assert.ThrowsAsync<ArgumentException>(() => Store(test).PreviewAsync(result.Fingerprint, result.Mode, " ", Ct));
        await using var db = await test.Provider.GetRequiredService<IDbContextFactory<DeckbuilderDbContext>>().CreateDbContextAsync(Ct);
        Assert.Empty(await db.DeckGenerationPlans.ToArrayAsync(Ct)); Assert.Empty(await db.Decks.ToArrayAsync(Ct));
    }
    private sealed class MutableClock : TimeProvider
    {
        public int Days { get; set; }
        public override DateTimeOffset GetUtcNow() => new(DeckValidationPersistenceTests.SourceTime.AddHours(1).AddDays(Days));
    }
    private sealed class ApplyFailure : SaveChangesInterceptor
    {
        public bool Armed { get; set; }
        public bool Fired { get; private set; }
        public CancellationTokenSource? Cancel { get; set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData data, int result, CancellationToken cancellationToken = default)
        {
            if (Armed && data.Context!.ChangeTracker.Entries<DeckGenerationPlan>().Any(x => x.Entity.AppliedDeckId is not null))
            { Fired = true; if (Cancel is { } cancel) cancel.Cancel(); else throw new IOException("Injected failure after generated deck SQL writes"); }
            return ValueTask.FromResult(result);
        }
    }
    private sealed class InterceptingFactory(AppPaths paths, SaveChangesInterceptor interceptor) : IDbContextFactory<DeckbuilderDbContext>
    {
        public DeckbuilderDbContext CreateDbContext() => new(new DbContextOptionsBuilder<DeckbuilderDbContext>()
            .UseSqlite(new SqliteConnectionStringBuilder { DataSource = paths.DatabasePath, ForeignKeys = true }.ToString()).AddInterceptors(interceptor).Options);
    }
}
