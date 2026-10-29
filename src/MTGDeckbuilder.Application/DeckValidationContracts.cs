using MTGDeckbuilder.Domain;

namespace MTGDeckbuilder.Application;

public sealed record RuleProfileOption(DeckRuleProfile Id, string Label);
public enum ValidationSeverity { Error, Unknown }
public sealed record DeckValidationIssue(string Code, ValidationSeverity Severity, string Card, string Message, string Action)
{
    public string Label => (Severity == ValidationSeverity.Error ? "Fout" : "Onvolledig") +
        (string.IsNullOrEmpty(Card) ? "" : $" · {Card}");
}
public sealed record LegalitySnapshot(Guid? RunId, string Source, string SourceVersion, DateTime? SourceUpdatedUtc,
    DateTime? ImportedUtc, int MetadataVersion);
public sealed record ValidationCard(Guid PrintingId, string? OracleId, string Name, string? TypeLine,
    string? OracleText, CardLegality Standard, DeckZone Zone, int Quantity, bool AvailableOnArena);
public sealed record DeckValidation(DeckRuleProfile Profile, DateTime CheckedUtc, string RulesVersion,
    LegalitySnapshot Snapshot, int Mainboard, int Sideboard, IReadOnlyList<DeckValidationIssue> Issues)
{
    public int Errors => Issues.Count(x => x.Severity == ValidationSeverity.Error);
    public int Unknowns => Issues.Count(x => x.Severity == ValidationSeverity.Unknown);
    public bool Passed => Errors == 0 && Unknowns == 0;
    public string Summary => Errors > 0 ? $"Niet toegestaan volgens de controles · {Errors} {(Errors == 1 ? "fout" : "fouten")} · {Unknowns} onzekerheden"
        : Unknowns > 0 ? $"Onvolledig gecontroleerd · {Unknowns} onzekerheden"
        : "Geslaagd volgens opgeslagen brondata en ondersteunde regels";
    public string Counts => $"{Mainboard} mainboard · {Sideboard} sideboard · controle {CheckedUtc.ToLocalTime():dd-MM-yyyy HH:mm}";
    public string Sources => $"Regels: {RulesVersion}\nKaartbron: {Snapshot.Source} · {Snapshot.SourceVersion}\n" +
        $"Brondatum: {(Snapshot.SourceUpdatedUtc is { } d ? d.ToUniversalTime().ToString("dd-MM-yyyy HH:mm 'UTC'") : "onbekend (bestandsdatum is geen bewijs)")} · " +
        $"Geïmporteerd: {(Snapshot.ImportedUtc is { } i ? i.ToLocalTime().ToString("dd-MM-yyyy HH:mm") : "nog niet")}";
}
public interface IDeckValidationStore
{
    Task<DeckRuleProfile> GetProfileAsync(Guid deckId, CancellationToken ct = default);
    Task SetProfileAsync(Guid deckId, DeckRuleProfile profile, CancellationToken ct = default);
    Task<DeckValidation> ValidateAsync(Guid deckId, CancellationToken ct = default);
}
