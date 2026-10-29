using MTGDeckbuilder.Desktop.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MTGDeckbuilder.Application;

namespace MTGDeckbuilder.Application.Tests;


public sealed partial class LegacyShell : ObservableObject
{
    private readonly IDatabaseInitializer _initializer;
    private readonly IBackupService _backups;
    private readonly Dictionary<string, PageViewModel> _pages;
    public UiOperations Operations { get; }
    public string Version => $"v{typeof(LegacyShell).Assembly.GetName().Version?.ToString(3)} · Fase 14 · Fysiek";
    [ObservableProperty] private PageViewModel currentPage;
    [ObservableProperty] private bool isReady;
    [ObservableProperty] private bool archiveExpanded;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsPaper)), NotifyPropertyChangedFor(nameof(IsDashboard)), NotifyPropertyChangedFor(nameof(IsCollection)),
     NotifyPropertyChangedFor(nameof(IsDecks)), NotifyPropertyChangedFor(nameof(IsSettings)), NotifyPropertyChangedFor(nameof(IsCatalog)), NotifyPropertyChangedFor(nameof(IsTransfer)), NotifyPropertyChangedFor(nameof(IsDeckTransfer)), NotifyPropertyChangedFor(nameof(IsPool)), NotifyPropertyChangedFor(nameof(IsGeneration))]
    private string currentKey = "paper";
    public bool IsPaper => CurrentKey.StartsWith("paper", StringComparison.Ordinal);
    public bool IsDashboard => CurrentKey == "dashboard";
    public bool IsCollection => CurrentKey == "collection";
    public bool IsDecks => CurrentKey == "decks";
    public bool IsSettings => CurrentKey == "settings";
    public bool IsCatalog => CurrentKey == "catalog";
    public bool IsTransfer => CurrentKey == "transfer";
    public bool IsDeckTransfer => CurrentKey == "deck-transfer";
    public bool IsPool => CurrentKey == "pool";
    public bool IsGeneration => CurrentKey == "generation";

    public LegacyShell(IDatabaseInitializer initializer, UiOperations operations, DashboardViewModel dashboard,
        CollectionViewModel collection, DecksViewModel decks, SettingsViewModel settings, CatalogViewModel catalog,
        CollectionTransferViewModel transfer, DeckTransferViewModel deckTransfer, ArchetypePoolViewModel pool, DeckGenerationViewModel generation, IBackupService backups, PaperViewModel paper)
    {
        _initializer = initializer;
        _backups = backups;
        Operations = operations;
        _pages = new() { ["dashboard"] = dashboard, ["collection"] = collection, ["decks"] = decks, ["settings"] = settings,
            ["catalog"] = catalog, ["transfer"] = transfer, ["deck-transfer"] = deckTransfer, ["pool"] = pool, ["generation"] = generation,
            ["paper"] = paper, ["paper-import"] = paper, ["paper-decks"] = paper };
        currentPage = paper;
    }

    [RelayCommand]
    private Task InitializeAsync() => Operations.RunAsync(async () =>
    {
        await _initializer.InitializeAsync();
        await CurrentPage.RefreshAsync();
        IsReady = true;
    }, "Lokale opslag gereed");

    [RelayCommand]
    private Task CancelStartupRestoreAsync() => Operations.RunAsync(async () =>
    {
        await _backups.CancelAsync();
        await _initializer.InitializeAsync(); await CurrentPage.RefreshAsync(); IsReady = true;
    }, "Gepland herstel geannuleerd; lokale opslag gereed");

    [RelayCommand]
    private void ToggleArchive() => ArchiveExpanded = !ArchiveExpanded;

    [RelayCommand]
    private Task NavigateAsync(string key)
    {
        if (!IsReady || !_pages.TryGetValue(key, out var page)) return Task.CompletedTask;
        return Operations.RunAsync(async () =>
        {
            CurrentKey = key;
            CurrentPage = page;
            if (page is PaperViewModel paper) paper.SelectedTab = key == "paper-import" ? 1 : key == "paper-decks" ? 2 : 0;
            else if (key != "settings") ArchiveExpanded = true;
            await page.RefreshAsync();
        }, "Gereed");
    }
}
