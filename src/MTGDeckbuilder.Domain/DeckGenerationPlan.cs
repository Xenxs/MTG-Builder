namespace MTGDeckbuilder.Domain;

/// <summary>Reviewed generation request and receipt; deleting its deck preserves the receipt.</summary>
public sealed class DeckGenerationPlan
{
    private DeckGenerationPlan() { }
    public DeckGenerationPlan(string fingerprint, string name, string reviewedJson)
    {
        Fingerprint = Rules.Text(fingerprint, "Controlecode", 64);
        if (Fingerprint.Length != 64 || Fingerprint.Any(x => !Uri.IsHexDigit(x))) throw new ArgumentException("Ongeldige controlecode.");
        Name = Rules.Text(name, "Decknaam", 120); ReviewedJson = Rules.Text(reviewedJson, "Deckvoorbeeld", 2_000_000);
    }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Fingerprint { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string ReviewedJson { get; private set; } = "";
    public DateTime CreatedUtc { get; private set; } = DateTime.UtcNow;
    public Guid? AppliedDeckId { get; private set; }
    public void MarkApplied(Guid id)
    {
        if (AppliedDeckId is not null) throw new InvalidOperationException("Dit voorstel is al opgeslagen.");
        AppliedDeckId = Rules.Id(id);
    }
}
