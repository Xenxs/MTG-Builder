using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MTGDeckbuilder.Application;

namespace MTGDeckbuilder.Desktop.ViewModels;

public sealed partial class DecksViewModel
{
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasAdvice)), NotifyPropertyChangedFor(nameof(CanReviewAdvice)),
     NotifyPropertyChangedFor(nameof(HasAdviceNeeds)), NotifyPropertyChangedFor(nameof(HasAdviceChanges)), NotifyPropertyChangedFor(nameof(HasSupportedAdvice))]
    private DeckAdvice? advice;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasAdvicePreview))] private AdvicePreview? advicePreview;
    [ObservableProperty] private AdviceNeed? selectedAdviceNeed;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasAdviceCandidate))] private AdviceCandidate? selectedAdviceCandidate;
    [ObservableProperty] private AdviceChange? selectedAdviceChange;
    [ObservableProperty] private decimal adviceQuantity = 1;
    [ObservableProperty] private string adviceName = "";
    [ObservableProperty] private string candidateStatus = "Kies een tekort en zoek beschikbare alternatieven.";
    public ObservableCollection<AdviceNeed> AdviceNeeds { get; } = [];
    public ObservableCollection<AdviceCandidate> AdviceCandidates { get; } = [];
    public ObservableCollection<AdviceChange> AdviceChanges { get; } = [];
    public bool HasAdvice => Advice is not null;
    public bool HasSupportedAdvice => Advice?.Baseline.Passed == true;
    public bool HasAdviceNeeds => Advice?.Needs.Count > 0;
    public bool HasAdviceChanges => Advice?.Changes.Count > 0;
    public bool HasAdviceCandidate => SelectedAdviceCandidate is not null;
    public bool CanReviewAdvice => Advice?.CanPreview == true;
    public bool HasAdvicePreview => AdvicePreview is not null;
    partial void OnAdviceNameChanged(string value) => AdvicePreview = null;
    partial void OnSelectedAdviceNeedChanged(AdviceNeed? value)
    {
        AdviceCandidates.Clear(); SelectedAdviceCandidate = null;
        CandidateStatus = "Kies een tekort en zoek beschikbare alternatieven.";
    }
    partial void OnSelectedAdviceCandidateChanged(AdviceCandidate? value) => AdviceQuantity = 1;
    private AdviceChoice[] AdviceSelection() => AdviceChanges.Select(x => x.Choice).ToArray();
    private void ClearAdvice()
    {
        Advice = null; AdvicePreview = null; AdviceNeeds.Clear(); AdviceCandidates.Clear(); AdviceChanges.Clear();
        SelectedAdviceNeed = null; SelectedAdviceCandidate = null; SelectedAdviceChange = null;
    }
    private void ShowAdvice(DeckAdvice result)
    {
        Advice = result; AdvicePreview = null;
        var previous = SelectedAdviceNeed;
        AdviceNeeds.Clear(); foreach (var need in result.Needs) AdviceNeeds.Add(need);
        SelectedAdviceNeed = AdviceNeeds.FirstOrDefault(x => x.PrintingId == previous?.PrintingId && x.Zone == previous.Zone) ?? AdviceNeeds.FirstOrDefault();
        AdviceCandidates.Clear(); SelectedAdviceCandidate = null;
        AdviceChanges.Clear(); foreach (var change in result.Changes) AdviceChanges.Add(change);
        SelectedAdviceChange = null;
    }
    [RelayCommand]
    private Task CalculateAdviceAsync() => operations.RunAsync(async () =>
    {
        if (SelectedDeck is null) throw new ArgumentException("Selecteer eerst een deck.");
        var id = SelectedDeck.Id; ClearAdvice();
        var result = await adviceStore.GetAsync(id);
        if (SelectedDeck?.Id != id) return;
        ShowAdvice(result);
        AdviceName = result.Name[..Math.Min(result.Name.Length, 104)] + " · alternatieven";
    }, "Advies opnieuw berekend op basis van het opgeslagen controleprofiel");
    [RelayCommand]
    private Task FindAdviceCandidatesAsync() => operations.RunAsync(async () =>
    {
        if (Advice is not { } current || SelectedAdviceNeed is not { } need) throw new ArgumentException("Bereken eerst advies en kies een tekort.");
        AdviceCandidates.Clear(); SelectedAdviceCandidate = null;
        var candidates = await adviceStore.CandidatesAsync(current.DeckId, current.Fingerprint, AdviceSelection(), need.PrintingId, need.Zone);
        if (Advice != current || SelectedAdviceNeed != need) return;
        foreach (var candidate in candidates) AdviceCandidates.Add(candidate);
        SelectedAdviceCandidate = AdviceCandidates.FirstOrDefault();
        CandidateStatus = candidates.Count == 0 ? "Geen passende vrije exemplaren. Voor landen en sideboard worden alleen andere uitgaven van dezelfde kaart aangeboden."
            : $"{candidates.Count} beschikbare printings · dezelfde kaart, dan rolmatch · lees beperkingen en onzekerheid.";
    }, "Beschikbare alternatieven gecontroleerd");
    [RelayCommand]
    private Task AddAdviceChoiceAsync() => operations.RunAsync(async () =>
    {
        if (Advice is not { } current || SelectedAdviceNeed is not { } need || SelectedAdviceCandidate is not { } candidate)
            throw new ArgumentException("Kies een tekort en een alternatief.");
        var count = UiOperations.ReadQuantity(AdviceQuantity);
        var selection = AdviceSelection().Append(new AdviceChoice(need.PrintingId, need.Zone, candidate.PrintingId, count)).ToArray();
        var result = await adviceStore.GetAsync(current.DeckId, current.Fingerprint, selection);
        if (Advice == current) ShowAdvice(result);
    }, "Keuze toegevoegd en gezamenlijke voorraad gecontroleerd");
    [RelayCommand]
    private Task RemoveAdviceChoiceAsync() => operations.RunAsync(async () =>
    {
        if (Advice is not { } current || SelectedAdviceChange is not { } change) throw new ArgumentException("Selecteer een gekozen vervanging.");
        var selection = AdviceChanges.ToList(); selection.Remove(change);
        var result = await adviceStore.GetAsync(current.DeckId, current.Fingerprint, selection.Select(x => x.Choice).ToArray());
        if (Advice == current) ShowAdvice(result);
    }, "Keuze verwijderd en voorstel opnieuw gecontroleerd");
    [RelayCommand]
    private Task PreviewAdviceAsync() => operations.RunAsync(async () =>
    {
        if (Advice is not { } current) throw new ArgumentException("Bereken eerst het advies.");
        AdvicePreview = null; var name = AdviceName;
        var preview = await adviceStore.PreviewAsync(current.DeckId, current.Fingerprint, AdviceSelection(), name);
        if (Advice == current && AdviceName == name) AdvicePreview = preview;
    }, "Voorbeeld gereed. Controleer wijzigingen en resterende tekorten vóór opslag.");
    [RelayCommand]
    private Task ApplyAdviceAsync() => operations.RunAsync(async () =>
    {
        if (AdvicePreview is not { } preview) throw new ArgumentException("Maak eerst een actueel voorbeeld.");
        Guid id;
        try { id = await adviceStore.ApplyAsync(preview.Id); }
        catch { AdvicePreview = null; throw; }
        await RefreshAsync();
        SelectedDeck = Decks.FirstOrDefault(x => x.Id == id); await LoadCardsAsync();
    }, "Voorstel als nieuw deck opgeslagen; origineel en collectie zijn behouden");
}
