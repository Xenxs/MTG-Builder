namespace MTGDeckbuilder.Domain;

/// <summary>Immutable reviewed request plus an idempotent receipt. Deck IDs intentionally survive deletion.</summary>
public sealed class DeckAdvicePlan
{
    private DeckAdvicePlan() { }
    public DeckAdvicePlan(Guid sourceDeckId, string fingerprint, string name, string choicesJson)
    {
        SourceDeckId = Rules.Id(sourceDeckId); Fingerprint = Rules.Text(fingerprint, "Controlecode", 64);
        Name = Rules.Text(name, "Decknaam", 120); ChoicesJson = Rules.Text(choicesJson, "Vervangingen", 2_000_000);
    }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid SourceDeckId { get; private set; }
    public string Fingerprint { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string ChoicesJson { get; private set; } = "";
    public DateTime CreatedUtc { get; private set; } = DateTime.UtcNow;
    public Guid? AppliedDeckId { get; private set; }
    public void MarkApplied(Guid deckId)
    {
        if (AppliedDeckId is not null) throw new InvalidOperationException("Dit voorstel is al toegepast.");
        AppliedDeckId = Rules.Id(deckId);
    }
}
