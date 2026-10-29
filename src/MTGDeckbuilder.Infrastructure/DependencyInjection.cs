using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MTGDeckbuilder.Application;

namespace MTGDeckbuilder.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddDeckbuilder(this IServiceCollection services, AppPaths paths)
    {
        services.AddSingleton(paths);
        services.AddSingleton(TimeProvider.System);
        services.AddDbContextFactory<DeckbuilderDbContext>(options => Configure(options, paths));
        services.AddSingleton<DatabaseWriteLock>();
        services.AddSingleton<SqliteDeckbuilderStore>();
        services.AddSingleton<IDeckbuilderStore>(sp => new BackgroundDeckbuilderStore(
            sp.GetRequiredService<SqliteDeckbuilderStore>(), sp.GetRequiredService<DatabaseWriteLock>()));
        services.AddSingleton<ICatalogStore, SqliteCatalogStore>();
        services.AddSingleton<CatalogService>();
        services.AddSingleton(_ => ScryfallClient.CreateHttpClient());
        services.AddSingleton<ScryfallClient>();
        services.AddSingleton<ICatalogImporter, CatalogImporter>();
        services.AddSingleton<ICollectionTransfer, SqliteCollectionTransfer>();
        services.AddSingleton<IDeckWorkspace, SqliteDeckWorkspace>();
        services.AddSingleton<IDeckValidationStore, SqliteDeckValidationStore>();
        services.AddSingleton<IDeckAdviceStore, SqliteDeckAdviceStore>();
        services.AddSingleton<IArchetypePoolStore, SqliteArchetypePoolStore>();
        services.AddSingleton<IDeckGenerationStore, SqliteDeckGenerationStore>();
        services.AddSingleton<IPaperCatalog, ScryfallPaperCatalog>();
        services.AddSingleton<IPaperWorkspace, SqlitePaperWorkspace>();
        services.AddSingleton<IDatabaseResetService, SqliteDatabaseResetService>();
        services.AddSingleton<SqliteBackupService>();
        services.AddSingleton<IBackupService>(sp => sp.GetRequiredService<SqliteBackupService>());
        services.AddSingleton<DatabaseInitializer>();
        services.AddSingleton<IDatabaseInitializer>(sp => sp.GetRequiredService<DatabaseInitializer>());
        services.AddSingleton<DeckbuilderService>();
        return services;
    }

    internal static void Configure(DbContextOptionsBuilder options, AppPaths paths)
        => options.UseSqlite(new SqliteConnectionStringBuilder
        {
            DataSource = paths.DatabasePath,
            ForeignKeys = true,
            DefaultTimeout = 15
        }.ToString());
}

public sealed class DatabaseInitializer(
    IDbContextFactory<DeckbuilderDbContext> factory, AppPaths paths, ILogger<DatabaseInitializer> logger,
    ICatalogImporter importer) : IDatabaseInitializer
{
    public Task InitializeAsync(CancellationToken ct = default) => Task.Run(async () =>
    {
        Directory.CreateDirectory(paths.DataDirectory);
        var existed = File.Exists(paths.DatabasePath);
        await using var db = await factory.CreateDbContextAsync(ct);
        if (existed && (await db.Database.GetPendingMigrationsAsync(ct)).Any())
        {
            Directory.CreateDirectory(paths.BackupsDirectory);
            var backupPath = Path.Combine(paths.BackupsDirectory, $"pre-migration-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.db");
            using var source = new SqliteConnection(db.Database.GetConnectionString());
            using var destination = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = backupPath, Pooling = false }.ToString());
            await source.OpenAsync(ct);
            await destination.OpenAsync(ct);
            source.BackupDatabase(destination);
            logger.LogInformation("Pre-migration backup saved to {BackupPath}", backupPath);
        }
        await db.Database.MigrateAsync(ct);
        await importer.RecoverInterruptedAsync(ct);
        logger.LogInformation("Database initialized with migrations at {DatabasePath}", paths.DatabasePath);
    }, ct);
}

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DeckbuilderDbContext>
{
    public DeckbuilderDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<DeckbuilderDbContext>();
        DependencyInjection.Configure(options, new AppPaths());
        return new DeckbuilderDbContext(options.Options);
    }
}
