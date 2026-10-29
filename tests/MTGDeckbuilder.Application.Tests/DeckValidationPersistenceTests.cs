using System.Net;
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

public sealed class DeckValidationPersistenceTests
{
    internal static readonly DateTime SourceTime = new(2026, 10, 22, 8, 0, 0, DateTimeKind.Utc);
    internal sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(SourceTime.AddHours(1));
    }
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    internal sealed class BulkHandler : HttpMessageHandler
    {
        public Dictionary<string, object?>[] Cards { get; set; } = [LegalCard()];
        public int Version { get; set; }
        public int Downloads { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var data = CatalogFixtures.Gzip(Cards);
            if (request.RequestUri!.Host == "api.scryfall.com")
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new
                {
                    id = "e2ef41e3-5778-4bc2-af3f-78eca4dd9c23", updated_at = SourceTime.AddSeconds(Version).ToString("O"),
                    jsonl_download_uri = "https://data.scryfall.io/default-cards/test.jsonl.gz", compressed_size = data.Length
                }), Encoding.UTF8, "application/json") });
            Downloads++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(data) });
        }
    }
    internal static Dictionary<string, object?> LegalCard(string name = "Forest", string collector = "1", string? oracle = null, string status = "legal")
    {
        var card = CatalogFixtures.Card(name, collector: collector, oracle: oracle, type: name == "Forest" ? "Basic Land — Forest" : "Instant");
        card["legalities"] = new Dictionary<string, object> { ["standard"] = status };
        return card;
    }
    internal static TestDatabase Database(BulkHandler handler) => new(s =>
    {
        s.AddSingleton(new HttpClient(handler, disposeHandler: false)); s.AddSingleton<TimeProvider>(new FixedClock());
    });
    internal static IDeckValidationStore Store(TestDatabase test) => test.Provider.GetRequiredService<IDeckValidationStore>();
    internal static async Task<Guid> SeedDeck(TestDatabase test, string format = "Standard")
    {
        await test.InitializeAsync();
        await test.Provider.GetRequiredService<ICatalogImporter>().ImportOnlineAsync(ct: Ct);
        var item = (await test.Provider.GetRequiredService<CatalogService>().SearchAsync(new(), Ct)).Items.Single(x => x.Name == "Forest");
        var deck = await test.Service.CreateDeckAsync("Validation", format, Ct);
        await test.Service.SetDeckCardAsync(deck, item.PrintingId, DeckZone.Mainboard, 60, Ct);
        return deck;
    }

    [Fact]
    public async Task OnlineMetadataProfileAndValidationSurviveRestartWithoutChangingFormatOrOwnership()
    {
        var handler = new BulkHandler(); await using var test = Database(handler); var deck = await SeedDeck(test);
        Assert.Equal(DeckRuleProfile.Unspecified, await Store(test).GetProfileAsync(deck, Ct));
        Assert.False((await Store(test).ValidateAsync(deck, Ct)).Passed);
        await Store(test).SetProfileAsync(deck, DeckRuleProfile.ArenaStandardBo3, Ct);
        var result = await Store(test).ValidateAsync(deck, Ct); Assert.True(result.Passed, result.Summary);
        Assert.NotNull(result.Snapshot.SourceUpdatedUtc); Assert.Equal(1, result.Snapshot.MetadataVersion);
        Assert.Equal(SourceTime, result.Snapshot.SourceUpdatedUtc); Assert.Equal(DateTimeKind.Utc, result.Snapshot.SourceUpdatedUtc.Value.Kind);
        await test.ReopenAsync(); Assert.Equal(DeckRuleProfile.ArenaStandardBo3, await Store(test).GetProfileAsync(deck, Ct));
        Assert.True((await Store(test).ValidateAsync(deck, Ct)).Passed);
        Assert.Equal("Standard", Assert.Single(await test.Service.GetDecksAsync(Ct)).Format);
        Assert.Empty(await test.Service.GetCollectionAsync(Ct));
    }
    [Fact]
    public async Task RefreshChangesLegalityButPreservesDeckRowsAndOwnedCounts()
    {
        var handler = new BulkHandler(); await using var test = Database(handler); var deck = await SeedDeck(test, DeckRuleProfiles.Bo1);
        var catalog = test.Provider.GetRequiredService<CatalogService>(); var item = Assert.Single((await catalog.SearchAsync(new(), Ct)).Items);
        await catalog.SetOwnedQuantityAsync(item.PrintingId, 3, Ct);
        handler.Cards[0]["legalities"] = new Dictionary<string, object> { ["standard"] = "banned" }; handler.Version++;
        await test.Provider.GetRequiredService<ICatalogImporter>().ImportOnlineAsync(ct: Ct);
        Assert.Contains((await Store(test).ValidateAsync(deck, Ct)).Issues, x => x.Code == "card-not-legal");
        Assert.Equal(60, Assert.Single(await test.Service.GetDeckCardsAsync(deck, Ct)).Quantity);
        Assert.Equal(3, Assert.Single(await test.Service.GetCollectionAsync(Ct)).Quantity);
    }
    [Fact]
    public async Task LocalFileLosesProvenSourceDateAndMissingFieldsClearOldLegality()
    {
        var handler = new BulkHandler(); await using var test = Database(handler); var deck = await SeedDeck(test, DeckRuleProfiles.Bo1);
        handler.Cards[0].Remove("legalities");
        var file = await CatalogFixtures.WriteAsync(test, handler.Cards, "json", Ct);
        await test.Provider.GetRequiredService<ICatalogImporter>().ImportFileAsync(file, ct: Ct);
        var result = await Store(test).ValidateAsync(deck, Ct);
        Assert.Null(result.Snapshot.SourceUpdatedUtc); Assert.Contains(result.Issues, x => x.Code == "source-stale");
        Assert.Contains(result.Issues, x => x.Code == "legality"); Assert.False(result.Passed);
    }
    [Fact]
    public async Task ReimportingSameSourceUpgradesOldMetadataRatherThanSkipping()
    {
        var handler = new BulkHandler(); await using var test = Database(handler); await SeedDeck(test);
        await using (var db = await test.Provider.GetRequiredService<IDbContextFactory<DeckbuilderDbContext>>().CreateDbContextAsync(Ct))
            await db.Database.ExecuteSqlRawAsync("UPDATE CatalogImports SET MetadataVersion = 0", Ct);
        var importer = test.Provider.GetRequiredService<ICatalogImporter>();
        Assert.False((await importer.ImportOnlineAsync(ct: Ct)).AlreadyCurrent);
        Assert.True((await importer.ImportOnlineAsync(ct: Ct)).AlreadyCurrent); Assert.Equal(2, handler.Downloads);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MalformedAndConflictingLegalityPreserveTheLastGoodSnapshot(bool conflicting)
    {
        var handler = new BulkHandler(); await using var test = Database(handler); var deck = await SeedDeck(test, DeckRuleProfiles.Bo1);
        var before = await Store(test).ValidateAsync(deck, Ct);
        if (conflicting)
            handler.Cards = [handler.Cards[0], LegalCard(collector: "2", oracle: (string)handler.Cards[0]["oracle_id"]!, status: "banned")];
        else handler.Cards[0]["legalities"] = false;
        handler.Version++;
        await Assert.ThrowsAsync<InvalidDataException>(() => test.Provider.GetRequiredService<ICatalogImporter>().ImportOnlineAsync(ct: Ct));
        var after = await Store(test).ValidateAsync(deck, Ct);
        Assert.Equal(before.Snapshot, after.Snapshot); Assert.True(after.Passed);
        Assert.Equal(60, Assert.Single(await test.Service.GetDeckCardsAsync(deck, Ct)).Quantity);
    }
    [Fact]
    public async Task UnknownFutureSourceStatusIsShownAsIncomplete()
    {
        var handler = new BulkHandler { Cards = [LegalCard(status: "future_status")] };
        await using var test = Database(handler); var deck = await SeedDeck(test, DeckRuleProfiles.Bo1);
        Assert.Contains((await Store(test).ValidateAsync(deck, Ct)).Issues, x => x.Code == "legality");
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailureOrCancellationAfterLegalitySqlWritesRollsBackEntireRefresh(bool cancel)
    {
        var handler = new BulkHandler(); var failure = new LegalityFailure();
        await using var test = new TestDatabase(s =>
        {
            s.AddSingleton(new HttpClient(handler, disposeHandler: false));
            s.AddSingleton<TimeProvider>(new FixedClock());
            s.AddSingleton<IDbContextFactory<DeckbuilderDbContext>>(sp => new InterceptingFactory(sp.GetRequiredService<AppPaths>(), failure));
        });
        var deck = await SeedDeck(test, DeckRuleProfiles.Bo1); var before = await Store(test).ValidateAsync(deck, Ct);
        handler.Cards[0]["legalities"] = new Dictionary<string, object> { ["standard"] = "banned" }; handler.Version++;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        failure.Armed = true; failure.Cancel = cancel ? cancellation : null;
        var action = () => test.Provider.GetRequiredService<ICatalogImporter>().ImportOnlineAsync(ct: cancellation.Token);
        if (cancel) await Assert.ThrowsAnyAsync<OperationCanceledException>(action); else await Assert.ThrowsAsync<IOException>(action);
        failure.Armed = false; Assert.True(failure.Fired);
        var after = await Store(test).ValidateAsync(deck, Ct); Assert.Equal(before.Snapshot, after.Snapshot); Assert.True(after.Passed);
        Assert.Equal(60, Assert.Single(await test.Service.GetDeckCardsAsync(deck, Ct)).Quantity);
    }
    [Fact]
    public async Task FilledPhaseFourUpgradePreservesKeysRowsAndAmbiguousLabelsAndCreatesBackup()
    {
        await using var test = new TestDatabase(); Directory.CreateDirectory(test.Paths.DataDirectory);
        var card = Guid.NewGuid(); var printing = Guid.NewGuid(); var deck = Guid.NewGuid();
        await using (var db = await test.Provider.GetRequiredService<IDbContextFactory<DeckbuilderDbContext>>().CreateDbContextAsync(Ct))
        {
            await db.GetService<IMigrator>().MigrateAsync("20261022073037_AddDeckWorkspace", Ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Cards (Id,Name,SearchName,ColorIdentity,ManaValue) VALUES ({card},{"Forest"},{"FOREST"},{"G"},{0d})", Ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Printings (Id,CardId,SetCode,CollectorNumber,Language,SetName,Rarity,IsInCatalog) VALUES ({printing},{card},{"OLD"},{"1"},{"en"},{"Old"},{"common"},{false})", Ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO CollectionEntries (PrintingId,Quantity,Version) VALUES ({printing},{4},{Guid.NewGuid()})", Ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Decks (Id,Name,Format,CreatedUtc) VALUES ({deck},{"Old"},{"Standard"},{DateTime.UtcNow})", Ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO DeckCards (DeckId,PrintingId,Zone,Quantity) VALUES ({deck},{printing},{2},{1})", Ct);
        }
        await test.InitializeAsync(); Assert.Equal(4, Assert.Single(await test.Service.GetCollectionAsync(Ct)).Quantity);
        Assert.Equal(DeckZone.Commander, Assert.Single(await test.Service.GetDeckCardsAsync(deck, Ct)).Zone);
        Assert.Equal(DeckRuleProfile.Unspecified, await Store(test).GetProfileAsync(deck, Ct));
        Assert.Equal("Standard", Assert.Single(await test.Service.GetDecksAsync(Ct)).Format);
        Assert.Single(Directory.GetFiles(test.Paths.BackupsDirectory, "*.db"));
        await test.InitializeAsync(); Assert.Single(Directory.GetFiles(test.Paths.BackupsDirectory, "*.db"));
    }
    private sealed class LegalityFailure : SaveChangesInterceptor
    {
        public bool Armed { get; set; }
        public bool Fired { get; private set; }
        public CancellationTokenSource? Cancel { get; set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (Armed && eventData.Context!.ChangeTracker.Entries<CardFormatLegality>().Any())
            { Fired = true; if (Cancel is { } source) source.Cancel(); else throw new IOException("Failure after legality SQL writes"); }
            return ValueTask.FromResult(result);
        }
    }
    private sealed class InterceptingFactory(AppPaths paths, SaveChangesInterceptor interceptor) : IDbContextFactory<DeckbuilderDbContext>
    {
        public DeckbuilderDbContext CreateDbContext() => new(new DbContextOptionsBuilder<DeckbuilderDbContext>()
            .UseSqlite(new SqliteConnectionStringBuilder { DataSource = paths.DatabasePath, ForeignKeys = true }.ToString()).AddInterceptors(interceptor).Options);
    }
}
