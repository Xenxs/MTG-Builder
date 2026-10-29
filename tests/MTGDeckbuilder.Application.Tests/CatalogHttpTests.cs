using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MTGDeckbuilder.Application;
using MTGDeckbuilder.Domain;
using MTGDeckbuilder.Infrastructure;
using Xunit;

namespace MTGDeckbuilder.Application.Tests;

public sealed class CatalogHttpTests
{
    [Fact]
    public async Task OnlineImportUsesManifestAndSkipsUnchangedSourceWithoutRedownload()
    {
        var ct = TestContext.Current.CancellationToken;
        var handler = new BulkHandler(truncate: false);
        await using var test = new TestDatabase(services => services.AddSingleton(_ => CreateClient(handler)));
        await test.InitializeAsync();
        var importer = test.Provider.GetRequiredService<ICatalogImporter>();
        var first = await importer.ImportOnlineAsync(ct: ct);
        var second = await importer.ImportOnlineAsync(ct: ct);
        Assert.Equal(1, first.Added);
        Assert.True(second.AlreadyCurrent);
        Assert.Equal(1, handler.Downloads);
        Assert.Equal(2, handler.Manifests);
        Assert.Single(await test.Provider.GetRequiredService<CatalogService>().GetHistoryAsync(ct));
        using var actualClient = ScryfallClient.CreateHttpClient();
        Assert.Contains(actualClient.DefaultRequestHeaders.UserAgent, x => x.Product?.Name == "MTGDeckbuilder");
        Assert.Contains(actualClient.DefaultRequestHeaders.Accept, x => x.MediaType == "application/json");
    }

    [Fact]
    public async Task TruncatedDownloadIsRecordedAndDoesNotAlterCatalog()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = new TestDatabase(services => services.AddSingleton(_ => CreateClient(new BulkHandler(truncate: true))));
        await test.InitializeAsync();
        await Assert.ThrowsAsync<InvalidDataException>(() => test.Provider.GetRequiredService<ICatalogImporter>().ImportOnlineAsync(ct: ct));
        var catalog = test.Provider.GetRequiredService<CatalogService>();
        Assert.Equal(0, (await catalog.GetStatusAsync(ct)).Printings);
        var run = Assert.Single(await catalog.GetHistoryAsync(ct));
        Assert.Equal(CatalogImportState.Failed, run.State);
        Assert.False(run.CacheReady);
        Assert.False(run.CanResume);
        Assert.False(File.Exists(test.Paths.CatalogCachePath(run.Id) + ".part"));
    }

    [Fact]
    public async Task OnlineCheckRestoresOfficialSnapshotAfterALocalImport()
    {
        var ct = TestContext.Current.CancellationToken;
        var handler = new BulkHandler(truncate: false);
        await using var test = new TestDatabase(services => services.AddSingleton(_ => CreateClient(handler)));
        await test.InitializeAsync();
        var importer = test.Provider.GetRequiredService<ICatalogImporter>();
        await importer.ImportOnlineAsync(ct: ct);
        var local = await CatalogFixtures.WriteAsync(test, [CatalogFixtures.Card("Local snapshot", collector: "999")], "json", ct);
        await importer.ImportFileAsync(local, ct: ct);
        var restored = await importer.ImportOnlineAsync(ct: ct);
        Assert.False(restored.AlreadyCurrent);
        Assert.Equal(2, handler.Downloads);
        Assert.Equal("Llanowar Elves", Assert.Single((await test.Provider.GetRequiredService<CatalogService>().SearchAsync(new(), ct)).Items).Name);
    }

    [Theory]
    [InlineData("http://data.scryfall.io/file.gz")]
    [InlineData("https://example.com/file.gz")]
    [InlineData("https://data.scryfall.io:444/file.gz")]
    public void ManifestDownloadMustUseTheExpectedHttpsHost(string url)
        => Assert.Throws<InvalidDataException>(() => ScryfallClient.ValidateDownloadUri(new Uri(url)));

    private static HttpClient CreateClient(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MTGDeckbuilder/0.2.0");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private sealed class BulkHandler(bool truncate) : HttpMessageHandler
    {
        private readonly byte[] _data = CatalogFixtures.Gzip([CatalogFixtures.Card()]);
        public int Downloads { get; private set; }
        public int Manifests { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.NotEmpty(request.Headers.UserAgent);
            Assert.NotEmpty(request.Headers.Accept);
            if (request.RequestUri!.Host == "api.scryfall.com")
            {
                Manifests++;
                var json = JsonSerializer.Serialize(new
                {
                    id = "e2ef41e3-5778-4bc2-af3f-78eca4dd9c23", updated_at = "2026-10-21T09:05:44Z",
                    jsonl_download_uri = "https://data.scryfall.io/default-cards/fixture.jsonl.gz",
                    compressed_size = _data.Length + (truncate ? 10 : 0)
                });
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
            }
            Downloads++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(_data) });
        }
    }
}
