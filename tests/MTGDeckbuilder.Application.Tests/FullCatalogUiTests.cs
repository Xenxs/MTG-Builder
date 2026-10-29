using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using MTGDeckbuilder.Application;
using MTGDeckbuilder.Desktop;
using MTGDeckbuilder.Desktop.ViewModels;
using Xunit;

namespace MTGDeckbuilder.Application.Tests;

public sealed class FullCatalogUiTests
{
    [AvaloniaFact]
    public async Task FullPhysicalCatalogCanBeDisplayedFilteredAndItsSavedCommanderReopened()
    {
        var directory = Environment.GetEnvironmentVariable("MTGDECKBUILDER_FULL_CATALOG_TESTDATA");
        Assert.SkipWhen(directory is null, "Requires the explicitly prepared, isolated all-card simulation.");
        var fullPath = Path.GetFullPath(directory!);
        Assert.True(File.Exists(Path.Combine(fullPath, "FULL-CATALOG-SIMULATION.json")));
        Assert.Contains(Path.DirectorySeparatorChar + ".local" + Path.DirectorySeparatorChar, fullPath);
        var ct = TestContext.Current.CancellationToken;
        await using var test = new TestDatabase();
        Directory.CreateDirectory(test.Paths.DataDirectory);
        await using (var source = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = Path.Combine(fullPath, "deckbuilder.db"), Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString()))
        await using (var destination = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = test.Paths.DatabasePath, Pooling = false }.ToString()))
        {
            await source.OpenAsync(ct);
            await destination.OpenAsync(ct);
            source.BackupDatabase(destination);
        }
        var shell = test.Provider.GetRequiredService<ShellViewModel>();
        var window = new MainWindow { DataContext = shell };
        window.Show();
        try
        {
            await shell.InitializeCommand.ExecuteAsync(null);
            var vm = Assert.IsType<PaperViewModel>(shell.CurrentPage);
            if (vm.FilteringTask is { } task) await task;
            Assert.Equal(33727, vm.Collection.Count);
            Assert.Contains("138106", vm.Summary);
            Capture(window, "phase17-all-cards-collection");
            vm.ThemeFilter = CardThemes.Choices.Single(x => x.Key == "graveyard");
            Assert.True(vm.IsFiltering);
            var dispatcherTick = false;
            Dispatcher.UIThread.Post(() => dispatcherTick = true);
            Dispatcher.UIThread.RunJobs();
            Assert.True(dispatcherTick);
            await vm.FilteringTask!;
            Assert.NotEmpty(vm.Collection);
            Assert.All(vm.Collection, x => Assert.True(CardThemes.Matches(x.Card, "graveyard")));
            Capture(window, "phase17-all-cards-filter");
            await shell.NavigateCommand.ExecuteAsync("paper-decks");
            var id = Environment.GetEnvironmentVariable("MTGDECKBUILDER_FULL_CATALOG_DECK");
            Assert.True(Guid.TryParse(id, out var deckId));
            vm.SelectedDeck = vm.Decks.Single(x => x.Id == deckId);
            await vm.LoadDeckCommand.ExecuteAsync(null);
            Assert.NotNull(vm.DeckSynergy);
            Assert.Contains("Angel", vm.DeckDetails);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            var panel = window.GetVisualDescendants().OfType<Expander>().Single(x => x.Name == "PaperSynergyPanel");
            panel.IsExpanded = true;
            panel.BringIntoView();
            Capture(window, "phase17-all-cards-commander");
            Assert.False(shell.Operations.HasError, shell.Operations.ErrorMessage);
        }
        finally { window.Close(); }
    }

    private static void Capture(Window window, string name)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        var folder = Environment.GetEnvironmentVariable("MTGDECKBUILDER_SCREENSHOTS");
        if (string.IsNullOrEmpty(folder)) return;
        Directory.CreateDirectory(folder);
        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        frame.Save(Path.Combine(folder, name + ".png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }
}
