namespace MTGDeckbuilder.Application;

public sealed record DatabaseResetReview(Guid Id, string Fingerprint, IReadOnlyDictionary<string,long> Tables)
{
    public string Summary => $"{Tables.GetValueOrDefault("PaperHoldings")} fysieke voorraadregels · {Tables.GetValueOrDefault("PaperDecks")} fysieke decks · {Tables.Values.Sum()} opgeslagen dataregels in totaal.\n" +
        "Alle collectie-, kaart-, deck-, import- en profielgegevens worden verwijderd, inclusief oudere gegevens. Eerst wordt verplicht een volledige reservekopie gemaakt. Het databaseschema en bestaande back-ups blijven behouden; voorbeelden komen niet automatisch terug.";
}
public sealed record DatabaseResetResult(Guid ReviewId, string BackupPath);
public interface IDatabaseResetService
{
    Task<DatabaseResetReview> ReviewAsync(CancellationToken ct = default);
    Task<DatabaseResetResult> ClearAsync(Guid reviewId, string confirmation, CancellationToken ct = default);
}
