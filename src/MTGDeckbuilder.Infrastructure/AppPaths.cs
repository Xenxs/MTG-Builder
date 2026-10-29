namespace MTGDeckbuilder.Infrastructure;

public sealed class AppPaths
{
    public AppPaths(string? dataDirectory = null)
    {
        DataDirectory = Path.GetFullPath(dataDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MTGDeckbuilder"));
    }
    public string DataDirectory { get; }
    public string DatabasePath => Path.Combine(DataDirectory, "deckbuilder.db");
    public string LogPath => Path.Combine(DataDirectory, "logs", "app-.log");
    public string CatalogCacheDirectory => Path.Combine(DataDirectory, "catalog-cache");
    public string BackupsDirectory => Path.Combine(DataDirectory, "backups");
    public string ImportLockPath => Path.Combine(DataDirectory, "catalog-import.lock");
    public string CatalogCachePath(Guid runId) => Path.Combine(CatalogCacheDirectory, runId.ToString("N") + ".data");
}
