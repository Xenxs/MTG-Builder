using MTGDeckbuilder.Domain;

namespace MTGDeckbuilder.Application;

public sealed record AdvicePrinting(Guid Id, string? OracleId, string Name, string SetCode, string CollectorNumber,
    string ColorIdentity, double ManaValue, string? TypeLine, string? OracleText, CardLegality Standard, bool AvailableOnArena, string? ManaCost = null)
{
    public string Label => $"{Name} ({SetCode}) {CollectorNumber}";
    public ValidationCard Validate(DeckZone zone, int quantity) => new(Id, OracleId, Name, TypeLine, OracleText, Standard, zone, quantity, AvailableOnArena);
}
public sealed record AdviceLine(Guid PrintingId, DeckZone Zone, int Quantity);
public sealed record AdviceStock(Guid PrintingId, int Quantity, Guid Version);
public sealed record AdviceSnapshot(Guid DeckId, string Name, string Format, DeckRuleProfile Profile,
    LegalitySnapshot Source, IReadOnlyList<AdvicePrinting> Printings, IReadOnlyList<AdviceLine> Lines, IReadOnlyList<AdviceStock> Stock);
public sealed record AdviceChoice(Guid FromPrintingId, DeckZone Zone, Guid ToPrintingId, int Quantity);
public sealed record AdviceNeed(Guid PrintingId, DeckZone Zone, string Name, int Missing)
{
    public string Label => $"{Missing}× ontbreekt · {Name} · {Zone}";
}
public sealed record AdviceCandidate(Guid PrintingId, string Name, int Available, bool SameCard, string Reason, string RulesText, CardRoleComparison? Role = null)
{
    public string Label => $"{Name} · maximaal {Available}× · " + (SameCard ? "Dezelfde kaart" : Role?.Label ?? "Alleen structureel");
}
public sealed record AdviceChange(AdviceChoice Choice, string From, string To, CardRoleComparison? Role = null)
{
    public string Label => $"{Choice.Quantity}× {From} → {To} · {Choice.Zone}";
    public string Details => Role?.Details ?? "Controleer de kaartteksten.";
}
public sealed record DeckAdvice(Guid DeckId, string Fingerprint, string Name, DeckValidation Baseline, DeckValidation Result,
    IReadOnlyList<AdviceNeed> Needs, IReadOnlyList<AdviceChange> Changes, IReadOnlyList<AdviceLine> Lines)
{
    public bool CanPreview => Baseline.Passed && Result.Passed && Changes.Count > 0;
    public string Summary => !Baseline.Passed ? "Eerst de formaatcontrole herstellen: " + Baseline.Summary
        : $"{Changes.Sum(x => x.Choice.Quantity)} vervangingen gekozen · {Needs.Sum(x => x.Missing)} exemplaren nog niet lokaal gedekt";
}
public sealed record AdvicePreview(Guid Id, string Name, DeckAdvice Advice);
public interface IDeckAdviceStore
{
    Task<DeckAdvice> GetAsync(Guid deckId, string? expectedFingerprint = null, IReadOnlyList<AdviceChoice>? choices = null, CancellationToken ct = default);
    Task<IReadOnlyList<AdviceCandidate>> CandidatesAsync(Guid deckId, string fingerprint, IReadOnlyList<AdviceChoice> choices, Guid printingId, DeckZone zone, CancellationToken ct = default);
    Task<AdvicePreview> PreviewAsync(Guid deckId, string fingerprint, IReadOnlyList<AdviceChoice> choices, string name, CancellationToken ct = default);
    Task<Guid> ApplyAsync(Guid previewId, CancellationToken ct = default);
}
