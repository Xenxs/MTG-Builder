namespace MTGDeckbuilder.Domain;

public enum CollectionImportState { Prepared, Applied, Discarded, Undone }

public sealed class CollectionImportBatch
{
    private CollectionImportBatch() { }
    public CollectionImportBatch(string fileName, string sha256)
    {
        FileName = Rules.Text(fileName, "Bestandsnaam", 255);
        Sha256 = Rules.Text(sha256, "Bestandshash", 64);
    }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string FileName { get; private set; } = "";
    public string Sha256 { get; private set; } = "";
    public DateTime CreatedUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? AppliedUtc { get; private set; }
    public DateTime? UndoneUtc { get; private set; }
    public CollectionImportState State { get; private set; }
    public int ChangedCount { get; private set; }
    public int SkippedCount { get; private set; }
    public Guid Version { get; private set; } = Guid.NewGuid();
    public void Touch()
    {
        RequirePrepared();
        Version = Guid.NewGuid();
    }
    public void RequirePrepared()
    {
        if (State != CollectionImportState.Prepared) throw new ArgumentException("Deze voorbereiding is niet meer actief.");
    }
    public void Apply(int changed, int skipped)
    {
        RequirePrepared();
        if (changed < 0 || skipped < 0) throw new ArgumentException("Ongeldige importtotalen.");
        ChangedCount = changed; SkippedCount = skipped;
        State = CollectionImportState.Applied; AppliedUtc = DateTime.UtcNow; Version = Guid.NewGuid();
    }
    public void Discard()
    {
        RequirePrepared(); State = CollectionImportState.Discarded; Version = Guid.NewGuid();
    }
    public void Undo()
    {
        if (State != CollectionImportState.Applied) throw new ArgumentException("Alleen een toegepaste import kan worden hersteld.");
        State = CollectionImportState.Undone; UndoneUtc = DateTime.UtcNow; Version = Guid.NewGuid();
    }
}

public sealed class CollectionImportRow
{
    public Guid BatchId { get; set; }
    public int Number { get; set; }
    public string SourceData { get; set; } = "";
    public string SourceLabel { get; set; } = "";
    public int Quantity { get; set; }
    public Guid? PrintingId { get; set; }
    public bool Skipped { get; set; }
    public bool ExplicitChoice { get; set; }
    public string MatchReason { get; set; } = "";
    public int? BeforeQuantity { get; set; }
    public Guid? BeforeVersion { get; set; }
    public string? PrintingSignature { get; set; }
    public bool Changed { get; set; }
    public Guid? AppliedVersion { get; set; }
}
