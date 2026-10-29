namespace MTGDeckbuilder.Domain;

public enum DeckImportState { Prepared, Applied, Discarded }

public sealed class DeckImportBatch
{
    private DeckImportBatch() { }
    public DeckImportBatch(string name, string format, string sourceText)
    {
        Name = Rules.Text(name, "Decknaam", 120);
        Format = Rules.Text(format, "Formaat", 40);
        SourceText = Rules.Text(sourceText, "Decktekst", 262144);
    }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid Version { get; private set; } = Guid.NewGuid();
    public DateTime CreatedUtc { get; private set; } = DateTime.UtcNow;
    public string Name { get; private set; } = "";
    public string Format { get; private set; } = "";
    public string SourceText { get; private set; } = "";
    public DeckImportState State { get; private set; }
    public Guid? DeckId { get; private set; }
    public void RequirePrepared()
    {
        if (State != DeckImportState.Prepared) throw new ArgumentException("Deze deckvoorbereiding is niet meer actief.");
    }
    public void Touch() { RequirePrepared(); Version = Guid.NewGuid(); }
    public void Apply(Guid deckId)
    {
        RequirePrepared(); DeckId = Rules.Id(deckId); State = DeckImportState.Applied; Version = Guid.NewGuid();
    }
    public void Discard() { RequirePrepared(); State = DeckImportState.Discarded; Version = Guid.NewGuid(); }
}

public sealed class DeckImportRow
{
    public Guid BatchId { get; set; }
    public int Number { get; set; }
    public string Name { get; set; } = "";
    public string? SetCode { get; set; }
    public string? CollectorNumber { get; set; }
    public int Quantity { get; set; }
    public DeckZone Zone { get; set; }
    public Guid? PrintingId { get; set; }
    public string? PrintingSignature { get; set; }
    public string Reason { get; set; } = "";
}
