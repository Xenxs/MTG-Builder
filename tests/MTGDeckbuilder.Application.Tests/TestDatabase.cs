using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using MTGDeckbuilder.Application;
using MTGDeckbuilder.Desktop.ViewModels;
using MTGDeckbuilder.Infrastructure;

namespace MTGDeckbuilder.Application.Tests;

internal sealed class TestDatabase : IAsyncDisposable
{
    private readonly Action<IServiceCollection>? _configure;
    public AppPaths Paths { get; } = new(Path.Combine(Path.GetTempPath(), "MTGDeckbuilder.Tests", Guid.NewGuid().ToString("N")));
    public ServiceProvider Provider { get; private set; }
    public DeckbuilderService Service => Provider.GetRequiredService<DeckbuilderService>();
    public TestDatabase(Action<IServiceCollection>? configure = null)
    {
        _configure = configure;
        Provider = BuildProvider();
    }
    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDeckbuilder(Paths);
        services.AddSingleton<IPaperCatalog>(sp => new OfflineImagesCatalog(ActivatorUtilities.CreateInstance<ScryfallPaperCatalog>(sp)));
        services.AddSingleton<UiOperations>();
        services.AddSingleton<PaperViewModel>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<CollectionViewModel>();
        services.AddSingleton<DecksViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<CatalogViewModel>();
        services.AddSingleton<ArchetypePoolViewModel>();
        services.AddSingleton<DeckGenerationViewModel>();
        services.AddSingleton<CollectionTransferViewModel>();
        services.AddSingleton<DeckTransferViewModel>();
        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<LegacyShell>();
        services.AddSingleton<DeckWizardViewModel>();
        _configure?.Invoke(services);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }
    private sealed class OfflineImagesCatalog(IPaperCatalog inner) : IPaperCatalog
    {
        public Task<PaperCard> GetAsync(Guid id, bool refresh = false, CancellationToken ct = default) => inner.GetAsync(id, refresh, ct);
        public Task<PaperCard> GetAsync(string set, string number, string language, CancellationToken ct = default) => inner.GetAsync(set, number, language, ct);
        public Task<IReadOnlyList<PaperCard>> SearchAsync(string query, CancellationToken ct = default) => inner.SearchAsync(query, ct);
        public Task<IReadOnlyList<PaperCard>> CommanderPoolAsync(string colors, CancellationToken ct = default) => inner.CommanderPoolAsync(colors, ct);
        public Task<IReadOnlyList<PaperCard>> WizardPoolAsync(DeckWizardRequest request, CancellationToken ct = default) => inner.WizardPoolAsync(request, ct);
        public Task<string?> ImageAsync(PaperCard card, bool back, CancellationToken ct = default)
        {
            var file=Environment.GetEnvironmentVariable("MTGDECKBUILDER_REAL_IMAGE");
            return Task.FromResult(!back&&file is not null&&Path.GetFileName(file).StartsWith(card.Id.ToString("N"),StringComparison.OrdinalIgnoreCase)&&File.Exists(file)?file:null);
        }
    }
    public Task InitializeAsync() => Provider.GetRequiredService<IDatabaseInitializer>().InitializeAsync();
    public async Task ReopenAsync()
    {
        await Provider.DisposeAsync();
        SqliteConnection.ClearAllPools();
        Provider = BuildProvider();
        await InitializeAsync();
    }
    public async ValueTask DisposeAsync()
    {
        // Closing a bound deck selector can leave its async load command finishing after the test body.
        var deckLoad = Provider.GetRequiredService<DecksViewModel>().LoadDeckCommand.ExecutionTask;
        if (deckLoad is not null) await deckLoad;
        await Provider.DisposeAsync();
        SqliteConnection.ClearAllPools();
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MTGDeckbuilder.Tests")) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(Paths.DataDirectory).StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Test cleanup must stay inside the isolated test root.");
        if (Directory.Exists(Paths.DataDirectory)) Directory.Delete(Paths.DataDirectory, recursive: true);
    }
}
