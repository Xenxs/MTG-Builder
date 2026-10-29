namespace MTGDeckbuilder.Infrastructure;

public sealed class DatabaseWriteLock
{
    public SemaphoreSlim Gate { get; } = new(1, 1);
    public async Task RunAsync(Func<Task> action, CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        try { await action(); }
        finally { Gate.Release(); }
    }
}
