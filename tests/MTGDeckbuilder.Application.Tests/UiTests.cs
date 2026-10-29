using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using MTGDeckbuilder.Desktop;
using MTGDeckbuilder.Desktop.ViewModels;
using MTGDeckbuilder.Desktop.Views;
using MTGDeckbuilder.Application;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(MTGDeckbuilder.Application.Tests.UiTestApp))]
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace MTGDeckbuilder.Application.Tests;

public static class UiTestApp
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

public sealed class UiTests
{
    [AvaloniaFact]
    public async Task NavigationBindingsRenderAllSevenPages()
    {
        await using var test = new TestDatabase();
        var shell = test.Provider.GetRequiredService<LegacyShell>();
        using var lifetime = new WindowLifetime(new LegacyWindow { DataContext = shell });
        var window = lifetime.Window;
        window.Show();
        await shell.InitializeCommand.ExecuteAsync(null);
        Assert.True(shell.IsReady);
        Assert.False(shell.Operations.HasError, shell.Operations.ErrorMessage);
        var pages = new (string Button, string Key, Type View)[]
        {
            ("DashboardNavigation", "dashboard", typeof(DashboardView)),
            ("CollectionNavigation", "collection", typeof(CollectionView)),
            ("CatalogNavigation", "catalog", typeof(CatalogView)),
            ("TransferNavigation", "transfer", typeof(CollectionTransferView)),
            ("DecksNavigation", "decks", typeof(DecksView)),
            ("DeckTransferNavigation", "deck-transfer", typeof(DeckTransferView)),
            ("SettingsNavigation", "settings", typeof(SettingsView))
        };
        foreach (var page in pages)
        {
            var button = window.FindControl<Button>(page.Button)!;
            Assert.True(button.IsEffectivelyEnabled);
            Assert.Equal(page.Key, button.CommandParameter);
            await Assert.IsAssignableFrom<IAsyncRelayCommand>(button.Command).ExecuteAsync(button.CommandParameter);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Assert.Equal(page.Key, shell.CurrentKey);
            Assert.Contains(window.GetVisualDescendants(), v => v.GetType() == page.View);
            Assert.False(shell.Operations.HasError, shell.Operations.ErrorMessage);
            Capture(window, page.Key);
        }
    }

    [AvaloniaFact]
    public async Task ViewModelCommandsPersistDataAndRefreshDashboard()
    {
        await using var test = new TestDatabase();
        var shell = test.Provider.GetRequiredService<LegacyShell>();
        await shell.InitializeCommand.ExecuteAsync(null);
        await shell.NavigateCommand.ExecuteAsync("collection");
        var collection = Assert.IsType<CollectionViewModel>(shell.CurrentPage);
        collection.CardName = "Llanowar Elves";
        collection.SetCode = "FDN";
        collection.CollectorNumber = "227";
        collection.Quantity = 4;
        await collection.SaveCommand.ExecuteAsync(null);
        Assert.Single(collection.Items);
        await shell.NavigateCommand.ExecuteAsync("decks");
        var decks = Assert.IsType<DecksViewModel>(shell.CurrentPage);
        decks.DeckName = "Elves";
        await decks.CreateCommand.ExecuteAsync(null);
        decks.Quantity = 4;
        await decks.SaveCardCommand.ExecuteAsync(null);
        Assert.Equal(4, Assert.Single(decks.Cards).Quantity);
        await shell.NavigateCommand.ExecuteAsync("settings");
        var settings = Assert.IsType<SettingsViewModel>(shell.CurrentPage);
        settings.PlayerName = "Nissa";
        await settings.SaveCommand.ExecuteAsync(null);
        await shell.NavigateCommand.ExecuteAsync("dashboard");
        var dashboard = Assert.IsType<DashboardViewModel>(shell.CurrentPage);
        Assert.Equal(4, dashboard.TotalCards);
        Assert.Equal(1, dashboard.Decks);
        Assert.Equal("Nissa", dashboard.PlayerName);
        Assert.False(shell.Operations.HasError, shell.Operations.ErrorMessage);
        using var lifetime = new WindowLifetime(new LegacyWindow { DataContext = shell });
        lifetime.Window.Show();
        Dispatcher.UIThread.RunJobs();
        Capture(lifetime.Window, "dashboard-populated");
    }

    [AvaloniaFact]
    public async Task ValidationErrorIsVisibleAndNextSuccessfulCommandRecovers()
    {
        await using var test = new TestDatabase();
        await test.InitializeAsync();
        var vm = test.Provider.GetRequiredService<CollectionViewModel>();
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.True(vm.Operations.HasError);
        Assert.Contains("Kaartnaam", vm.Operations.ErrorMessage);
        Assert.False(vm.Operations.IsBusy);
        vm.CardName = "Negate"; vm.SetCode = "FDN"; vm.CollectorNumber = "710";
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.False(vm.Operations.HasError);
        Assert.Single(vm.Items);
    }

    private static void Capture(Window window, string name)
    {
        var directory = Environment.GetEnvironmentVariable("MTGDECKBUILDER_SCREENSHOTS");
        if (string.IsNullOrEmpty(directory)) return;
        Directory.CreateDirectory(directory);
        using var bitmap = window.CaptureRenderedFrame();
        Assert.NotNull(bitmap);
        bitmap.Save(Path.Combine(directory, name + ".png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }

    private sealed class WindowLifetime(Window window) : IDisposable
    {
        public Window Window => window;
        public void Dispose() => Window.Close();
    }

    [AvaloniaFact]
    public async Task StartupFailureKeepsEditingDisabledAndRetryCanRecover()
    {
        await using var test = new TestDatabase();
        var provider = test.Provider;
        var shell = new LegacyShell(new FailOnceInitializer(provider.GetRequiredService<IDatabaseInitializer>()),
            provider.GetRequiredService<UiOperations>(), provider.GetRequiredService<DashboardViewModel>(),
            provider.GetRequiredService<CollectionViewModel>(), provider.GetRequiredService<DecksViewModel>(),
            provider.GetRequiredService<SettingsViewModel>(), provider.GetRequiredService<CatalogViewModel>(),
            provider.GetRequiredService<CollectionTransferViewModel>(), provider.GetRequiredService<DeckTransferViewModel>(),
            provider.GetRequiredService<ArchetypePoolViewModel>(), provider.GetRequiredService<DeckGenerationViewModel>(), provider.GetRequiredService<IBackupService>(), provider.GetRequiredService<PaperViewModel>());
        await shell.InitializeCommand.ExecuteAsync(null);
        Assert.False(shell.IsReady);
        Assert.True(shell.Operations.HasError);
        Assert.DoesNotContain("technical failure", shell.Operations.ErrorMessage!);
        Assert.False(shell.Operations.IsBusy);
        await shell.InitializeCommand.ExecuteAsync(null);
        Assert.True(shell.IsReady);
        Assert.False(shell.Operations.HasError);
    }

    [AvaloniaFact]
    public async Task FractionalCountIsRejectedInsteadOfSilentlyTruncated()
    {
        await using var test = new TestDatabase();
        await test.InitializeAsync();
        var collection = test.Provider.GetRequiredService<CollectionViewModel>();
        collection.CardName = "Negate"; collection.SetCode = "FDN"; collection.CollectorNumber = "710";
        collection.Quantity = 1.5m;
        await collection.SaveCommand.ExecuteAsync(null);
        Assert.True(collection.Operations.HasError);
        Assert.Empty(await test.Service.GetCollectionAsync(TestContext.Current.CancellationToken));

        collection.Quantity = 4;
        await collection.SaveCommand.ExecuteAsync(null);
        collection.SelectedItem = Assert.Single(collection.Items);
        collection.SelectedQuantity = 2.5m;
        await collection.UpdateQuantityCommand.ExecuteAsync(null);
        Assert.True(collection.Operations.HasError);
        Assert.Equal(4, Assert.Single(await test.Service.GetCollectionAsync(TestContext.Current.CancellationToken)).Quantity);

        var decks = test.Provider.GetRequiredService<DecksViewModel>();
        await decks.RefreshAsync();
        decks.DeckName = "Test deck";
        await decks.CreateCommand.ExecuteAsync(null);
        decks.Quantity = 1.5m;
        await decks.SaveCardCommand.ExecuteAsync(null);
        Assert.True(decks.Operations.HasError);
        Assert.Empty(await test.Service.GetDeckCardsAsync(decks.SelectedDeck!.Id, TestContext.Current.CancellationToken));
    }

    private sealed class FailOnceInitializer(IDatabaseInitializer inner) : IDatabaseInitializer
    {
        private bool _failed;
        public Task InitializeAsync(CancellationToken ct = default)
        {
            if (_failed) return inner.InitializeAsync(ct);
            _failed = true;
            throw new IOException("technical failure");
        }
    }
}
