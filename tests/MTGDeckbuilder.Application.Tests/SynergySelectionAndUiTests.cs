using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using MTGDeckbuilder.Desktop;
using MTGDeckbuilder.Desktop.ViewModels;
using MTGDeckbuilder.Domain;
using MTGDeckbuilder.Infrastructure;
using Xunit;

namespace MTGDeckbuilder.Application.Tests;

public sealed class SynergySelectionAndUiTests
{
    private static CancellationToken Ct=>TestContext.Current.CancellationToken;
    private static IPaperWorkspace Store(TestDatabase t)=>t.Provider.GetRequiredService<IPaperWorkspace>();
    [Fact]
    public async Task AddingSupportChangesCandidatePriorityAndOpponentDeathPrefersActualDeathOverExile()
    {
        await using var t=new TestDatabase();await t.InitializeAsync();await Store(t).InitializeExampleAsync(Ct);var cards=(await Store(t).CollectionAsync(ct:Ct)).Select(x=>x.Card).DistinctBy(x=>x.OracleId).ToArray();
        PaperCard Card(string name)=>cards.Single(c=>c.Name==name);
        var selector=new SynergySelection(cards,36,new("Context","casual60","selected","WR","equipment","balanced",true));
        var cloud=Card("Cloud, Planet's Champion");var before=selector.Score(cloud);selector.Pick(Card("Warrior's Sword"));Assert.True(selector.Score(cloud)>before);
        var sephiroth=new SynergySelection(cards,62,commander:Card("Sephiroth, Planet's Heir"));
        Assert.True(sephiroth.Score(Card("Sephiroth's Intervention"))>sephiroth.Score(Card("Path to Exile")));
        Assert.DoesNotContain(SynergyEngine.Between(SynergyProfiles.Analyze(Card("Sephiroth, Planet's Heir")),SynergyProfiles.Analyze(Card("Al Bhed Salvagers"))),x=>x.Weight>1);
    }
    [Theory]
    [InlineData("equipment")]
    [InlineData("graveyard")]
    [InlineData("exalted")]
    [InlineData("draw")]
    public async Task NoColorPreferenceChoosesEnoughOwnedCardsWithinAFocusedCastingPlan(string theme)
    {
        await using var t=new TestDatabase();await t.InitializeAsync();await Store(t).InitializeExampleAsync(Ct);
        var request=new DeckWizardRequest("Focused "+theme,"casual60","any","",theme,"balanced",false);var first=await Store(t).PrepareWizardAsync(request,Ct);var second=await Store(t).PrepareWizardAsync(request,Ct);
        Assert.Equal(first.Lines.Select(x=>(x.Card.Id,x.Quantity)),second.Lines.Select(x=>(x.Card.Id,x.Quantity)));Assert.Equal(60,first.Lines.Sum(x=>x.Quantity));Assert.Empty(first.Report.Problems);
        var demand=DeckManaHeuristics.Demand(first.Lines.Where(x=>!x.Card.TypeLine.Contains("Land",StringComparison.Ordinal)).Select(x=>(x.Card,x.Quantity)));
        Assert.InRange(demand.Count(x=>x.Key!='C'&&x.Value>0),1,2);
        var own=await Store(t).CollectionAsync(ct:Ct);foreach(var row in first.Lines)Assert.InRange(row.Quantity,1,own.Single(x=>x.Card.Id==row.Card.Id).Quantity);
        Assert.Equal(202,(await Store(t).SummaryAsync(Ct)).Copies);
    }
    [Fact]
    public async Task DomainUsesFiveLandTypesWhileManaColorsAndForbiddenColorsRemainSeparate()
    {
        await using var t=new TestDatabase();await t.InitializeAsync();await Store(t).InitializeExampleAsync(Ct);
        var draft=await Store(t).PrepareWizardAsync(new("Domain","casual60","any","","domain","balanced",false),Ct);Assert.Equal(5,draft.Report.Synergy!.BasicTypes.Count);
        var colored=DeckManaHeuristics.Demand(draft.Lines.Select(x=>(x.Card,x.Quantity))).Count(x=>x.Key!='C'&&x.Value>0);Assert.InRange(colored,1,3);
        var limited=await Store(t).PrepareWizardAsync(new("WR only","casual60","selected","WR","equipment","balanced",false),Ct);Assert.All(limited.Lines,x=>Assert.All(x.Card.Colors,c=>Assert.Contains(c,"WR")));
    }
    [Fact]
    public async Task ManaOutputIsNotCountedAsACostAndRealActivatedCostsAreCounted()
    {
        await using var t=new TestDatabase();await t.InitializeAsync();await Store(t).InitializeExampleAsync(Ct);var cards=(await Store(t).CollectionAsync(ct:Ct)).Select(x=>x.Card).ToArray();
        var kaleido=DeckManaHeuristics.Demand([(cards.First(c=>c.Name=="Kaleidostone"),1)]);Assert.All(kaleido.Values,x=>Assert.Equal(0,x));
        var mystics=DeckManaHeuristics.Demand([(cards.First(c=>c.Name=="Aerie Mystics"),1)]);Assert.True(mystics['G']>0);Assert.True(mystics['U']>0);Assert.True(mystics['W']>0);
    }
    [Fact]
    public async Task ReviewedThematicAlternativesAreExplicitlyNotFunctionallyEquivalent()
    {
        await using var t=new TestDatabase();await t.InitializeAsync();await Store(t).InitializeExampleAsync(Ct);var cards=(await Store(t).CollectionAsync(ct:Ct)).Select(x=>x.Card).ToArray();
        PaperCard Card(string name)=>cards.First(c=>c.Name==name);
        var deck=new[]{new PaperDeckLine(Card("Cloud, Planet's Champion"),DeckZone.Commander,1,1)};
        var match=SynergyReplacement.Compare(Card("Warrior's Sword"),Card("Magitek Scythe"),deck);Assert.True(match.ThematicRole);Assert.Contains("geen functie-equivalente",match.Explanation);
        Assert.False(SynergyReplacement.Compare(Card("Frontline Sage"),Card("Gwafa Hazid, Profiteer"),deck).ThematicRole);
    }
    [AvaloniaFact]
    public async Task DeckAndWizardExposeActualRelationsConditionsAndReviewedCoverage()
    {
        await using var t=new TestDatabase();var shell=t.Provider.GetRequiredService<ShellViewModel>();var window=new MainWindow{DataContext=shell};window.Show();
        try
        {
            await shell.InitializeCommand.ExecuteAsync(null);await shell.NavigateCommand.ExecuteAsync("paper-decks");var vm=Assert.IsType<PaperViewModel>(shell.CurrentPage);
            vm.SelectedDeck=vm.Decks.Single(x=>x.Name.Contains("Cloud",StringComparison.Ordinal));await vm.LoadDeckCommand.ExecuteAsync(null);Assert.NotNull(vm.DeckSynergy);Assert.Contains(vm.DeckSynergy.Positive,x=>x.Theme=="equipment");
            Layout(window);var expander=Find<Expander>(window,"PaperSynergyPanel");expander.IsExpanded=true;expander.BringIntoView();Layout(window);Assert.Contains("gerichte relaties",Find<TextBlock>(window,"PaperSynergySummary").Text);Capture(window,"phase16-deck-synergy");
            await shell.NavigateCommand.ExecuteAsync("wizard");var wizard=Assert.IsType<DeckWizardViewModel>(shell.CurrentPage);wizard.Name="Visible synergy";wizard.SelectedTheme=CardThemes.Choices.Single(x=>x.Key=="equipment");
            for(var i=0;i<4;i++)await wizard.NextCommand.ExecuteAsync(null);Assert.False(shell.Operations.HasError,shell.Operations.ErrorMessage);Assert.True(wizard.IsStep4);Assert.NotNull(wizard.Draft!.Report.Synergy);
            Layout(window);var panel=Find<Expander>(window,"WizardSynergyPanel");panel.IsExpanded=true;panel.BringIntoView();Layout(window);Assert.NotEmpty(Find<ListBox>(window,"WizardSynergyRelations").Items);Capture(window,"phase16-wizard-synergy");
            window.Width=1050;window.Height=740;panel.BringIntoView();Layout(window);Capture(window,"phase16-synergy-minimum");
            Assert.Equal(4,(await Store(t).DecksAsync(Ct)).Count);Assert.False(wizard.Planning);Assert.False(wizard.Confirm);
        }
        finally{window.Close();}
    }
    private static T Find<T>(Window w,string name)where T:Control=>w.GetVisualDescendants().OfType<T>().Single(x=>x.Name==name);
    private static void Layout(Window w){Dispatcher.UIThread.RunJobs();w.UpdateLayout();}
    private static void Capture(Window w,string name)
    {var folder=Environment.GetEnvironmentVariable("MTGDECKBUILDER_SCREENSHOTS");if(string.IsNullOrEmpty(folder))return;Directory.CreateDirectory(folder);using var image=w.CaptureRenderedFrame();Assert.NotNull(image);image.Save(Path.Combine(folder,name+".png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);}
}
