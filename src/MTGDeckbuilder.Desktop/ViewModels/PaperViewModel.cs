using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MTGDeckbuilder.Application;
using MTGDeckbuilder.Domain;

namespace MTGDeckbuilder.Desktop.ViewModels;

public sealed partial class PaperViewModel(IPaperWorkspace workspace, IPaperCatalog catalog, UiOperations operations, ILogger<PaperViewModel> logger) : PageViewModel, IDisposable
{
    public UiOperations Operations => operations;
    public ObservableCollection<PaperOwned> Collection { get; } = [];
    public ObservableCollection<PaperCard> SearchResults { get; } = [];
    public ObservableCollection<PaperImportRow> ImportRows { get; } = [];
    public ObservableCollection<PaperImportHistory> History { get; } = [];
    public ObservableCollection<PaperDeckInfo> Decks { get; } = [];
    public ObservableCollection<PaperDeckLine> DeckLines { get; } = [];
    public ObservableCollection<PaperAlternative> Alternatives { get; } = [];
    public ObservableCollection<PaperCard> Commanders { get; } = [];
    public DeckZone[] Zones { get; } = Enum.GetValues<DeckZone>();
    [ObservableProperty] private int selectedTab;
    [ObservableProperty] private string summary = "Fysieke collectie laden…";
    [ObservableProperty] private bool isDemoActive;
    [ObservableProperty] private string search = "";
    [ObservableProperty] private PaperOwned? selectedOwned;
    [ObservableProperty] private PaperCard? selectedCard;
    [ObservableProperty] private Bitmap? cardImage;
    [ObservableProperty] private string imageStatus = "Selecteer een kaart voor de afbeelding.";
    [ObservableProperty] private bool showBack;
    [ObservableProperty] private decimal ownedQuantity;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanApply))] private PaperImportPreview? importPreview;
    [ObservableProperty] private PaperImportRow? selectedImportRow;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanApply))] private bool confirmImport;
    [ObservableProperty] private bool confirmUndo;
    [ObservableProperty] private PaperImportHistory? selectedHistory;
    [ObservableProperty] private PaperDeckInfo? selectedDeck;
    [ObservableProperty] private PaperDeckLine? selectedDeckLine;
    [ObservableProperty] private PaperAlternative? selectedAlternative;
    [ObservableProperty] private string deckName = "Mijn fysieke deck";
    [ObservableProperty] private string deckSummary = "Selecteer een deck.";
    [ObservableProperty] private string deckDetails = "";
    [ObservableProperty] private SynergyAssessment? deckSynergy;
    [ObservableProperty] private string exportText = "";
    [ObservableProperty] private string deckImportText = "";
    [ObservableProperty] private string deckImportLanguage = "en";
    [ObservableProperty] private PaperDeckDraft? deckDraft;
    [ObservableProperty] private bool confirmDeckDraft;
    [ObservableProperty] private bool planningProposal;
    [ObservableProperty] private PaperCard? selectedCommander;
    [ObservableProperty] private string alternativeStatus = "Voorwaarden en volledige deckcontrole blijven nodig.";
    [ObservableProperty] private decimal deckQuantity = 1;
    [ObservableProperty] private DeckZone zone;
    [ObservableProperty] private bool standardProfile;
    [ObservableProperty] private bool commanderProfile;
    [ObservableProperty] private bool confirmDelete;
    public bool CanApply => ImportPreview?.CanApply == true && ConfirmImport;
    private CancellationTokenSource? _imageCancel;
    partial void OnSelectedOwnedChanged(PaperOwned? value) { if (value is not null) { SelectedCard = value.Card; OwnedQuantity = value.Quantity; } }
    public Task? ImageLoadTask { get; private set; }
    partial void OnSelectedCardChanged(PaperCard? value) { ShowBack = false; ImageLoadTask = LoadImageAsync(); }
    partial void OnShowBackChanged(bool value) => ImageLoadTask = LoadImageAsync();
    partial void OnSelectedDeckLineChanged(PaperDeckLine? value) { if (value is not null) { SelectedCard = value.Card; DeckQuantity = value.Quantity; Zone = value.Zone; } }
    partial void OnSelectedAlternativeChanged(PaperAlternative? value) { if (value is not null) SelectedCard = value.Card; }
    partial void OnSelectedCommanderChanged(PaperCard? value) { if (value is not null && SelectedTab == 2) SelectedCard = value; }
    partial void OnCommanderProfileChanged(bool value) { if (value) StandardProfile = false; }
    partial void OnStandardProfileChanged(bool value) { if (value) CommanderProfile = false; }
    partial void OnSelectedDeckChanged(PaperDeckInfo? value) { ConfirmDelete = false; DeckLines.Clear(); DeckSummary = "Klik Deck laden/controleren."; DeckDetails = ""; DeckSynergy=null; ExportText = ""; Alternatives.Clear(); }
    private async Task LoadImageAsync()
    {
        _imageCancel?.Cancel(); _imageCancel?.Dispose(); var cancellation = _imageCancel = new CancellationTokenSource();
        var selected = SelectedCard; var back = ShowBack; var old = CardImage; CardImage = null; old?.Dispose();
        if (selected is null) { ImageStatus = "Selecteer een kaart."; return; }
        ImageStatus = "Afbeelding laden via Scryfall/cache…";
        try
        {
            var path = await catalog.ImageAsync(selected, back, cancellation.Token);
            if (cancellation.IsCancellationRequested || SelectedCard != selected || ShowBack != back) return;
            if (path is null) { ImageStatus = "Geen afbeelding voor deze zijde beschikbaar."; return; }
            var bitmap = await Task.Run(() => new Bitmap(path), cancellation.Token);
            if (cancellation.IsCancellationRequested || SelectedCard != selected || ShowBack != back) { bitmap.Dispose(); return; }
            CardImage = bitmap; ImageStatus = "Scryfall-afbeelding · exacte geselecteerde printing · lokaal gecachet";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { logger.LogWarning(ex, "Paper card image unavailable"); if (!cancellation.IsCancellationRequested) ImageStatus = "Afbeelding niet beschikbaar. Controleer de verbinding of kies opnieuw; kaartgegevens blijven bruikbaar."; }
    }
    public override async Task RefreshAsync()
    {
        await workspace.InitializeExampleAsync(); var stats=await workspace.SummaryAsync();Summary=stats.Label;IsDemoActive=stats.DemoActive;
        var own = await workspace.CollectionAsync(); LoadFilters(own);
        var oldCommander = SelectedCommander;
        Commanders.Clear(); foreach (var card in own.Select(x => x.Card).Where(x => x.IsCommanderCandidate)) Commanders.Add(card);
        SelectedCommander = Commanders.FirstOrDefault(c => c.Id == oldCommander?.Id) ?? Commanders.FirstOrDefault(c => c.Name == "Cloud, Planet's Champion") ?? Commanders.FirstOrDefault();
        var decks = await workspace.DecksAsync(); var previous = SelectedDeck?.Id;
        Decks.Clear(); foreach (var d in decks) Decks.Add(d); SelectedDeck = Decks.FirstOrDefault(x => x.Id == previous) ?? Decks.FirstOrDefault();
        await ReloadHistory();
    }
    private async Task ReloadHistory() { var rows = await workspace.HistoryAsync(); History.Clear(); foreach (var row in rows) History.Add(row); SelectedHistory = History.FirstOrDefault(); }
    [RelayCommand] private Task ReloadAsync() => operations.RunAsync(RefreshAsync, "Fysieke collectie vernieuwd");
    [RelayCommand] private Task SearchOnlineAsync() => operations.RunAsync(async () =>
    { var rows = await catalog.SearchAsync(Search); SearchResults.Clear(); foreach (var row in rows) SearchResults.Add(row); SelectedCard = SearchResults.FirstOrDefault(); }, "Eerste 40 fysieke Scryfall-printings geladen; verfijn zo nodig je zoekopdracht.");
    [RelayCommand] private Task RefreshCardAsync() => operations.RunAsync(async () =>
    { if (SelectedCard is not { } c) throw new ArgumentException("Selecteer een kaart."); SelectedCard = await catalog.GetAsync(c.Id, true); }, "Kaart en papieren legaliteit vernieuwd");
    [RelayCommand] private Task SetOwnedAsync() => operations.RunAsync(async () =>
    { if (SelectedCard is not { } c) throw new ArgumentException("Selecteer een kaart."); await workspace.SetOwnedAsync(c.Id, UiOperations.ReadQuantity(OwnedQuantity)); await RefreshAsync(); }, "Handmatige map bijgewerkt; echte invoer vervangt demo als actieve voorraadbron.");
    [RelayCommand] private Task PrepareCsvAsync(string path) => operations.RunAsync(async () =>
    { ImportPreview = null; ConfirmImport = false; ShowImport(await workspace.PrepareCsvAsync(path)); await ReloadHistory(); }, "CSV voorbereid; controleer identiteit, uitvoering, map en alle regels vóór toepassen.");
    private void ShowImport(PaperImportPreview preview) { ImportPreview = preview; ConfirmImport = false; ImportRows.Clear(); foreach (var row in preview.Rows) ImportRows.Add(row); SelectedImportRow = ImportRows.FirstOrDefault(); }
    [RelayCommand] private Task LoadImportAsync() => operations.RunAsync(async () =>
    { if (SelectedHistory is null) throw new ArgumentException("Kies een import."); ShowImport(await workspace.PreviewAsync(SelectedHistory.Id)); }, "Importvoorbeeld geladen");
    [RelayCommand] private Task PreviewRowAsync() => operations.RunAsync(async () =>
    { if (SelectedImportRow?.PrintingId is not { } id) throw new ArgumentException("Deze regel heeft nog geen doelprinting. Zoek en koppel eerst de juiste kaart."); SelectedCard = await catalog.GetAsync(id); }, "Doelprinting voor geselecteerde CSV-regel geladen");
    [RelayCommand] private Task LinkRowAsync() => ResolveRow(false);
    [RelayCommand] private Task SkipRowAsync() => ResolveRow(true);
    private Task ResolveRow(bool skip) => operations.RunAsync(async () =>
    { if (ImportPreview is not { } p || SelectedImportRow is not { } row) throw new ArgumentException("Kies een regel."); ShowImport(await workspace.ResolveAsync(p.Id, p.Version, row.Source.Number, SelectedCard?.Id, skip)); }, "Regel expliciet gecontroleerd");
    [RelayCommand] private Task ApplyImportAsync() => operations.RunAsync(async () =>
    { if (!CanApply || ImportPreview is not { } p) throw new ArgumentException("Controleer en bevestig eerst het volledige importvoorbeeld."); await workspace.ApplyAsync(p.Id, p.Version); ShowImport(await workspace.PreviewAsync(p.Id)); await RefreshAsync(); }, "CSV toegepast. Aantallen absoluut per printing/taal/finish/conditie/map; demo niet meer meegeteld.");
    [RelayCommand] private Task UndoImportAsync() => operations.RunAsync(async () =>
    { if (!ConfirmUndo || SelectedHistory is not { } h) throw new ArgumentException("Kies en bevestig de import om te herstellen."); await workspace.UndoAsync(h.Id); ConfirmUndo = false; await RefreshAsync(); }, "Import hersteld zonder latere wijzigingen te overschrijven");
    [RelayCommand] private Task LoadDeckAsync() => operations.RunAsync(LoadDeck, "Fysiek deck gecontroleerd");
    private async Task LoadDeck()
    { if (SelectedDeck is null) throw new ArgumentException("Kies een deck."); var d = await workspace.DeckAsync(SelectedDeck.Id); DeckLines.Clear(); foreach (var row in d.Lines) DeckLines.Add(row); SelectedDeckLine = DeckLines.FirstOrDefault(); StandardProfile = d.Deck.Profile == "standard"; CommanderProfile = d.Deck.Profile == "commander"; DeckSummary = d.Summary; DeckDetails = d.Details;DeckSynergy=d.Synergy; }
    [RelayCommand] private Task CreateDeckAsync() => operations.RunAsync(async () =>
    { var id = await workspace.CreateDeckAsync(DeckName); await RefreshAsync(); SelectedDeck = Decks.Single(x => x.Id == id); await LoadDeck(); }, "Nieuw fysiek deck opgeslagen");
    [RelayCommand] private Task SaveDeckCardAsync() => operations.RunAsync(async () =>
    { if (SelectedDeck is null || SelectedCard is null) throw new ArgumentException("Selecteer deck en kaart."); await workspace.SetDeckCardAsync(SelectedDeck.Id, SelectedCard.Id, Zone, UiOperations.ReadQuantity(DeckQuantity)); await LoadDeck(); }, "Deckregel opgeslagen; bezit ongewijzigd");
    [RelayCommand] private Task SaveProfileAsync() => operations.RunAsync(async () =>
    { if (SelectedDeck is null) throw new ArgumentException("Kies een deck."); await workspace.SetProfileAsync(SelectedDeck.Id, CommanderProfile ? "commander" : StandardProfile ? "standard" : "casual60"); await LoadDeck(); }, "Papieren controleprofiel bewaard");
    [RelayCommand] private Task ExportDeckAsync() => operations.RunAsync(async () =>
    { if (SelectedDeck is null) throw new ArgumentException("Kies een deck."); ExportText = await workspace.ExportDeckAsync(SelectedDeck.Id); }, "Decktekst gereed om te kopiëren");
    [RelayCommand] private Task RefreshDeckSourcesAsync() => operations.RunAsync(async () =>
    { if (SelectedDeck is null) throw new ArgumentException("Kies een deck."); var d = await workspace.DeckAsync(SelectedDeck.Id); foreach (var row in d.Lines.Select(x => x.Card.Id).Distinct()) await catalog.GetAsync(row, true); await LoadDeck(); }, "Deckkaarten en papieren legaliteit bij Scryfall vernieuwd");
    [RelayCommand] private Task PrepareDeckTextAsync() => operations.RunAsync(async () =>
    { DeckDraft = null; ConfirmDeckDraft = false; DeckDraft = await workspace.PrepareDeckTextAsync(DeckImportText, DeckName, DeckImportLanguage); }, "Decktekst voorbereid; bezit en bestaande decks ongewijzigd");
    [RelayCommand] private Task PrepareProposalAsync() => operations.RunAsync(async () =>
    { DeckDraft = null; ConfirmDeckDraft = false; DeckDraft = await workspace.PrepareProposalAsync(DeckName, PlanningProposal); }, "Begrensd fysiek Conflux-voorstel voorbereid; controleer vóór opslag");
    [RelayCommand] private Task UseCommander()
    {
        if (SelectedCard?.IsCommanderCandidate == true) { if (!Commanders.Any(c => c.Id == SelectedCard.Id)) Commanders.Add(SelectedCard); SelectedCommander = SelectedCard; return Task.CompletedTask; }
        return operations.RunAsync(() => throw new ArgumentException("Selecteer eerst een ondersteunde commander in collectie of Scryfall."), "");
    }
    [RelayCommand] private Task PrepareCommanderAsync() => operations.RunAsync(async () =>
    { DeckDraft = null; ConfirmDeckDraft = false; if (SelectedCommander is null) throw new ArgumentException("Kies een commander."); DeckDraft = await workspace.PrepareCommanderAsync(DeckName, SelectedCommander.Id, PlanningProposal); }, "Commander-voorstel voorbereid; controleer de volledige lijst en alle tekorten");
    [RelayCommand] private Task PrepareDeckCopyAsync() => PrepareCopy(false);
    [RelayCommand] private Task PrepareReplacementAsync() => PrepareCopy(true);
    private Task PrepareCopy(bool replace) => operations.RunAsync(async () =>
    {
        DeckDraft = null; ConfirmDeckDraft = false;
        if (SelectedDeck is null) throw new ArgumentException("Kies een deck.");
        if (replace && (SelectedDeckLine is null || SelectedAlternative is null)) throw new ArgumentException("Kies een mainboardregel en lokaal alternatief.");
        DeckDraft = await workspace.PrepareDeckCopyAsync(SelectedDeck.Id, DeckName,
            replace ? SelectedDeckLine!.Card.Id : null, replace ? SelectedAlternative!.Card.Id : null,
            replace ? UiOperations.ReadQuantity(DeckQuantity) : 0);
    }, "Voorbeeld van nieuw fysiek deck gereed");
    [RelayCommand] private Task SaveDeckDraftAsync() => operations.RunAsync(async () =>
    { if (!ConfirmDeckDraft || DeckDraft is null) throw new ArgumentException("Controleer en bevestig eerst het deckvoorbeeld."); var id = await workspace.SaveDeckDraftAsync(DeckDraft); ConfirmDeckDraft = false; DeckDraft = null; await RefreshAsync(); SelectedDeck = Decks.Single(x => x.Id == id); await LoadDeck(); }, "Als nieuw fysiek deck opgeslagen; bron en bezit behouden");
    [RelayCommand] private Task FindAlternativesAsync() => operations.RunAsync(async () =>
    { if (SelectedDeck is null || SelectedDeckLine is null) throw new ArgumentException("Kies een deckregel."); var rows = await workspace.AlternativesAsync(SelectedDeck.Id, SelectedDeckLine.Card.Id); Alternatives.Clear(); foreach (var row in rows) Alternatives.Add(row); SelectedAlternative = Alternatives.FirstOrDefault(); AlternativeStatus = rows.Count == 0 ? "Geen bewezen passende lokale optie binnen beperkte rolherkenning. Onbekend is geen slechte kaart." : "Beperkte lokale opties; voorwaarden en volledige deck-/manacontrole blijven nodig. Geen AI-optimizer."; }, "Fysieke alternatieven gecontroleerd");
    [RelayCommand] private Task DeleteDeckAsync() => operations.RunAsync(async () =>
    { if (!ConfirmDelete || SelectedDeck is null) throw new ArgumentException("Selecteer en bevestig het deck om te verwijderen."); await workspace.DeleteDeckAsync(SelectedDeck.Id); await RefreshAsync(); }, "Fysiek deck verwijderd; bezit behouden");
    public Task PickerErrorAsync(Exception ex) => operations.RunAsync(() => Task.FromException(ex), "");
    public void Dispose() { _filterGeneration++;_filterCancel?.Cancel();_filterCancel?.Dispose();_imageCancel?.Cancel(); _imageCancel?.Dispose(); CardImage?.Dispose(); }
}
