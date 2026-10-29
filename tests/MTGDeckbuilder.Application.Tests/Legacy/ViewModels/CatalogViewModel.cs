using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MTGDeckbuilder.Application;

namespace MTGDeckbuilder.Desktop.ViewModels;

public sealed record CatalogFilterOption(string Value, string Label);

public sealed partial class CatalogViewModel(CatalogService service, ICatalogImporter importer,
    UiOperations operations, ILogger<CatalogViewModel> logger) : PageViewModel
{
    private CancellationTokenSource? _importCancellation;
    private int _importGeneration;
    public UiOperations Operations => operations;
    public ObservableCollection<CatalogItem> Items { get; } = [];
    public ObservableCollection<CatalogSet> Sets { get; } = [new("", "Alle sets")];
    public ObservableCollection<ImportHistoryItem> History { get; } = [];
    public IReadOnlyList<CatalogFilterOption> Colors { get; } =
    [new("", "Alle kleuren"), new("W", "Bevat wit"), new("U", "Bevat blauw"), new("B", "Bevat zwart"),
     new("R", "Bevat rood"), new("G", "Bevat groen"), new("C", "Kleurloos"), new("M", "Meerkleurig")];
    public IReadOnlyList<CatalogFilterOption> Rarities { get; } =
    [new("", "Alle zeldzaamheden"), new("common", "Common"), new("uncommon", "Uncommon"),
     new("rare", "Rare"), new("mythic", "Mythic"), new("special", "Special"), new("bonus", "Bonus")];

    [ObservableProperty] private string searchText = "";
    [ObservableProperty] private string typeFilter = "";
    [ObservableProperty] private CatalogSet? selectedSet;
    [ObservableProperty] private CatalogFilterOption? selectedColor;
    [ObservableProperty] private CatalogFilterOption? selectedRarity;
    [ObservableProperty] private decimal? maxManaValue;
    [ObservableProperty] private bool ownedOnly;
    [ObservableProperty] private string catalogSummary = "Nog geen catalogus geladen";
    [ObservableProperty] private string progressText = "Importeer de catalogus om Arena-kaarten te doorzoeken.";
    [ObservableProperty] private bool showImport;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanInteract)), NotifyCanExecuteChangedFor(nameof(ImportOnlineCommand)),
     NotifyCanExecuteChangedFor(nameof(ImportFileCommand)), NotifyCanExecuteChangedFor(nameof(ResumeImportCommand)),
     NotifyCanExecuteChangedFor(nameof(CancelImportCommand))]
    private bool isImporting;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasSelection))] private CatalogItem? selectedItem;
    [ObservableProperty] private decimal ownedQuantity = 1;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(ResumeImportCommand))] private ImportHistoryItem? selectedImport;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PageInfo)), NotifyPropertyChangedFor(nameof(PageCount)),
     NotifyCanExecuteChangedFor(nameof(NextPageCommand)), NotifyCanExecuteChangedFor(nameof(PreviousPageCommand))]
    private int total;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PageInfo)), NotifyCanExecuteChangedFor(nameof(NextPageCommand)),
     NotifyCanExecuteChangedFor(nameof(PreviousPageCommand))]
    private int page = 1;

    public bool CanInteract => !IsImporting;
    public bool HasSelection => SelectedItem is not null;
    public int PageCount => Math.Max(1, (Total + 49) / 50);
    public string PageInfo => $"{Total:N0} resultaten · pagina {Page} van {PageCount}";
    private bool CanResume => !IsImporting && SelectedImport?.CanResume == true;
    private bool CanCancel => IsImporting;
    private bool CanNext => !IsImporting && Page < PageCount;
    private bool CanPrevious => !IsImporting && Page > 1;
    partial void OnSelectedItemChanged(CatalogItem? value) => OwnedQuantity = value?.OwnedQuantity > 0 ? value.OwnedQuantity : 1;

    public override async Task RefreshAsync()
    {
        var status = await service.GetStatusAsync();
        CatalogSummary = $"{status.Printings:N0} Arena-printings · {status.Cards:N0} kaarten" +
            (status.UpdatedUtc is { } date ? $" · bijgewerkt {date.ToLocalTime():dd-MM-yyyy HH:mm}" : " · nog geen import voltooid");
        var setCode = SelectedSet?.Code ?? "";
        Sets.Clear(); Sets.Add(new CatalogSet("", "Alle sets"));
        foreach (var set in await service.GetSetsAsync()) Sets.Add(set);
        SelectedSet = Sets.FirstOrDefault(x => x.Code == setCode) ?? Sets[0];
        var selectedRun = SelectedImport?.Id;
        History.Clear();
        foreach (var run in await service.GetHistoryAsync()) History.Add(run);
        SelectedImport = History.FirstOrDefault(x => x.Id == selectedRun) ?? History.FirstOrDefault();
        if (status.Printings == 0) ShowImport = true;
        await SearchCoreAsync();
    }

    private async Task SearchCoreAsync()
    {
        var selected = SelectedItem?.PrintingId;
        var result = await service.SearchAsync(new CatalogQuery(SearchText, SelectedSet?.Code ?? "",
            SelectedRarity?.Value ?? "", SelectedColor?.Value ?? "", TypeFilter,
            MaxManaValue is { } mana ? (double)mana : null, OwnedOnly, Page));
        Total = result.Total; Page = result.Page;
        Items.Clear();
        foreach (var item in result.Items) Items.Add(item);
        SelectedItem = Items.FirstOrDefault(x => x.PrintingId == selected) ?? Items.FirstOrDefault();
    }

    [RelayCommand]
    private Task SearchAsync() => operations.RunAsync(async () => { Page = 1; await SearchCoreAsync(); }, "Catalogus doorzocht");
    [RelayCommand]
    private Task ResetFiltersAsync() => operations.RunAsync(async () =>
    {
        SearchText = ""; TypeFilter = ""; SelectedSet = Sets[0]; SelectedColor = Colors[0];
        SelectedRarity = Rarities[0]; MaxManaValue = null; OwnedOnly = false; Page = 1;
        await SearchCoreAsync();
    }, "Filters gewist");
    [RelayCommand(CanExecute = nameof(CanNext))]
    private Task NextPageAsync() => operations.RunAsync(async () => { Page++; await SearchCoreAsync(); }, "Volgende pagina");
    [RelayCommand(CanExecute = nameof(CanPrevious))]
    private Task PreviousPageAsync() => operations.RunAsync(async () => { Page--; await SearchCoreAsync(); }, "Vorige pagina");
    [RelayCommand]
    private Task SaveOwnedAsync() => operations.RunAsync(async () =>
    {
        if (SelectedItem is null) throw new ArgumentException("Selecteer eerst een catalogusprinting.");
        await service.SetOwnedQuantityAsync(SelectedItem.PrintingId, UiOperations.ReadQuantity(OwnedQuantity));
        await SearchCoreAsync();
    }, "Collectieaantal opgeslagen");

    [RelayCommand(CanExecute = nameof(CanInteract))]
    private Task ImportOnlineAsync() => RunImportAsync((progress, ct) => importer.ImportOnlineAsync(progress, ct));
    [RelayCommand(CanExecute = nameof(CanInteract))]
    private Task ImportFileAsync(string path) => RunImportAsync((progress, ct) => importer.ImportFileAsync(path, progress, ct));
    [RelayCommand(CanExecute = nameof(CanResume))]
    private Task ResumeImportAsync() => SelectedImport is null ? Task.CompletedTask :
        RunImportAsync((progress, ct) => importer.ResumeAsync(SelectedImport.Id, progress, ct));
    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void CancelImport() => _importCancellation?.Cancel();

    private async Task RunImportAsync(Func<IProgress<ImportProgress>, CancellationToken, Task<ImportResult>> action)
    {
        if (IsImporting) return;
        _importCancellation = new CancellationTokenSource();
        var cancellation = _importCancellation;
        var generation = ++_importGeneration;
        IsImporting = true; ShowImport = true; operations.ErrorMessage = null;
        operations.Status = "Catalogusimport bezig"; ProgressText = "Import voorbereiden…";
        try
        {
            var progress = new Progress<ImportProgress>(p =>
            {
                if (IsImporting && generation == _importGeneration) ProgressText = p.Message;
            });
            var result = await action(progress, cancellation.Token);
            ProgressText = result.AlreadyCurrent ? "De catalogus gebruikt de actuele Scryfall-bronversie." :
                $"Voltooid: {result.Added:N0} nieuwe en {result.Updated:N0} bijgewerkte printings. Collectieaantallen behouden.";
            if (result.UnlinkedManualPrintings > 0)
                ProgressText += $" {result.UnlinkedManualPrintings} handmatige printings zijn wegens een onzekere koppeling apart behouden.";
            ShowImport = result.UnlinkedManualPrintings > 0;
            operations.Status = "Catalogusimport voltooid";
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            ProgressText = "Import gepauzeerd. De catalogus is ongewijzigd. Selecteer de import in de geschiedenis om te hervatten.";
            operations.Status = "Import gepauzeerd";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Catalog import UI operation failed");
            operations.ErrorMessage = ex is ArgumentException or InvalidDataException ? ex.Message :
                "De catalogusimport is mislukt. Controleer verbinding/bestand en bekijk de importgeschiedenis of het logboek.";
            ProgressText = "Import niet voltooid. De bestaande catalogus en collectie zijn behouden.";
            operations.Status = "Catalogusimport mislukt";
        }
        finally
        {
            IsImporting = false; cancellation.Dispose(); _importCancellation = null;
            NextPageCommand.NotifyCanExecuteChanged(); PreviousPageCommand.NotifyCanExecuteChanged();
            try { await RefreshAsync(); }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not refresh catalog after import");
                operations.ErrorMessage ??= "Vernieuwen mislukt. Open de catalogus opnieuw.";
            }
        }
    }

    public void HandleFilePickerError(Exception ex)
    {
        logger.LogError(ex, "Could not open import file picker");
        operations.ErrorMessage = "Het importbestand kon niet worden gekozen. Probeer opnieuw.";
    }
}
