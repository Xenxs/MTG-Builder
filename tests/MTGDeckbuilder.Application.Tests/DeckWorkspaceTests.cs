using Microsoft.Data.Sqlite;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using MTGDeckbuilder.Domain;
using MTGDeckbuilder.Infrastructure;
using Xunit;

namespace MTGDeckbuilder.Application.Tests;

public sealed class DeckWorkspaceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    internal static IDeckWorkspace Workspace(TestDatabase test) => test.Provider.GetRequiredService<IDeckWorkspace>();
    internal const string Source = "About\nName Elves plan\n\nCommander\n1 Llanowar Elves (FDN) 227\n\nDeck\n4 Llanowar Elves (FDN) 227\n\nSideboard\n2 Negate (FDN) 710\n";
    internal static async Task<IReadOnlyList<CatalogItem>> Seed(TestDatabase test)
    {
        await test.InitializeAsync();
        var elf = CatalogFixtures.Card(); var negate = CatalogFixtures.Card("Negate", collector: "710", type: "Instant", colors: ["U"], mana: 2, rarity: "uncommon");
        await test.Provider.GetRequiredService<ICatalogImporter>().ImportFileAsync(await CatalogFixtures.WriteAsync(test, [elf, negate], "json", Ct), ct: Ct);
        return (await test.Provider.GetRequiredService<CatalogService>().SearchAsync(new CatalogQuery(), Ct)).Items;
    }
    private static async Task<DeckImportPreview> Prepare(TestDatabase test, string text = Source, string name = "")
    {
        var id = await Workspace(test).PrepareAsync(text, name, "Overig", Ct);
        return await Workspace(test).GetPreviewAsync(id, ct: Ct);
    }
    [Fact]
    public async Task PreparedImportReopensCreatesExactlyOneNewDeckAndLeavesOwnershipAndOtherDecksAlone()
    {
        await using var test = new TestDatabase(); var items = await Seed(test);
        var elf = items.Single(x => x.Name == "Llanowar Elves");
        await test.Provider.GetRequiredService<CatalogService>().SetOwnedQuantityAsync(elf.PrintingId, 2, Ct);
        var old = await test.Service.CreateDeckAsync("Existing", "Standard", Ct);
        await test.Service.SetDeckCardAsync(old, elf.PrintingId, DeckZone.Mainboard, 1, Ct);
        var before = await test.Service.GetCollectionAsync(Ct);
        var p = await Prepare(test); Assert.True(p.CanApply); Assert.Equal("Elves plan", p.Name);
        Assert.Equal(3, p.Total); Assert.Equal(7, p.TotalCards); Assert.Single(await test.Service.GetDecksAsync(Ct));
        await test.ReopenAsync();
        var deck = await Workspace(test).ApplyAsync(p.Id, p.Version, Ct);
        await Assert.ThrowsAsync<ArgumentException>(() => Workspace(test).ApplyAsync(p.Id, p.Version, Ct));
        await test.ReopenAsync();
        Assert.Equal(2, (await test.Service.GetDecksAsync(Ct)).Count);
        Assert.Equal(before, await test.Service.GetCollectionAsync(Ct));
        Assert.Equal(1, Assert.Single(await test.Service.GetDeckCardsAsync(old, Ct)).Quantity);
        var cards = await test.Service.GetDeckCardsAsync(deck, Ct);
        Assert.Equal(3, cards.Count); Assert.Equal(1, cards.Single(x => x.Zone == DeckZone.Commander).Quantity);
        var history = Assert.Single(await Workspace(test).GetHistoryAsync(ct: Ct));
        Assert.Equal(deck, history.DeckId); Assert.Equal(DeckImportState.Applied, history.State);
    }
    [Fact]
    public async Task ExportedDeckCanBeImportedAgainWithZonesNamesAndAmounts()
    {
        await using var test = new TestDatabase(); await Seed(test);
        var p = await Prepare(test);
        var deck = await Workspace(test).ApplyAsync(p.Id, p.Version, Ct);
        var text = await Workspace(test).ExportAsync(deck, Ct);
        Assert.Contains("Commander", text); Assert.Contains("Sideboard", text); Assert.Contains("Name Elves plan", text);
        var repeat = await Prepare(test, text); Assert.True(repeat.CanApply);
        var second = await Workspace(test).ApplyAsync(repeat.Id, repeat.Version, Ct);
        var expected = (await test.Service.GetDeckCardsAsync(deck, Ct)).OrderBy(x => x.Zone).ThenBy(x => x.PrintingId).ToArray();
        var actual = (await test.Service.GetDeckCardsAsync(second, Ct)).OrderBy(x => x.Zone).ThenBy(x => x.PrintingId).ToArray();
        Assert.Equal(expected, actual); Assert.Empty(await test.Service.GetCollectionAsync(Ct));
    }
    [Fact]
    public async Task NamesOnlyPreferOwnedReprintOnlyWhenOracleIdentityIsUnique()
    {
        await using var test = new TestDatabase(); await test.InitializeAsync();
        var oracle = Guid.NewGuid().ToString();
        var cards = new[] { CatalogFixtures.Card(oracle: oracle), CatalogFixtures.Card(set: "M19", collector: "314", oracle: oracle) };
        await test.Provider.GetRequiredService<ICatalogImporter>().ImportFileAsync(await CatalogFixtures.WriteAsync(test, cards, "json", Ct), ct: Ct);
        var catalog = test.Provider.GetRequiredService<CatalogService>();
        var printings = (await catalog.SearchAsync(new CatalogQuery(), Ct)).Items;
        var preferred = printings.Single(x => x.SetCode == "M19");
        await catalog.SetOwnedQuantityAsync(preferred.PrintingId, 4, Ct);
        var p = await Prepare(test, "4 Llanowar Elves");
        Assert.True(p.CanApply); Assert.Equal(preferred.PrintingId, Assert.Single(p.Rows).PrintingId);
        Assert.Contains("Oracle", p.Rows[0].Reason);
    }
    [Fact]
    public async Task UnknownAndAmbiguousRowsRequireExplicitChoiceAndOldChoiceVersionIsRejected()
    {
        await using var test = new TestDatabase(); await test.InitializeAsync();
        var cards = new[] { CatalogFixtures.Card(), CatalogFixtures.Card() };
        await test.Provider.GetRequiredService<ICatalogImporter>().ImportFileAsync(await CatalogFixtures.WriteAsync(test, cards, "json", Ct), ct: Ct);
        var p = await Prepare(test, "Deck\n4 Llanowar Elves (FDN) 227\n2 Unknown\n");
        Assert.False(p.CanApply); Assert.Equal(2, p.Unresolved);
        await Assert.ThrowsAsync<ArgumentException>(() => Workspace(test).ApplyAsync(p.Id, p.Version, Ct));
        var candidates = await Workspace(test).GetCandidatesAsync(p.Id, 2, Ct); Assert.Equal(2, candidates.Count);
        await Workspace(test).ResolveAsync(p.Id, p.Version, 2, candidates[0].Id, Ct);
        await Assert.ThrowsAsync<ArgumentException>(() => Workspace(test).ResolveAsync(p.Id, p.Version, 3, candidates[1].Id, Ct));
        var next = await Workspace(test).GetPreviewAsync(p.Id, ct: Ct);
        await Workspace(test).ResolveAsync(next.Id, next.Version, 3, candidates[1].Id, Ct);
        next = await Workspace(test).GetPreviewAsync(next.Id, ct: Ct); Assert.True(next.CanApply);
        var deck = await Workspace(test).ApplyAsync(next.Id, next.Version, Ct);
        Assert.Equal(6, (await test.Service.GetDeckCardsAsync(deck, Ct)).Sum(x => x.Quantity));
        Assert.Empty(await test.Service.GetCollectionAsync(Ct));
    }
    [Fact]
    public async Task DuplicateRowsSumWithinZoneButNotAcrossZones()
    {
        await using var test = new TestDatabase(); await Seed(test);
        var p = await Prepare(test, "Deck\n2 Negate\n2 Negate\nSideboard\n1 Negate");
        var deck = await Workspace(test).ApplyAsync(p.Id, p.Version, Ct);
        var cards = await test.Service.GetDeckCardsAsync(deck, Ct);
        Assert.Equal(2, cards.Count); Assert.Equal(4, cards.Single(x => x.Zone == DeckZone.Mainboard).Quantity);
        Assert.Equal(1, cards.Single(x => x.Zone == DeckZone.Sideboard).Quantity);
    }
    [Fact]
    public async Task TotalsPerPrintingZoneCannotExceedStorageLimit()
    {
        await using var test = new TestDatabase(); await Seed(test);
        var p = await Prepare(test, "9999 Negate\n1 Negate");
        Assert.False(p.CanApply); Assert.Equal(1, p.QuantityConflicts);
        await Assert.ThrowsAsync<ArgumentException>(() => Workspace(test).ApplyAsync(p.Id, p.Version, Ct));
        Assert.Empty(await test.Service.GetDecksAsync(Ct));
    }
    [Fact]
    public async Task FrontNameAliasMatchesDoubleFacedCardWithoutLosingFullExportName()
    {
        await using var test = new TestDatabase(); await test.InitializeAsync();
        var card = CatalogFixtures.Card("Front // Back");
        await test.Provider.GetRequiredService<ICatalogImporter>().ImportFileAsync(await CatalogFixtures.WriteAsync(test, [card], "json", Ct), ct: Ct);
        var p = await Prepare(test, "1 Front (FDN) 227"); Assert.True(p.CanApply);
        var id = await Workspace(test).ApplyAsync(p.Id, p.Version, Ct);
        Assert.Contains("Front // Back (FDN) 227", await Workspace(test).ExportAsync(id, Ct));
    }
    [Fact]
    public async Task CatalogIdentityChangesBlockApplyButOwnershipChangesDoNot()
    {
        await using var test = new TestDatabase(); var items = await Seed(test);
        var p = await Prepare(test, "4 Negate");
        var negate = items.Single(x => x.Name == "Negate");
        await test.Provider.GetRequiredService<CatalogService>().SetOwnedQuantityAsync(negate.PrintingId, 3, Ct);
        await Workspace(test).ApplyAsync(p.Id, p.Version, Ct);
        p = await Prepare(test, "4 Negate");
        var factory = test.Provider.GetRequiredService<IDbContextFactory<DeckbuilderDbContext>>();
        await using (var db = await factory.CreateDbContextAsync(Ct))
        {
            var printing = await db.Printings.FindAsync([negate.PrintingId], Ct);
            var card = await db.Cards.FindAsync([printing!.CardId], Ct);
            card!.UpdateCatalog(card.OracleId!, "New name", card.ManaCost!, card.TypeLine!, card.OracleText!, card.ColorIdentity, card.ManaValue);
            await db.SaveChangesAsync(Ct);
        }
        await Assert.ThrowsAsync<ArgumentException>(() => Workspace(test).ApplyAsync(p.Id, p.Version, Ct));
        Assert.Single(await test.Service.GetDecksAsync(Ct));
    }
    [Fact]
    public async Task DiscardInvalidSourcesAndCancelledPreparationCreateNoDecks()
    {
        await using var test = new TestDatabase(); await Seed(test);
        var p = await Prepare(test); await Workspace(test).DiscardAsync(p.Id, p.Version, Ct);
        await Assert.ThrowsAsync<ArgumentException>(() => Workspace(test).ApplyAsync(p.Id, p.Version, Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => Workspace(test).PrepareAsync("Companion\n1 Negate", "X", "Standard", Ct));
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Workspace(test).PrepareAsync(Source, "X", "Standard", cancel.Token));
        Assert.Empty(await test.Service.GetDecksAsync(Ct)); Assert.Single(await Workspace(test).GetHistoryAsync(ct: Ct));
    }
    [Fact]
    public async Task TwoIndependentClientsCannotApplyOnePreparationTwice()
    {
        await using var test = new TestDatabase(); await Seed(test);
        var services = new ServiceCollection(); services.AddLogging(); services.AddDeckbuilder(test.Paths);
        await using var other = services.BuildServiceProvider(); var second = other.GetRequiredService<IDeckWorkspace>();
        var p = await Prepare(test);
        var attempts = await Task.WhenAll(Try(Workspace(test)), Try(second));
        Assert.Single(attempts, x => x); Assert.Single(await test.Service.GetDecksAsync(Ct));
        async Task<bool> Try(IDeckWorkspace workspace)
        {
            try { await workspace.ApplyAsync(p.Id, p.Version, Ct); return true; }
            catch (ArgumentException) { return false; }
        }
    }
    [Fact]
    public async Task DeletedCreatedDeckDoesNotRemoveReceiptOrPermitReapply()
    {
        await using var test = new TestDatabase(); await Seed(test);
        var p = await Prepare(test); var id = await Workspace(test).ApplyAsync(p.Id, p.Version, Ct);
        await test.Service.DeleteDeckAsync(id, Ct);
        Assert.Single(await Workspace(test).GetHistoryAsync(ct: Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => Workspace(test).ApplyAsync(p.Id, p.Version, Ct));
        Assert.Empty(await test.Service.GetDecksAsync(Ct));
    }
    [Fact]
    public async Task AnalysisReadsActualOwnershipAcrossReprintsAndRefreshesAfterCollectionChange()
    {
        await using var test = new TestDatabase(); await test.InitializeAsync();
        var oracle = Guid.NewGuid().ToString();
        var fixtures = new[] { CatalogFixtures.Card(oracle: oracle), CatalogFixtures.Card(set: "M19", collector: "314", oracle: oracle) };
        await test.Provider.GetRequiredService<ICatalogImporter>().ImportFileAsync(await CatalogFixtures.WriteAsync(test, fixtures, "json", Ct), ct: Ct);
        var catalog = test.Provider.GetRequiredService<CatalogService>();
        var printings = (await catalog.SearchAsync(new CatalogQuery(), Ct)).Items;
        var first = printings.Single(x => x.SetCode == "FDN"); var other = printings.Single(x => x.SetCode == "M19");
        await catalog.SetOwnedQuantityAsync(other.PrintingId, 3, Ct);
        var p = await Prepare(test, "Deck\n4 Llanowar Elves (FDN) 227\nSideboard\n1 Llanowar Elves (FDN) 227");
        var id = await Workspace(test).ApplyAsync(p.Id, p.Version, Ct);
        var a = await Workspace(test).AnalyzeAsync(id, Ct);
        Assert.Equal(2, a.Missing); Assert.Equal(5, a.ExactPrintingMissing); Assert.Equal(1, a.AverageManaValue);
        await catalog.SetOwnedQuantityAsync(first.PrintingId, 2, Ct);
        a = await Workspace(test).AnalyzeAsync(id, Ct); Assert.Equal(0, a.Missing); Assert.Equal(3, a.ExactPrintingMissing);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailureOrCancellationAfterSqlWritesRollsBackDeckRowsAndReceipt(bool cancel)
    {
        var interceptor = new AfterSave();
        await using var test = new TestDatabase(s => s.AddSingleton<IDbContextFactory<DeckbuilderDbContext>>(sp => new Factory(sp.GetRequiredService<AppPaths>(), interceptor)));
        await Seed(test); var p = await Prepare(test);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Ct); interceptor.Armed = true; interceptor.Cancel = cancel ? cts : null;
        if (cancel) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Workspace(test).ApplyAsync(p.Id, p.Version, cts.Token));
        else await Assert.ThrowsAsync<IOException>(() => Workspace(test).ApplyAsync(p.Id, p.Version, Ct));
        interceptor.Armed = false;
        Assert.Empty(await test.Service.GetDecksAsync(Ct));
        var unchanged = await Workspace(test).GetPreviewAsync(p.Id, ct: Ct);
        Assert.Equal(DeckImportState.Prepared, unchanged.State); Assert.Equal(p.Version, unchanged.Version);
        await Workspace(test).ApplyAsync(p.Id, p.Version, Ct); Assert.Single(await test.Service.GetDecksAsync(Ct));
    }
    [Fact]
    public async Task MigrationFromPhaseThreePreservesPreparedCollectionAndAllOldDeckZones()
    {
        await using var test = new TestDatabase(); Directory.CreateDirectory(test.Paths.DataDirectory);
        var factory = test.Provider.GetRequiredService<IDbContextFactory<DeckbuilderDbContext>>();
        var c = Guid.NewGuid(); var p = Guid.NewGuid(); var d = Guid.NewGuid();
        await using (var db = await factory.CreateDbContextAsync(Ct))
        {
            await db.GetService<IMigrator>().MigrateAsync("20261022065339_AddCollectionTransfer", Ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Cards (Id,Name,SearchName,ColorIdentity,ManaValue) VALUES ({c},{"Elves"},{"ELVES"},{"G"},{1d})", Ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Printings (Id,CardId,SetCode,CollectorNumber,Language,SetName,Rarity,IsInCatalog) VALUES ({p},{c},{"FDN"},{"227"},{"en"},{"Foundations"},{"common"},{false})", Ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO CollectionEntries (PrintingId,Quantity,Version) VALUES ({p},{4},{Guid.NewGuid()})", Ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Decks (Id,Name,Format,CreatedUtc) VALUES ({d},{"Old"},{"Standard"},{DateTime.UtcNow})", Ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO DeckCards (DeckId,PrintingId,Zone,Quantity) VALUES ({d},{p},{0},{3}),({d},{p},{1},{1})", Ct);
            db.CollectionImports.Add(new CollectionImportBatch("previous.json", new string('A', 64))); await db.SaveChangesAsync(Ct);
        }
        await test.InitializeAsync();
        var cards = await test.Service.GetDeckCardsAsync(d, Ct); Assert.Equal(2, cards.Count); Assert.Equal(4, cards.Sum(x => x.Quantity));
        Assert.Single(await test.Provider.GetRequiredService<ICollectionTransfer>().GetHistoryAsync(ct: Ct));
        await test.Service.SetDeckCardAsync(d, p, DeckZone.Commander, 1, Ct);
        Assert.Equal(3, (await test.Service.GetDeckCardsAsync(d, Ct)).Count);
        Assert.Equal(4, Assert.Single(await test.Service.GetCollectionAsync(Ct)).Quantity);
        Assert.Single(Directory.GetFiles(test.Paths.BackupsDirectory, "*.db"));
    }
    [Fact]
    public async Task PagedPreviewAccountsForUnresolvedRowsOnOtherPages()
    {
        await using var test = new TestDatabase(); await Seed(test);
        var p = await Prepare(test, string.Join('\n', Enumerable.Repeat("1 Negate", 50)) + "\n1 Unknown");
        Assert.Equal(50, p.Rows.Count); Assert.Equal(1, p.Unresolved); Assert.False(p.CanApply);
        Assert.Equal("Unknown", Assert.Single((await Workspace(test).GetPreviewAsync(p.Id, 2, Ct)).Rows).Name);
    }
    [Fact]
    public async Task InvalidManualPrintingDoesNotProduceCorruptExport()
    {
        await using var test = new TestDatabase(); await test.InitializeAsync();
        await test.Service.SaveCollectionAsync(new("Test", "BAD SET", "1", 1), Ct);
        var id = await test.Service.CreateDeckAsync("Test", "Standard", Ct);
        await test.Service.SetDeckCardAsync(id, Assert.Single(await test.Service.GetCollectionAsync(Ct)).PrintingId, DeckZone.Mainboard, 1, Ct);
        await Assert.ThrowsAsync<ArgumentException>(() => Workspace(test).ExportAsync(id, Ct));
    }
    private sealed class AfterSave : SaveChangesInterceptor
    {
        public bool Armed { get; set; }
        public CancellationTokenSource? Cancel { get; set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (Armed) { if (Cancel is not null) Cancel.Cancel(); else throw new IOException("Failure after actual SQL writes"); }
            return ValueTask.FromResult(result);
        }
    }
    [Fact]
    public async Task InterruptedTableRebuildRollsBackOldDeckCardsAndMigrationReceipt()
    {
        var interceptor = new RebuildFailure();
        await using var test = new TestDatabase(s => s.AddSingleton<IDbContextFactory<DeckbuilderDbContext>>(sp =>
            new MigrationFactory(sp.GetRequiredService<AppPaths>(), interceptor)));
        Directory.CreateDirectory(test.Paths.DataDirectory);
        var factory = test.Provider.GetRequiredService<IDbContextFactory<DeckbuilderDbContext>>();
        var card = Guid.NewGuid(); var printing = Guid.NewGuid(); var deck = Guid.NewGuid();
        await using (var db = await factory.CreateDbContextAsync(Ct))
        {
            await db.GetService<IMigrator>().MigrateAsync("20261022065339_AddCollectionTransfer", Ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Cards (Id,Name,SearchName,ColorIdentity,ManaValue) VALUES ({card},{"Elves"},{"ELVES"},{"G"},{1d})", Ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Printings (Id,CardId,SetCode,CollectorNumber,Language,SetName,Rarity,IsInCatalog) VALUES ({printing},{card},{"FDN"},{"227"},{"en"},{"Foundations"},{"common"},{false})", Ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Decks (Id,Name,Format,CreatedUtc) VALUES ({deck},{"Old deck"},{"Standard"},{DateTime.UtcNow})", Ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO DeckCards (DeckId,PrintingId,Zone,Quantity) VALUES ({deck},{printing},{0},{4})", Ct);
        }
        interceptor.Armed = true;
        await Assert.ThrowsAsync<IOException>(() => test.InitializeAsync());
        Assert.True(interceptor.HadTransaction);
        Assert.Equal(4, Assert.Single(await test.Service.GetDeckCardsAsync(deck, Ct)).Quantity);
        await using (var db = await factory.CreateDbContextAsync(Ct))
            Assert.Equal(3, (await db.Database.GetAppliedMigrationsAsync(Ct)).Count());
        interceptor.Armed = false;
        await test.InitializeAsync();
        Assert.Equal(4, Assert.Single(await test.Service.GetDeckCardsAsync(deck, Ct)).Quantity);
        await test.Service.SetDeckCardAsync(deck, printing, DeckZone.Commander, 1, Ct);
        Assert.Equal(2, (await test.Service.GetDeckCardsAsync(deck, Ct)).Count);
    }
    private sealed class RebuildFailure : DbCommandInterceptor
    {
        public bool Armed { get; set; }
        public bool HadTransaction { get; private set; }
        public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (Armed && command.CommandText.Contains("CREATE TABLE \"__phase4_DeckCards\"", StringComparison.Ordinal))
            {
                HadTransaction = command.Transaction is not null;
                throw new IOException("Failure after table copy/drop/rename, before migration commit.");
            }
            return ValueTask.FromResult(result);
        }
    }
    private sealed class MigrationFactory(AppPaths paths, DbCommandInterceptor interceptor) : IDbContextFactory<DeckbuilderDbContext>
    {
        public DeckbuilderDbContext CreateDbContext() => new(new DbContextOptionsBuilder<DeckbuilderDbContext>()
            .UseSqlite(new SqliteConnectionStringBuilder { DataSource = paths.DatabasePath, ForeignKeys = true }.ToString()).AddInterceptors(interceptor).Options);
    }
    private sealed class Factory(AppPaths paths, SaveChangesInterceptor interceptor) : IDbContextFactory<DeckbuilderDbContext>
    {
        public DeckbuilderDbContext CreateDbContext() => new(new DbContextOptionsBuilder<DeckbuilderDbContext>()
            .UseSqlite(new SqliteConnectionStringBuilder { DataSource = paths.DatabasePath, ForeignKeys = true }.ToString()).AddInterceptors(interceptor).Options);
    }
}
