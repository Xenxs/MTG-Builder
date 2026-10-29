using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using MTGDeckbuilder.Application;
using MTGDeckbuilder.Desktop;
using MTGDeckbuilder.Desktop.ViewModels;
using Xunit;

namespace MTGDeckbuilder.Application.Tests;

public sealed class ArchetypePoolUiTests
{
    [AvaloniaFact]
    public async Task NavigationFiltersConditionsAndFreshOwnedAlternativesAreBoundToControls()
    {
        var handler=ArchetypePoolPersistenceTests.Handler();await using var test=DeckValidationPersistenceTests.Database(handler);await ArchetypePoolPersistenceTests.Seed(test);
        var shell=test.Provider.GetRequiredService<LegacyShell>();await shell.InitializeCommand.ExecuteAsync(null);
        var window=new LegacyWindow{DataContext=shell,Height=1150};
        try
        {
            window.Show();Layout(window);
            await Assert.IsAssignableFrom<IAsyncRelayCommand>(Find<Button>(window,"PoolNavigation").Command).ExecuteAsync("pool");Layout(window);
            var vm=Assert.IsType<ArchetypePoolViewModel>(shell.CurrentPage);Assert.True(shell.IsPool);Assert.Equal(27,vm.Cards.Count);
            Assert.Contains("27 actueel geschikt",Find<TextBlock>(window,"PoolSummary").Text);
            Find<ComboBox>(window,"PoolCardPicker").SelectedItem=vm.Cards.Single(x=>x.Entry.Name=="Slickshot Show-Off");Layout(window);
            Assert.Contains("noncreature",Find<TextBlock>(window,"PoolCautions").Text);
            await Execute(window,"FindPoolAlternatives");
            Assert.Equal(new[]{"Emberheart Challenger","Fugitive Codebreaker"},vm.Alternatives.Select(x=>x.Name));
            Capture(window,"pool-owned-options");
            Find<Expander>(window,"PoolAlternativeDetailsExpander").IsExpanded=true;Layout(window);
            Assert.Contains("Valiant",Find<TextBlock>(window,"PoolAlternativeDetails").Text);
            Find<Expander>(window,"PoolAlternativeDetailsExpander").IsExpanded=false;Layout(window);
            var catalog=test.Provider.GetRequiredService<CatalogService>();
            var owned=(await catalog.SearchAsync(new(Search:"Emberheart Challenger"))).Items.Single();await catalog.SetOwnedQuantityAsync(owned.PrintingId,0);
            await Execute(window,"FindPoolAlternatives");Assert.Equal("Fugitive Codebreaker",Assert.Single(vm.Alternatives).Name);
            Find<CheckBox>(window,"PoolOnlyOwned").IsChecked=true;Layout(window);Assert.Equal(3,vm.Cards.Count);
            var filteredSource=vm.SelectedCard!.Entry.Name;
            var depleted=(await catalog.SearchAsync(new(Search:filteredSource))).Items.Single();await catalog.SetOwnedQuantityAsync(depleted.PrintingId,0);
            await Execute(window,"FindPoolAlternatives");Assert.Empty(vm.Alternatives);Assert.Contains("buiten je filter",vm.AlternativeStatus);
            Assert.NotEqual(filteredSource,vm.SelectedCard?.Entry.Name);
            Find<CheckBox>(window,"PoolOnlyOwned").IsChecked=false;Find<CheckBox>(window,"PoolShowExcluded").IsChecked=true;Layout(window);
            Find<TextBox>(window,"PoolSearch").Text="Leyline";Layout(window);Assert.Single(vm.Cards);
            Assert.Contains("Wizards-banlijst",Find<TextBlock>(window,"PoolCardStatus").Text,StringComparison.OrdinalIgnoreCase);
            await Execute(window,"FindPoolAlternatives");Assert.Empty(vm.Alternatives);Assert.Contains("geblokkeerd",vm.AlternativeStatus);Capture(window,"pool-excluded");
            Find<TextBox>(window,"PoolSearch").Text="Emberheart";Layout(window);
            handler.Cards.Single(x=>(string)x["name"]! == "Emberheart Challenger")["oracle_text"]="Changed Oracle text.";handler.Version++;
            await test.Provider.GetRequiredService<ICatalogImporter>().ImportOnlineAsync();await Execute(window,"FindPoolAlternatives");
            Assert.Contains("Metadata gewijzigd",vm.AlternativeStatus);Capture(window,"pool-metadata-changed");
            Assert.False(vm.Operations.HasError,vm.Operations.ErrorMessage);
        }
        finally{window.Close();}
    }
    [AvaloniaFact]
    public async Task MissingSourceDisplaysBlockedPoolInsteadOfSuccessfulAdvice()
    {
        await using var test=new TestDatabase();var shell=test.Provider.GetRequiredService<LegacyShell>();await shell.InitializeCommand.ExecuteAsync(null);
        await shell.NavigateCommand.ExecuteAsync("pool");var vm=Assert.IsType<ArchetypePoolViewModel>(shell.CurrentPage);
        var window=new LegacyWindow{DataContext=shell,Height=1150};
        try
        {
            window.Show();Layout(window);Assert.Contains("0 actueel geschikt",Find<TextBlock>(window,"PoolSummary").Text);
            await Execute(window,"FindPoolAlternatives");Assert.Empty(vm.Alternatives);Assert.Contains("geblokkeerd",vm.AlternativeStatus);
            Find<Expander>(window,"PoolSourceExpander").IsExpanded=true;Layout(window);
            Assert.Contains("Geen catalogus",Find<TextBlock>(window,"PoolProvenance").Text);
            Capture(window,"pool-no-source");Assert.False(vm.Operations.HasError,vm.Operations.ErrorMessage);
        }
        finally{window.Close();}
    }
    private static async Task Execute(Window window,string name){await Assert.IsAssignableFrom<IAsyncRelayCommand>(Find<Button>(window,name).Command).ExecuteAsync(null);Layout(window);}
    private static T Find<T>(Window window,string name)where T:Control=>window.GetVisualDescendants().OfType<T>().Single(x=>x.Name==name);
    private static void Layout(Window window){Dispatcher.UIThread.RunJobs();window.UpdateLayout();}
    private static void Capture(Window window,string name)
    {
        var directory=Environment.GetEnvironmentVariable("MTGDECKBUILDER_SCREENSHOTS");if(string.IsNullOrEmpty(directory))return;
        Directory.CreateDirectory(directory);Layout(window);using var bitmap=window.CaptureRenderedFrame();Assert.NotNull(bitmap);
        bitmap.Save(Path.Combine(directory,name+".png"),Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }
}
