using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MTGDeckbuilder.Application;

namespace MTGDeckbuilder.Desktop.ViewModels;

public sealed partial class DeckGenerationViewModel(IDeckGenerationStore store, UiOperations operations) : PageViewModel
{
    public UiOperations Operations => operations;
    private int _revision;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasResult)), NotifyPropertyChangedFor(nameof(CanReview))] private GeneratedDeck? result;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasPreview)), NotifyPropertyChangedFor(nameof(CanSave))] private GenerationPreview? preview;
    [ObservableProperty] private GeneratedLine? selectedLine;
    [ObservableProperty] private bool planning;
    [ObservableProperty] private string deckName = "Mono-rood BO1 · voorstel";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanSave))] private Guid? savedDeckId;
    public ObservableCollection<GeneratedLine> Lines { get; } = [];
    public bool HasResult => Result is not null;
    public bool HasPreview => Preview is not null;
    public bool CanReview => Result?.CanPreview == true;
    public bool CanSave => Preview is not null && SavedDeckId is null;
    public string SaveLabel => Planning ? "Nieuw planningdeck opslaan" : "Nieuw deck uit bezit opslaan";
    public string Groups => Result is null ? "" : string.Join("\n", Result.Groups.Select(x => x.LabelText));
    public string Receipt => SavedDeckId is { } id ? $"Nieuw deck opgeslagen ({id}). Je vindt het bij Mijn decks; exporteer daar naar Arena. Bezit is ongewijzigd." : "";
    partial void OnPlanningChanged(bool value) { _revision++; Clear(); OnPropertyChanged(nameof(SaveLabel)); }
    partial void OnDeckNameChanged(string value) { _revision++; Preview = null; SavedDeckId = null; OnPropertyChanged(nameof(Receipt)); }
    private void Clear()
    {
        Result = null; Preview = null; SavedDeckId = null; Lines.Clear(); SelectedLine = null;
        OnPropertyChanged(nameof(Groups)); OnPropertyChanged(nameof(Receipt));
    }
    public override async Task RefreshAsync()
    {
        var revision = ++_revision; var mode = Planning ? GenerationMode.Planning : GenerationMode.OwnedOnly;
        Clear(); var deck = await store.CalculateAsync(mode);
        if (_revision != revision) return;
        Result = deck; foreach (var line in deck.Lines) Lines.Add(line); SelectedLine = Lines.FirstOrDefault();
        OnPropertyChanged(nameof(Groups));
    }
    [RelayCommand]
    private Task CalculateAsync() => operations.RunAsync(RefreshAsync, "Voorstel berekend; controleer plan, curve, voorwaarden en tekorten.");
    [RelayCommand]
    private Task ReviewAsync() => operations.RunAsync(async () =>
    {
        if (Result is not { CanPreview: true } current) throw new ArgumentException("Bereken eerst een compleet voorstel.");
        Preview = null; SavedDeckId = null; OnPropertyChanged(nameof(Receipt));
        var revision = _revision; var name = DeckName;
        var reviewed = await store.PreviewAsync(current.Fingerprint, current.Mode, name);
        if (Result == current && _revision == revision) Preview = reviewed;
    }, "Voorbeeld opnieuw gecontroleerd. Opslaan maakt een nieuw deck; tekorten blijven tekorten.");
    [RelayCommand]
    private Task SaveAsync() => operations.RunAsync(async () =>
    {
        if (Preview is not { } current || SavedDeckId is not null) throw new ArgumentException("Controleer eerst een nieuw voorbeeld.");
        var id = await store.ApplyAsync(current.Id);
        if (Preview == current) { SavedDeckId = id; OnPropertyChanged(nameof(Receipt)); }
    }, "Voorstel als nieuw deck opgeslagen; bestaande decks en bezit behouden.");
}
