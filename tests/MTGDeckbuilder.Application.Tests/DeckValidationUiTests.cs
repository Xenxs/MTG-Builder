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
using MTGDeckbuilder.Domain;
using Xunit;

namespace MTGDeckbuilder.Application.Tests;

public sealed class DeckValidationUiTests
{
    [AvaloniaFact]
    public async Task ProfileSelectionControlsPersistAndShowPassedErrorsAndIncompleteResults()
    {
        var handler = new DeckValidationPersistenceTests.BulkHandler();
        await using var test = DeckValidationPersistenceTests.Database(handler);
        var id = await DeckValidationPersistenceTests.SeedDeck(test);
        var shell = test.Provider.GetRequiredService<LegacyShell>(); await shell.InitializeCommand.ExecuteAsync(null);
        await shell.NavigateCommand.ExecuteAsync("decks"); var vm = Assert.IsType<DecksViewModel>(shell.CurrentPage);
        var window = new LegacyWindow { DataContext = shell, Height = 1150 };
        try
        {
            window.Show(); Layout(window); Find<TabControl>(window, "DeckWorkspaceTabs").SelectedIndex = 3; Layout(window);
            var command = Assert.IsAssignableFrom<IAsyncRelayCommand>(Find<Button>(window, "ValidateDeck").Command);
            await command.ExecuteAsync(null); Assert.Contains(vm.Validation!.Issues, x => x.Code == "profile");
            Find<ComboBox>(window, "ValidationProfilePicker").SelectedItem = StandardDeckValidator.Profiles[1]; Layout(window);
            Assert.Null(vm.Validation); await command.ExecuteAsync(null);
            Assert.True(vm.Validation!.Passed); Assert.Contains("Geslaagd", Find<TextBlock>(window, "DeckValidationSummary").Text);
            Assert.Equal(DeckRuleProfile.ArenaStandardBo1, await DeckValidationPersistenceTests.Store(test).GetProfileAsync(id));
            Capture(window, "deck-validation-passed");
            var printing = Assert.Single(vm.Cards).PrintingId;
            await test.Service.SetDeckCardAsync(id, printing, DeckZone.Mainboard, 59);
            await vm.AnalyzeCommand.ExecuteAsync(null); Assert.Null(vm.Validation);
            await command.ExecuteAsync(null); Assert.Contains(vm.Validation!.Issues, x => x.Code == "main-min");
            Capture(window, "deck-validation-errors");
            await test.Service.SetDeckCardAsync(id, printing, DeckZone.Mainboard, 60);
            handler.Cards[0].Remove("legalities");
            var file = await CatalogFixtures.WriteAsync(test, handler.Cards, "json", TestContext.Current.CancellationToken);
            await test.Provider.GetRequiredService<ICatalogImporter>().ImportFileAsync(file);
            // Use the UI refresh boundary, so bound selection changes cannot race a direct refresh with validation.
            await shell.NavigateCommand.ExecuteAsync("decks"); Layout(window);
            Assert.False(vm.Operations.IsBusy); Assert.Null(vm.Validation); await command.ExecuteAsync(null);
            Assert.Equal(0, vm.Validation!.Errors); Assert.True(vm.Validation.Unknowns > 0);
            Assert.Contains("Onvolledig", Find<TextBlock>(window, "DeckValidationSummary").Text);
            Assert.Equal(vm.Validation.Issues.Count, Find<ListBox>(window, "DeckValidationIssues").ItemCount);
            Capture(window, "deck-validation-incomplete");
            Assert.False(vm.Operations.HasError, vm.Operations.ErrorMessage);
        }
        finally { window.Close(); }
    }
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
