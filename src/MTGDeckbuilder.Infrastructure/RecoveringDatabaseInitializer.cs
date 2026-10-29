using MTGDeckbuilder.Application;

namespace MTGDeckbuilder.Infrastructure;

public sealed class RecoveringDatabaseInitializer(IDatabaseInitializer inner, SqliteBackupService backups, DataDirectoryLease lease) : IDatabaseInitializer
{
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await backups.ApplyPendingAsync(lease, ct);
        await inner.InitializeAsync(ct);
    }
}
