using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MTGDeckbuilder.Application;
using MTGDeckbuilder.Domain;

namespace MTGDeckbuilder.Desktop.ViewModels;

public sealed partial class DeckTransferViewModel(IDeckWorkspace workspace, ILogger<DeckTransferViewModel> logger) : PageViewModel
{
    private CancellationTokenSource? _cancel;
    private string _preparedText = "";
    public ObservableCollection<DeckImportPreviewRow> Rows { get; } = [];
    public ObservableCollection<CollectionTarget> Targets { get; } = [];
    public ObservableCollection<DeckImportHistory> History { get; } = [];
    public IReadOnlyList<string> Formats { get; } = [DeckRuleProfiles.Bo1, DeckRuleProfiles.Bo3, "Standard", "Alchemy", "Historic", "Explorer", "Timeless", "Brawl", "Overig"];
    [ObservableProperty, NotifyPropertyChangedFor(nameof(SourceChanged))] private string sourceText = "";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(SourceChanged))] private string deckName = "";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(SourceChanged))] private string format = DeckRuleProfiles.Bo1;
    [ObservableProperty] private string targetSearch = "";
    [ObservableProperty] private string status = "Plak een Arena-decklijst of kies een tekstbestand. Er wordt eerst een voorbereiding gemaakt.";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasError))] private string? error;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanInteract)), NotifyCanExecuteChangedFor(nameof(ApplyCommand)),
     NotifyCanExecuteChangedFor(nameof(DiscardCommand)), NotifyCanExecuteChangedFor(nameof(CancelCommand))] private bool isWorking;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasPreview)), NotifyPropertyChangedFor(nameof(IsPrepared)),
     NotifyPropertyChangedFor(nameof(Summary)), NotifyPropertyChangedFor(nameof(PageLabel)),
     NotifyPropertyChangedFor(nameof(SourceChanged)),
     NotifyCanExecuteChangedFor(nameof(ApplyCommand)), NotifyCanExecuteChangedFor(nameof(DiscardCommand))] private DeckImportPreview? preview;
    [ObservableProperty] private DeckImportPreviewRow? selectedRow;
    [ObservableProperty] private CollectionTarget? selectedTarget;
    [ObservableProperty] private DeckImportHistory? selectedHistory;
    [ObservableProperty] private int historyPage = 1;
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(ApplyCommand))] private bool reviewed;
    public bool CanInteract => !IsWorking;
    public bool HasError => Error is not null;
    public bool HasPreview => Preview is not null;
    public bool IsPrepared => Preview?.State == DeckImportState.Prepared;
    public string Summary => Preview?.Summary ?? "";
    public string PageLabel => Preview is null ? "" : $"Pagina {Preview.Page} van {Math.Max(1, (Preview.Total + 49) / 50)} · " +
        (Preview.State == DeckImportState.Prepared ? "Voorbereid" : Preview.State == DeckImportState.Applied ? "Deck aangemaakt" : "Verworpen");
    public bool SourceChanged => Preview is not null && (SourceText.Trim() != _preparedText || DeckName.Trim() != Preview.Name || Format != Preview.Format);
    private bool CanApply => !IsWorking && Reviewed && Preview?.CanApply == true && !SourceChanged;
    private bool CanDiscard => !IsWorking && IsPrepared;
    private bool CanCancel => IsWorking;
    partial void OnSourceTextChanged(string value) { Reviewed = false; ApplyCommand.NotifyCanExecuteChanged(); }
    partial void OnDeckNameChanged(string value) { Reviewed = false; ApplyCommand.NotifyCanExecuteChanged(); }
    partial void OnFormatChanged(string value) { Reviewed = false; ApplyCommand.NotifyCanExecuteChanged(); }
    partial void OnSelectedRowChanged(DeckImportPreviewRow? value) { Targets.Clear(); SelectedTarget = null; }
    public override async Task RefreshAsync()
    {
        if (IsWorking) return;
        await HistoryCoreAsync();
        if (Preview is { } p) await LoadAsync(p.Id, p.Page, restoreInput: false);
    }
    private async Task HistoryCoreAsync()
    {
        var selected = SelectedHistory?.Id;
        var items = await workspace.GetHistoryAsync(HistoryPage);
        History.Clear(); foreach (var item in items) History.Add(item);
        SelectedHistory = History.FirstOrDefault(x => x.Id == selected) ?? History.FirstOrDefault();
    }
    private async Task LoadAsync(Guid id, int page = 1, bool restoreInput = true)
    {
        var number = SelectedRow?.Number;
        Preview = await workspace.GetPreviewAsync(id, page);
        if (restoreInput)
        {
            SourceText = Preview.SourceText; _preparedText = SourceText.Trim(); DeckName = Preview.Name; Format = Preview.Format;
        }
        Rows.Clear(); foreach (var row in Preview.Rows) Rows.Add(row);
        SelectedRow = Rows.FirstOrDefault(x => x.Number == number) ?? Rows.FirstOrDefault(); Reviewed = false;
        OnPropertyChanged(nameof(SourceChanged));
    }
    private async Task RunAsync(Func<CancellationToken, Task> action, string success)
    {
        if (IsWorking) return;
        IsWorking = true; Error = null; Status = "Bezig…"; _cancel = new CancellationTokenSource();
        try { await action(_cancel.Token); Status = success; }
        catch (OperationCanceledException) { Status = "Geannuleerd. Er is geen gedeeltelijk deck opgeslagen."; }
        catch (ArgumentException ex) { Error = ex.Message; Status = "Controleer de deckinvoer"; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Deck transfer failed");
            Error = "De deckbewerking is mislukt. Probeer opnieuw. Technische details staan in het applicatielog.";
            Status = "Bewerking mislukt";
        }
        finally { _cancel.Dispose(); _cancel = null; IsWorking = false; }
    }
    public void HandleFilePickerError(Exception ex) { logger.LogError(ex, "Deck file picker failed"); Error = "Het bestandsvenster kon niet worden geopend."; }
    [RelayCommand]
    private Task PrepareAsync() => RunAsync(async ct =>
    {
        Reviewed = false;
        var id = await workspace.PrepareAsync(SourceText, DeckName, Format, ct);
        await LoadAsync(id); HistoryPage = 1; await HistoryCoreAsync();
    }, "Voorbereiding opgeslagen. Controleer de gekoppelde printings; bezit blijft ongewijzigd.");
    [RelayCommand]
    private Task ReadFileAsync(string path) => RunAsync(async ct =>
    {
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        if (file.Length > ArenaDeckText.MaxBytes) throw new ArgumentException("Deckbestanden mogen maximaal 256 KiB groot zijn.");
        using var reader = new StreamReader(file, new UTF8Encoding(false, true), true);
        var buffer = new char[ArenaDeckText.MaxBytes + 1];
        var count = await reader.ReadBlockAsync(buffer.AsMemory(), ct);
        if (count > ArenaDeckText.MaxBytes) throw new ArgumentException("Deckbestanden mogen maximaal 256 KiB groot zijn.");
        SourceText = new string(buffer, 0, count);
        if (Encoding.UTF8.GetByteCount(SourceText) > ArenaDeckText.MaxBytes) throw new ArgumentException("Deckbestanden mogen maximaal 256 KiB groot zijn.");
    }, "Tekstbestand geladen. Kies Voorbereiden om de kaartkoppelingen te controleren.");
    [RelayCommand(CanExecute = nameof(CanCancel))] private void Cancel() => _cancel?.Cancel();
    [RelayCommand] private Task CandidatesAsync() => RunAsync(async ct =>
    {
        if (Preview is null || SelectedRow is null) throw new ArgumentException("Selecteer een deckregel.");
        var items = await workspace.GetCandidatesAsync(Preview.Id, SelectedRow.Number, ct);
        Targets.Clear(); foreach (var item in items) Targets.Add(item);
        SelectedTarget = Targets.FirstOrDefault(x => x.Id == SelectedRow.PrintingId) ?? Targets.FirstOrDefault();
    }, "Mogelijke printings geladen, maximaal 50.");
    [RelayCommand] private Task SearchTargetsAsync() => RunAsync(async ct =>
    {
        var items = await workspace.SearchTargetsAsync(TargetSearch, ct);
        Targets.Clear(); foreach (var item in items) Targets.Add(item); SelectedTarget = Targets.FirstOrDefault();
    }, "Printings geladen, maximaal 50. Bezit is niet vereist om een deck te plannen.");
    [RelayCommand] private Task LinkAsync() => RunAsync(async ct =>
    {
        if (SourceChanged) throw new ArgumentException("De broninvoer is gewijzigd. Kies opnieuw Voorbereiden voordat je koppelingen wijzigt.");
        if (Preview is null || SelectedRow is null || SelectedTarget is null) throw new ArgumentException("Selecteer een bronregel en doelprinting.");
        await workspace.ResolveAsync(Preview.Id, Preview.Version, SelectedRow.Number, SelectedTarget.Id, ct);
        await LoadAsync(Preview.Id, Preview.Page);
    }, "Printing expliciet gekoppeld.");
    [RelayCommand(CanExecute = nameof(CanApply))] private Task ApplyAsync() => RunAsync(async ct =>
    {
        var p = Preview!; await workspace.ApplyAsync(p.Id, p.Version, ct);
        await LoadAsync(p.Id, p.Page); await HistoryCoreAsync();
    }, "Nieuw deck aangemaakt. Open Mijn decks voor de inhoud en analyse. Je bezit is ongewijzigd.");
    [RelayCommand(CanExecute = nameof(CanDiscard))] private Task DiscardAsync() => RunAsync(async ct =>
    {
        var p = Preview!; await workspace.DiscardAsync(p.Id, p.Version, ct);
        await LoadAsync(p.Id, p.Page); await HistoryCoreAsync();
    }, "Voorbereiding verworpen; er is geen deck aangemaakt.");
    [RelayCommand] private Task OpenHistoryAsync() => RunAsync(async _ =>
    {
        if (SelectedHistory is null) throw new ArgumentException("Selecteer een eerdere deckimport.");
        await LoadAsync(SelectedHistory.Id);
    }, "Deckvoorbereiding geopend.");
    [RelayCommand] private Task PreviousPageAsync() => RunAsync(async _ => { if (Preview is { Page: > 1 } p) await LoadAsync(p.Id, p.Page - 1, restoreInput: false); }, "Pagina geladen");
    [RelayCommand] private Task NextPageAsync() => RunAsync(async _ => { if (Preview is { } p && p.Page * 50 < p.Total) await LoadAsync(p.Id, p.Page + 1, restoreInput: false); }, "Pagina geladen");
    [RelayCommand] private Task PreviousHistoryAsync() => RunAsync(async _ => { HistoryPage = Math.Max(1, HistoryPage - 1); await HistoryCoreAsync(); }, "Historie geladen");
    [RelayCommand] private Task NextHistoryAsync() => RunAsync(async _ => { if (History.Count == 50) HistoryPage++; await HistoryCoreAsync(); }, "Historie geladen");
}
