using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using MTGDeckbuilder.Application;

namespace MTGDeckbuilder.Infrastructure;

public sealed record ScryfallManifest(string Version, Uri DownloadUri, long? Size, DateTime? UpdatedUtc = null);

public sealed class ScryfallClient(HttpClient client)
{
    public const long MaxFileBytes = 512L * 1024 * 1024;
    private readonly SemaphoreSlim _requests = new(1, 1);
    private DateTime _lastRequestUtc;

    public static HttpClient CreateHttpClient()
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, AutomaticDecompression = DecompressionMethods.None })
            { Timeout = TimeSpan.FromMinutes(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"MTGDeckbuilder/{typeof(ScryfallClient).Assembly.GetName().Version?.ToString(3) ?? "0.12.0"}");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    public async Task<ScryfallManifest> GetManifestAsync(CancellationToken ct)
    {
        await _requests.WaitAsync(ct);
        try
        {
            var wait = TimeSpan.FromMilliseconds(110) - (DateTime.UtcNow - _lastRequestUtc);
            if (wait > TimeSpan.Zero) await Task.Delay(wait, ct);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromSeconds(30));
            using var response = await client.GetAsync("https://api.scryfall.com/bulk-data/default_cards", deadline.Token);
            _lastRequestUtc = DateTime.UtcNow;
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: deadline.Token);
            var root = json.RootElement;
            var id = root.GetProperty("id").GetString();
            var updated = root.GetProperty("updated_at").GetDateTimeOffset().ToUniversalTime().ToString("O");
            var url = root.TryGetProperty("jsonl_download_uri", out var jsonl)
                ? jsonl.GetString() : root.GetProperty("download_uri").GetString();
            var uri = new Uri(url ?? throw new InvalidDataException("De bron bevat geen downloadadres."));
            ValidateDownloadUri(uri);
            long? size = root.TryGetProperty("compressed_size", out var compressed) ? compressed.GetInt64() :
                root.TryGetProperty("size", out var legacy) ? legacy.GetInt64() : null;
            if (size is <= 0 or > MaxFileBytes) throw new InvalidDataException("De catalogusdownload heeft een ongeldige bestandsgrootte.");
            return new ScryfallManifest($"{id}:{updated}", uri, size, DateTimeOffset.Parse(updated, System.Globalization.CultureInfo.InvariantCulture).UtcDateTime);
        }
        finally { _requests.Release(); }
    }

    public static void ValidateDownloadUri(Uri uri)
    {
        if (uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort || uri.UserInfo.Length > 0 ||
            !string.Equals(uri.Host, "data.scryfall.io", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("De bron verwijst niet naar een ondersteunde Scryfall-download.");
    }

    public async Task<(string Hash, long Size)> DownloadAsync(Uri uri, string destination,
        IProgress<ImportProgress>? progress, long? expectedSize, CancellationToken ct)
    {
        ValidateDownloadUri(uri);
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        var advertised = response.Content.Headers.ContentLength;
        if (advertised > MaxFileBytes) throw new InvalidDataException("De catalogusdownload is te groot.");
        await using var input = await response.Content.ReadAsStreamAsync(ct);
        var copied = await CopyAsync(input, destination, "Downloaden", progress, expectedSize ?? advertised, ct);
        if (expectedSize is { } expected && copied.Size != expected || advertised is { } actual && copied.Size != actual)
            throw new InvalidDataException("De download is onvolledig. Probeer opnieuw.");
        return copied;
    }

    internal static async Task<(string Hash, long Size)> CopyAsync(Stream input, string destination, string stage,
        IProgress<ImportProgress>? progress, long? size, CancellationToken ct)
    {
        await using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 65536, useAsync: true);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[65536];
        long copied = 0;
        long lastReported = -1048576;
        while (true)
        {
            var read = await input.ReadAsync(buffer, ct);
            if (read == 0) break;
            copied += read;
            if (copied > MaxFileBytes) throw new InvalidDataException("Het importbestand is groter dan 512 MB.");
            hash.AppendData(buffer, 0, read);
            await output.WriteAsync(buffer.AsMemory(0, read), ct);
            if (copied - lastReported >= 1048576)
            {
                progress?.Report(new ImportProgress(stage, Bytes: copied, TotalBytes: size));
                lastReported = copied;
            }
        }
        if (copied == 0) throw new InvalidDataException("Het importbestand is leeg.");
        await output.FlushAsync(ct);
        progress?.Report(new ImportProgress(stage, Bytes: copied, TotalBytes: size));
        return (Convert.ToHexString(hash.GetHashAndReset()), copied);
    }
}
