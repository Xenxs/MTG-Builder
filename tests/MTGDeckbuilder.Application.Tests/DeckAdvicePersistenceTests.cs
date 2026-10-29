using Microsoft.Data.Sqlite;
using System.Text.Json;
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

public sealed class DeckAdvicePersistenceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    internal static IDeckAdviceStore Store(TestDatabase test) => test.Provider.GetRequiredService<IDeckAdviceStore>();
    internal static DeckValidationPersistenceTests.BulkHandler Handler() => new()
    {
        Cards = [DeckValidationPersistenceTests.LegalCard(), DeckValidationPersistenceTests.LegalCard("Missing", "2"),
            DeckValidationPersistenceTests.LegalCard("Spare", "3")]
    };
    internal static async Task<(Guid Deck, Guid Missing, Guid Spare)> Seed(TestDatabase test)
    {
        var deck = await DeckValidationPersistenceTests.SeedDeck(test, DeckRuleProfiles.Bo1);
        var catalog = await test.Provider.GetRequiredService<CatalogService>().SearchAsync(new(), Ct);
        var forest = catalog.Items.Single(x => x.Name == "Forest").PrintingId;
        var missing = catalog.Items.Single(x => x.Name == "Missing").PrintingId;
        var spare = catalog.Items.Single(x => x.Name == "Spare").PrintingId;
        await test.Service.SetDeckCardAsync(deck, missing, DeckZone.Mainboard, 2, Ct);
        await test.Provider.GetRequiredService<CatalogService>().SetOwnedQuantityAsync(forest, 60, Ct);
        await test.Provider.GetRequiredService<CatalogService>().SetOwnedQuantityAsync(spare, 2, Ct);
        return (deck, missing, spare);
    }
    private static async Task<AdvicePreview> Preview(TestDatabase test, (Guid Deck, Guid Missing, Guid Spare) seed)
    {
        var advice = await Store(test).GetAsync(seed.Deck, ct: Ct);
        return await Store(test).PreviewAsync(seed.Deck, advice.Fingerprint,
            [new(seed.Missing, DeckZone.Mainboard, seed.Spare, 2)], "Reviewed copy", Ct);
    }
    [Fact]
    public async Task PreparationStoresClassificationVersionRulesAndReviewedExplanations()
    {
        await using var test = DeckValidationPersistenceTests.Database(Handler()); var seed = await Seed(test);
        var preview = await Preview(test, seed);
        await using var db = await test.Provider.GetRequiredService<IDbContextFactory<DeckbuilderDbContext>>().CreateDbContextAsync(Ct);
        var plan = await db.DeckAdvicePlans.AsNoTracking().SingleAsync(x => x.Id == preview.Id, Ct);
        using var json = JsonDocument.Parse(plan.ChoicesJson); var root = json.RootElement;
        Assert.Equal(CardRoleClassifier.Version, root.GetProperty("ClassifierVersion").GetString());
        Assert.Equal(DeckAdviceEngine.Version, root.GetProperty("EngineVersion").GetString());
        var role = root.GetProperty("ReviewedChanges")[0].GetProperty("Role");
        Assert.Equal("unknown", role.GetProperty("Source").GetProperty("RuleId").GetString());
        Assert.False(string.IsNullOrWhiteSpace(role.GetProperty("Explanation").GetString()));
        Assert.Equal(preview.Advice.Changes[0].Role!.Explanation, role.GetProperty("Explanation").GetString());
    }
    [Fact]
    public async Task OldPreparedPlansArePreservedButCannotBeAppliedAndAppliedReceiptsRemainIdempotent()
    {
        await using var test = DeckValidationPersistenceTests.Database(Handler()); var seed = await Seed(test);
        Guid oldId; Guid appliedId;
        await using (var db = await test.Provider.GetRequiredService<IDbContextFactory<DeckbuilderDbContext>>().CreateDbContextAsync(Ct))
        {
            var old = new DeckAdvicePlan(seed.Deck, "phase6-hash", "Old pending", "[]"); oldId = old.Id;
            var receipt = new DeckAdvicePlan(seed.Deck, "phase6-hash", "Old applied", "[]");
            receipt.MarkApplied(seed.Deck); appliedId = receipt.Id;
            db.DeckAdvicePlans.AddRange(old, receipt); await db.SaveChangesAsync(Ct);
        }
        await test.ReopenAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => Store(test).ApplyAsync(oldId, Ct));
        Assert.Equal(seed.Deck, await Store(test).ApplyAsync(appliedId, Ct));
        await using var after = await test.Provider.GetRequiredService<IDbContextFactory<DeckbuilderDbContext>>().CreateDbContextAsync(Ct);
        Assert.Equal(2, await after.DeckAdvicePlans.CountAsync(Ct)); Assert.Single(await test.Service.GetDecksAsync(Ct));
        Assert.Empty(await after.Database.GetPendingMigrationsAsync(Ct));
    }
    [Fact]
    public async Task PreviewSurvivesRestartAndConcurrentApplyCreatesExactlyOneNewDeckWithoutChangingOriginalOrStock()
    {
        await using var test = DeckValidationPersistenceTests.Database(Handler()); var seed = await Seed(test);
        var before = await test.Service.GetDeckCardsAsync(seed.Deck, Ct); var stock = await test.Service.GetCollectionAsync(Ct);
        var preview = await Preview(test, seed);
        Assert.Single(await test.Service.GetDecksAsync(Ct)); Assert.Empty(preview.Advice.Needs);
        await test.ReopenAsync(); var store = Store(test);
        // Independent writer gates exercise SQLite serialization as well as process-local locking.
        var secondStore = new SqliteDeckAdviceStore(test.Provider.GetRequiredService<IDbContextFactory<DeckbuilderDbContext>>(),
            new DatabaseWriteLock(), new DeckValidationPersistenceTests.FixedClock());
        var ids = await Task.WhenAll(store.ApplyAsync(preview.Id, Ct), secondStore.ApplyAsync(preview.Id, Ct)); Assert.Equal(ids[0], ids[1]);
        Assert.Equal(2, (await test.Service.GetDecksAsync(Ct)).Count);
        Assert.Equal(before, await test.Service.GetDeckCardsAsync(seed.Deck, Ct)); Assert.Equal(stock, await test.Service.GetCollectionAsync(Ct));
        var rows = await test.Service.GetDeckCardsAsync(ids[0], Ct); Assert.Contains(rows, x => x.PrintingId == seed.Spare && x.Quantity == 2);
        Assert.DoesNotContain(rows, x => x.PrintingId == seed.Missing);
        Assert.True((await DeckValidationPersistenceTests.Store(test).ValidateAsync(ids[0], Ct)).Passed);
        await test.Service.DeleteDeckAsync(ids[0], Ct);
        Assert.Equal(ids[0], await store.ApplyAsync(preview.Id, Ct)); Assert.Single(await test.Service.GetDecksAsync(Ct));
    }
    [Theory]
    [InlineData("collection")]
    [InlineData("deck")]
    [InlineData("profile")]
    [InlineData("source")]
    [InlineData("metadata")]
    [InlineData("cost")]
    [InlineData("delete")]
    public async Task ChangedInputsBlockPreviewReuseAndNeverCreateADeck(string change)
    {
        var handler = Handler(); await using var test = DeckValidationPersistenceTests.Database(handler); var seed = await Seed(test);
        var preview = await Preview(test, seed);
        switch (change)
        {
            case "collection":
                await test.Service.SetCollectionQuantityAsync(seed.Spare, 1, Ct);
                await test.Service.SetCollectionQuantityAsync(seed.Spare, 2, Ct); // Same count, changed version.
                break;
            case "deck": await test.Service.SetDeckCardAsync(seed.Deck, seed.Missing, DeckZone.Mainboard, 3, Ct); break;
            case "profile": await DeckValidationPersistenceTests.Store(test).SetProfileAsync(seed.Deck, DeckRuleProfile.ArenaStandardBo3, Ct); break;
            case "source": handler.Version++; await test.Provider.GetRequiredService<ICatalogImporter>().ImportOnlineAsync(ct: Ct); break;
            case "delete": await test.Service.DeleteDeckAsync(seed.Deck, Ct); break;
            default:
                await using (var db = await test.Provider.GetRequiredService<IDbContextFactory<DeckbuilderDbContext>>().CreateDbContextAsync(Ct))
                {
                    var printing = await db.Printings.SingleAsync(x => x.Id == seed.Spare, Ct);
                    if (change == "cost")
                        await db.Cards.Where(x => x.Id == printing.CardId).ExecuteUpdateAsync(x => x.SetProperty(c => c.ManaCost, "{6}{U}"), Ct);
                    else await db.Cards.Where(x => x.Id == printing.CardId).ExecuteUpdateAsync(x => x.SetProperty(c => c.ManaValue, 7), Ct);
                }
                break;
        }
        await Assert.ThrowsAsync<ArgumentException>(() => Store(test).ApplyAsync(preview.Id, Ct));
        Assert.Equal(change == "delete" ? 0 : 1, (await test.Service.GetDecksAsync(Ct)).Count);
        if (change != "delete") await Assert.ThrowsAsync<ArgumentException>(() => Store(test).GetAsync(seed.Deck, preview.Advice.Fingerprint, ct: Ct));
    }
    [Theory]
    [InlineData(2)]
    [InlineData(16)]
    public async Task TimePassingWithoutDataChangesStillBlocksSaving(int days)
    {
        var clock = new MutableClock();
        await using var test = new TestDatabase(s => { s.AddSingleton(new HttpClient(Handler())); s.AddSingleton<TimeProvider>(clock); });
        var seed = await Seed(test); var preview = await Preview(test, seed); clock.Days = days;
        await Assert.ThrowsAsync<ArgumentException>(() => Store(test).ApplyAsync(preview.Id, Ct));
        Assert.Single(await test.Service.GetDecksAsync(Ct));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailureOrCancellationAfterSqlWritesRollsBackDeckAndReceipt(bool cancellation)
    {
        var fault = new ApplyFailure(); AppPaths? paths = null;
        await using var test = new TestDatabase(s =>
        {
            paths = (AppPaths)s.Single(x => x.ServiceType == typeof(AppPaths)).ImplementationInstance!;
            s.AddSingleton(new HttpClient(Handler())); s.AddSingleton<TimeProvider>(new DeckValidationPersistenceTests.FixedClock());
            s.AddSingleton<IDbContextFactory<DeckbuilderDbContext>>(new InterceptingFactory(paths, fault));
        });
        var seed = await Seed(test); var preview = await Preview(test, seed);
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        fault.Armed = true; if (cancellation) fault.Cancel = cancel;
        if (cancellation) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Store(test).ApplyAsync(preview.Id, cancel.Token));
        else await Assert.ThrowsAsync<IOException>(() => Store(test).ApplyAsync(preview.Id, Ct));
        Assert.True(fault.Fired); Assert.Single(await test.Service.GetDecksAsync(Ct));
        await using (var db = await test.Provider.GetRequiredService<IDbContextFactory<DeckbuilderDbContext>>().CreateDbContextAsync(Ct))
            Assert.Null((await db.DeckAdvicePlans.SingleAsync(Ct)).AppliedDeckId);
        fault.Armed = false; var id = await Store(test).ApplyAsync(preview.Id, Ct); Assert.NotEqual(seed.Deck, id);
    }
    [Fact]
    public async Task InvalidOrOverspentSelectionCannotBePreparedAndPartialReplacementIsExplicit()
    {
        await using var test = DeckValidationPersistenceTests.Database(Handler()); var seed = await Seed(test);
        var advice = await Store(test).GetAsync(seed.Deck, ct: Ct);
        await Assert.ThrowsAsync<ArgumentException>(() => Store(test).PreviewAsync(seed.Deck, advice.Fingerprint,
            [new(seed.Missing, DeckZone.Mainboard, seed.Spare, 3)], "Invalid", Ct));
        var preview = await Store(test).PreviewAsync(seed.Deck, advice.Fingerprint,
            [new(seed.Missing, DeckZone.Mainboard, seed.Spare, 1)], "Partial", Ct);
        Assert.Equal(1, Assert.Single(preview.Advice.Needs).Missing);
        var copy = await Store(test).ApplyAsync(preview.Id, Ct);
        Assert.Contains(await test.Service.GetDeckCardsAsync(copy, Ct), x => x.PrintingId == seed.Missing && x.Quantity == 1);
    }
    [Fact]
    public async Task PhaseFiveUpgradeAddsOnlyPlansAndPreservesFilledDataWithBackup()
    {
        await using var test = new TestDatabase(); Directory.CreateDirectory(test.Paths.DataDirectory);
        var factory = test.Provider.GetRequiredService<IDbContextFactory<DeckbuilderDbContext>>();
        Guid deckId;
        await using (var db = await factory.CreateDbContextAsync(Ct))
        {
            await db.GetService<IMigrator>().MigrateAsync("20261022082737_AddStandardValidation", Ct);
            var card = new Card("Preserve"); var printing = new Printing(card.Id, "OLD", "1");
            var deck = new Deck("Preserve", DeckRuleProfiles.Bo3); deckId = deck.Id;
            db.Cards.Add(card); db.Printings.Add(printing); db.CollectionEntries.Add(new(printing.Id, 3)); db.Decks.Add(deck);
            db.DeckCards.Add(new(deck.Id, printing.Id, DeckZone.Sideboard, 2)); await db.SaveChangesAsync(Ct);
        }
        await test.InitializeAsync();
        Assert.Equal(3, Assert.Single(await test.Service.GetCollectionAsync(Ct)).Quantity);
        Assert.Equal(2, Assert.Single(await test.Service.GetDeckCardsAsync(deckId, Ct)).Quantity);
        Assert.Equal(DeckRuleProfile.ArenaStandardBo3, await DeckValidationPersistenceTests.Store(test).GetProfileAsync(deckId, Ct));
        Assert.Single(Directory.GetFiles(test.Paths.BackupsDirectory, "*.db"));
        await using var after = await factory.CreateDbContextAsync(Ct); Assert.Empty(await after.DeckAdvicePlans.ToListAsync(Ct));
        Assert.Equal(9, (await after.Database.GetAppliedMigrationsAsync(Ct)).Count());
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
            if (Armed && data.Context!.ChangeTracker.Entries<DeckAdvicePlan>().Any(x => x.Entity.AppliedDeckId is not null))
            { Fired = true; if (Cancel is { } cancel) cancel.Cancel(); else throw new IOException("Injected failure after advice SQL writes"); }
            return ValueTask.FromResult(result);
        }
    }
    private sealed class InterceptingFactory(AppPaths paths, SaveChangesInterceptor interceptor) : IDbContextFactory<DeckbuilderDbContext>
    {
        public DeckbuilderDbContext CreateDbContext() => new(new DbContextOptionsBuilder<DeckbuilderDbContext>()
            .UseSqlite(new SqliteConnectionStringBuilder { DataSource = paths.DatabasePath, ForeignKeys = true }.ToString()).AddInterceptors(interceptor).Options);
    }
}
