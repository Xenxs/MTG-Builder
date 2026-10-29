using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using MTGDeckbuilder.Desktop;
using MTGDeckbuilder.Desktop.ViewModels;
using MTGDeckbuilder.Domain;
using MTGDeckbuilder.Infrastructure;
using Xunit;

namespace MTGDeckbuilder.Application.Tests;

public sealed class DeckWorkspaceUiTests
{
    [AvaloniaFact]
    public async Task ImportResolveAndAnalyzeThroughRealBindingsWithoutGiftingOwnership()
    {
        await using var test = new TestDatabase(); var items = await DeckWorkspaceTests.Seed(test);
        var shell = test.Provider.GetRequiredService<LegacyShell>();
        await shell.InitializeCommand.ExecuteAsync(null); await shell.NavigateCommand.ExecuteAsync("deck-transfer");
        var vm = Assert.IsType<DeckTransferViewModel>(shell.CurrentPage);
        var window = new LegacyWindow { DataContext = shell, Height = 1250 };
        try
        {
            window.Show(); Layout(window);
            Find<TextBox>(window, "DeckSourceText").Text = "Deck\n4 Llanowar Elves (FDN) 227\nSideboard\n2 Unknown";
            Find<TextBox>(window, "ImportDeckName").Text = "UI Elves"; Layout(window);
            await Command(window, "PrepareDeckImport").ExecuteAsync(null);
            Assert.False(vm.HasError, vm.Error); Assert.Equal(1, vm.Preview!.Unresolved);
            Assert.False(vm.ApplyCommand.CanExecute(null));
            Find<ListBox>(window, "DeckPreviewRows").SelectedItem = vm.Rows[1]; Layout(window);
            var expander = window.GetVisualDescendants().OfType<Expander>().Single(); expander.IsExpanded = true; Layout(window);
            Find<TextBox>(window, "DeckTargetSearch").Text = "Negate"; Layout(window);
            await vm.SearchTargetsCommand.ExecuteAsync(null);
            Assert.Equal("Negate", Assert.Single(vm.Targets).Name);
            Find<ComboBox>(window, "DeckTargetPicker").SelectedItem = vm.Targets[0]; Layout(window);
            await Command(window, "LinkDeckRow").ExecuteAsync(null);
            expander.IsExpanded = false; Layout(window);
            Find<CheckBox>(window, "ReviewDeckImport").IsChecked = true; Layout(window);
            Assert.True(Find<Button>(window, "ApplyDeckImport").IsEffectivelyEnabled);
            Capture(window, "deck-import-preview");
            await Command(window, "ApplyDeckImport").ExecuteAsync(null);
            Assert.False(vm.HasError, vm.Error); Assert.Equal(DeckImportState.Applied, vm.Preview!.State);
            Assert.Empty(await test.Service.GetCollectionAsync());
            await shell.NavigateCommand.ExecuteAsync("decks"); var decks = Assert.IsType<DecksViewModel>(shell.CurrentPage);
            Assert.Equal("UI Elves", decks.SelectedDeck!.Name); Assert.Equal(6, decks.Analysis!.Missing);
            var tabs = Find<TabControl>(window, "DeckWorkspaceTabs"); tabs.SelectedIndex = 1; Layout(window);
            Assert.Equal(2, Find<ListBox>(window, "DeckMissingCards").ItemCount);
            Capture(window, "deck-analysis-missing");
            var catalog = test.Provider.GetRequiredService<CatalogService>();
            await catalog.SetOwnedQuantityAsync(items.Single(x => x.Name == "Llanowar Elves").PrintingId, 4);
            await decks.AnalyzeCommand.ExecuteAsync(null); Assert.Equal(2, decks.Analysis!.Missing);
            tabs.SelectedIndex = 2; Layout(window);
            await Command(window, "GenerateDeckExport").ExecuteAsync(null);
            Assert.Contains("2 Negate (FDN) 710", Find<TextBox>(window, "DeckExportText").Text);
            Capture(window, "deck-export");
            var exportFile = Path.Combine(test.Paths.DataDirectory, "ui-deck.txt");
            await decks.SaveExportFileCommand.ExecuteAsync(exportFile);
            Assert.Equal(decks.ExportText, await File.ReadAllTextAsync(exportFile));
            Assert.False(decks.Operations.HasError, decks.Operations.ErrorMessage);
        }
        finally { window.Close(); }
    }
    [AvaloniaFact]
    public async Task AddingUnownedCatalogCardAndCommanderWorksFromTheDeckEditor()
    {
        await using var test = new TestDatabase(); await DeckWorkspaceTests.Seed(test);
        var shell = test.Provider.GetRequiredService<LegacyShell>(); await shell.InitializeCommand.ExecuteAsync(null);
        await shell.NavigateCommand.ExecuteAsync("decks"); var vm = Assert.IsType<DecksViewModel>(shell.CurrentPage);
        vm.DeckName = "Brawl planning"; vm.Format = "Brawl"; await vm.CreateCommand.ExecuteAsync(null);
        var window = new LegacyWindow { DataContext = shell, Height = 1100 };
        try
        {
            window.Show(); Layout(window);
            var expanders = window.GetVisualDescendants().OfType<Expander>().ToList();
            expanders.Single(x => x.Header?.ToString()?.StartsWith("Plannen", StringComparison.Ordinal) == true).IsExpanded = true; Layout(window);
            Find<TextBox>(window, "DeckCatalogSearch").Text = "Llanowar"; Layout(window);
            await vm.SearchCatalogCommand.ExecuteAsync(null);
            Find<ComboBox>(window, "DeckCatalogPicker").SelectedItem = Assert.Single(vm.CatalogResults); Layout(window);
            vm.Zone = DeckZone.Commander; vm.Quantity = 1;
            await Command(window, "AddUnownedDeckCard").ExecuteAsync(null);
            Assert.False(vm.Operations.HasError, vm.Operations.ErrorMessage);
            Assert.Equal(DeckZone.Commander, Assert.Single(vm.Cards).Zone);
            Assert.Equal(1, vm.Analysis!.Commanders); Assert.Equal(1, vm.Analysis.Missing);
            Assert.Empty(await test.Service.GetCollectionAsync());
            Capture(window, "deck-editor-unowned");
        }
        finally { window.Close(); }
    }
    [AvaloniaFact]
    public async Task ChangedSourceDisablesApplyAndCancelRemainsAvailableDuringPreparation()
    {
        await using var test = new TestDatabase(); await DeckWorkspaceTests.Seed(test);
        var vm = test.Provider.GetRequiredService<DeckTransferViewModel>();
        vm.SourceText = DeckWorkspaceTests.Source; await vm.PrepareCommand.ExecuteAsync(null);
        vm.Reviewed = true; Assert.True(vm.ApplyCommand.CanExecute(null));
        vm.SourceText += "\n1 Negate"; Assert.False(vm.Reviewed); Assert.False(vm.ApplyCommand.CanExecute(null));
        var edited = vm.SourceText; await vm.RefreshAsync();
        Assert.Equal(edited, vm.SourceText); Assert.True(vm.SourceChanged);
        vm.Reviewed = true; Assert.False(vm.ApplyCommand.CanExecute(null));
        var gate = test.Provider.GetRequiredService<DatabaseWriteLock>().Gate; await gate.WaitAsync();
        try
        {
            var pending = vm.PrepareCommand.ExecuteAsync(null); Assert.True(vm.IsWorking);
            Assert.True(vm.CancelCommand.CanExecute(null)); vm.CancelCommand.Execute(null); await pending;
            Assert.False(vm.IsWorking); Assert.False(vm.HasError, vm.Error); Assert.Contains("Geannuleerd", vm.Status);
        }
        finally { gate.Release(); }
        Assert.Empty(await test.Service.GetDecksAsync());
    }
    private static IAsyncRelayCommand Command(Window window, string name) => Assert.IsAssignableFrom<IAsyncRelayCommand>(Find<Button>(window, name).Command);
    private static T Find<T>(Window window, string name) where T : Control => window.GetVisualDescendants().OfType<T>().Single(x => x.Name == name);
    private static void Layout(Window window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private static void Capture(Window window, string name)
    {
        var directory = Environment.GetEnvironmentVariable("MTGDECKBUILDER_SCREENSHOTS"); if (string.IsNullOrEmpty(directory)) return;
        Directory.CreateDirectory(directory); Layout(window);
        window.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault(x => x.Content is ContentControl)?.ScrollToHome(); Layout(window);
        using var bitmap = window.CaptureRenderedFrame(); Assert.NotNull(bitmap);
        bitmap.Save(Path.Combine(directory, name + ".png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }
}
