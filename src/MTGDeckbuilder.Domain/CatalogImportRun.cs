namespace MTGDeckbuilder.Domain;

public enum CatalogImportState { Downloading, Processing, Applying, Completed, Cancelled, Failed, Interrupted }

public sealed class CatalogImportRun
{
    private CatalogImportRun() { }
    public CatalogImportRun(string source, string sourceVersion, string? sourceUri = null, DateTime? sourceUpdatedUtc = null)
    {
        Source = Rules.Text(source, "Importbron", 200);
        SourceVersion = Rules.Text(sourceVersion, "Bronversie", 200);
        SourceUri = sourceUri;
        SourceUpdatedUtc = sourceUpdatedUtc?.ToUniversalTime();
    }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Source { get; private set; } = "";
    public string SourceVersion { get; private set; } = "";
    public string? SourceUri { get; private set; }
    public DateTime? SourceUpdatedUtc { get; private set; }
    public int MetadataVersion { get; private set; }
    public DateTime StartedUtc { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? CompletedUtc { get; private set; }
    public CatalogImportState State { get; private set; } = CatalogImportState.Downloading;
    public bool CacheReady { get; private set; }
    public string? Sha256 { get; private set; }
    public long FileSize { get; private set; }
    public int ProcessedRecords { get; private set; }
    public int StagedRecords { get; private set; }
    public int AddedPrintings { get; private set; }
    public int UpdatedPrintings { get; private set; }
    public int UnlinkedManualPrintings { get; private set; }
    public string? Error { get; private set; }

    public void SetCache(string sha256, long size, bool useHashAsVersion)
    {
        if (sha256.Length != 64 || size <= 0) throw new ArgumentException("Ongeldige importcache.");
        Sha256 = sha256; FileSize = size; CacheReady = true;
        if (useHashAsVersion) SourceVersion = "sha256:" + sha256;
        SetState(CatalogImportState.Processing);
    }
    public void Checkpoint(int processed, int staged)
    {
        if (processed < ProcessedRecords || staged < StagedRecords || staged > processed)
            throw new ArgumentException("Ongeldig importcheckpoint.");
        ProcessedRecords = processed; StagedRecords = staged; UpdatedUtc = DateTime.UtcNow;
    }
    public void SetState(CatalogImportState state, string? error = null)
    {
        if (!Enum.IsDefined(state)) throw new ArgumentException("Ongeldige importstatus.");
        State = state; Error = error; UpdatedUtc = DateTime.UtcNow;
    }
    public void Complete(int added, int updated, int unlinkedManual = 0, int metadataVersion = 0)
    {
        if (added < 0 || updated < 0 || unlinkedManual < 0) throw new ArgumentException("Ongeldige importtotalen.");
        AddedPrintings = added; UpdatedPrintings = updated; UnlinkedManualPrintings = unlinkedManual; CompletedUtc = DateTime.UtcNow;
        MetadataVersion = metadataVersion;
        SetState(CatalogImportState.Completed);
    }
}
