using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MTGDeckbuilder.Application;
using MTGDeckbuilder.Domain;
using MTGDeckbuilder.Infrastructure;

namespace MTGDeckbuilder.Desktop.ViewModels;

public sealed partial class DashboardViewModel(DeckbuilderService service) : PageViewModel
{
    [ObservableProperty] private int uniquePrintings;
    [ObservableProperty] private int totalCards;
    [ObservableProperty] private int decks;
    [ObservableProperty] private string playerName = "Planeswalker";
    public override async Task RefreshAsync()
    {
        var summary = await service.GetSummaryAsync();
        UniquePrintings = summary.UniquePrintings;
        TotalCards = summary.TotalCards;
        Decks = summary.Decks;
        PlayerName = await service.GetPlayerNameAsync();
    }
}

public sealed partial class CollectionViewModel(DeckbuilderService service, UiOperations operations) : PageViewModel
{
    public ObservableCollection<CollectionItem> Items { get; } = [];
    public UiOperations Operations => operations;
    [ObservableProperty] private string cardName = "";
    [ObservableProperty] private string setCode = "";
    [ObservableProperty] private string collectorNumber = "";
    [ObservableProperty] private decimal quantity = 1;
    [ObservableProperty] private decimal selectedQuantity;
    [ObservableProperty] private CollectionItem? selectedItem;
    partial void OnSelectedItemChanged(CollectionItem? value) => SelectedQuantity = value?.Quantity ?? 0;

    public override async Task RefreshAsync()
    {
        var selectedId = SelectedItem?.PrintingId;
        var items = await service.GetCollectionAsync();
        Items.Clear();
        foreach (var item in items) Items.Add(item);
        SelectedItem = Items.FirstOrDefault(x => x.PrintingId == selectedId);
    }
    [RelayCommand]
    private Task SaveAsync() => operations.RunAsync(async () =>
    {
        await service.SaveCollectionAsync(new CollectionInput(CardName, SetCode, CollectorNumber, UiOperations.ReadQuantity(Quantity)));
        await RefreshAsync();
    }, "Collectie opgeslagen");
    [RelayCommand]
    private Task UpdateQuantityAsync() => operations.RunAsync(async () =>
    {
        if (SelectedItem is null) throw new ArgumentException("Selecteer eerst een kaart.");
        await service.SetCollectionQuantityAsync(SelectedItem.PrintingId, UiOperations.ReadQuantity(SelectedQuantity));
        await RefreshAsync();
    }, "Aantal opgeslagen");
}

public sealed partial class DecksViewModel(DeckbuilderService service, UiOperations operations,
    IDeckWorkspace workspace, CatalogService catalog, IDeckValidationStore validationStore, IDeckAdviceStore adviceStore) : PageViewModel
{
    public UiOperations Operations => operations;
    public ObservableCollection<DeckItem> Decks { get; } = [];
    public ObservableCollection<CollectionItem> Collection { get; } = [];
    public ObservableCollection<DeckCardItem> Cards { get; } = [];
    public ObservableCollection<CatalogItem> CatalogResults { get; } = [];
    public ObservableCollection<DeckNeed> MissingCards { get; } = [];
    public IReadOnlyList<string> Formats { get; } = [DeckRuleProfiles.Bo1, DeckRuleProfiles.Bo3, "Standard", "Alchemy", "Historic", "Explorer", "Timeless", "Brawl", "Overig"];
    public IReadOnlyList<RuleProfileOption> ValidationProfiles => StandardDeckValidator.Profiles;
    [ObservableProperty] private RuleProfileOption selectedValidationProfile = StandardDeckValidator.Profiles[0];
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasValidation))] private DeckValidation? validation;
    public bool HasValidation => Validation is not null;
    partial void OnSelectedValidationProfileChanged(RuleProfileOption value) { Validation = null; ClearAdvice(); }
    partial void OnSelectedDeckChanged(DeckItem? value) { Validation = null; ClearAdvice(); }
    public IReadOnlyList<DeckZone> Zones { get; } = [DeckZone.Mainboard, DeckZone.Sideboard, DeckZone.Commander];
    [ObservableProperty] private string deckName = "";
    [ObservableProperty] private string format = DeckRuleProfiles.Bo1;
    [ObservableProperty] private DeckItem? selectedDeck;
    [ObservableProperty] private CollectionItem? selectedPrinting;
    [ObservableProperty] private DeckCardItem? selectedCard;
    [ObservableProperty] private DeckZone zone;
    [ObservableProperty] private decimal quantity = 1;
    [ObservableProperty] private bool deleteConfirmation;
    [ObservableProperty] private string catalogSearch = "";
    [ObservableProperty] private CatalogItem? selectedCatalogItem;
    [ObservableProperty] private string catalogResultsInfo = "Zoek een kaart om buiten je bezit te plannen.";
    [ObservableProperty] private string exportText = "";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasAnalysis)), NotifyPropertyChangedFor(nameof(HasShortages)),
     NotifyPropertyChangedFor(nameof(HasNoShortages)), NotifyPropertyChangedFor(nameof(HasNoCards))] private DeckAnalysis? analysis;
    public bool HasAnalysis => Analysis is not null;
    public bool HasShortages => Analysis?.Needs.Any(x => x.Missing > 0 || x.ExactPrintingMissing > 0) == true;
    public bool HasNoCards => Analysis is { Mainboard: 0, Sideboard: 0, Commanders: 0 };
    public bool HasNoShortages => HasAnalysis && !HasNoCards && !HasShortages;

    public override async Task RefreshAsync()
    {
        var selectedId = SelectedDeck?.Id;
        var decks = await service.GetDecksAsync();
        Decks.Clear();
        foreach (var deck in decks) Decks.Add(deck);
        SelectedDeck = Decks.FirstOrDefault(x => x.Id == selectedId) ?? Decks.FirstOrDefault();
        var collection = await service.GetCollectionAsync();
        Collection.Clear();
        foreach (var item in collection) Collection.Add(item);
        SelectedPrinting = Collection.FirstOrDefault();
        DeleteConfirmation = false;
        await LoadCardsAsync();
    }
    private async Task LoadCardsAsync()
    {
        ClearAdvice();
        Validation = null;
        var profile = SelectedDeck is null ? DeckRuleProfile.Unspecified : await validationStore.GetProfileAsync(SelectedDeck.Id);
        SelectedValidationProfile = StandardDeckValidator.Profiles.Single(x => x.Id == profile);
        var cards = SelectedDeck is null ? [] : await service.GetDeckCardsAsync(SelectedDeck.Id);
        Cards.Clear();
        foreach (var card in cards) Cards.Add(card);
        SelectedCard = null;
        ExportText = "";
        Analysis = SelectedDeck is null ? null : await workspace.AnalyzeAsync(SelectedDeck.Id);
        MissingCards.Clear();
        if (Analysis is { } analysis)
            foreach (var need in analysis.Needs.Where(x => x.Missing > 0 || x.ExactPrintingMissing > 0)) MissingCards.Add(need);
    }
    [RelayCommand]
    private Task LoadDeckAsync() => operations.RunAsync(async () =>
    {
        DeleteConfirmation = false;
        await LoadCardsAsync();
    }, "Deck geladen");
    [RelayCommand]
    private Task CreateAsync() => operations.RunAsync(async () =>
    {
        var id = await service.CreateDeckAsync(DeckName, Format);
        await RefreshAsync();
        SelectedDeck = Decks.Single(x => x.Id == id);
        await LoadCardsAsync();
        DeckName = "";
    }, "Deck aangemaakt");
    [RelayCommand]
    private void RequestDelete() => DeleteConfirmation = SelectedDeck is not null;
    [RelayCommand]
    private void CancelDelete() => DeleteConfirmation = false;
    [RelayCommand]
    private Task DeleteAsync() => operations.RunAsync(async () =>
    {
        if (SelectedDeck is null || !DeleteConfirmation) return;
        await service.DeleteDeckAsync(SelectedDeck.Id);
        await RefreshAsync();
    }, "Deck verwijderd");
    [RelayCommand]
    private Task SaveCardAsync() => operations.RunAsync(async () =>
    {
        if (SelectedDeck is null || SelectedPrinting is null) throw new ArgumentException("Selecteer een deck en een collectiekaart.");
        await service.SetDeckCardAsync(SelectedDeck.Id, SelectedPrinting.PrintingId, Zone, UiOperations.ReadQuantity(Quantity));
        await LoadCardsAsync();
    }, "Deckkaart opgeslagen");
    [RelayCommand]
    private Task RemoveCardAsync() => operations.RunAsync(async () =>
    {
        if (SelectedDeck is null || SelectedCard is null) throw new ArgumentException("Selecteer eerst een deckkaart.");
        await service.SetDeckCardAsync(SelectedDeck.Id, SelectedCard.PrintingId, SelectedCard.Zone, 0);
        await LoadCardsAsync();
    }, "Kaart uit deck verwijderd");

    [RelayCommand]
    private Task SearchCatalogAsync() => operations.RunAsync(async () =>
    {
        if (CatalogSearch.Trim().Length < 2) throw new ArgumentException("Zoek met minstens twee tekens op kaartnaam.");
        var page = await catalog.SearchAsync(new CatalogQuery(Search: CatalogSearch));
        CatalogResults.Clear(); foreach (var item in page.Items) CatalogResults.Add(item);
        SelectedCatalogItem = CatalogResults.FirstOrDefault();
        CatalogResultsInfo = $"{page.Total} resultaten; eerste 50 getoond. Verfijn zo nodig je kaartnaam.";
    }, "Catalogus doorzocht");
    [RelayCommand]
    private Task AddCatalogCardAsync() => operations.RunAsync(async () =>
    {
        if (SelectedDeck is null || SelectedCatalogItem is null) throw new ArgumentException("Selecteer een deck en een catalogusprinting.");
        await service.SetDeckCardAsync(SelectedDeck.Id, SelectedCatalogItem.PrintingId, Zone, UiOperations.ReadQuantity(Quantity));
        await LoadCardsAsync();
    }, "Deckplanning opgeslagen; je bezit is niet gewijzigd");
    [RelayCommand]
    private Task AnalyzeAsync() => operations.RunAsync(LoadCardsAsync, "Deck en actuele bezitsanalyse geladen");
    [RelayCommand]
    private Task ValidateDeckAsync() => operations.RunAsync(async () =>
    {
        if (SelectedDeck is null) throw new ArgumentException("Selecteer eerst een deck.");
        Validation = null;
        var id = SelectedDeck.Id;
        await validationStore.SetProfileAsync(id, SelectedValidationProfile.Id);
        var result = await validationStore.ValidateAsync(id);
        if (SelectedDeck?.Id == id && SelectedValidationProfile.Id == result.Profile) Validation = result;
    }, "Controleprofiel bewaard en deck gecontroleerd");
    [RelayCommand]
    private Task GenerateExportAsync() => operations.RunAsync(async () =>
    {
        if (SelectedDeck is null) throw new ArgumentException("Selecteer eerst een deck.");
        ExportText = await workspace.ExportAsync(SelectedDeck.Id);
    }, "Decktekst gereed. Selecteer en kopieer de tekst of sla een tekstbestand op.");
    [RelayCommand]
    private Task SaveExportFileAsync(string path) => operations.RunAsync(async () =>
    {
        if (SelectedDeck is null) throw new ArgumentException("Selecteer eerst een deck.");
        if (!string.Equals(Path.GetExtension(path), ".txt", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Kies een tekstbestand met extensie .txt.");
        var text = await workspace.ExportAsync(SelectedDeck.Id);
        var full = Path.GetFullPath(path); var temp = full + $".{Guid.NewGuid():N}.tmp";
        try { await File.WriteAllTextAsync(temp, text); File.Move(temp, full, overwrite: true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
        ExportText = text;
    }, "Decktekst geëxporteerd");
    public Task HandleExportFilePickerErrorAsync(Exception ex) => operations.RunAsync(() => Task.FromException(ex), "");
}

