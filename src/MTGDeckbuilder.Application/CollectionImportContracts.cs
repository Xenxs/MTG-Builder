using MTGDeckbuilder.Domain;

namespace MTGDeckbuilder.Application;

public sealed record CollectionTarget(Guid Id, string Name, string SetCode, string CollectorNumber,
    string Language, bool Manual, int Quantity)
{
    public string IdentityLabel => $"{Name} · {SetCode} #{CollectorNumber} · {Language}" + (Manual ? " · handmatig" : "");
    public string Label => IdentityLabel + $" · {Quantity}×";
}
public sealed record CollectionPreviewRow(int Number, string SourceLabel, int Quantity, Guid? PrintingId,
    string TargetLabel, int? BeforeQuantity, bool Skipped, string Problem, bool ExplicitChoice)
{
    public string Label => $"{Number}. {SourceLabel} → {Quantity}×";
    public string Details => Skipped ? "Overgeslagen" : Problem.Length > 0 ? Problem : $"{TargetLabel} · {BeforeQuantity ?? 0} → {Quantity}×" + (ExplicitChoice ? " · expliciet gekoppeld" : "");
}
public sealed record CollectionPreview(Guid Id, Guid Version, string FileName, CollectionImportState State,
    int Total, int Unresolved, int Duplicates, int Skipped, int Changes, int Page,
    IReadOnlyList<CollectionPreviewRow> Rows)
{
    public bool CanApply => State == CollectionImportState.Prepared && Unresolved == 0 && Duplicates == 0 && Total > Skipped;
    public string Summary => $"{Total:N0} regels · {Changes:N0} wijzigingen · {Unresolved:N0} ongekoppeld · {Duplicates:N0} dubbele doelen · {Skipped:N0} overgeslagen";
}
public sealed record CollectionImportHistory(Guid Id, string FileName, DateTime CreatedUtc,
    CollectionImportState State, int ChangedCount, int SkippedCount)
{
    public bool CanResume => State == CollectionImportState.Prepared;
    public bool CanUndo => State == CollectionImportState.Applied && ChangedCount > 0;
    public string Label => $"{CreatedUtc.ToLocalTime():dd-MM HH:mm} · {FileName} · " + (State switch
    {
        CollectionImportState.Prepared => "Voorbereid", CollectionImportState.Applied => "Toegepast",
        CollectionImportState.Discarded => "Verworpen", _ => "Hersteld"
    });
    public string Details => State == CollectionImportState.Prepared ? "Voorbereiding · nog geen bezit gewijzigd" :
        State == CollectionImportState.Discarded ? "Verworpen · geen bezit gewijzigd" : $"{ChangedCount:N0} wijzigingen · {SkippedCount:N0} overgeslagen";
}

public interface ICollectionTransfer
{
    Task<Guid> PrepareAsync(string path, CancellationToken ct = default);
    Task<CollectionPreview> GetPreviewAsync(Guid batchId, int page = 1, CancellationToken ct = default);
    Task<IReadOnlyList<CollectionTarget>> GetCandidatesAsync(Guid batchId, int number, CancellationToken ct = default);
    Task<IReadOnlyList<CollectionTarget>> SearchTargetsAsync(string search, CancellationToken ct = default);
    Task ResolveAsync(Guid batchId, Guid expectedVersion, int number, Guid? targetId, bool skip, CancellationToken ct = default);
    Task ApplyAsync(Guid batchId, Guid expectedVersion, CancellationToken ct = default);
    Task DiscardAsync(Guid batchId, Guid expectedVersion, CancellationToken ct = default);
    Task UndoAsync(Guid batchId, CancellationToken ct = default);
    Task<IReadOnlyList<CollectionImportHistory>> GetHistoryAsync(int page = 1, CancellationToken ct = default);
    Task ExportAsync(string path, CancellationToken ct = default);
}
