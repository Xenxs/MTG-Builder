using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MTGDeckbuilder.Application;
using MTGDeckbuilder.Domain;
using MTGDeckbuilder.Infrastructure;
using Xunit;

namespace MTGDeckbuilder.Application.Tests;

public sealed class PersistenceTests
{
    [Fact]
    public async Task MigrationsCreateRealDatabaseAndAreIdempotent()
    {
        await using var test = new TestDatabase();
        await test.InitializeAsync();
        await test.InitializeAsync();
        Assert.True(File.Exists(test.Paths.DatabasePath));
        await using var db = await test.Provider.GetRequiredService<IDbContextFactory<DeckbuilderDbContext>>().CreateDbContextAsync(TestContext.Current.CancellationToken);
        Assert.Equal(db.Database.GetMigrations(), await db.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await db.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Equal(new DashboardSummary(0, 0, 0), await test.Service.GetSummaryAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CollectionDeckAndSettingsSurviveProviderRestart()
    {
        await using var test = new TestDatabase();
        await test.InitializeAsync();
        await test.Service.SaveCollectionAsync(new(" Llanowar Elves ", " fdn ", " 227 ", 4), TestContext.Current.CancellationToken);
        var printing = Assert.Single(await test.Service.GetCollectionAsync(TestContext.Current.CancellationToken));
        var deck = await test.Service.CreateDeckAsync("Elves", "Standard", TestContext.Current.CancellationToken);
        await test.Service.SetDeckCardAsync(deck, printing.PrintingId, DeckZone.Mainboard, 4, TestContext.Current.CancellationToken);
        await test.Service.SavePlayerNameAsync(" Nissa ", TestContext.Current.CancellationToken);
        await test.ReopenAsync();
        Assert.Equal(printing, Assert.Single(await test.Service.GetCollectionAsync(TestContext.Current.CancellationToken)));
        Assert.Equal("Nissa", await test.Service.GetPlayerNameAsync(TestContext.Current.CancellationToken));
        Assert.Equal(4, Assert.Single(await test.Service.GetDeckCardsAsync(deck, TestContext.Current.CancellationToken)).Quantity);
        Assert.Contains("FDN #227", Assert.Single(await test.Service.GetDeckCardsAsync(deck, TestContext.Current.CancellationToken)).Label);
        Assert.Equal(new DashboardSummary(1, 4, 1), await test.Service.GetSummaryAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReenteringPrintingReplacesCountAndConflictDoesNotCorruptData()
    {
        await using var test = new TestDatabase();
        await test.InitializeAsync();
        await test.Service.SaveCollectionAsync(new("Llanowar Elves", "FDN", "227", 4), TestContext.Current.CancellationToken);
        await test.Service.SaveCollectionAsync(new("llanowar elves", "fdn", "227", 2), TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<ArgumentException>(() => test.Service.SaveCollectionAsync(new("Other Card", "FDN", "227", 5), TestContext.Current.CancellationToken));
        Assert.Equal(2, Assert.Single(await test.Service.GetCollectionAsync(TestContext.Current.CancellationToken)).Quantity);
        await using var db = await test.Provider.GetRequiredService<IDbContextFactory<DeckbuilderDbContext>>().CreateDbContextAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, await db.Cards.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await db.Printings.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DifferentPrintingsCanShareCardAndZeroCountRemainsEditable()
    {
        await using var test = new TestDatabase();
        await test.InitializeAsync();
        await test.Service.SaveCollectionAsync(new("Llanowar Elves", "FDN", "227", 4), TestContext.Current.CancellationToken);
        await test.Service.SaveCollectionAsync(new("Llanowar Elves", "DAR", "168", 1), TestContext.Current.CancellationToken);
        var first = (await test.Service.GetCollectionAsync(TestContext.Current.CancellationToken)).First();
        await test.Service.SetCollectionQuantityAsync(first.PrintingId, 0, TestContext.Current.CancellationToken);
        Assert.Equal(new DashboardSummary(1, 4, 0), await test.Service.GetSummaryAsync(TestContext.Current.CancellationToken));
        Assert.Equal(2, (await test.Service.GetCollectionAsync(TestContext.Current.CancellationToken)).Count);
        await using var db = await test.Provider.GetRequiredService<IDbContextFactory<DeckbuilderDbContext>>().CreateDbContextAsync(TestContext.Current.CancellationToken);
        Assert.Single(await db.Cards.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MainboardAndSideboardAreIndependentAndDeckDeletionPreservesCollection()
    {
        await using var test = new TestDatabase();
        await test.InitializeAsync();
        await test.Service.SaveCollectionAsync(new("Negate", "FDN", "710", 4), TestContext.Current.CancellationToken);
        var printing = Assert.Single(await test.Service.GetCollectionAsync(TestContext.Current.CancellationToken));
        var deck = await test.Service.CreateDeckAsync("Blue", "Standard", TestContext.Current.CancellationToken);
        await test.Service.SetDeckCardAsync(deck, printing.PrintingId, DeckZone.Mainboard, 2, TestContext.Current.CancellationToken);
        await test.Service.SetDeckCardAsync(deck, printing.PrintingId, DeckZone.Sideboard, 1, TestContext.Current.CancellationToken);
        await test.Service.SetDeckCardAsync(deck, printing.PrintingId, DeckZone.Mainboard, 3, TestContext.Current.CancellationToken);
        Assert.Equal(4, (await test.Service.GetDeckCardsAsync(deck, TestContext.Current.CancellationToken)).Sum(x => x.Quantity));
        await test.Service.SetDeckCardAsync(deck, printing.PrintingId, DeckZone.Mainboard, 0, TestContext.Current.CancellationToken);
        Assert.Equal(DeckZone.Sideboard, Assert.Single(await test.Service.GetDeckCardsAsync(deck, TestContext.Current.CancellationToken)).Zone);
        await test.Service.DeleteDeckAsync(deck, TestContext.Current.CancellationToken);
        Assert.Empty(await test.Service.GetDeckCardsAsync(deck, TestContext.Current.CancellationToken));
        Assert.Equal(4, Assert.Single(await test.Service.GetCollectionAsync(TestContext.Current.CancellationToken)).Quantity);
    }

    [Fact]
    public async Task DatabaseEnforcesForeignKeysAndCountConstraints()
    {
        await using var test = new TestDatabase();
        await test.InitializeAsync();
        await using var db = await test.Provider.GetRequiredService<IDbContextFactory<DeckbuilderDbContext>>().CreateDbContextAsync(TestContext.Current.CancellationToken);
        db.DeckCards.Add(new DeckCard(Guid.NewGuid(), Guid.NewGuid(), DeckZone.Mainboard, 1));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
        db.ChangeTracker.Clear();
        await test.Service.SaveCollectionAsync(new("Negate", "FDN", "710", 4), TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => db.Database.ExecuteSqlRawAsync("UPDATE CollectionEntries SET Quantity = -1", TestContext.Current.CancellationToken));
        Assert.Equal(4, Assert.Single(await test.Service.GetCollectionAsync(TestContext.Current.CancellationToken)).Quantity);
    }

    [Fact]
    public async Task InvalidMutationsAndCancellationLeaveDatabaseUntouched()
    {
        await using var test = new TestDatabase();
        await test.InitializeAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => test.Service.SetDeckCardAsync(Guid.NewGuid(), Guid.NewGuid(), DeckZone.Mainboard, 1, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => test.Service.SaveCollectionAsync(new("Negate", "FDN", "710", -1), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => test.Service.SavePlayerNameAsync(" ", TestContext.Current.CancellationToken));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => test.Service.GetCollectionAsync(cancelled.Token));
        Assert.Equal(new DashboardSummary(0, 0, 0), await test.Service.GetSummaryAsync(TestContext.Current.CancellationToken));
    }
}
