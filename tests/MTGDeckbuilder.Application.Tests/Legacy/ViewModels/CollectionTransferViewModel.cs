using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MTGDeckbuilder.Application;
using MTGDeckbuilder.Domain;

namespace MTGDeckbuilder.Desktop.ViewModels;

public sealed partial class CollectionTransferViewModel(ICollectionTransfer transfer,
    ILogger<CollectionTransferViewModel> logger) : PageViewModel
{
    private CancellationTokenSource? _cancellation;
    public ObservableCollection<CollectionPreviewRow> Rows { get; } = [];
    public ObservableCollection<CollectionTarget> Targets { get; } = [];
    public ObservableCollection<CollectionImportHistory> History { get; } = [];
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanInteract)), NotifyCanExecuteChangedFor(nameof(ApplyCommand)),
     NotifyCanExecuteChangedFor(nameof(UndoCommand)), NotifyCanExecuteChangedFor(nameof(DiscardCommand)),
     NotifyCanExecuteChangedFor(nameof(CancelCommand))] private bool isWorking;
    [ObservableProperty] private string status = "Selecteer een collectiebestand om wijzigingen voor te bereiden.";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasError))] private string? error;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasPreview)), NotifyPropertyChangedFor(nameof(IsPrepared)),
     NotifyPropertyChangedFor(nameof(Summary)), NotifyPropertyChangedFor(nameof(PageLabel)),
     NotifyPropertyChangedFor(nameof(PreviewHeading)),
     NotifyCanExecuteChangedFor(nameof(ApplyCommand)), NotifyCanExecuteChangedFor(nameof(DiscardCommand))]
    private CollectionPreview? preview;
    [ObservableProperty] private CollectionPreviewRow? selectedRow;
    [ObservableProperty] private CollectionTarget? selectedTarget;
    [ObservableProperty] private string targetSearch = "";
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(ApplyCommand))] private bool reviewed;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(UndoCommand))] private bool confirmUndo;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(UndoCommand))] private CollectionImportHistory? selectedHistory;
    [ObservableProperty] private int historyPage = 1;
    public bool CanInteract => !IsWorking;
    public bool HasError => !string.IsNullOrEmpty(Error);
    public bool HasPreview => Preview is not null;
    public bool IsPrepared => Preview?.State == CollectionImportState.Prepared;
    public string Summary => Preview?.Summary ?? "";
    public string PreviewHeading => Preview?.State switch
    {
        CollectionImportState.Applied => "Importdetails · Toegepast", CollectionImportState.Discarded => "Importdetails · Verworpen",
        CollectionImportState.Undone => "Importdetails · Hersteld", _ => "Controleer de voorbereiding"
    };
    public string PageLabel => Preview is null ? "" : $"Pagina {Preview.Page} van {Math.Max(1, (Preview.Total + 49) / 50)} · {Preview.FileName}";
    private bool CanApply => !IsWorking && Reviewed && Preview?.CanApply == true;
    private bool CanDiscard => !IsWorking && IsPrepared;
    private bool CanUndo => !IsWorking && ConfirmUndo && SelectedHistory?.CanUndo == true;
    private bool CanCancel => IsWorking;

    partial void OnSelectedRowChanged(CollectionPreviewRow? value)
    {
        Targets.Clear(); SelectedTarget = null; TargetSearch = "";
    }
    partial void OnSelectedHistoryChanged(CollectionImportHistory? value) => ConfirmUndo = false;

    public override async Task RefreshAsync()
    {
        if (IsWorking) return;
        await HistoryCoreAsync();
        if (Preview is { } preview) await LoadPreviewAsync(preview.Id, preview.Page);
    }
    private async Task HistoryCoreAsync()
    {
        var id = SelectedHistory?.Id;
        var items = await transfer.GetHistoryAsync(HistoryPage);
        History.Clear(); foreach (var item in items) History.Add(item);
        SelectedHistory = History.FirstOrDefault(x => x.Id == id) ?? History.FirstOrDefault();
    }
    private async Task LoadPreviewAsync(Guid id, int page = 1)
    {
        var number = SelectedRow?.Number;
        Preview = await transfer.GetPreviewAsync(id, page);
        Rows.Clear(); foreach (var row in Preview.Rows) Rows.Add(row);
        SelectedRow = Rows.FirstOrDefault(x => x.Number == number) ?? Rows.FirstOrDefault();
        Reviewed = false;
    }
    private async Task RunAsync(Func<CancellationToken, Task> action, string success)
    {
        if (IsWorking) return;
        IsWorking = true; Error = null; Status = "Bezig…";
        _cancellation = new CancellationTokenSource();
        try { await action(_cancellation.Token); Status = success; }
        catch (OperationCanceledException) { Status = "Geannuleerd. Er zijn geen gedeeltelijke collectiewijzigingen opgeslagen."; }
        catch (ArgumentException ex) { Error = ex.Message; Status = "Controleer de import"; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Collection transfer failed");
            Error = "De bewerking is mislukt. Controleer het bestand en probeer opnieuw. Technische details staan in het applicatielog.";
            Status = "Bewerking mislukt";
        }
        finally { _cancellation.Dispose(); _cancellation = null; IsWorking = false; }
    }
    public void HandleFilePickerError(Exception ex)
    {
        logger.LogError(ex, "Collection file picker failed"); Error = "Het bestandsvenster kon niet worden geopend. Probeer opnieuw.";
    }
    [RelayCommand]
    private Task PrepareAsync(string path) => RunAsync(async ct =>
    {
        Reviewed = false;
        var id = await transfer.PrepareAsync(path, ct);
        await LoadPreviewAsync(id); HistoryPage = 1; await HistoryCoreAsync();
    }, "Voorbereiding opgeslagen. Controleer de wijzigingen voordat je toepast.");
    [RelayCommand]
    private Task ExportAsync(string path) => RunAsync(ct => transfer.ExportAsync(path, ct), "Collectie geëxporteerd.");
    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel() => _cancellation?.Cancel();
    [RelayCommand]
    private Task CandidatesAsync() => RunAsync(async ct =>
    {
        if (Preview is null || SelectedRow is null) throw new ArgumentException("Selecteer eerst een importregel.");
        var matches = await transfer.GetCandidatesAsync(Preview.Id, SelectedRow.Number, ct);
        Targets.Clear(); foreach (var target in matches) Targets.Add(target);
        SelectedTarget = Targets.FirstOrDefault(x => x.Id == SelectedRow.PrintingId) ?? Targets.FirstOrDefault();
    }, "Mogelijke printings geladen (maximaal 50). Controleer naam, set, nummer en taal.");
    [RelayCommand]
    private Task SearchTargetsAsync() => RunAsync(async ct =>
    {
        var matches = await transfer.SearchTargetsAsync(TargetSearch, ct);
        Targets.Clear(); foreach (var target in matches) Targets.Add(target);
        SelectedTarget = Targets.FirstOrDefault();
    }, "Zoekresultaten geladen (maximaal 50). Handmatige printings zijn ook beschikbaar.");
    [RelayCommand]
    private Task LinkAsync() => ResolveAsync(false);
    [RelayCommand]
    private Task SkipAsync() => ResolveAsync(true);
    private Task ResolveAsync(bool skip) => RunAsync(async ct =>
    {
        if (Preview is null || SelectedRow is null) throw new ArgumentException("Selecteer eerst een importregel.");
        await transfer.ResolveAsync(Preview.Id, Preview.Version, SelectedRow.Number, SelectedTarget?.Id, skip, ct);
        await LoadPreviewAsync(Preview.Id, Preview.Page);
    }, skip ? "Regel expliciet overgeslagen." : "Koppeling opgeslagen. Controleer de gewijzigde voorbereiding.");
    [RelayCommand(CanExecute = nameof(CanApply))]
    private Task ApplyAsync() => RunAsync(async ct =>
    {
        var preview = Preview!;
        await transfer.ApplyAsync(preview.Id, preview.Version, ct);
        await LoadPreviewAsync(preview.Id, preview.Page); await HistoryCoreAsync();
    }, "Collectie-import volledig toegepast. Niet genoemde kaarten en decks zijn behouden.");
    [RelayCommand(CanExecute = nameof(CanDiscard))]
    private Task DiscardAsync() => RunAsync(async ct =>
    {
        var preview = Preview!;
        await transfer.DiscardAsync(preview.Id, preview.Version, ct);
        await LoadPreviewAsync(preview.Id, preview.Page); await HistoryCoreAsync();
    }, "Voorbereiding verworpen. De collectie is niet gewijzigd.");
    [RelayCommand(CanExecute = nameof(CanUndo))]
    private Task UndoAsync() => RunAsync(async ct =>
    {
        await transfer.UndoAsync(SelectedHistory!.Id, ct);
        if (Preview?.Id == SelectedHistory.Id) await LoadPreviewAsync(Preview.Id, Preview.Page);
        ConfirmUndo = false; await HistoryCoreAsync();
    }, "Import hersteld. Latere wijzigingen worden nooit overschreven.");
    [RelayCommand]
    private Task OpenHistoryAsync() => RunAsync(async _ =>
    {
        if (SelectedHistory is null) throw new ArgumentException("Selecteer eerst een import uit de historie.");
        await LoadPreviewAsync(SelectedHistory.Id);
    }, "Importdetails geopend.");
    [RelayCommand]
    private Task PreviousPageAsync() => RunAsync(async _ =>
    {
        if (Preview is { } preview && preview.Page > 1) await LoadPreviewAsync(preview.Id, preview.Page - 1);
    }, "Pagina geladen");
    [RelayCommand]
    private Task NextPageAsync() => RunAsync(async _ =>
    {
        if (Preview is { } preview && preview.Page * 50 < preview.Total) await LoadPreviewAsync(preview.Id, preview.Page + 1);
    }, "Pagina geladen");
    [RelayCommand]
    private Task PreviousHistoryAsync() => RunAsync(async _ => { HistoryPage = Math.Max(1, HistoryPage - 1); await HistoryCoreAsync(); }, "Historie geladen");
    [RelayCommand]
    private Task NextHistoryAsync() => RunAsync(async _ => { if (History.Count == 50) HistoryPage++; await HistoryCoreAsync(); }, "Historie geladen");
}
