using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MTGDeckbuilder.Application;
using MTGDeckbuilder.Domain;

namespace MTGDeckbuilder.Desktop.ViewModels;

public sealed partial class DeckWizardViewModel(IPaperWorkspace workspace,IPaperCatalog catalog,UiOperations operations):PageViewModel
{
    public UiOperations Operations=>operations;
    public CardChoice[] Formats=>DeckWizardRequest.Formats;
    public CardChoice[] Themes=>CardThemes.Choices;
    public CardChoice[] Styles=>DeckWizardRequest.Styles;
    public CardChoice[] ColorModes {get;}=[new("any","Geen kleurvoorkeur"),new("selected","Alleen de gekozen kleuren"),new("colorless","Kleurloos")];
    public ObservableCollection<PaperCard> Commanders {get;}=[];
    private PaperCard[] _knownCommanders=[];
    [ObservableProperty,NotifyPropertyChangedFor(nameof(IsStep0)),NotifyPropertyChangedFor(nameof(IsStep1)),NotifyPropertyChangedFor(nameof(IsStep2)),NotifyPropertyChangedFor(nameof(IsStep3)),NotifyPropertyChangedFor(nameof(IsStep4)),NotifyPropertyChangedFor(nameof(Progress)),NotifyPropertyChangedFor(nameof(CanBack)),NotifyPropertyChangedFor(nameof(NextLabel))]private int step;
    [ObservableProperty]private string name="Mijn nieuwe deck";
    [ObservableProperty,NotifyPropertyChangedFor(nameof(IsCommander))]private CardChoice? selectedFormat=DeckWizardRequest.Formats[0];
    [ObservableProperty,NotifyPropertyChangedFor(nameof(SelectColors))]private CardChoice? selectedColorMode=new("any","Geen kleurvoorkeur");
    [ObservableProperty]private bool white;
    [ObservableProperty]private bool blue;
    [ObservableProperty]private bool black;
    [ObservableProperty]private bool red;
    [ObservableProperty]private bool green;
    [ObservableProperty]private CardChoice? selectedTheme=CardThemes.Choices[0];
    [ObservableProperty]private CardChoice? selectedStyle=DeckWizardRequest.Styles[0];
    [ObservableProperty]private bool planning;
    [ObservableProperty]private PaperCard? selectedCommander;
    [ObservableProperty]private string commanderSearch="";
    [ObservableProperty]private string collectionSummary="";
    [ObservableProperty]private PaperDeckDraft? draft;
    [ObservableProperty]private bool confirm;
    [ObservableProperty,NotifyPropertyChangedFor(nameof(IsSaved))]private Guid? savedDeckId;
    [ObservableProperty]private string savedMessage="";
    public Func<Guid,Task>? OpenSavedDeck {get;set;}
    public bool IsStep0=>Step==0;public bool IsStep1=>Step==1;public bool IsStep2=>Step==2;public bool IsStep3=>Step==3;public bool IsStep4=>Step==4;
    public bool CanBack=>Step>0&&!IsSaved;public bool IsSaved=>SavedDeckId is not null;public bool IsCommander=>SelectedFormat?.Key=="commander";public bool SelectColors=>SelectedColorMode?.Key=="selected";
    public string Progress=>$"Stap {Step+1} van 5";public string NextLabel=>Step==3?"Voorstel berekenen":"Volgende stap";
    private string Colors=>(White?"W":"")+(Blue?"U":"")+(Black?"B":"")+(Red?"R":"")+(Green?"G":"");
    public DeckWizardRequest Request()=>new(Name,SelectedFormat?.Key??"",SelectedColorMode?.Key??"",Colors,SelectedTheme?.Key??"any",SelectedStyle?.Key??"balanced",Planning,SelectedCommander?.Id);
    public override async Task RefreshAsync()
    {
        await workspace.InitializeExampleAsync();CollectionSummary=(await workspace.SummaryAsync()).Label;var own=await workspace.CollectionAsync();
        _knownCommanders=own.Select(x=>x.Card).Where(x=>x.IsCommanderCandidate).ToArray();UpdateCommanders();Step=0;Draft=null;Confirm=false;SavedDeckId=null;SavedMessage="";
    }
    private void UpdateCommanders()
    {
        var selected=SelectedCommander?.Id;var request=Request();Commanders.Clear();foreach(var c in _knownCommanders.Where(request.Allows))Commanders.Add(c);
        SelectedCommander=Commanders.FirstOrDefault(c=>c.Id==selected)??Commanders.FirstOrDefault(c=>c.Name=="Cloud, Planet's Champion")??Commanders.FirstOrDefault();
    }
    partial void OnSelectedColorModeChanged(CardChoice? value)=>UpdateCommanders();
    partial void OnWhiteChanged(bool value)=>UpdateCommanders();partial void OnBlueChanged(bool value)=>UpdateCommanders();partial void OnBlackChanged(bool value)=>UpdateCommanders();partial void OnRedChanged(bool value)=>UpdateCommanders();partial void OnGreenChanged(bool value)=>UpdateCommanders();
    [RelayCommand]private Task NextAsync()=>operations.RunAsync(async()=>
    {
        if(Step==0){Rules.Text(Name,"Decknaam",120);if(SelectedFormat is null)throw new ArgumentException("Kies een formaat.");}
        if(Step==1&&SelectColors&&Colors.Length==0)throw new ArgumentException("Kies minstens één kleur, of kies bewust kleurloos/geen voorkeur.");
        if(Step==3){var request=Request();request.Validate();Draft=await workspace.PrepareWizardAsync(request);Confirm=false;}
        if(Step<4)Step++;
    },"Wizardstap gereed");
    [RelayCommand]private void Back(){if(CanBack){Step--;Draft=null;Confirm=false;}}
    [RelayCommand]private Task SearchCommanderAsync()=>operations.RunAsync(async()=>
    {var request=Request();var found=await catalog.SearchAsync(CommanderSearch);foreach(var c in found.Where(c=>c.IsCommanderCandidate&&request.Allows(c)))if(!Commanders.Any(x=>x.Id==c.Id))Commanders.Add(c);SelectedCommander=Commanders.LastOrDefault();},"Commander-zoekresultaten geladen; controleer de gekozen uitgave");
    [RelayCommand]private Task SaveAsync()=>operations.RunAsync(async()=>
    {if(SavedDeckId is not null)return;if(Draft is null||!Confirm)throw new ArgumentException("Bekijk de volledige lijst en bevestig vóór opslag.");SavedDeckId=await workspace.SaveDeckDraftAsync(Draft);SavedMessage="Nieuw deck opgeslagen. Je collectie en bestaande decks zijn behouden.";OnPropertyChanged(nameof(CanBack));},"Wizarddeck opgeslagen");
    [RelayCommand]private async Task OpenSavedAsync(){if(SavedDeckId is{} id&&OpenSavedDeck is{} open)await open(id);}
}
