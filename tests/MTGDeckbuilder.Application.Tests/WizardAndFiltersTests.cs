using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using MTGDeckbuilder.Desktop;
using MTGDeckbuilder.Desktop.ViewModels;
using MTGDeckbuilder.Infrastructure;
using Xunit;
namespace MTGDeckbuilder.Application.Tests;

public sealed class WizardAndFiltersTests
{
    private static CancellationToken Ct=>TestContext.Current.CancellationToken;
    private static IPaperWorkspace Store(TestDatabase t)=>t.Provider.GetRequiredService<IPaperWorkspace>();
    [Fact]
    public async Task CollectionFiltersCombinePrintedColorThemeTypeAndFullSetName()
    {
        await using var t=new TestDatabase();await t.InitializeAsync();await Store(t).InitializeExampleAsync(Ct);var all=await Store(t).CollectionAsync(ct:Ct);
        var rows=CardThemes.Filter(all,"","W","equipment","Creature","fin");Assert.NotEmpty(rows);Assert.Contains(rows,x=>x.Card.Name=="Adelbert Steiner");Assert.All(rows,x=>{Assert.Contains("W",x.Card.PrintedColors);Assert.Contains("Creature",x.Card.TypeLine);Assert.Equal("fin",x.Card.Set);});
        var basics=CardThemes.Filter(all,"","colorless","any","Land","any");Assert.NotEmpty(basics);Assert.Contains(basics,x=>x.Card.Name=="Forest");Assert.All(basics,x=>Assert.Empty(x.Card.PrintedColors!));
        Assert.All(all.Where(x=>x.Card.Set=="fin"),x=>Assert.Equal("Final Fantasy",x.Card.SetName));Assert.Empty(CardThemes.Filter(all,"no such card","any","any","any","any"));
    }
    [Fact]
    public async Task WizardHonorsPaletteThemeAndInventoryAndDoesNotOverwriteSourceDecks()
    {
        await using var t=new TestDatabase();await t.InitializeAsync();await Store(t).InitializeExampleAsync(Ct);
        var request=new DeckWizardRequest("Equipment from my collection","casual60","selected","WR","equipment","aggro",false);var draft=await Store(t).PrepareWizardAsync(request,Ct);
        Assert.Equal(60,draft.Lines.Sum(x=>x.Quantity));Assert.Empty(draft.Report.Problems);Assert.All(draft.Lines,x=>Assert.True(request.Allows(x.Card)));Assert.Contains(draft.Lines,x=>x.Card.TypeLine.Contains("Equipment",StringComparison.Ordinal));Assert.Contains("Wizard:",draft.Details);
        var own=await Store(t).CollectionAsync(ct:Ct);foreach(var row in draft.Lines)Assert.InRange(row.Quantity,1,own.Single(x=>x.Card.Id==row.Card.Id).Quantity);
        var id=await Store(t).SaveDeckDraftAsync(draft,Ct);Assert.Equal(id,await Store(t).SaveDeckDraftAsync(draft,Ct));await t.ReopenAsync();Assert.Equal(5,(await Store(t).DecksAsync(Ct)).Count);Assert.Equal(202,(await Store(t).SummaryAsync(Ct)).Copies);
    }
    [Theory]
    [InlineData("selected","", "casual60","any")]
    [InlineData("selected","X", "casual60","any")]
    [InlineData("any","", "unsupported","any")]
    [InlineData("any","", "casual60","invented synergy")]
    public void WizardRejectsAmbiguousOrUnsupportedChoices(string mode,string colors,string format,string theme)
        =>Assert.Throws<ArgumentException>(()=>new DeckWizardRequest("Test",format,mode,colors,theme,"balanced",false).Validate());
    [Fact]
    public async Task InsufficientColorlessAndIllegalStandardOwnedPlansCannotInventCards()
    {
        await using var t=new TestDatabase();await t.InitializeAsync();await Store(t).InitializeExampleAsync(Ct);
        await Assert.ThrowsAsync<ArgumentException>(()=>Store(t).PrepareWizardAsync(new("No colorless inventory","casual60","colorless","","any","balanced",false),Ct));
        await Assert.ThrowsAsync<ArgumentException>(()=>Store(t).PrepareWizardAsync(new("No Standard green pool","standard","selected","G","any","balanced",false),Ct));
        Assert.Equal(202,(await Store(t).SummaryAsync(Ct)).Copies);Assert.Equal(4,(await Store(t).DecksAsync(Ct)).Count);
    }
    [AvaloniaFact]
    public async Task ProductHasNoArenaRoutesAndFiltersAndWizardReallyWork()
    {
        await using var t=new TestDatabase();var shell=t.Provider.GetRequiredService<ShellViewModel>();var window=new MainWindow{DataContext=shell};window.Show();
        try
        {
            await shell.InitializeCommand.ExecuteAsync(null);Assert.False(shell.Operations.HasError,shell.Operations.ErrorMessage);Assert.NotNull(window.Icon);Assert.Null(window.FindControl<Button>("DashboardNavigation"));
            Assert.DoesNotContain(typeof(App).Assembly.GetTypes(),x=>x.Name=="DeckGenerationViewModel");await shell.NavigateCommand.ExecuteAsync("dashboard");Assert.Equal("paper",shell.CurrentKey);
            var paper=Assert.IsType<PaperViewModel>(shell.CurrentPage);paper.ColorFilter=CardThemes.Colors.Single(x=>x.Key=="W");paper.ThemeFilter=CardThemes.Choices.Single(x=>x.Key=="equipment");paper.TypeFilter=CardThemes.Types.Single(x=>x.Key=="Creature");Assert.NotEmpty(paper.Collection);Capture(window,"phase15-collection");
            await shell.NavigateCommand.ExecuteAsync("wizard");var wizard=Assert.IsType<DeckWizardViewModel>(shell.CurrentPage);Assert.False(wizard.Planning);wizard.Name="Wizard equipment";
            await wizard.NextCommand.ExecuteAsync(null);Assert.True(wizard.IsStep1);wizard.SelectedColorMode=wizard.ColorModes.Single(x=>x.Key=="selected");
            await wizard.NextCommand.ExecuteAsync(null);Assert.True(shell.Operations.HasError);Assert.True(wizard.IsStep1);wizard.White=true;wizard.Red=true;await wizard.NextCommand.ExecuteAsync(null);Assert.True(wizard.IsStep2);
            wizard.SelectedTheme=CardThemes.Choices.Single(x=>x.Key=="equipment");wizard.SelectedStyle=DeckWizardRequest.Styles.Single(x=>x.Key=="aggro");Capture(window,"phase15-wizard-choices");
            await wizard.NextCommand.ExecuteAsync(null);Assert.True(wizard.IsStep3);await wizard.NextCommand.ExecuteAsync(null);Assert.False(shell.Operations.HasError,shell.Operations.ErrorMessage);Assert.True(wizard.IsStep4);Assert.NotNull(wizard.Draft);Capture(window,"phase15-wizard-review");
            window.GetVisualDescendants().OfType<Button>().Single(x=>x.Name=="WizardSave").BringIntoView();Capture(window,"phase15-wizard-save");
            window.Width=1050;window.Height=740;Capture(window,"phase15-wizard-minimum");window.Width=1280;window.Height=880;
            await wizard.SaveCommand.ExecuteAsync(null);Assert.True(shell.Operations.HasError);Assert.Equal(4,(await Store(t).DecksAsync(Ct)).Count);wizard.Confirm=true;await wizard.SaveCommand.ExecuteAsync(null);Assert.False(shell.Operations.HasError,shell.Operations.ErrorMessage);Assert.True(wizard.IsSaved);
            await wizard.OpenSavedCommand.ExecuteAsync(null);Assert.True(shell.IsDecks);Assert.Equal(wizard.SavedDeckId,paper.SelectedDeck!.Id);Assert.Contains("60 mainboard",paper.DeckSummary);
        }
        finally{window.Close();}
    }
    [AvaloniaFact]
    public async Task SettingsRequireExactResetConfirmationAndDoNotReseedAfterNavigation()
    {
        await using var t=new TestDatabase();var shell=t.Provider.GetRequiredService<ShellViewModel>();await shell.InitializeCommand.ExecuteAsync(null);await shell.NavigateCommand.ExecuteAsync("settings");var vm=Assert.IsType<SettingsViewModel>(shell.CurrentPage);var window=new MainWindow{DataContext=shell};window.Show();
        try
        {
            await vm.ReviewResetCommand.ExecuteAsync(null);Assert.NotNull(vm.ResetReview);Assert.False(vm.CanReset);vm.ResetPhrase="wissen";await vm.ClearDatabaseCommand.ExecuteAsync(null);Assert.True(shell.Operations.HasError);Assert.Equal(202,(await Store(t).SummaryAsync(Ct)).Copies);Capture(window,"phase15-settings-reset-review");
            vm.ResetPhrase="WISSEN";Assert.True(vm.CanReset);await vm.ClearDatabaseCommand.ExecuteAsync(null);Assert.False(shell.Operations.HasError,shell.Operations.ErrorMessage);Assert.Contains("Reservekopie",vm.ResetResult);
            await shell.NavigateCommand.ExecuteAsync("paper");var paper=Assert.IsType<PaperViewModel>(shell.CurrentPage);Assert.Empty(paper.Collection);Assert.False(paper.IsDemoActive);await t.ReopenAsync();await Store(t).InitializeExampleAsync(Ct);Assert.Equal(0,(await Store(t).SummaryAsync(Ct)).Copies);
        }
        finally{window.Close();}
    }
    private static void Capture(Window w,string name)
    {Dispatcher.UIThread.RunJobs();w.UpdateLayout();var path=Environment.GetEnvironmentVariable("MTGDECKBUILDER_SCREENSHOTS");if(string.IsNullOrEmpty(path))return;Directory.CreateDirectory(path);using var frame=w.CaptureRenderedFrame();Assert.NotNull(frame);frame.Save(Path.Combine(path,name+".png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
}
