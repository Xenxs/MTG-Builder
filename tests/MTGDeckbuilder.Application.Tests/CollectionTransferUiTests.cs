using Avalonia;
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
using MTGDeckbuilder.Infrastructure;
using Xunit;

namespace MTGDeckbuilder.Application.Tests;

public sealed class CollectionTransferUiTests
{
    [AvaloniaFact]
    public async Task PreviewBindingsResolveManualPrintingApplyAndUndo()
    {
        await using var test = new TestDatabase();
        var shell = test.Provider.GetRequiredService<LegacyShell>();
        await shell.InitializeCommand.ExecuteAsync(null);
        await test.Service.SaveCollectionAsync(new CollectionInput("Llanowar Elves", "FDN", "227", 2));
        await test.Service.SaveCollectionAsync(new CollectionInput("Negate", "FDN", "710", 3));
        await shell.NavigateCommand.ExecuteAsync("transfer");
        var vm = Assert.IsType<CollectionTransferViewModel>(shell.CurrentPage);
        var window = new LegacyWindow { DataContext = shell, Height = 1250 };
        try
        {
            window.Show();
            await vm.PrepareCommand.ExecuteAsync(await CollectionTransferTests.Source(test,
                CollectionTransferTests.Natural(4), CollectionTransferTests.Natural(6, "999")));
            Layout(window);
            Assert.False(vm.HasError, vm.Error); Assert.True(vm.HasPreview); Assert.False(vm.ApplyCommand.CanExecute(null));
            Assert.Equal(1, vm.Preview!.Unresolved);
            var list = Find<ListBox>(window, "ImportPreviewRows");
            list.SelectedItem = vm.Rows[1]; Layout(window);
            Assert.Equal(2, vm.SelectedRow!.Number);
            var expander = window.GetVisualDescendants().OfType<Expander>().Single();
            expander.IsExpanded = true; Layout(window);
            Find<TextBox>(window, "ImportTargetSearch").Text = "Negate";
            Layout(window);
            await vm.SearchTargetsCommand.ExecuteAsync(null);
            Assert.Equal("Negate", Assert.Single(vm.Targets).Name);
            Assert.True(vm.Targets[0].Manual);
            Find<ComboBox>(window, "ImportTargetPicker").SelectedItem = vm.Targets[0]; Layout(window);
            await Assert.IsAssignableFrom<IAsyncRelayCommand>(Find<Button>(window, "LinkCollectionRow").Command).ExecuteAsync(null);
            Assert.True(vm.Preview!.CanApply); Assert.Equal(0, vm.Preview.Unresolved);
            expander.IsExpanded = false; Layout(window);
            var review = Find<CheckBox>(window, "ReviewCollectionChanges");
            review.IsChecked = true; Layout(window);
            Assert.True(vm.Reviewed);
            Assert.True(Find<Button>(window, "ApplyCollectionImport").IsEffectivelyEnabled);
            Capture(window, "transfer-preview");
            await Assert.IsAssignableFrom<IAsyncRelayCommand>(Find<Button>(window, "ApplyCollectionImport").Command).ExecuteAsync(null);
            Assert.False(vm.HasError, vm.Error);
            Assert.Equal(10, (await test.Service.GetSummaryAsync()).TotalCards);
            Assert.Equal(CollectionImportState.Applied, vm.Preview.State);
            Find<CheckBox>(window, "ConfirmCollectionUndo").IsChecked = true; Layout(window);
            await Assert.IsAssignableFrom<IAsyncRelayCommand>(Find<Button>(window, "UndoCollectionImport").Command).ExecuteAsync(null);
            Assert.False(vm.HasError, vm.Error);
            Assert.Equal(5, (await test.Service.GetSummaryAsync()).TotalCards);
            Assert.Equal(CollectionImportState.Undone, vm.Preview.State);
            var scroll = window.GetVisualDescendants().OfType<ScrollViewer>().First(x => x.Content is ContentControl);
            scroll.ScrollToEnd(); Layout(window);
            Capture(window, "transfer-history");
            await shell.NavigateCommand.ExecuteAsync("dashboard");
            Assert.Equal(5, Assert.IsType<DashboardViewModel>(shell.CurrentPage).TotalCards);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task CancelRemainsEnabledAndErrorsDoNotLeaveBusyStateStuck()
    {
        await using var test = new TestDatabase();
        var shell = test.Provider.GetRequiredService<LegacyShell>();
        await shell.InitializeCommand.ExecuteAsync(null);
        await shell.NavigateCommand.ExecuteAsync("transfer");
        var vm = Assert.IsType<CollectionTransferViewModel>(shell.CurrentPage);
        var path = await CollectionTransferTests.Source(test, CollectionTransferTests.Natural(4));
        var gate = test.Provider.GetRequiredService<DatabaseWriteLock>().Gate;
        await gate.WaitAsync();
        try
        {
            var pending = vm.PrepareCommand.ExecuteAsync(path);
            Assert.True(vm.IsWorking); Assert.False(shell.Operations.IsBusy);
            Assert.True(vm.CancelCommand.CanExecute(null));
            vm.CancelCommand.Execute(null);
            await pending;
            Assert.False(vm.IsWorking); Assert.False(vm.HasError, vm.Error);
            Assert.Contains("Geannuleerd", vm.Status);
        }
        finally { gate.Release(); }
        Assert.Empty(await CollectionTransferTests.Transfer(test).GetHistoryAsync());
        await File.WriteAllTextAsync(path, "invalid");
        await vm.PrepareCommand.ExecuteAsync(path);
        Assert.True(vm.HasError); Assert.False(vm.IsWorking);
        path = await CollectionTransferTests.Source(test, CollectionTransferTests.Natural(4));
        await vm.PrepareCommand.ExecuteAsync(path);
        Assert.False(vm.HasError, vm.Error); Assert.True(vm.HasPreview);
    }

    private static T Find<T>(Window window, string name) where T : Control => window.GetVisualDescendants().OfType<T>().Single(x => x.Name == name);
    private static void Layout(Window window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private static void Capture(Window window, string name)
    {
        var directory = Environment.GetEnvironmentVariable("MTGDECKBUILDER_SCREENSHOTS");
        if (string.IsNullOrEmpty(directory)) return;
        Directory.CreateDirectory(directory); Layout(window);
        using var bitmap = window.CaptureRenderedFrame(); Assert.NotNull(bitmap);
        bitmap.Save(Path.Combine(directory, name + ".png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }
}
