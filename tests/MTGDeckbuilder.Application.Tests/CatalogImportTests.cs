using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MTGDeckbuilder.Application;
using MTGDeckbuilder.Domain;
using MTGDeckbuilder.Infrastructure;
using Xunit;

namespace MTGDeckbuilder.Application.Tests;

public sealed class CatalogImportTests
{
    [Theory]
    [InlineData("json")]
    [InlineData("jsonl")]
    [InlineData("jsonl.gz")]
    public async Task ImportsSupportedFormatsWithoutGivingTheUserCards(string format)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = new TestDatabase();
        await test.InitializeAsync();
        var path = await CatalogFixtures.WriteAsync(test, [CatalogFixtures.Card(), CatalogFixtures.Card("Paper card", collector: "228", arena: false)], format, ct);
        var result = await Importer(test).ImportFileAsync(path, ct: ct);
        Assert.Equal(2, result.Processed);
        Assert.Equal(1, result.Added);
        var item = Assert.Single((await Catalog(test).SearchAsync(new CatalogQuery(), ct)).Items);
        Assert.Equal("FDN", item.SetCode);
        Assert.Equal("227", item.CollectorNumber);
        Assert.Equal(93940, item.ArenaId);
        Assert.Equal(0, item.OwnedQuantity);
        Assert.Empty(await test.Service.GetCollectionAsync(ct));
        var run = Assert.Single(await Catalog(test).GetHistoryAsync(ct));
        Assert.Equal(CatalogImportState.Completed, run.State);
        Assert.StartsWith("sha256:", run.SourceVersion);
    }

    [Fact]
    public async Task ReimportAndRestartPreservePrintingIdCollectionAndDecks()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = new TestDatabase();
        await test.InitializeAsync();
        var card = CatalogFixtures.Card();
        var path = await CatalogFixtures.WriteAsync(test, [card], "jsonl.gz", ct);
        await Importer(test).ImportFileAsync(path, ct: ct);
        var first = Assert.Single((await Catalog(test).SearchAsync(new CatalogQuery(), ct)).Items);
        await Catalog(test).SetOwnedQuantityAsync(first.PrintingId, 4, ct);
        var deck = await test.Service.CreateDeckAsync("Test", "Standard", ct);
        await test.Service.SetDeckCardAsync(deck, first.PrintingId, DeckZone.Mainboard, 4, ct);
        await test.Service.SavePlayerNameAsync("Nissa", ct);
        var repeat = await Importer(test).ImportFileAsync(path, ct: ct);
        Assert.Equal(0, repeat.Added);
        Assert.Equal(0, repeat.Updated);
        await test.ReopenAsync();
        var reopened = Assert.Single((await Catalog(test).SearchAsync(new CatalogQuery(), ct)).Items);
        Assert.Equal(first.PrintingId, reopened.PrintingId);
        Assert.Equal(4, reopened.OwnedQuantity);
        Assert.Equal(first.PrintingId, Assert.Single(await test.Service.GetDeckCardsAsync(deck, ct)).PrintingId);
        Assert.Equal("Nissa", await test.Service.GetPlayerNameAsync(ct));
        await using var db = await Context(test, ct);
        Assert.Equal(1, await db.Cards.CountAsync(ct));
        Assert.Equal(1, await db.Printings.CountAsync(ct));
        Assert.Equal(0, await db.CatalogStaging.CountAsync(ct));
    }

    [Fact]
    public async Task PromotesMatchingManualPrintingWithoutChangingItsKeysOrCounts()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = new TestDatabase();
        await test.InitializeAsync();
        await test.Service.SaveCollectionAsync(new("Llanowar Elves", "FDN", "227", 4), ct);
        var manual = Assert.Single(await test.Service.GetCollectionAsync(ct));
        var deck = await test.Service.CreateDeckAsync("Elves", "Standard", ct);
        await test.Service.SetDeckCardAsync(deck, manual.PrintingId, DeckZone.Mainboard, 3, ct);
        var path = await CatalogFixtures.WriteAsync(test, [CatalogFixtures.Card()], "json", ct);
        await Importer(test).ImportFileAsync(path, ct: ct);
        var imported = Assert.Single((await Catalog(test).SearchAsync(new CatalogQuery(), ct)).Items);
        Assert.Equal(manual.PrintingId, imported.PrintingId);
        Assert.Equal(4, imported.OwnedQuantity);
        Assert.Equal(3, Assert.Single(await test.Service.GetDeckCardsAsync(deck, ct)).Quantity);
        await using var db = await Context(test, ct);
        Assert.Single(await db.Cards.ToListAsync(ct));
    }

    [Fact]
    public async Task UncertainManualMatchIsKeptSeparateAndReported()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = new TestDatabase();
        await test.InitializeAsync();
        await test.Service.SaveCollectionAsync(new("Wrong handwritten name", "FDN", "227", 3), ct);
        var original = Assert.Single(await test.Service.GetCollectionAsync(ct));
        var path = await CatalogFixtures.WriteAsync(test, [CatalogFixtures.Card()], "jsonl", ct);
        var result = await Importer(test).ImportFileAsync(path, ct: ct);
        Assert.Equal(1, result.UnlinkedManualPrintings);
        Assert.Equal(original, Assert.Single(await test.Service.GetCollectionAsync(ct)));
        var item = Assert.Single((await Catalog(test).SearchAsync(new CatalogQuery(), ct)).Items);
        Assert.NotEqual(original.PrintingId, item.PrintingId);
        Assert.Equal(0, item.OwnedQuantity);
        Assert.Equal(1, (await Importer(test).ImportFileAsync(path, ct: ct)).UnlinkedManualPrintings);
    }

    [Fact]
    public async Task OracleIdentityGroupsReprintsAndSourceIdsDistinguishVariantsAndLanguages()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = new TestDatabase();
        await test.InitializeAsync();
        var oracle = Guid.NewGuid().ToString();
        var path = await CatalogFixtures.WriteAsync(test,
            [CatalogFixtures.Card(oracle: oracle), CatalogFixtures.Card(oracle: oracle),
             CatalogFixtures.Card(oracle: oracle, language: "ja"), CatalogFixtures.Card(set: "dar", collector: "168", oracle: oracle)], "json", ct);
        await Importer(test).ImportFileAsync(path, ct: ct);
        var items = (await Catalog(test).SearchAsync(new CatalogQuery(), ct)).Items;
        Assert.Equal(4, items.Count);
        Assert.Equal(4, items.Select(x => x.PrintingId).Distinct().Count());
        Assert.Contains(items, x => x.Language == "ja");
        await Assert.ThrowsAsync<ArgumentException>(() => test.Service.SaveCollectionAsync(new("Llanowar Elves", "FDN", "227", 4), ct));
        await using var db = await Context(test, ct);
        Assert.Equal(1, await db.Cards.CountAsync(ct));
        Assert.Equal(4, await db.Printings.CountAsync(ct));
    }

    [Theory]
    [InlineData("Valideren")]
    [InlineData("Toepassen")]
    public async Task CancellationIsAtomicAndCanResumeAfterRestart(string stage)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = new TestDatabase();
        await test.InitializeAsync();
        var originalPath = await CatalogFixtures.WriteAsync(test, [CatalogFixtures.Card("Kept", collector: "999")], "json", ct);
        await Importer(test).ImportFileAsync(originalPath, ct: ct);
        var original = Assert.Single((await Catalog(test).SearchAsync(new CatalogQuery(), ct)).Items);
        await Catalog(test).SetOwnedQuantityAsync(original.PrintingId, 2, ct);
        var path = await CatalogFixtures.WriteAsync(test, Enumerable.Range(0, 600)
            .Select(i => CatalogFixtures.Card($"Test {i}", collector: i.ToString())), "jsonl.gz", ct);
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var progress = new InlineProgress(p => { if (p.Stage == stage && p.Processed >= 500) cancel.Cancel(); });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Importer(test).ImportFileAsync(path, progress, cancel.Token));
        var run = (await Catalog(test).GetHistoryAsync(ct)).First();
        Assert.Equal(CatalogImportState.Cancelled, run.State);
        Assert.True(run.CanResume);
        Assert.Equal(stage == "Valideren" ? 500 : 600, run.ProcessedRecords);
        Assert.Equal(original.PrintingId, Assert.Single((await Catalog(test).SearchAsync(new CatalogQuery(), ct)).Items).PrintingId);
        await test.ReopenAsync();
        var result = await Importer(test).ResumeAsync(run.Id, ct: ct);
        Assert.Equal(600, result.Added);
        Assert.Equal(600, (await Catalog(test).GetStatusAsync(ct)).Printings);
        Assert.Equal(2, Assert.Single(await test.Service.GetCollectionAsync(ct)).Quantity);
        await using var db = await Context(test, ct);
        Assert.Equal(0, await db.CatalogStaging.CountAsync(ct));
    }

    [Fact]
    public async Task InvalidOrTruncatedSourceDoesNotReplaceCatalogOrLoseLastCommittedCheckpoint()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = new TestDatabase();
        await test.InitializeAsync();
        var baseline = await CatalogFixtures.WriteAsync(test, [CatalogFixtures.Card()], "json", ct);
        await Importer(test).ImportFileAsync(baseline, ct: ct);
        var before = await Catalog(test).GetStatusAsync(ct);
        var invalid = await CatalogFixtures.WriteAsync(test, Enumerable.Range(0, 550).Select(i => CatalogFixtures.Card($"Test {i}", collector: i.ToString())), "jsonl", ct);
        await File.AppendAllTextAsync(invalid, "\n{broken", ct);
        await Assert.ThrowsAnyAsync<System.Text.Json.JsonException>(() => Importer(test).ImportFileAsync(invalid, ct: ct));
        Assert.Equal(before, await Catalog(test).GetStatusAsync(ct));
        var run = (await Catalog(test).GetHistoryAsync(ct)).First();
        Assert.Equal(CatalogImportState.Failed, run.State);
        Assert.Equal(500, run.ProcessedRecords);
        await using var db = await Context(test, ct);
        Assert.Equal(500, await db.CatalogStaging.CountAsync(x => x.RunId == run.Id, ct));
    }

    [Fact]
    public async Task CorruptResumeCacheIsRejectedAndInterruptedRunIsRecovered()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = new TestDatabase();
        await test.InitializeAsync();
        var path = await CatalogFixtures.WriteAsync(test, Enumerable.Range(0, 501).Select(i => CatalogFixtures.Card($"Test {i}", collector: i.ToString())), "json", ct);
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(ct);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Importer(test).ImportFileAsync(path,
            new InlineProgress(p => { if (p.Stage == "Valideren") cancel.Cancel(); }), cancel.Token));
        var run = (await Catalog(test).GetHistoryAsync(ct)).First();
        await using (var db = await Context(test, ct))
        {
            (await db.CatalogImports.FindAsync([run.Id], ct))!.SetState(CatalogImportState.Processing);
            await db.SaveChangesAsync(ct);
        }
        await test.ReopenAsync();
        Assert.Equal(CatalogImportState.Interrupted, (await Catalog(test).GetHistoryAsync(ct)).First().State);
        await File.AppendAllTextAsync(test.Paths.CatalogCachePath(run.Id), "tampered", ct);
        await Assert.ThrowsAsync<InvalidDataException>(() => Importer(test).ResumeAsync(run.Id, ct: ct));
        Assert.Equal(0, (await Catalog(test).GetStatusAsync(ct)).Printings);
    }

    [Fact]
    public async Task WithdrawnPrintingLeavesCollectionAndDeckIntactAndDoubleFacesAreReadable()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = new TestDatabase();
        await test.InitializeAsync();
        var ordinary = CatalogFixtures.Card();
        var doubleFace = CatalogFixtures.Card("Front // Back", collector: "2");
        doubleFace.Remove("mana_cost"); doubleFace.Remove("oracle_text");
        doubleFace["card_faces"] = new[]
        {
            new { mana_cost = "{1}{G}", oracle_text = "Front text" }, new { mana_cost = "", oracle_text = "Back text" }
        };
        var path = await CatalogFixtures.WriteAsync(test, [ordinary, doubleFace], "json", ct);
        await Importer(test).ImportFileAsync(path, ct: ct);
        var before = (await Catalog(test).SearchAsync(new CatalogQuery(), ct)).Items;
        var owned = before.Single(x => x.Name == "Llanowar Elves");
        await Catalog(test).SetOwnedQuantityAsync(owned.PrintingId, 4, ct);
        var deck = await test.Service.CreateDeckAsync("Keep", "Standard", ct);
        await test.Service.SetDeckCardAsync(deck, owned.PrintingId, DeckZone.Mainboard, 4, ct);
        var replacement = await CatalogFixtures.WriteAsync(test, [doubleFace], "jsonl", ct);
        await Importer(test).ImportFileAsync(replacement, ct: ct);
        var remaining = Assert.Single((await Catalog(test).SearchAsync(new CatalogQuery(), ct)).Items);
        Assert.Contains("Front text // Back text", remaining.OracleText);
        Assert.Equal(4, Assert.Single(await test.Service.GetCollectionAsync(ct)).Quantity);
        Assert.Equal(owned.PrintingId, Assert.Single(await test.Service.GetDeckCardsAsync(deck, ct)).PrintingId);
    }

    [Fact]
    public async Task EmptyArenaSelectionAndConcurrentImportsAreRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = new TestDatabase();
        await test.InitializeAsync();
        var path = await CatalogFixtures.WriteAsync(test, [CatalogFixtures.Card(arena: false)], "json", ct);
        await Assert.ThrowsAsync<InvalidDataException>(() => Importer(test).ImportFileAsync(path, ct: ct));
        using var heldLock = new FileStream(test.Paths.ImportLockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        await Assert.ThrowsAsync<ArgumentException>(() => Importer(test).ImportFileAsync(path, ct: ct));
        Assert.Equal(0, (await Catalog(test).GetStatusAsync(ct)).Printings);
    }

    [Fact]
    public async Task RefreshesCanonicalMetadataAcrossReprintsWithoutReplacingUserAmounts()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = new TestDatabase();
        await test.InitializeAsync();
        var oracle = Guid.NewGuid().ToString();
        var first = CatalogFixtures.Card(oracle: oracle);
        var second = CatalogFixtures.Card(oracle: oracle, collector: "228");
        var path = await CatalogFixtures.WriteAsync(test, [first, second], "json", ct);
        await Importer(test).ImportFileAsync(path, ct: ct);
        var original = (await Catalog(test).SearchAsync(new(), ct)).Items;
        await Catalog(test).SetOwnedQuantityAsync(original[0].PrintingId, 4, ct);
        first["oracle_text"] = second["oracle_text"] = "Updated official text";
        var replacement = await CatalogFixtures.WriteAsync(test, [first, second], "jsonl", ct);
        var refreshed = await Importer(test).ImportFileAsync(replacement, ct: ct);
        Assert.Equal(0, refreshed.Added);
        Assert.Equal(2, refreshed.Updated);
        Assert.All((await Catalog(test).SearchAsync(new(), ct)).Items, x => Assert.Equal("Updated official text", x.OracleText));
        Assert.Equal(4, Assert.Single(await test.Service.GetCollectionAsync(ct)).Quantity);
    }

    private static ICatalogImporter Importer(TestDatabase test) => test.Provider.GetRequiredService<ICatalogImporter>();
    private static CatalogService Catalog(TestDatabase test) => test.Provider.GetRequiredService<CatalogService>();
    private static Task<DeckbuilderDbContext> Context(TestDatabase test, CancellationToken ct)
        => test.Provider.GetRequiredService<IDbContextFactory<DeckbuilderDbContext>>().CreateDbContextAsync(ct);
    internal sealed class InlineProgress(Action<ImportProgress> action) : IProgress<ImportProgress>
    {
        public void Report(ImportProgress value) => action(value);
    }
}
