using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using MTGDeckbuilder.Application;
using MTGDeckbuilder.Desktop;
using MTGDeckbuilder.Desktop.ViewModels;
using Xunit;

namespace MTGDeckbuilder.Application.Tests;

public sealed class CatalogUiTests
{
    [AvaloniaFact]
    public async Task CatalogBindingsImportSearchAndOwnedQuantityAreUsable()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var test = new TestDatabase();
        var shell = test.Provider.GetRequiredService<LegacyShell>();
        await shell.InitializeCommand.ExecuteAsync(null);
        await shell.NavigateCommand.ExecuteAsync("catalog");
        var vm = Assert.IsType<CatalogViewModel>(shell.CurrentPage);
        var window = new LegacyWindow { DataContext = shell, Height = 1000 };
        try
        {
            window.Show();
            var path = await CatalogFixtures.WriteAsync(test,
            [CatalogFixtures.Card(), CatalogFixtures.Card("Negate", collector: "710", colors: ["U"], type: "Instant", mana: 2, rarity: "uncommon")], "jsonl.gz", ct);
            await vm.ImportFileCommand.ExecuteAsync(path);
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            Assert.False(vm.Operations.HasError, vm.Operations.ErrorMessage);
            Assert.Equal(2, vm.Total);
            Assert.Single(vm.History);
            Assert.False(vm.IsImporting);
            Assert.False(vm.ShowImport);
            var textBox = window.GetVisualDescendants().OfType<TextBox>().Single(x => x.Name == "CatalogSearchInput");
            textBox.Text = "Negate";
            Dispatcher.UIThread.RunJobs();
            await vm.SearchCommand.ExecuteAsync(null);
            Assert.Equal("Negate", Assert.Single(vm.Items).Name);
            vm.OwnedQuantity = 3;
            await vm.SaveOwnedCommand.ExecuteAsync(null);
            Assert.Equal(3, Assert.Single(await test.Service.GetCollectionAsync(ct)).Quantity);
            Assert.Equal(3, Assert.Single(vm.Items).OwnedQuantity);
            Capture(window, "catalog-populated");
            await shell.NavigateCommand.ExecuteAsync("collection");
            Assert.Equal("Negate", Assert.Single(Assert.IsType<CollectionViewModel>(shell.CurrentPage).Items).Name);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task ImportCanBeCancelledFromTheUiWithoutDisablingItsCancelButton()
    {
        await using var test = new TestDatabase(services => services.AddSingleton<ICatalogImporter, WaitingImporter>());
        await test.InitializeAsync();
        var vm = test.Provider.GetRequiredService<CatalogViewModel>();
        var task = vm.ImportOnlineCommand.ExecuteAsync(null);
        Assert.True(vm.IsImporting);
        Assert.False(vm.Operations.IsBusy);
        Assert.True(vm.CancelImportCommand.CanExecute(null));
        vm.CancelImportCommand.Execute(null);
        await task;
        Assert.False(vm.IsImporting);
        Assert.False(vm.Operations.HasError);
        Assert.Contains("gepauzeerd", vm.ProgressText);
    }

    private static void Capture(Window window, string name)
    {
        var directory = Environment.GetEnvironmentVariable("MTGDECKBUILDER_SCREENSHOTS");
        if (string.IsNullOrEmpty(directory)) return;
        Directory.CreateDirectory(directory);
        Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
        using var image = window.CaptureRenderedFrame();
        Assert.NotNull(image);
        image.Save(Path.Combine(directory, name + ".png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }

    private sealed class WaitingImporter : ICatalogImporter
    {
        public async Task<ImportResult> ImportOnlineAsync(IProgress<ImportProgress>? progress = null, CancellationToken ct = default)
        { await Task.Delay(Timeout.InfiniteTimeSpan, ct); throw new InvalidOperationException(); }
        public Task<ImportResult> ImportFileAsync(string path, IProgress<ImportProgress>? progress = null, CancellationToken ct = default)
            => ImportOnlineAsync(progress, ct);
        public Task<ImportResult> ResumeAsync(Guid runId, IProgress<ImportProgress>? progress = null, CancellationToken ct = default)
            => ImportOnlineAsync(progress, ct);
        public Task RecoverInterruptedAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
