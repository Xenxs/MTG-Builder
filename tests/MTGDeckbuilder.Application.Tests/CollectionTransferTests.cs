using System.Text;
using System.Text.Json;
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

public sealed class CollectionTransferTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    internal static ICollectionTransfer Transfer(TestDatabase test) => test.Provider.GetRequiredService<ICollectionTransfer>();
    internal static async Task<string> Source(TestDatabase test, params object[] cards)
    {
        var path = Path.Combine(test.Paths.DataDirectory, Guid.NewGuid().ToString("N") + ".json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new { schemaVersion = 1, cards }), Ct);
        return path;
    }
    internal static object Natural(int qty, string collector = "227", string? name = null) =>
        new { setCode = "fdn", collectorNumber = collector, language = "en", name, quantity = qty };
    internal static async Task Seed(TestDatabase test)
    {
        await test.InitializeAsync();
        await test.Service.SaveCollectionAsync(new CollectionInput("Llanowar Elves", "FDN", "227", 2), Ct);
        await test.Service.SaveCollectionAsync(new CollectionInput("Negate", "FDN", "710", 3), Ct);
    }
    private static async Task<CollectionPreview> Prepare(TestDatabase test, params object[] cards)
    {
        var id = await Transfer(test).PrepareAsync(await Source(test, cards), Ct);
        return await Transfer(test).GetPreviewAsync(id, ct: Ct);
    }

    [Fact]
    public async Task PreviewApplyRepeatReopenAndUndoPreserveDecksAndUnmentionedCounts()
    {
        await using var test = new TestDatabase(); await Seed(test);
        var first = (await test.Service.GetCollectionAsync(Ct)).Single(x => x.CollectorNumber == "227");
        var deck = await test.Service.CreateDeckAsync("Elves", "Standard", Ct);
        await test.Service.SetDeckCardAsync(deck, first.PrintingId, DeckZone.Mainboard, 4, Ct);
        await test.Service.SavePlayerNameAsync("Nissa", Ct);
        var preview = await Prepare(test, Natural(4));
        Assert.True(preview.CanApply); Assert.Equal(1, preview.Changes);
        Assert.Equal(2, (await test.Service.GetCollectionAsync(Ct)).Single(x => x.PrintingId == first.PrintingId).Quantity);
        File.Delete(Path.Combine(test.Paths.DataDirectory, preview.FileName));
        await test.ReopenAsync();
        await Transfer(test).ApplyAsync(preview.Id, preview.Version, Ct);
        await Assert.ThrowsAsync<ArgumentException>(() => Transfer(test).ApplyAsync(preview.Id, preview.Version, Ct));
        await test.ReopenAsync();
        Assert.Equal(4, (await test.Service.GetCollectionAsync(Ct)).Single(x => x.PrintingId == first.PrintingId).Quantity);
        Assert.Equal(3, (await test.Service.GetCollectionAsync(Ct)).Single(x => x.CollectorNumber == "710").Quantity);
        Assert.Equal(4, Assert.Single(await test.Service.GetDeckCardsAsync(deck, Ct)).Quantity);
        var repeated = await Prepare(test, Natural(4));
        Assert.Equal(0, repeated.Changes);
        await Transfer(test).ApplyAsync(repeated.Id, repeated.Version, Ct);
        await Transfer(test).UndoAsync(preview.Id, Ct);
        await test.ReopenAsync();
        Assert.Equal(2, (await test.Service.GetCollectionAsync(Ct)).Single(x => x.PrintingId == first.PrintingId).Quantity);
        Assert.Equal(CollectionImportState.Undone, (await Transfer(test).GetHistoryAsync(ct: Ct)).Single(x => x.Id == preview.Id).State);
        Assert.Equal("Nissa", await test.Service.GetPlayerNameAsync(Ct));
        Assert.Single(await test.Service.GetDeckCardsAsync(deck, Ct));
    }

    [Fact]
    public async Task ExplicitZeroIsAppliedAndRestoredWithoutTouchingOtherCards()
    {
        await using var test = new TestDatabase(); await Seed(test);
        var preview = await Prepare(test, Natural(0));
        await Transfer(test).ApplyAsync(preview.Id, preview.Version, Ct);
        Assert.Equal(0, (await test.Service.GetCollectionAsync(Ct)).Single(x => x.CollectorNumber == "227").Quantity);
        Assert.Equal(3, (await test.Service.GetCollectionAsync(Ct)).Single(x => x.CollectorNumber == "710").Quantity);
        await Transfer(test).UndoAsync(preview.Id, Ct);
        Assert.Equal(2, (await test.Service.GetCollectionAsync(Ct)).Single(x => x.CollectorNumber == "227").Quantity);
    }

    [Fact]
    public async Task UnknownRowsRequireAnExplicitTargetOrSkipAndDuplicateTargetsBlockApply()
    {
        await using var test = new TestDatabase(); await Seed(test);
        var preview = await Prepare(test, Natural(4), Natural(8, "unknown"), Natural(1, "999"));
        Assert.Equal(2, preview.Unresolved); Assert.False(preview.CanApply);
        await Assert.ThrowsAsync<ArgumentException>(() => Transfer(test).ApplyAsync(preview.Id, preview.Version, Ct));
        var target = Assert.Single(await Transfer(test).SearchTargetsAsync("Llanowar", Ct));
        Assert.True(target.Manual);
        await Transfer(test).ResolveAsync(preview.Id, preview.Version, 2, target.Id, false, Ct);
        var changed = await Transfer(test).GetPreviewAsync(preview.Id, ct: Ct);
        Assert.Equal(1, changed.Duplicates);
        await Assert.ThrowsAsync<ArgumentException>(() => Transfer(test).ResolveAsync(preview.Id, preview.Version, 3, null, true, Ct));
        await Transfer(test).ResolveAsync(changed.Id, changed.Version, 2, null, true, Ct);
        changed = await Transfer(test).GetPreviewAsync(changed.Id, ct: Ct);
        await Transfer(test).ResolveAsync(changed.Id, changed.Version, 3, null, true, Ct);
        changed = await Transfer(test).GetPreviewAsync(changed.Id, ct: Ct);
        Assert.True(changed.CanApply); Assert.Equal(2, changed.Skipped);
        await Transfer(test).ApplyAsync(changed.Id, changed.Version, Ct);
        Assert.Equal(4, (await test.Service.GetCollectionAsync(Ct)).Single(x => x.CollectorNumber == "227").Quantity);
    }

    [Fact]
    public async Task DuplicateSourceRowsAreNeverSummedEvenWithTheSameQuantity()
    {
        await using var test = new TestDatabase(); await Seed(test);
        var preview = await Prepare(test, Natural(4), Natural(4));
        Assert.False(preview.CanApply); Assert.Equal(1, preview.Duplicates);
        await Assert.ThrowsAsync<ArgumentException>(() => Transfer(test).ApplyAsync(preview.Id, preview.Version, Ct));
        Assert.Equal(2, (await test.Service.GetCollectionAsync(Ct)).Single(x => x.CollectorNumber == "227").Quantity);
    }

    [Fact]
    public async Task LaterEditsBlockWholeApplyAndWholeUndoEvenIfCountWasChangedBack()
    {
        await using var test = new TestDatabase(); await Seed(test);
        var elves = (await test.Service.GetCollectionAsync(Ct)).Single(x => x.CollectorNumber == "227").PrintingId;
        var preview = await Prepare(test, Natural(4), Natural(6, "710"));
        await test.Service.SetCollectionQuantityAsync(elves, 3, Ct);
        await test.Service.SetCollectionQuantityAsync(elves, 2, Ct);
        await Assert.ThrowsAsync<ArgumentException>(() => Transfer(test).ApplyAsync(preview.Id, preview.Version, Ct));
        Assert.Equal(3, (await test.Service.GetCollectionAsync(Ct)).Single(x => x.CollectorNumber == "710").Quantity);
        preview = await Prepare(test, Natural(4), Natural(6, "710"));
        await Transfer(test).ApplyAsync(preview.Id, preview.Version, Ct);
        await test.Service.SetCollectionQuantityAsync(elves, 1, Ct);
        await test.Service.SetCollectionQuantityAsync(elves, 4, Ct);
        await Assert.ThrowsAsync<ArgumentException>(() => Transfer(test).UndoAsync(preview.Id, Ct));
        Assert.Equal(6, (await test.Service.GetCollectionAsync(Ct)).Single(x => x.CollectorNumber == "710").Quantity);
        Assert.Equal(CollectionImportState.Applied, (await Transfer(test).GetPreviewAsync(preview.Id, ct: Ct)).State);
    }

    [Fact]
    public async Task TwoPreviewsForOneCountCannotBothOverwriteEachOther()
    {
        await using var test = new TestDatabase(); await Seed(test);
        var services = new ServiceCollection(); services.AddLogging(); services.AddDeckbuilder(test.Paths);
        await using var other = services.BuildServiceProvider();
        var otherTransfer = other.GetRequiredService<ICollectionTransfer>();
        var first = await Prepare(test, Natural(4)); var second = await Prepare(test, Natural(7));
        var results = await Task.WhenAll(Attempt(Transfer(test), first), Attempt(otherTransfer, second));
        Assert.Single(results, x => x); Assert.Single(results, x => !x);
        var count = (await test.Service.GetCollectionAsync(Ct)).Single(x => x.CollectorNumber == "227").Quantity;
        Assert.Contains(count, new[] { 4, 7 });
        async Task<bool> Attempt(ICollectionTransfer transfer, CollectionPreview p)
        {
            try { await transfer.ApplyAsync(p.Id, p.Version, Ct); return true; }
            catch (ArgumentException) { return false; }
        }
    }

    [Fact]
    public async Task ScryfallIdentityWorksButSharedArenaIdAndNameMismatchNeedResolution()
    {
        await using var test = new TestDatabase(); await test.InitializeAsync();
        var one = CatalogFixtures.Card(); var two = CatalogFixtures.Card(collector: "228");
        var path = await CatalogFixtures.WriteAsync(test, [one, two], "json", Ct);
        await test.Provider.GetRequiredService<ICatalogImporter>().ImportFileAsync(path, ct: Ct);
        var preview = await Prepare(test, new { scryfallId = one["id"], quantity = 4 }, new { arenaId = 93940, quantity = 2 },
            new { scryfallId = two["id"], name = "Wrong card", quantity = 1 });
        Assert.Equal(2, preview.Unresolved); Assert.NotNull(preview.Rows[0].PrintingId);
        var candidates = await Transfer(test).GetCandidatesAsync(preview.Id, 2, Ct);
        Assert.Equal(2, candidates.Count);
        var nameMismatch = Assert.Single(await Transfer(test).GetCandidatesAsync(preview.Id, 3, Ct));
        Assert.Equal("Llanowar Elves", nameMismatch.Name);
    }

    [Fact]
    public async Task NaturalIdentityRespectsLanguageAndDoesNotPickFirstVariant()
    {
        await using var test = new TestDatabase(); await test.InitializeAsync();
        var cards = new[] { CatalogFixtures.Card(), CatalogFixtures.Card(), CatalogFixtures.Card(language: "fr") };
        await test.Provider.GetRequiredService<ICatalogImporter>().ImportFileAsync(await CatalogFixtures.WriteAsync(test, cards, "json", Ct), ct: Ct);
        var preview = await Prepare(test, Natural(4), new { setCode = "FDN", collectorNumber = "227", language = "fr", quantity = 2 });
        Assert.Equal(1, preview.Unresolved);
        Assert.Equal(2, (await Transfer(test).GetCandidatesAsync(preview.Id, 1, Ct)).Count);
        Assert.NotNull(preview.Rows[1].PrintingId);
    }

    [Fact]
    public async Task NewOwnedRowsAreRemovedOnUndoAndExportCanBeReimported()
    {
        await using var test = new TestDatabase(); await test.InitializeAsync();
        var card = CatalogFixtures.Card();
        await test.Provider.GetRequiredService<ICatalogImporter>().ImportFileAsync(await CatalogFixtures.WriteAsync(test, [card], "json", Ct), ct: Ct);
        var preview = await Prepare(test, new { scryfallId = card["id"], quantity = 4 });
        await Transfer(test).ApplyAsync(preview.Id, preview.Version, Ct);
        var path = Path.Combine(test.Paths.DataDirectory, "export.json");
        await Transfer(test).ExportAsync(path, Ct);
        var repeat = await Transfer(test).GetPreviewAsync(await Transfer(test).PrepareAsync(path, Ct), ct: Ct);
        Assert.True(repeat.CanApply); Assert.Equal(0, repeat.Changes);
        await Transfer(test).UndoAsync(preview.Id, Ct);
        Assert.Empty(await test.Service.GetCollectionAsync(Ct));
        // Old export preview has stale ownership; fresh preparation restores exactly four, not eight.
        await Assert.ThrowsAsync<ArgumentException>(() => Transfer(test).ApplyAsync(repeat.Id, repeat.Version, Ct));
        repeat = await Transfer(test).GetPreviewAsync(await Transfer(test).PrepareAsync(path, Ct), ct: Ct);
        await Transfer(test).ApplyAsync(repeat.Id, repeat.Version, Ct);
        Assert.Equal(4, Assert.Single(await test.Service.GetCollectionAsync(Ct)).Quantity);
    }

    [Fact]
    public async Task ManualExportRoundTripsAndUtf8BomIsAccepted()
    {
        await using var test = new TestDatabase(); await Seed(test);
        var path = Path.Combine(test.Paths.DataDirectory, "export.json");
        await Transfer(test).ExportAsync(path, Ct);
        var text = await File.ReadAllTextAsync(path, Ct);
        await File.WriteAllTextAsync(path, text, new UTF8Encoding(true), Ct);
        var p = await Transfer(test).GetPreviewAsync(await Transfer(test).PrepareAsync(path, Ct), ct: Ct);
        Assert.Equal(2, p.Total); Assert.Equal(0, p.Changes); Assert.True(p.CanApply);
        Assert.All(p.Rows, x => Assert.NotNull(x.PrintingId));
    }

    [Fact]
    public async Task DiscardAndCancellationKeepCollectionAndAllowNewPreparation()
    {
        await using var test = new TestDatabase(); await Seed(test);
        var preview = await Prepare(test, Natural(4));
        await Transfer(test).DiscardAsync(preview.Id, preview.Version, Ct);
        await Assert.ThrowsAsync<ArgumentException>(() => Transfer(test).ApplyAsync(preview.Id, preview.Version, Ct));
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Transfer(test).PrepareAsync(awaitPath(), cancel.Token));
        string awaitPath() => Path.Combine(test.Paths.DataDirectory, "irrelevant.json");
        Assert.Equal(2, (await test.Service.GetCollectionAsync(Ct)).Single(x => x.CollectorNumber == "227").Quantity);
        Assert.True((await Prepare(test, Natural(4))).CanApply);
    }

    [Theory]
    [InlineData("{\"schemaVersion\":2,\"cards\":[]}")]
    [InlineData("{\"schemaVersion\":1,\"cards\":[]}")]
    [InlineData("{\"schemaVersion\":1,\"cards\":[{\"arenaId\":1,\"quantity\":-1}]}")]
    [InlineData("{\"schemaVersion\":1,\"cards\":[{\"arenaId\":1,\"quantity\":10000}]}")]
    [InlineData("{\"schemaVersion\":1,\"cards\":[{\"arenaId\":1,\"quantity\":1.5}]}")]
    [InlineData("{\"schemaVersion\":1,\"cards\":[{\"arenaId\":1}]}")]
    [InlineData("{\"schemaVersion\":1,\"cards\":[{\"arenaId\":1,\"quantity\":1,\"quantity\":2}]}")]
    [InlineData("{\"schemaVersion\":1,\"cards\":[{\"arenaId\":1,\"quantity\":1,\"count\":2}]}")]
    [InlineData("{\"schemaVersion\":1,\"cards\":[{\"setCode\":\"FDN\",\"quantity\":1}]}")]
    [InlineData("{\"schemaVersion\":1,\"cards\":[{\"scryfallId\":\"bad\",\"quantity\":1}]}")]
    [InlineData("{\"schemaVersion\":1,\"cards\":[{\"arenaId\":1,\"setCode\":\"FDN\",\"collectorNumber\":\"1\",\"language\":\"en\",\"quantity\":1}]}")]
    [InlineData("{\"schemaVersion\":1,\"cards\":[{\"arenaId\":\"1\",\"quantity\":1}]}")]
    [InlineData("{\"schemaVersion\":1,\"cards\":[")]
    [InlineData("[]")]
    public async Task InvalidSourcesCreateNoDraftOrCollectionChanges(string json)
    {
        await using var test = new TestDatabase(); await Seed(test);
        var path = Path.Combine(test.Paths.DataDirectory, "bad.json");
        await File.WriteAllTextAsync(path, json, Ct);
        await Assert.ThrowsAsync<ArgumentException>(() => Transfer(test).PrepareAsync(path, Ct));
        Assert.Empty(await Transfer(test).GetHistoryAsync(ct: Ct));
        Assert.Equal(5, (await test.Service.GetSummaryAsync(Ct)).TotalCards);
    }

    [Fact]
    public async Task OversizedInputAndExcessRowsAreRejected()
    {
        await using var test = new TestDatabase(); await test.InitializeAsync();
        var path = Path.Combine(test.Paths.DataDirectory, "large.json");
        await using (var file = File.Create(path)) file.SetLength(16 * 1024 * 1024 + 1);
        await Assert.ThrowsAsync<ArgumentException>(() => Transfer(test).PrepareAsync(path, Ct));
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new { schemaVersion = 1, cards = Enumerable.Repeat(new { arenaId = 1, quantity = 1 }, 50001) }), Ct);
        await Assert.ThrowsAsync<ArgumentException>(() => Transfer(test).PrepareAsync(path, Ct));
        Assert.Empty(await Transfer(test).GetHistoryAsync(ct: Ct));
    }

    [Fact]
    public async Task PreviewAndHistoryArePagedAndUnresolvedRowsOnOtherPagesBlockApply()
    {
        await using var test = new TestDatabase(); await Seed(test);
        var cards = Enumerable.Range(0, 51).Select(x => Natural(1, (1000 + x).ToString())).ToArray();
        var p = await Prepare(test, cards);
        Assert.Equal(51, p.Total); Assert.Equal(51, p.Unresolved); Assert.Equal(50, p.Rows.Count);
        var page2 = await Transfer(test).GetPreviewAsync(p.Id, 2, Ct);
        Assert.Equal(51, Assert.Single(page2.Rows).Number); Assert.False(page2.CanApply);
        // Seed history directly; paging tests SQL ordering rather than slow parser repetition.
        var factory = test.Provider.GetRequiredService<IDbContextFactory<DeckbuilderDbContext>>();
        await using (var db = await factory.CreateDbContextAsync(Ct))
        {
            for (int i = 0; i < 50; i++) db.CollectionImports.Add(new CollectionImportBatch($"{i}.json", new string('A', 64)));
            await db.SaveChangesAsync(Ct);
        }
        Assert.Equal(50, (await Transfer(test).GetHistoryAsync(ct: Ct)).Count);
        Assert.Single(await Transfer(test).GetHistoryAsync(2, Ct));
    }

    [Fact]
    public async Task FailureAfterDatabaseWritesRollsBackApplyAndHistoryTogether()
    {
        var interceptor = new FailureAfterSave();
        await using var test = new TestDatabase(s => s.AddSingleton<IDbContextFactory<DeckbuilderDbContext>>(sp =>
            new InterceptedFactory(sp.GetRequiredService<AppPaths>(), interceptor)));
        await Seed(test);
        var p = await Prepare(test, Natural(4), Natural(6, "710"));
        interceptor.Fail = true;
        await Assert.ThrowsAsync<IOException>(() => Transfer(test).ApplyAsync(p.Id, p.Version, Ct));
        interceptor.Fail = false;
        Assert.Equal(5, (await test.Service.GetSummaryAsync(Ct)).TotalCards);
        var unchanged = await Transfer(test).GetPreviewAsync(p.Id, ct: Ct);
        Assert.Equal(CollectionImportState.Prepared, unchanged.State); Assert.Equal(p.Version, unchanged.Version);
        await Transfer(test).ApplyAsync(p.Id, p.Version, Ct);
        Assert.Equal(10, (await test.Service.GetSummaryAsync(Ct)).TotalCards);
        interceptor.Fail = true;
        await Assert.ThrowsAsync<IOException>(() => Transfer(test).UndoAsync(p.Id, Ct));
        interceptor.Fail = false;
        Assert.Equal(10, (await test.Service.GetSummaryAsync(Ct)).TotalCards);
        Assert.Equal(CollectionImportState.Applied, (await Transfer(test).GetPreviewAsync(p.Id, ct: Ct)).State);
        await Transfer(test).UndoAsync(p.Id, Ct);
        Assert.Equal(5, (await test.Service.GetSummaryAsync(Ct)).TotalCards);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationAfterRealWritesRollsBackBeforeCommit(bool undo)
    {
        var interceptor = new FailureAfterSave();
        await using var test = new TestDatabase(s => s.AddSingleton<IDbContextFactory<DeckbuilderDbContext>>(sp =>
            new InterceptedFactory(sp.GetRequiredService<AppPaths>(), interceptor)));
        await Seed(test);
        var p = await Prepare(test, Natural(4), Natural(6, "710"));
        if (undo) await Transfer(test).ApplyAsync(p.Id, p.Version, Ct);
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        interceptor.CancelAfterSave = cancel;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => undo ? Transfer(test).UndoAsync(p.Id, cancel.Token) : Transfer(test).ApplyAsync(p.Id, p.Version, cancel.Token));
        interceptor.CancelAfterSave = null;
        Assert.Equal(undo ? 10 : 5, (await test.Service.GetSummaryAsync(Ct)).TotalCards);
        Assert.Equal(undo ? CollectionImportState.Applied : CollectionImportState.Prepared, (await Transfer(test).GetPreviewAsync(p.Id, ct: Ct)).State);
    }

    [Fact]
    public async Task ChangedCatalogIdentityBlocksOldPreviewAndUnicodeTargetSearchWorks()
    {
        await using var test = new TestDatabase(); await test.InitializeAsync();
        var card = CatalogFixtures.Card("Æther Elf");
        var importer = test.Provider.GetRequiredService<ICatalogImporter>();
        await importer.ImportFileAsync(await CatalogFixtures.WriteAsync(test, [card], "json", Ct), ct: Ct);
        var p = await Prepare(test, new { scryfallId = card["id"], quantity = 4 });
        Assert.Equal("Æther Elf", Assert.Single(await Transfer(test).SearchTargetsAsync("æther", Ct)).Name);
        card["name"] = "New name";
        await importer.ImportFileAsync(await CatalogFixtures.WriteAsync(test, [card], "json", Ct), ct: Ct);
        await Assert.ThrowsAsync<ArgumentException>(() => Transfer(test).ApplyAsync(p.Id, p.Version, Ct));
        Assert.Empty(await test.Service.GetCollectionAsync(Ct));
    }

    [Fact]
    public async Task ExportRejectsEmptyCollectionAndNonJsonTargetsWithoutOverwritingFiles()
    {
        await using var test = new TestDatabase(); await test.InitializeAsync();
        var path = Path.Combine(test.Paths.DataDirectory, "export.json");
        await File.WriteAllTextAsync(path, "existing", Ct);
        await Assert.ThrowsAsync<ArgumentException>(() => Transfer(test).ExportAsync(path, Ct));
        Assert.Equal("existing", await File.ReadAllTextAsync(path, Ct));
        await test.Service.SaveCollectionAsync(new CollectionInput("Elves", "FDN", "227", 4), Ct);
        await Assert.ThrowsAsync<ArgumentException>(() => Transfer(test).ExportAsync(test.Paths.DatabasePath, Ct));
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Transfer(test).ExportAsync(path, cancel.Token));
        Assert.Equal("existing", await File.ReadAllTextAsync(path, Ct));
    }

    [Fact]
    public async Task UpgradeFromPhaseTwoPreservesCountsAndDeckReferencesAndMakesBackup()
    {
        await using var test = new TestDatabase();
        var factory = test.Provider.GetRequiredService<IDbContextFactory<DeckbuilderDbContext>>();
        Directory.CreateDirectory(test.Paths.DataDirectory);
        var card = Guid.NewGuid(); var printing = Guid.NewGuid(); var deck = Guid.NewGuid();
        await using (var db = await factory.CreateDbContextAsync(Ct))
        {
            await db.GetService<IMigrator>().MigrateAsync("20261021193331_AddCatalogAndImportHistory", Ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Cards (Id,Name,SearchName,ColorIdentity,ManaValue) VALUES ({card},{"Elves"},{"ELVES"},{"G"},{1.0})", Ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Printings (Id,CardId,SetCode,CollectorNumber,Language,SetName,Rarity,IsInCatalog) VALUES ({printing},{card},{"FDN"},{"227"},{"en"},{"Foundations"},{"common"},{false})", Ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO CollectionEntries (PrintingId,Quantity) VALUES ({printing},{4})", Ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Decks (Id,Name,Format,CreatedUtc) VALUES ({deck},{"Elves"},{"Standard"},{DateTime.UtcNow})", Ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO DeckCards (DeckId,PrintingId,Zone,Quantity) VALUES ({deck},{printing},{0},{4})", Ct);
        }
        await test.InitializeAsync();
        Assert.Equal(4, Assert.Single(await test.Service.GetCollectionAsync(Ct)).Quantity);
        Assert.Equal(printing, Assert.Single(await test.Service.GetDeckCardsAsync(deck, Ct)).PrintingId);
        var backup = Assert.Single(Directory.GetFiles(test.Paths.BackupsDirectory, "*.db"));
        using (var backupFile = new FileStream(backup, FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.True(backupFile.Length > 0); // The backup destination must not remain in a connection pool.
        using var connection = new SqliteConnection($"Data Source={backup}"); await connection.OpenAsync(Ct);
        using var command = connection.CreateCommand(); command.CommandText = "SELECT Quantity FROM CollectionEntries";
        Assert.Equal(4L, await command.ExecuteScalarAsync(Ct));
        var p = await Prepare(test, Natural(2));
        await Transfer(test).ApplyAsync(p.Id, p.Version, Ct);
        await Transfer(test).UndoAsync(p.Id, Ct);
        Assert.Equal(4, Assert.Single(await test.Service.GetCollectionAsync(Ct)).Quantity);
    }

    private sealed class FailureAfterSave : SaveChangesInterceptor
    {
        public bool Fail { get; set; }
        public CancellationTokenSource? CancelAfterSave { get; set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (Fail) throw new IOException("Simulated failure after real database writes, before transaction commit.");
            CancelAfterSave?.Cancel();
            return ValueTask.FromResult(result);
        }
    }
    private sealed class InterceptedFactory(AppPaths paths, SaveChangesInterceptor interceptor) : IDbContextFactory<DeckbuilderDbContext>
    {
        public DeckbuilderDbContext CreateDbContext() => new(new DbContextOptionsBuilder<DeckbuilderDbContext>()
            .UseSqlite(new SqliteConnectionStringBuilder { DataSource = paths.DatabasePath, ForeignKeys = true }.ToString())
            .AddInterceptors(interceptor).Options);
    }
}
