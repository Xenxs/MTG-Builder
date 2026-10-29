using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging;
using MTGDeckbuilder.Application;

namespace MTGDeckbuilder.Infrastructure;

public sealed class SqliteBackupService(AppPaths paths, DatabaseWriteLock writes, ILogger<SqliteBackupService> logger) : IBackupService
{
    public const long MaxBytes = 512L * 1024 * 1024;
    private string RecoveryDirectory => Path.Combine(paths.DataDirectory, "recovery");
    private string JournalPath => Path.Combine(RecoveryDirectory, "restore.json");
    private string CopyPath(Guid id) => Path.Combine(RecoveryDirectory, id.ToString("N") + ".db");
    private string ReviewPath(Guid id) => Path.Combine(RecoveryDirectory, id.ToString("N") + ".json");
    private sealed record Journal(int Version, RestoreReview Review, string State, string? SafetyBackup, string? SafetySha256);
    internal async Task<bool> HasPendingRestoreUnderWriteLockAsync(CancellationToken ct) => await ReadJournalAsync(ct) is { State: not "Applied" };

    public Task<IReadOnlyList<BackupFile>> ListAsync(CancellationToken ct = default) => Task.Run<IReadOnlyList<BackupFile>>(() =>
    {
        ct.ThrowIfCancellationRequested();
        return Directory.Exists(paths.BackupsDirectory) ? Directory.GetFiles(paths.BackupsDirectory, "*.db")
            .Select(p => new FileInfo(p)).Select(f => new BackupFile(f.FullName, f.LastWriteTimeUtc, f.Length))
            .OrderByDescending(x => x.CreatedUtc).ThenBy(x => x.Path, StringComparer.Ordinal).ToArray() : [];
    }, ct);
    public Task<BackupFile> CreateAsync(CancellationToken ct = default) => ExclusiveAsync(async () =>
    {
        var file = await CreateCopyAsync("manual", ct);
        logger.LogInformation("Manual database backup created at {Path}", file.Path); return file;
    }, ct);
    public Task<RestoreReview> InspectAsync(string path, CancellationToken ct = default) => ExclusiveAsync(async () =>
    {
        var full = Path.GetFullPath(path);
        if (string.Equals(full, paths.DatabasePath, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Kies een back-up; de actieve database is geen herstelbron.");
        CheckSize(full); var id = Guid.NewGuid(); var copy = CopyPath(id);
        try
        {
            await SnapshotAsync(full, copy, ct);
            var review = await ValidateAsync(copy, id, Path.GetFileName(full), ct);
            await WriteJsonAsync(ReviewPath(id), review, ct); return review;
        }
        catch { DeletePrivateCopy(copy); throw; }
    }, ct);
    public Task ScheduleAsync(Guid reviewId, bool confirmed, CancellationToken ct = default) => ExclusiveAsync(async () =>
    {
        if (!confirmed) throw new ArgumentException("Bevestig expliciet dat de volledige database wordt teruggezet.");
        if (await ReadJournalAsync(ct) is { State: not "Applied" }) throw new ArgumentException("Er staat al herstel gepland. Annuleer dat eerst.");
        var review = await ReadJsonAsync<RestoreReview>(ReviewPath(reviewId), ct) ?? throw new ArgumentException("Herstelvoorbeeld ontbreekt. Controleer de bron opnieuw.");
        if (review.Id != reviewId) throw new ArgumentException("Ongeldig herstelvoorbeeld.");
        await VerifyHashAsync(review, ct);
        await WriteJsonAsync(JournalPath, new Journal(1, review, "Scheduled", null, null), ct);
        logger.LogInformation("Database restore {Id} scheduled for next startup", reviewId);
        return true;
    }, ct);
    public Task CancelAsync(CancellationToken ct = default) => ExclusiveAsync(async () =>
    {
        var journal = await ReadJournalAsync(ct);
        if (journal is { State: "Applying" }) throw new ArgumentException("Herstel is onderbroken tijdens toepassen. Start opnieuw om dit af te ronden.");
        if (File.Exists(JournalPath)) File.Delete(JournalPath);
        return true;
    }, ct);
    public Task<RecoveryStatus> StatusAsync(CancellationToken ct = default) => ExclusiveAsync<RecoveryStatus>(async () =>
    {
        var journal = await ReadJournalAsync(ct);
        return journal switch
        {
            null => new(false, "Geen herstel gepland."),
            { State: "Applied" } => new(false, $"Herstel voltooid bij appstart. Veiligheidskopie: {journal.SafetyBackup}"),
            _ => new(true, $"Herstel gepland voor de volgende appstart: {journal.Review.FileName}. Sluit en start de app opnieuw. Wijzigingen tot die start komen in de veiligheidskopie.")
        };
    }, ct);

    /// <summary>Call only before any application database use, holding the data-directory lease.</summary>
    public Task ApplyPendingAsync(DataDirectoryLease lease, CancellationToken ct = default) => ExclusiveAsync(async () =>
    {
        if (!lease.IsActive || lease.DirectoryPath != paths.DataDirectory) throw new ArgumentException("Exclusieve toegang tot deze gegevensmap ontbreekt.");
        var journal = await ReadJournalAsync(ct);
        if (journal is null || journal.State == "Applied") return false;
        await VerifyHashAsync(journal.Review, ct);
        var review = await ValidateAsync(CopyPath(journal.Review.Id), journal.Review.Id, journal.Review.FileName, ct);
        if (review != journal.Review) throw new ArgumentException("De herstelkopie is gewijzigd; herstel geblokkeerd.");
        // On retry keep the first safety snapshot; never replace it with the restored state.
        if (journal.SafetyBackup is null)
        {
            var safety = await CreateCopyAsync("pre-restore", ct);
            using var safetyStream = File.OpenRead(safety.Path);
            journal = journal with { State = "Applying", SafetyBackup = safety.Path,
                SafetySha256 = Convert.ToHexString(await SHA256.HashDataAsync(safetyStream, ct)) };
            await WriteJsonAsync(JournalPath, journal, ct);
        }
        else
        {
            if (!string.Equals(Path.GetDirectoryName(journal.SafetyBackup), paths.BackupsDirectory, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(journal.SafetyBackup).StartsWith("pre-restore-", StringComparison.Ordinal) || !File.Exists(journal.SafetyBackup))
                throw new ArgumentException("De veiligheidskopie ontbreekt of heeft een ongeldige locatie; herstel geblokkeerd.");
            using var safetyStream = File.OpenRead(journal.SafetyBackup);
            if (Convert.ToHexString(await SHA256.HashDataAsync(safetyStream, ct)) != journal.SafetySha256)
                throw new ArgumentException("De veiligheidskopie is gewijzigd; herstel geblokkeerd.");
        }
        ct.ThrowIfCancellationRequested();
        using (var guard = new FileStream(CopyPath(review.Id), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(guard, ct));
            if (hash != review.Sha256) throw new ArgumentException("De herstelkopie is gewijzigd.");
            using var source = Open(CopyPath(review.Id), SqliteOpenMode.ReadOnly);
            using var destination = Open(paths.DatabasePath, SqliteOpenMode.ReadWriteCreate);
            // The backup API owns the destination transaction, including WAL/journal rollback on failure.
            source.BackupDatabase(destination);
        }
        // Do not honor cancellation after commit: persist the receipt before any UI/database use.
        await WriteJsonAsync(JournalPath, journal with { State = "Applied" }, CancellationToken.None);
        logger.LogInformation("Database restore {Id} applied; safety backup {Path}", review.Id, journal.SafetyBackup);
        return true;
    }, ct);

    private async Task<BackupFile> CreateCopyAsync(string prefix, CancellationToken ct)
    {
        Directory.CreateDirectory(paths.BackupsDirectory);
        var final = Path.Combine(paths.BackupsDirectory, $"{prefix}-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.db");
        var temporary = final + ".part";
        try
        {
            await SnapshotAsync(paths.DatabasePath, temporary, ct);
            await ValidateAsync(temporary, Guid.NewGuid(), Path.GetFileName(final), ct);
            ct.ThrowIfCancellationRequested(); File.Move(temporary, final);
            return new(final, DateTime.UtcNow, new FileInfo(final).Length);
        }
        finally { DeletePrivateCopy(temporary); }
    }
    private static SqliteConnection Open(string path, SqliteOpenMode mode)
    {
        var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = mode, Pooling = false, DefaultTimeout = 5 }.ToString());
        try { db.Open(); using var cmd = db.CreateCommand(); cmd.CommandText = "PRAGMA trusted_schema=OFF"; cmd.ExecuteNonQuery(); return db; }
        catch { db.Dispose(); throw; }
    }
    private static void CheckSize(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length is < 512 or > MaxBytes) throw new ArgumentException("Kies een bestaande SQLite-back-up van maximaal 512 MiB.");
    }
    private static async Task SnapshotAsync(string sourcePath, string destinationPath, CancellationToken ct)
    {
        CheckSize(sourcePath); ct.ThrowIfCancellationRequested();
        using var source = Open(sourcePath, SqliteOpenMode.ReadOnly);
        using var destination = Open(destinationPath, SqliteOpenMode.ReadWriteCreate);
        source.BackupDatabase(destination);
        using var normalize = destination.CreateCommand(); normalize.CommandText = "PRAGMA journal_mode=DELETE"; await normalize.ExecuteScalarAsync(ct);
        ct.ThrowIfCancellationRequested();
    }
    internal async Task ValidateResetBackupUnderWriteLockAsync(string path,CancellationToken ct)
    {
        Directory.CreateDirectory(RecoveryDirectory);
        await ValidateAsync(path,Guid.NewGuid(),Path.GetFileName(path),ct);
    }
    private async Task<RestoreReview> ValidateAsync(string path, Guid id, string fileName, CancellationToken ct)
    {
        CheckSize(path);
        using var db = Open(path, SqliteOpenMode.ReadOnly);
        using var check = db.CreateCommand(); check.CommandText = "PRAGMA integrity_check";
        if (!string.Equals((string?)await check.ExecuteScalarAsync(ct), "ok", StringComparison.Ordinal)) throw new ArgumentException("De back-up is beschadigd.");
        check.CommandText = "PRAGMA foreign_key_check";
        using (var reader = await check.ExecuteReaderAsync(ct)) if (await reader.ReadAsync(ct)) throw new ArgumentException("De back-up heeft ongeldige verwijzingen.");
        check.CommandText = "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId";
        var migrations = new List<string>(); using (var reader = await check.ExecuteReaderAsync(ct)) while (await reader.ReadAsync(ct)) migrations.Add(reader.GetString(0));
        var schemaPath = Path.Combine(RecoveryDirectory, "schema-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            var options = new DbContextOptionsBuilder<DeckbuilderDbContext>().UseSqlite(new SqliteConnectionStringBuilder { DataSource = schemaPath, Pooling = false }.ToString()).Options;
            await using var reference = new DeckbuilderDbContext(options);
            var known = reference.Database.GetMigrations().ToArray();
            if (migrations.Count == 0 || migrations.Count > known.Length || !migrations.SequenceEqual(known.Take(migrations.Count)))
                throw new ArgumentException("Onbekende of nieuwere databaseversie; herstel geweigerd.");
            await reference.GetService<IMigrator>().MigrateAsync(migrations[^1], ct);
            using var expected = Open(schemaPath, SqliteOpenMode.ReadOnly);
            if (await SchemaAsync(db, ct) != await SchemaAsync(expected, ct)) throw new ArgumentException("Het databaseschema wijkt af van deze app; herstel geweigerd.");
        }
        finally { DeletePrivateCopy(schemaPath); }
        async Task<long> Count(string table)
        { check.CommandText = "SELECT COUNT(*) FROM " + table; return (long)(await check.ExecuteScalarAsync(ct))!; }
        var collection = await Count("CollectionEntries"); var decks = await Count("Decks"); var cards = await Count("DeckCards");
        var paper = migrations.Count >= 8;
        var paperCollection = paper ? await Count("PaperHoldings") : 0; var paperDecks = paper ? await Count("PaperDecks") : 0; var paperCards = paper ? await Count("PaperDeckCards") : 0;
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return new(id, fileName, Convert.ToHexString(await SHA256.HashDataAsync(stream, ct)), migrations.Count, collection, decks, cards, paperCollection, paperDecks, paperCards);
    }
    private static async Task<string> SchemaAsync(SqliteConnection db, CancellationToken ct)
    {
        using var cmd = db.CreateCommand(); cmd.CommandText = "SELECT type,name,tbl_name,sql FROM sqlite_master WHERE name NOT LIKE 'sqlite_%' ORDER BY type,name";
        using var reader = await cmd.ExecuteReaderAsync(ct); var rows = new List<string>();
        while (await reader.ReadAsync(ct)) rows.Add(JsonSerializer.Serialize(Enumerable.Range(0, 4).Select(i => reader.IsDBNull(i) ? null : reader.GetString(i)).ToArray()));
        return string.Join("\n", rows);
    }
    private async Task VerifyHashAsync(RestoreReview review, CancellationToken ct)
    {
        if (!File.Exists(CopyPath(review.Id))) throw new ArgumentException("De gecontroleerde herstelkopie ontbreekt; controleer de bron opnieuw.");
        using var stream = new FileStream(CopyPath(review.Id), FileMode.Open, FileAccess.Read, FileShare.Read);
        if (Convert.ToHexString(await SHA256.HashDataAsync(stream, ct)) != review.Sha256) throw new ArgumentException("De herstelkopie is gewijzigd; controleer opnieuw.");
    }
    private async Task<Journal?> ReadJournalAsync(CancellationToken ct)
    {
        var journal = await ReadJsonAsync<Journal>(JournalPath, ct);
        if (journal is not null && (journal.Version != 1 || journal.Review is null || journal.Review.Id == Guid.Empty ||
            journal.Review.Sha256 is not { Length: 64 } || journal.State is not ("Scheduled" or "Applying" or "Applied")))
            throw new ArgumentException("Onbekend hersteljournaal; herstel/start geblokkeerd.");
        return journal;
    }
    private static async Task<T?> ReadJsonAsync<T>(string path, CancellationToken ct)
    {
        if (!File.Exists(path)) return default;
        if (new FileInfo(path).Length > 65536) throw new ArgumentException("Herstelregistratie te groot.");
        try { return JsonSerializer.Deserialize<T>(await File.ReadAllTextAsync(path, ct)) ?? throw new ArgumentException("Lege herstelregistratie."); }
        catch (JsonException ex) { throw new ArgumentException("Beschadigde herstelregistratie; herstel/start geblokkeerd.", ex); }
    }
    private static async Task WriteJsonAsync<T>(string path, T value, CancellationToken ct)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            { await JsonSerializer.SerializeAsync(stream, value, cancellationToken: ct); stream.Flush(flushToDisk: true); }
            ct.ThrowIfCancellationRequested(); File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private static void DeletePrivateCopy(string path)
    {
        foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" }) if (File.Exists(path + suffix)) File.Delete(path + suffix);
    }
    private Task<T> ExclusiveAsync<T>(Func<Task<T>> action, CancellationToken ct) => Task.Run(async () =>
    {
        T? result = default;
        await writes.RunAsync(async () =>
        {
            Directory.CreateDirectory(RecoveryDirectory);
            using var gate = new FileStream(Path.Combine(RecoveryDirectory, "operation.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            try { result = await action(); }
            catch (SqliteException ex)
            {
                logger.LogWarning(ex, "SQLite backup/recovery operation rejected");
                throw new ArgumentException("Geen geldige, beschikbare appback-up. Controleer het bestand en sluit andere databaseprogramma's.", ex);
            }
        }, ct);
        return result!;
    }, ct);
}
