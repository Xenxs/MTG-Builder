using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MTGDeckbuilder.Application;
namespace MTGDeckbuilder.Desktop.ViewModels;
public abstract class PageViewModel:ObservableObject {public abstract Task RefreshAsync();}
public sealed partial class ShellViewModel:ObservableObject
{
 private readonly IDatabaseInitializer _initializer;private readonly IBackupService _backups;private readonly Dictionary<string,PageViewModel> _pages;
 public UiOperations Operations {get;}public string Version=>$"v{typeof(ShellViewModel).Assembly.GetName().Version?.ToString(3)} · Fase 17";
 [ObservableProperty]private PageViewModel currentPage;
 [ObservableProperty]private bool isReady;
 [ObservableProperty,NotifyPropertyChangedFor(nameof(IsPaper)),NotifyPropertyChangedFor(nameof(IsCollection)),NotifyPropertyChangedFor(nameof(IsImport)),NotifyPropertyChangedFor(nameof(IsDecks)),NotifyPropertyChangedFor(nameof(IsWizard)),NotifyPropertyChangedFor(nameof(IsSettings))]private string currentKey="paper";
 public bool IsPaper=>CurrentKey.StartsWith("paper",StringComparison.Ordinal);public bool IsCollection=>CurrentKey=="paper";public bool IsImport=>CurrentKey=="paper-import";public bool IsDecks=>CurrentKey=="paper-decks";public bool IsWizard=>CurrentKey=="wizard";public bool IsSettings=>CurrentKey=="settings";
 public ShellViewModel(IDatabaseInitializer initializer,UiOperations operations,PaperViewModel paper,SettingsViewModel settings,DeckWizardViewModel wizard,IBackupService backups)
 {
  _initializer=initializer;_backups=backups;Operations=operations;currentPage=paper;
  _pages=new(){["paper"]=paper,["paper-import"]=paper,["paper-decks"]=paper,["settings"]=settings,["wizard"]=wizard};
  paper.PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(PaperViewModel.SelectedTab)&&ReferenceEquals(CurrentPage,paper))CurrentKey=paper.SelectedTab==1?"paper-import":paper.SelectedTab==2?"paper-decks":"paper";};
  wizard.OpenSavedDeck=async id=>{await NavigateAsync("paper-decks");paper.SelectedDeck=paper.Decks.SingleOrDefault(x=>x.Id==id);await paper.LoadDeckCommand.ExecuteAsync(null);};
 }
 [RelayCommand]private Task InitializeAsync()=>Operations.RunAsync(async()=>{await _initializer.InitializeAsync();await CurrentPage.RefreshAsync();IsReady=true;},"Lokale opslag gereed");
 [RelayCommand]private Task CancelStartupRestoreAsync()=>Operations.RunAsync(async()=>{await _backups.CancelAsync();await _initializer.InitializeAsync();await CurrentPage.RefreshAsync();IsReady=true;},"Herstel geannuleerd; lokale opslag gereed");
 [RelayCommand]private Task NavigateAsync(string key)
 {
  if(!IsReady||!_pages.TryGetValue(key,out var page))return Task.CompletedTask;
  return Operations.RunAsync(async()=>{CurrentPage=page;CurrentKey=key;if(page is PaperViewModel p)p.SelectedTab=key=="paper-import"?1:key=="paper-decks"?2:0;await page.RefreshAsync();},"Gereed");
 }
}
