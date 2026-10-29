using MTGDeckbuilder.Domain;

namespace MTGDeckbuilder.Application;

public sealed record DeckImportLine(int Number, string Name, string? SetCode, string? CollectorNumber, int Quantity, DeckZone Zone);
public sealed record ParsedDeck(string? Name, IReadOnlyList<DeckImportLine> Lines);
public sealed record DeckImportPreviewRow(int Number, string Name, int Quantity, DeckZone Zone,
    Guid? PrintingId, string Target, string Reason)
{
    public string Label => $"{Quantity}× {Name} · {Zone}";
    public string Details => PrintingId is null ? Reason : $"{Target} · {Reason}";
}
public sealed record DeckImportPreview(Guid Id, Guid Version, string Name, string Format, string SourceText,
    DeckImportState State, Guid? DeckId, int Total, int Unresolved, int TotalCards, int QuantityConflicts, int Page,
    IReadOnlyList<DeckImportPreviewRow> Rows)
{
    public bool CanApply => State == DeckImportState.Prepared && Total > 0 && Unresolved == 0 && QuantityConflicts == 0;
    public string Summary => $"{Name} · {Format} · {Total:N0} regels · {TotalCards:N0} kaarten · {Unresolved:N0} ongekoppeld · {QuantityConflicts} aantalconflicten";
}
public sealed record DeckImportHistory(Guid Id, string Name, string Format, DateTime CreatedUtc, DeckImportState State, Guid? DeckId)
{
    public string Label => $"{CreatedUtc.ToLocalTime():dd-MM HH:mm} · {Name} · " + (State switch
    { DeckImportState.Prepared => "Voorbereid", DeckImportState.Applied => "Deck aangemaakt", _ => "Verworpen" });
}
public sealed record DeckNeed(string Name, int Required, int Owned, int Missing, int ExactPrintingMissing, bool VerifiedIdentity)
{
    public string Label => $"{Name} · nodig {Required} · bezit {Owned} · ontbreekt {Missing}";
    public string Details => $"Tekort voor exacte printings: {ExactPrintingMissing}" +
        (VerifiedIdentity ? " · andere uitgaven met dezelfde Oracle-ID tellen mee" : " · handmatige identiteit: alleen deze printing telt mee");
}
public sealed record DeckMetric(string Label, int Count, double BarWidth = 0);
public sealed record DeckAnalysis(int Mainboard, int Sideboard, int Commanders, int Lands, int Nonlands,
    int UnknownMetadata, double? AverageManaValue, int MultiFaceCards, IReadOnlyList<DeckNeed> Needs,
    IReadOnlyList<DeckMetric> ManaCurve, IReadOnlyList<DeckMetric> Types, IReadOnlyList<DeckMetric> Colors)
{
    public int Missing => Needs.Sum(x => x.Missing);
    public int ExactPrintingMissing => Needs.Sum(x => x.ExactPrintingMissing);
    public string Summary => $"{Mainboard} mainboard · {Sideboard} sideboard · {Commanders} commander · {Missing} ontbrekende exemplaren";
    public string ManaSummary => $"{Lands} landen · {Nonlands} niet-landen · {UnknownMetadata} zonder metadata · gemiddelde manawaarde " +
        (AverageManaValue is { } value ? value.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture) : "onbekend");
}

public interface IDeckWorkspace
{
    Task<Guid> PrepareAsync(string text, string name, string format, CancellationToken ct = default);
    Task<DeckImportPreview> GetPreviewAsync(Guid id, int page = 1, CancellationToken ct = default);
    Task<IReadOnlyList<CollectionTarget>> GetCandidatesAsync(Guid id, int number, CancellationToken ct = default);
    Task<IReadOnlyList<CollectionTarget>> SearchTargetsAsync(string search, CancellationToken ct = default);
    Task ResolveAsync(Guid id, Guid version, int number, Guid targetId, CancellationToken ct = default);
    Task<Guid> ApplyAsync(Guid id, Guid version, CancellationToken ct = default);
    Task DiscardAsync(Guid id, Guid version, CancellationToken ct = default);
    Task<IReadOnlyList<DeckImportHistory>> GetHistoryAsync(int page = 1, CancellationToken ct = default);
    Task<string> ExportAsync(Guid deckId, CancellationToken ct = default);
    Task<DeckAnalysis> AnalyzeAsync(Guid deckId, CancellationToken ct = default);
}
