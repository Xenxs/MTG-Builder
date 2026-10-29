using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MTGDeckbuilder.Application;

namespace MTGDeckbuilder.Desktop.ViewModels;

public sealed partial class ArchetypePoolViewModel(IArchetypePoolStore store, UiOperations operations) : PageViewModel
{
    public UiOperations Operations => operations;
    [ObservableProperty] private ArchetypePoolReport? report;
    [ObservableProperty] private string search = "";
    [ObservableProperty] private bool onlyOwned;
    [ObservableProperty] private bool showExcluded;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasSelection))] private ArchetypePoolCard? selectedCard;
    [ObservableProperty] private ArchetypePoolAlternative? selectedAlternative;
    [ObservableProperty] private string alternativeStatus = "Kies een poolkaart en zoek beoordeelde opties uit je bezit.";
    public ObservableCollection<ArchetypePoolCard> Cards { get; } = [];
    public ObservableCollection<ArchetypePoolAlternative> Alternatives { get; } = [];
    public bool HasSelection => SelectedCard is not null;
    partial void OnSearchChanged(string value) => Filter();
    partial void OnOnlyOwnedChanged(bool value) => Filter();
    partial void OnShowExcludedChanged(bool value) => Filter();
    partial void OnSelectedCardChanged(ArchetypePoolCard? value)
    {
        Alternatives.Clear(); SelectedAlternative = null;
        AlternativeStatus = "Kies een poolkaart en zoek beoordeelde opties uit je bezit.";
    }
    public override async Task RefreshAsync()
    {
        Report = await store.ReadAsync(); Filter();
    }
    private void Filter()
    {
        var previous = SelectedCard?.Entry.OracleId;
        Cards.Clear();
        foreach (var card in Report?.Cards.Where(x => (ShowExcluded || x.Entry.Included) && (!OnlyOwned || x.OwnedTotal > 0)
                     && x.Entry.Name.Contains(Search.Trim(), StringComparison.OrdinalIgnoreCase)) ?? []) Cards.Add(card);
        SelectedCard = Cards.FirstOrDefault(x => x.Entry.OracleId == previous) ?? Cards.FirstOrDefault();
    }
    [RelayCommand]
    private Task ReloadAsync() => operations.RunAsync(RefreshAsync, "Kaartpool, bronstatus en bezit opnieuw gecontroleerd");
    [RelayCommand]
    private Task FindAlternativesAsync() => operations.RunAsync(async () =>
    {
        if (SelectedCard is not { } selected) throw new ArgumentException("Kies eerst een kaart uit de beoordeelde pool.");
        var id = selected.Entry.OracleId;
        await RefreshAsync();
        var current = Report!.Cards.Single(x => x.Entry.OracleId == id);
        Alternatives.Clear(); SelectedAlternative = null;
        if (SelectedCard?.Entry.OracleId != id) { AlternativeStatus = "De gekozen kaart valt na vernieuwen buiten je filter. Kies opnieuw een kaart."; return; }
        if (!current.Eligible) { AlternativeStatus = "Pooladvies geblokkeerd: " + current.Status; return; }
        foreach (var alternative in ArchetypePoolEngine.Alternatives(Report, id)) Alternatives.Add(alternative);
        SelectedAlternative = Alternatives.FirstOrDefault();
        AlternativeStatus = Alternatives.Count == 0 ? "Geen actueel geschikte andere kaart met dezelfde beoordeelde functie in je bezit. Landen krijgen hier geen andere kaart als vervanging."
            : $"{Alternatives.Count} beoordeelde opties · totale geregistreerde voorraad, geen deckreservering. Lees alle verschillen en voorwaarden.";
    }, "Poolopties op actuele gegevens gecontroleerd");
}
