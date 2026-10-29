namespace MTGDeckbuilder.Application;

public sealed record BackupFile(string Path, DateTime CreatedUtc, long Bytes)
{
    public string Label => $"{System.IO.Path.GetFileName(Path)} · {Bytes / 1048576d:0.0} MiB";
}
public sealed record RestoreReview(Guid Id, string FileName, string Sha256, int Migrations,
    long CollectionRows, long Decks, long DeckCards, long PaperCollectionRows = 0, long PaperDecks = 0, long PaperDeckCards = 0)
{
    public string Summary => $"{FileName}\nFysiek: {PaperCollectionRows} bezitsregels · {PaperDecks} decks · {PaperDeckCards} deckregels\n" +
        $"Eerdere gegevens: {CollectionRows} bezitsregels · {Decks} decks · {DeckCards} deckregels · {Migrations} migraties\n" +
        "Dit zet de volledige lokale database terug, inclusief instellingen en historie. Latere wijzigingen verdwijnen uit de actieve database.";
}
public sealed record RecoveryStatus(bool Scheduled, string Message);
public interface IBackupService
{
    Task<IReadOnlyList<BackupFile>> ListAsync(CancellationToken ct = default);
    Task<BackupFile> CreateAsync(CancellationToken ct = default);
    Task<RestoreReview> InspectAsync(string path, CancellationToken ct = default);
    Task ScheduleAsync(Guid reviewId, bool confirmed, CancellationToken ct = default);
    Task CancelAsync(CancellationToken ct = default);
    Task<RecoveryStatus> StatusAsync(CancellationToken ct = default);
}
