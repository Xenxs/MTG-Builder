using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using MTGDeckbuilder.Application;
using MTGDeckbuilder.Infrastructure;
using Xunit;

namespace MTGDeckbuilder.Application.Tests;

public sealed class BackupRecoveryTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    internal static IBackupService Backups(TestDatabase test) => test.Provider.GetRequiredService<IBackupService>();
    private static SqliteBackupService Recovery(TestDatabase test) => test.Provider.GetRequiredService<SqliteBackupService>();
    internal static async Task Seed(TestDatabase test)
    {
        await test.InitializeAsync(); await test.Service.SaveCollectionAsync(new("Example", "TEST", "1", 3), TestContext.Current.CancellationToken);
        await test.Service.SavePlayerNameAsync("Before backup", TestContext.Current.CancellationToken);
    }
    private static string Journal(TestDatabase test) => Path.Combine(test.Paths.DataDirectory, "recovery", "restore.json");
    private static string Stage(TestDatabase test, Guid id) => Path.Combine(test.Paths.DataDirectory, "recovery", id.ToString("N") + ".db");
    private static async Task<string> Hash(string path) { await using var file = File.OpenRead(path); return Convert.ToHexString(await SHA256.HashDataAsync(file)); }
    private static SqliteConnection Open(string path)
    {
        var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString()); db.Open(); return db;
    }
    [Fact]
    public async Task BackupRestoreRoundTripPreservesAllTablesIncludingCatalogPlansAndOwnershipVersions()
    {
        await using var test = DeckValidationPersistenceTests.Database(ArchetypePoolPersistenceTests.Handler()); await DeckGenerationPersistenceTests.Seed(test);
        var store = DeckGenerationPersistenceTests.Store(test); var result = await store.CalculateAsync(GenerationMode.OwnedOnly, Ct);
        var preview = await store.PreviewAsync(result.Fingerprint, result.Mode, "Saved before backup", Ct); var id = await store.ApplyAsync(preview.Id, Ct);
        var rows = await TableHashes(test.Paths.DatabasePath); var backup = await Backups(test).CreateAsync(Ct); var sourceHash = await Hash(backup.Path);
        await test.Service.SavePlayerNameAsync("Later", Ct); await test.Service.DeleteDeckAsync(id, Ct);
        var review = await Backups(test).InspectAsync(backup.Path, Ct); Assert.Equal(1, review.Decks); Assert.Equal(30, review.CollectionRows);
        await Assert.ThrowsAsync<ArgumentException>(() => Backups(test).ScheduleAsync(review.Id, false, Ct));
        await Backups(test).ScheduleAsync(review.Id, true, Ct); Assert.True((await Backups(test).StatusAsync(Ct)).Scheduled);
        Assert.Empty(await test.Service.GetDecksAsync(Ct)); SqliteConnection.ClearAllPools();
        using (var lease = DataDirectoryLease.Acquire(test.Paths)) await Recovery(test).ApplyPendingAsync(lease, Ct);
        await test.ReopenAsync(); Assert.Equal(rows, await TableHashes(test.Paths.DatabasePath));
        Assert.Equal(sourceHash, await Hash(backup.Path)); Assert.False((await Backups(test).StatusAsync(Ct)).Scheduled);
        Assert.Contains("voltooid", (await Backups(test).StatusAsync(Ct)).Message);
        var safety = Assert.Single(await Backups(test).ListAsync(Ct), x => Path.GetFileName(x.Path).StartsWith("pre-restore"));
        using (var db = Open(safety.Path)) { using var cmd = db.CreateCommand(); cmd.CommandText = "SELECT PlayerName FROM Settings"; Assert.Equal("Later", await cmd.ExecuteScalarAsync(Ct)); }
        using (var lease = DataDirectoryLease.Acquire(test.Paths)) await Recovery(test).ApplyPendingAsync(lease, Ct);
        Assert.Equal(rows, await TableHashes(test.Paths.DatabasePath)); Assert.Equal(2, (await Backups(test).ListAsync(Ct)).Count);
    }
    [Fact]
    public async Task OnlineBackupIncludesCommittedWalDataWithoutChangingTheSource()
    {
        await using var test = new TestDatabase(); await Seed(test);
        using var db = Open(test.Paths.DatabasePath); using var cmd = db.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA wal_autocheckpoint=0; UPDATE Settings SET PlayerName='In WAL'"; await cmd.ExecuteNonQueryAsync(Ct);
        Assert.True(File.Exists(test.Paths.DatabasePath + "-wal"));
        var before = await TableHashes(test.Paths.DatabasePath); var backup = await Backups(test).CreateAsync(Ct);
        Assert.Equal(before, await TableHashes(backup.Path)); Assert.Equal(before, await TableHashes(test.Paths.DatabasePath));
        Assert.False(File.Exists(backup.Path + "-wal"));
    }
    [Fact]
    public async Task CancelledScheduleDoesNotRestoreAndDoesNotDeleteBackups()
    {
        await using var test = new TestDatabase(); await Seed(test); var backup = await Backups(test).CreateAsync(Ct);
        var review = await Backups(test).InspectAsync(backup.Path, Ct); await Backups(test).ScheduleAsync(review.Id, true, Ct);
        await Assert.ThrowsAsync<ArgumentException>(() => Backups(test).ScheduleAsync(review.Id, true, Ct));
        await Backups(test).CancelAsync(Ct); await test.Service.SavePlayerNameAsync("Keep latest", Ct);
        using (var lease = DataDirectoryLease.Acquire(test.Paths)) await Recovery(test).ApplyPendingAsync(lease, Ct);
        Assert.Equal("Keep latest", await test.Service.GetPlayerNameAsync(Ct)); Assert.Single(await Backups(test).ListAsync(Ct));
    }
    [Theory]
    [InlineData("future")][InlineData("trigger")][InlineData("index")][InlineData("foreign-key")][InlineData("corrupt")]
    public async Task UnknownOrDamagedSourcesAreRejectedWithoutChangingUserData(string change)
    {
        await using var test = new TestDatabase(); await Seed(test); var before = await TableHashes(test.Paths.DatabasePath);
        var backup = await Backups(test).CreateAsync(Ct);
        if (change == "corrupt") await File.WriteAllBytesAsync(backup.Path, new byte[4096], Ct);
        else
        {
            using var db = Open(backup.Path); using var cmd = db.CreateCommand(); cmd.CommandText = change switch
            {
                "future" => "INSERT INTO __EFMigrationsHistory VALUES ('99999999999999_Future','99.0.0')",
                "trigger" => "CREATE TRIGGER strange AFTER INSERT ON Settings BEGIN DELETE FROM Decks; END",
                "index" => "CREATE INDEX strange ON Decks(Name)",
                _ => "PRAGMA foreign_keys=OFF; UPDATE CollectionEntries SET PrintingId='00000000-0000-0000-0000-000000000001'"
            }; await cmd.ExecuteNonQueryAsync(Ct);
        }
        await Assert.ThrowsAsync<ArgumentException>(() => Backups(test).InspectAsync(backup.Path, Ct));
        Assert.Equal(before, await TableHashes(test.Paths.DatabasePath)); Assert.False((await Backups(test).StatusAsync(Ct)).Scheduled);
    }
    [Fact]
    public async Task InspectionIsAnImmutableSnapshotOriginalCanChangeButChangedStageBlocksScheduleAndStartup()
    {
        await using var test = new TestDatabase(); await Seed(test); var backup = await Backups(test).CreateAsync(Ct);
        var review = await Backups(test).InspectAsync(backup.Path, Ct);
        using (var db = Open(backup.Path)) { using var cmd = db.CreateCommand(); cmd.CommandText = "UPDATE Settings SET PlayerName='Changed original'"; await cmd.ExecuteNonQueryAsync(Ct); }
        await Backups(test).ScheduleAsync(review.Id, true, Ct);
        await Backups(test).CancelAsync(Ct);
        await File.AppendAllTextAsync(Stage(test, review.Id), "tampered", Ct);
        await Assert.ThrowsAsync<ArgumentException>(() => Backups(test).ScheduleAsync(review.Id, true, Ct));
        var next = await Backups(test).InspectAsync(backup.Path, Ct); await Backups(test).ScheduleAsync(next.Id, true, Ct);
        await File.AppendAllTextAsync(Stage(test, next.Id), "tampered", Ct);
        using var lease = DataDirectoryLease.Acquire(test.Paths);
        await Assert.ThrowsAsync<ArgumentException>(() => Recovery(test).ApplyPendingAsync(lease, Ct));
        Assert.Equal("Before backup", await test.Service.GetPlayerNameAsync(Ct)); await Backups(test).CancelAsync(Ct);
    }
    [Fact]
    public async Task RestoreOlderKnownSchemaThenMigratePreservesKeysAndCreatesMigrationBackup()
    {
        await using var test = new TestDatabase(); await Seed(test);
        var oldFile = Path.Combine(test.Paths.DataDirectory, "old.db"); var options = new DbContextOptionsBuilder<DeckbuilderDbContext>().UseSqlite($"Data Source={oldFile};Pooling=False").Options;
        await using (var db = new DeckbuilderDbContext(options))
        {
            await db.GetService<IMigrator>().MigrateAsync("20261021182801_InitialCreate", Ct);
            await db.Database.ExecuteSqlRawAsync("INSERT INTO Settings VALUES (1,'Old player')", Ct);
        }
        var review = await Backups(test).InspectAsync(oldFile, Ct); Assert.Equal(1, review.Migrations);
        await Backups(test).ScheduleAsync(review.Id, true, Ct); SqliteConnection.ClearAllPools();
        using (var lease = DataDirectoryLease.Acquire(test.Paths))
            await new RecoveringDatabaseInitializer(test.Provider.GetRequiredService<IDatabaseInitializer>(), Recovery(test), lease).InitializeAsync(Ct);
        Assert.Equal("Old player", await test.Service.GetPlayerNameAsync(Ct));
        await using var after = await test.Provider.GetRequiredService<IDbContextFactory<DeckbuilderDbContext>>().CreateDbContextAsync(Ct);
        Assert.Equal(9, (await after.Database.GetAppliedMigrationsAsync(Ct)).Count()); Assert.Equal(2, (await Backups(test).ListAsync(Ct)).Count);
    }
    [Fact]
    public async Task InterruptedApplyAndPostCommitJournalFailureRetryWithoutReplacingTheFirstSafetyBackup()
    {
        await using var test = new TestDatabase(); await Seed(test); var backup = await Backups(test).CreateAsync(Ct);
        var review = await Backups(test).InspectAsync(backup.Path, Ct); await Backups(test).ScheduleAsync(review.Id, true, Ct);
        await test.Service.SavePlayerNameAsync("Safety state", Ct); SqliteConnection.ClearAllPools();
        using var lease = DataDirectoryLease.Acquire(test.Paths);
        using (var locked = new FileStream(test.Paths.DatabasePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            await Assert.ThrowsAsync<ArgumentException>(() => Recovery(test).ApplyPendingAsync(lease, Ct));
        Assert.Equal("Safety state", await test.Service.GetPlayerNameAsync(Ct));
        var safety = Assert.Single(await Backups(test).ListAsync(Ct), x => Path.GetFileName(x.Path).StartsWith("pre-restore")); var safetyHash = await Hash(safety.Path);
        await Assert.ThrowsAsync<ArgumentException>(() => Backups(test).CancelAsync(Ct));
        SqliteConnection.ClearAllPools();
        using (var locked = new FileStream(Journal(test), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var error = await Record.ExceptionAsync(() => Recovery(test).ApplyPendingAsync(lease, Ct));
            Assert.True(error is IOException or UnauthorizedAccessException, error?.ToString());
        }
        Assert.Equal("Before backup", await test.Service.GetPlayerNameAsync(Ct));
        await Recovery(test).ApplyPendingAsync(lease, Ct);
        Assert.Equal(safetyHash, await Hash(safety.Path)); Assert.Equal(2, (await Backups(test).ListAsync(Ct)).Count);
        Assert.False((await Backups(test).StatusAsync(Ct)).Scheduled);
    }
    [Fact]
    public async Task CancelledOperationsPublishNoPartialBackupsOrRestoreRequests()
    {
        await using var test = new TestDatabase(); await Seed(test); using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Backups(test).CreateAsync(cancel.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Backups(test).InspectAsync(test.Paths.DatabasePath, cancel.Token));
        await Assert.ThrowsAsync<ArgumentException>(() => Backups(test).InspectAsync(test.Paths.DatabasePath, Ct));
        Assert.Empty(await Backups(test).ListAsync(Ct)); Assert.False(File.Exists(Journal(test)));
    }
    [Fact]
    public async Task ChangedSafetySnapshotBlocksInterruptedRestoreRetry()
    {
        await using var test = new TestDatabase(); await Seed(test); var backup = await Backups(test).CreateAsync(Ct);
        var review = await Backups(test).InspectAsync(backup.Path, Ct); await Backups(test).ScheduleAsync(review.Id, true, Ct);
        SqliteConnection.ClearAllPools(); using var lease = DataDirectoryLease.Acquire(test.Paths);
        using (var locked = new FileStream(test.Paths.DatabasePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            await Assert.ThrowsAsync<ArgumentException>(() => Recovery(test).ApplyPendingAsync(lease, Ct));
        var safety = Assert.Single(await Backups(test).ListAsync(Ct), x => Path.GetFileName(x.Path).StartsWith("pre-restore"));
        await File.AppendAllTextAsync(safety.Path, "changed", Ct);
        await Assert.ThrowsAsync<ArgumentException>(() => Recovery(test).ApplyPendingAsync(lease, Ct));
        Assert.Equal("Before backup", await test.Service.GetPlayerNameAsync(Ct));
    }
    [Fact]
    public async Task LeaseBlocksSecondAppAndDisposedOrDifferentLeaseCannotApply()
    {
        await using var test = new TestDatabase(); await Seed(test);
        using (var first = DataDirectoryLease.Acquire(test.Paths)) Assert.Throws<ArgumentException>(() => DataDirectoryLease.Acquire(test.Paths));
        var disposed = DataDirectoryLease.Acquire(test.Paths); disposed.Dispose();
        await Assert.ThrowsAsync<ArgumentException>(() => Recovery(test).ApplyPendingAsync(disposed, Ct));
        using var other = DataDirectoryLease.Acquire(new AppPaths(Path.Combine(test.Paths.DataDirectory, "other")));
        await Assert.ThrowsAsync<ArgumentException>(() => Recovery(test).ApplyPendingAsync(other, Ct));
    }
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task CorruptOrUnknownRecoveryJournalBlocksStartupInsteadOfSilentlySkipping(bool future)
    {
        await using var test = new TestDatabase(); await Seed(test); var backup = await Backups(test).CreateAsync(Ct);
        var review = await Backups(test).InspectAsync(backup.Path, Ct); await Backups(test).ScheduleAsync(review.Id, true, Ct);
        if (!future) await File.WriteAllTextAsync(Journal(test), "broken", Ct);
        else { var json = JsonNode.Parse(await File.ReadAllTextAsync(Journal(test), Ct))!; json["Version"] = 99; await File.WriteAllTextAsync(Journal(test), json.ToJsonString(), Ct); }
        using var lease = DataDirectoryLease.Acquire(test.Paths);
        await Assert.ThrowsAsync<ArgumentException>(() => Recovery(test).ApplyPendingAsync(lease, Ct)); Assert.Equal("Before backup", await test.Service.GetPlayerNameAsync(Ct));
    }
    internal static async Task<string[]> TableHashes(string path)
    {
        using var db = Open(path); using var command = db.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' AND name <> '__EFMigrationsLock' ORDER BY name";
        var tables = new List<string>(); using (var reader = await command.ExecuteReaderAsync()) while (await reader.ReadAsync()) tables.Add(reader.GetString(0));
        var result = new List<string>();
        foreach (var table in tables)
        {
            command.CommandText = "SELECT * FROM \"" + table.Replace("\"", "\"\"") + "\"";
            using var reader = await command.ExecuteReaderAsync(); var rows = new List<string>();
            while (await reader.ReadAsync()) { var row = new object[reader.FieldCount]; reader.GetValues(row); rows.Add(System.Text.Json.JsonSerializer.Serialize(row)); }
            result.Add(table + ":" + string.Join("|", rows.Order(StringComparer.Ordinal)));
        }
        return result.ToArray();
    }
}
