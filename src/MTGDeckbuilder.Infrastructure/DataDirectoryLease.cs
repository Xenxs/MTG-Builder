namespace MTGDeckbuilder.Infrastructure;

/// <summary>Held for the desktop lifetime, before opening any database connection.</summary>
public sealed class DataDirectoryLease : IDisposable
{
    private readonly FileStream _stream;
    public string DirectoryPath { get; }
    internal bool IsActive => _stream.CanRead;
    private DataDirectoryLease(string directory, FileStream stream) { DirectoryPath = directory; _stream = stream; }
    public static DataDirectoryLease Acquire(AppPaths paths)
    {
        Directory.CreateDirectory(paths.DataDirectory);
        try { return new(paths.DataDirectory, new FileStream(Path.Combine(paths.DataDirectory, "app-session.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)); }
        catch (IOException ex) { throw new ArgumentException("Deze gegevensmap is al door een app geopend. Sluit die app en probeer opnieuw.", ex); }
    }
    public void Dispose() => _stream.Dispose();
}
