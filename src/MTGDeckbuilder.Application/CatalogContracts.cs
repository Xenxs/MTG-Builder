using MTGDeckbuilder.Domain;

namespace MTGDeckbuilder.Application;

public sealed record CatalogQuery(string Search = "", string SetCode = "", string Rarity = "",
    string Color = "", string Type = "", double? MaxManaValue = null, bool OwnedOnly = false,
    int Page = 1, int PageSize = 50);

public sealed record CatalogItem(Guid PrintingId, string Name, string ManaCost, string TypeLine,
    string OracleText, string ColorIdentity, double ManaValue, string SetCode, string SetName,
    string CollectorNumber, string Language, string Rarity, int OwnedQuantity, string ScryfallId, int? ArenaId)
{
    public string Label => $"{Name} · {SetCode} #{CollectorNumber} · {Language}";
    public string Details => $"{ManaCost}  ·  {TypeLine}  ·  Mana {ManaValue:0.##}";
    public string PrintingDetails => $"{SetName} ({SetCode}) #{CollectorNumber} · {Language} · {Rarity} · {OwnedQuantity}× in bezit";
}
public sealed record CatalogPage(IReadOnlyList<CatalogItem> Items, int Total, int Page, int PageSize);
public sealed record CatalogSet(string Code, string Name)
{
    public string Label => string.IsNullOrEmpty(Code) ? Name : $"{Code} · {Name}";
}
public sealed record CatalogStatus(int Printings, int Cards, DateTime? UpdatedUtc, string? SourceVersion);
public sealed record ImportProgress(string Stage, int Processed = 0, int Matched = 0, long Bytes = 0, long? TotalBytes = null)
{
    public string Message => Stage == "Downloaden" || Stage == "Bestand kopiëren"
        ? $"{Stage}: {Bytes / 1048576d:0.0} MB" + (TotalBytes > 0 ? $" / {TotalBytes / 1048576d:0.0} MB" : "")
        : $"{Stage}: {Processed:N0} verwerkt · {Matched:N0} Arena-printings";
}
public sealed record ImportResult(Guid RunId, int Added, int Updated, int Processed, int Matched, bool AlreadyCurrent = false, int UnlinkedManualPrintings = 0);
public sealed record ImportHistoryItem(Guid Id, string Source, string SourceVersion, DateTime StartedUtc,
    DateTime? CompletedUtc, CatalogImportState State, int ProcessedRecords, int StagedRecords,
    int AddedPrintings, int UpdatedPrintings, bool CacheReady, string? Error, int UnlinkedManualPrintings = 0)
{
    public bool CanResume => CacheReady && State is CatalogImportState.Cancelled or CatalogImportState.Failed or CatalogImportState.Interrupted;
    public string StateLabel => State switch
    {
        CatalogImportState.Downloading => "Downloaden", CatalogImportState.Processing => "Verwerken",
        CatalogImportState.Applying => "Toepassen", CatalogImportState.Completed => "Voltooid",
        CatalogImportState.Cancelled => "Gepauzeerd", CatalogImportState.Interrupted => "Onderbroken", _ => "Mislukt"
    };
    public string Label => $"{StartedUtc.ToLocalTime():dd-MM HH:mm} · {StateLabel} · {StagedRecords:N0} printings";
    public string Details => $"{Source} · {SourceVersion}\nNieuw: {AddedPrintings:N0} · bijgewerkt: {UpdatedPrintings:N0}" +
        (UnlinkedManualPrintings == 0 ? "" : $"\n{UnlinkedManualPrintings} handmatige printings apart behouden wegens onzekere koppeling.") +
        (Error is null ? "" : $"\n{Error}");
}

public interface ICatalogStore
{
    Task<CatalogPage> SearchAsync(CatalogQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<CatalogSet>> GetSetsAsync(CancellationToken ct = default);
    Task<CatalogStatus> GetStatusAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ImportHistoryItem>> GetHistoryAsync(CancellationToken ct = default);
    Task SetOwnedQuantityAsync(Guid printingId, int quantity, CancellationToken ct = default);
}

public interface ICatalogImporter
{
    Task<ImportResult> ImportOnlineAsync(IProgress<ImportProgress>? progress = null, CancellationToken ct = default);
    Task<ImportResult> ImportFileAsync(string path, IProgress<ImportProgress>? progress = null, CancellationToken ct = default);
    Task<ImportResult> ResumeAsync(Guid runId, IProgress<ImportProgress>? progress = null, CancellationToken ct = default);
    Task RecoverInterruptedAsync(CancellationToken ct = default);
}

public sealed class CatalogService(ICatalogStore store)
{
    public Task<CatalogPage> SearchAsync(CatalogQuery query, CancellationToken ct = default)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100 ||
            query.MaxManaValue is { } mana && (!double.IsFinite(mana) || mana < 0))
            throw new ArgumentException("Ongeldige catalogusfilters of paginanummer.");
        if (query.Color.Length > 0 && !new[] { "W", "U", "B", "R", "G", "C", "M" }.Contains(query.Color))
            throw new ArgumentException("Ongeldig kleurfilter.");
        return store.SearchAsync(query with
        {
            Search = query.Search.Trim(), SetCode = query.SetCode.Trim().ToUpperInvariant(),
            Rarity = query.Rarity.Trim().ToLowerInvariant(), Type = query.Type.Trim()
        }, ct);
    }
    public Task<IReadOnlyList<CatalogSet>> GetSetsAsync(CancellationToken ct = default) => store.GetSetsAsync(ct);
    public Task<CatalogStatus> GetStatusAsync(CancellationToken ct = default) => store.GetStatusAsync(ct);
    public Task<IReadOnlyList<ImportHistoryItem>> GetHistoryAsync(CancellationToken ct = default) => store.GetHistoryAsync(ct);
    public Task SetOwnedQuantityAsync(Guid id, int quantity, CancellationToken ct = default)
        => store.SetOwnedQuantityAsync(Rules.Id(id), Rules.Quantity(quantity), ct);
}
