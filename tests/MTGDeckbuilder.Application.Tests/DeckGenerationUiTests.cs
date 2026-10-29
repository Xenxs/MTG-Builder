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

public sealed class DeckGenerationUiTests
{
    [AvaloniaFact]
    public async Task OwnedNavigationPreviewInvalidationAndSaveAreBoundToRealControls()
    {
        await using var test = DeckValidationPersistenceTests.Database(ArchetypePoolPersistenceTests.Handler()); await DeckGenerationPersistenceTests.Seed(test);
        var shell = test.Provider.GetRequiredService<LegacyShell>(); await shell.InitializeCommand.ExecuteAsync(null);
        var window = new LegacyWindow { DataContext = shell, Height = 1150 };
        try
        {
            window.Show(); Layout(window);
            await Assert.IsAssignableFrom<IAsyncRelayCommand>(Find<Button>(window, "GenerationNavigation").Command).ExecuteAsync("generation"); Layout(window);
            var vm = Assert.IsType<DeckGenerationViewModel>(shell.CurrentPage); Assert.True(shell.IsGeneration); Assert.False(vm.Planning);
            Assert.Contains("60 kaarten", Find<TextBlock>(window, "GenerationSummary").Text); Assert.True(vm.CanReview);
            Assert.False(Find<Button>(window, "SaveGeneration").IsEffectivelyEnabled);
            Find<ComboBox>(window, "GenerationCardPicker").SelectedItem = vm.Lines.First(x => x.Entry.Slot == AggroSlot.CombatSupport); Layout(window);
            Find<Expander>(window, "GenerationCardExpander").IsExpanded = true; Layout(window);
            Assert.Contains(vm.SelectedLine!.Entry.Cautions, Find<TextBlock>(window, "GenerationCardDetails").Text);
            Capture(window, "generation-owned-conditions"); Find<Expander>(window, "GenerationCardExpander").IsExpanded = false; Layout(window);
            await Execute(window, "ReviewGeneration"); Assert.True(vm.CanSave); Assert.Empty(await test.Service.GetDecksAsync());
            Find<TextBox>(window, "GenerationName").Text = "My reviewed list"; Layout(window); Assert.Null(vm.Preview); Assert.False(vm.CanSave);
            await Execute(window, "ReviewGeneration"); Assert.Equal("My reviewed list", Find<TextBlock>(window, "GenerationPreviewName").Text);
            Capture(window, "generation-owned-overview");
            Find<Button>(window, "SaveGeneration").BringIntoView(); Layout(window); Capture(window, "generation-owned-preview");
            await Execute(window, "SaveGeneration");
            Assert.NotNull(vm.SavedDeckId); Assert.False(vm.CanSave); Assert.Contains("Nieuw deck opgeslagen", Find<TextBlock>(window, "GenerationReceipt").Text);
            Find<TextBlock>(window, "GenerationReceipt").BringIntoView(); Layout(window); Capture(window, "generation-saved"); Assert.Single(await test.Service.GetDecksAsync());
            window.Height = 850; Find<Expander>(window, "GenerationListExpander").IsExpanded = true; Layout(window);
            Find<Expander>(window, "GenerationListExpander").BringIntoView(); Layout(window); Capture(window, "generation-full-list-standard-window");
            await shell.NavigateCommand.ExecuteAsync("decks"); var decks = Assert.IsType<DecksViewModel>(shell.CurrentPage);
            Assert.Contains(decks.Decks, x => x.Name == "My reviewed list"); Assert.False(shell.Operations.HasError, shell.Operations.ErrorMessage);
        }
        finally { window.Close(); }
    }
    [AvaloniaFact]
    public async Task NoStockBlocksOwnedModeButExplicitPlanningShowsShortagesBeforeSave()
    {
        await using var test = DeckValidationPersistenceTests.Database(ArchetypePoolPersistenceTests.Handler()); await DeckGenerationPersistenceTests.Seed(test, false);
        var shell = test.Provider.GetRequiredService<LegacyShell>(); await shell.InitializeCommand.ExecuteAsync(null); await shell.NavigateCommand.ExecuteAsync("generation");
        var vm = Assert.IsType<DeckGenerationViewModel>(shell.CurrentPage); var window = new LegacyWindow { DataContext = shell, Height = 1150 };
        try
        {
            window.Show(); Layout(window); Assert.False(vm.CanReview); Assert.Contains("Mountains: 22", vm.Result!.Status);
            Assert.False(Find<Button>(window, "ReviewGeneration").IsEffectivelyEnabled); Capture(window, "generation-insufficient-stock");
            Find<CheckBox>(window, "GenerationPlanning").IsChecked = true; Layout(window); Assert.Null(vm.Result);
            await Execute(window, "CalculateGeneration"); Assert.True(vm.CanReview); Assert.Equal(60, vm.Result!.Missing);
            Assert.Contains("PLANNING", Find<TextBlock>(window, "GenerationMode").Text); Assert.Contains("22× Mountain", Find<TextBlock>(window, "GenerationShortages").Text);
            await Execute(window, "ReviewGeneration"); Assert.Equal("Nieuw planningdeck opslaan", Find<Button>(window, "SaveGeneration").Content);
            Capture(window, "generation-planning-overview");
            Find<Button>(window, "SaveGeneration").BringIntoView(); Layout(window); Capture(window, "generation-planning-preview");
            await Execute(window, "SaveGeneration"); Assert.Empty(await test.Service.GetCollectionAsync());
            Find<CheckBox>(window, "GenerationPlanning").IsChecked = false; Layout(window); Assert.Null(vm.Result); Assert.Null(vm.Preview); Assert.False(vm.CanSave);
            Assert.False(vm.Operations.HasError, vm.Operations.ErrorMessage);
        }
        finally { window.Close(); }
    }
    [AvaloniaFact]
    public async Task StalePreviewReportsCentralErrorAndRecalculationRecovers()
    {
        await using var test = DeckValidationPersistenceTests.Database(ArchetypePoolPersistenceTests.Handler()); await DeckGenerationPersistenceTests.Seed(test);
        var shell = test.Provider.GetRequiredService<LegacyShell>(); await shell.InitializeCommand.ExecuteAsync(null); await shell.NavigateCommand.ExecuteAsync("generation");
        var vm = Assert.IsType<DeckGenerationViewModel>(shell.CurrentPage); await vm.ReviewCommand.ExecuteAsync(null); Assert.NotNull(vm.Preview);
        var catalog = test.Provider.GetRequiredService<CatalogService>(); var card = (await catalog.SearchAsync(new(Search: "Mountain"))).Items.Single();
        await catalog.SetOwnedQuantityAsync(card.PrintingId, 0); await vm.SaveCommand.ExecuteAsync(null);
        Assert.True(vm.Operations.HasError); Assert.Contains("gewijzigd", vm.Operations.ErrorMessage); Assert.Empty(await test.Service.GetDecksAsync());
        await vm.CalculateCommand.ExecuteAsync(null); Assert.False(vm.Operations.HasError); Assert.Null(vm.Preview); Assert.False(vm.CanReview);
    }
    [AvaloniaFact]
    public async Task MissingSourceBlocksBothModesAndShowsSourceExplanation()
    {
        await using var test = new TestDatabase(); var shell = test.Provider.GetRequiredService<LegacyShell>();
        await shell.InitializeCommand.ExecuteAsync(null); await shell.NavigateCommand.ExecuteAsync("generation"); var vm = Assert.IsType<DeckGenerationViewModel>(shell.CurrentPage);
        vm.Planning = true; await vm.CalculateCommand.ExecuteAsync(null); Assert.False(vm.CanReview); Assert.Empty(vm.Lines);
        var window = new LegacyWindow { DataContext = shell, Height = 1150 };
        try
        {
            window.Show(); Layout(window); Find<Expander>(window, "GenerationPlanExpander").IsExpanded = true; Layout(window);
            Assert.Contains("Geen catalogus", Find<TextBlock>(window, "GenerationSource").Text); Capture(window, "generation-no-source");
        }
        finally { window.Close(); }
    }
    private static async Task Execute(Window window, string name) { await Assert.IsAssignableFrom<IAsyncRelayCommand>(Find<Button>(window, name).Command).ExecuteAsync(null); Layout(window); }
    private static T Find<T>(Window window, string name) where T : Control => window.GetVisualDescendants().OfType<T>().Single(x => x.Name == name);
    private static void Layout(Window window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private static void Capture(Window window, string name)
    {
        var directory = Environment.GetEnvironmentVariable("MTGDECKBUILDER_SCREENSHOTS"); if (string.IsNullOrEmpty(directory)) return;
        Directory.CreateDirectory(directory); Layout(window); using var bitmap = window.CaptureRenderedFrame(); Assert.NotNull(bitmap);
        bitmap.Save(Path.Combine(directory, name + ".png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }
}
