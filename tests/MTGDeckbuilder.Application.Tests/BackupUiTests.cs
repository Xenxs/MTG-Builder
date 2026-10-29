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
using MTGDeckbuilder.Infrastructure;
using Xunit;

namespace MTGDeckbuilder.Application.Tests;

public sealed class BackupUiTests
{
    [AvaloniaFact]
    public async Task SettingsBackupInspectionExplicitConfirmationSchedulingAndCancelAreBound()
    {
        await using var test = new TestDatabase(); await BackupRecoveryTests.Seed(test);
        var shell = test.Provider.GetRequiredService<ShellViewModel>(); await shell.InitializeCommand.ExecuteAsync(null); await shell.NavigateCommand.ExecuteAsync("settings");
        var vm = Assert.IsType<SettingsViewModel>(shell.CurrentPage); var window = new MainWindow { DataContext = shell, Height = 1150 };
        try
        {
            window.Show(); Layout(window); await Execute(window, "CreateBackup"); Assert.Single(vm.Backups);
            Assert.Contains("manual-", Find<SelectableTextBlock>(window, "BackupPath").Text);
            await Execute(window, "InspectBackup"); Assert.NotNull(vm.RestoreReview); Assert.Contains("1 bezitsregels", Find<TextBlock>(window, "RestoreSummary").Text);
            Assert.False(Find<Button>(window, "ScheduleRestore").IsEffectivelyEnabled);
            Find<CheckBox>(window, "ConfirmRestore").BringIntoView(); Layout(window); Capture(window, "backup-reviewed-confirmation");
            Find<CheckBox>(window, "ConfirmRestore").IsChecked = true; Layout(window); Assert.True(Find<Button>(window, "ScheduleRestore").IsEffectivelyEnabled);
            await Execute(window, "ScheduleRestore"); Assert.True(vm.RecoveryStatus!.Scheduled); Assert.Null(vm.RestoreReview);
            Find<Button>(window, "CancelRestore").BringIntoView(); Layout(window); Capture(window, "backup-restore-scheduled");
            await Execute(window, "CancelRestore"); Assert.False(vm.RecoveryStatus!.Scheduled); Assert.Equal("Before backup", await test.Service.GetPlayerNameAsync());
            await Execute(window, "CreateBackup"); Assert.Equal(2, vm.Backups.Count); await Execute(window, "InspectBackup");
            Find<CheckBox>(window, "ConfirmRestore").IsChecked = true; Layout(window);
            Find<ComboBox>(window, "BackupPicker").SelectedItem = vm.Backups.First(x => x != vm.SelectedBackup); Layout(window);
            Assert.Null(vm.RestoreReview); Assert.False(vm.ConfirmRestore); Assert.False(vm.CanScheduleRestore);
            Assert.False(vm.Operations.HasError, vm.Operations.ErrorMessage);
        }
        finally { window.Close(); }
    }
    [AvaloniaFact]
    public async Task ExternalFileCommandRejectsBadBackupCentrallyAndValidFileRecovers()
    {
        await using var test = new TestDatabase(); await BackupRecoveryTests.Seed(test);
        var shell = test.Provider.GetRequiredService<ShellViewModel>(); await shell.InitializeCommand.ExecuteAsync(null); await shell.NavigateCommand.ExecuteAsync("settings");
        var vm = Assert.IsType<SettingsViewModel>(shell.CurrentPage); var backup = await BackupRecoveryTests.Backups(test).CreateAsync();
        var invalid = Path.Combine(test.Paths.DataDirectory, "invalid.db"); await File.WriteAllBytesAsync(invalid, new byte[1024]);
        await vm.InspectBackupFileCommand.ExecuteAsync(invalid); Assert.True(vm.Operations.HasError); Assert.Null(vm.RestoreReview);
        await vm.InspectBackupFileCommand.ExecuteAsync(backup.Path); Assert.False(vm.Operations.HasError); Assert.NotNull(vm.RestoreReview);
        Assert.NotNull(new MTGDeckbuilder.Desktop.Views.SettingsView());
    }
    [AvaloniaFact]
    public async Task InvalidPendingCopyBlocksStartupAndCancelBeforeApplyRecoversWithoutDatabaseLoss()
    {
        await using var test = new TestDatabase(s =>
        {
            s.AddSingleton(sp => DataDirectoryLease.Acquire(sp.GetRequiredService<AppPaths>()));
            s.AddSingleton<IDatabaseInitializer>(sp => new RecoveringDatabaseInitializer(sp.GetRequiredService<DatabaseInitializer>(),
                sp.GetRequiredService<SqliteBackupService>(), sp.GetRequiredService<DataDirectoryLease>()));
        });
        await BackupRecoveryTests.Seed(test); var backups = BackupRecoveryTests.Backups(test); var backup = await backups.CreateAsync();
        var review = await backups.InspectAsync(backup.Path); await backups.ScheduleAsync(review.Id, true);
        await File.AppendAllTextAsync(Path.Combine(test.Paths.DataDirectory, "recovery", review.Id.ToString("N") + ".db"), "tampered");
        var shell = test.Provider.GetRequiredService<ShellViewModel>(); await shell.InitializeCommand.ExecuteAsync(null);
        Assert.False(shell.IsReady); Assert.True(shell.Operations.HasError); Assert.Contains("gewijzigd", shell.Operations.ErrorMessage);
        var window = new MainWindow { DataContext = shell };
        try
        {
            window.Show(); Layout(window); Capture(window, "backup-startup-blocked");
            await Execute(window, "CancelStartupRestore"); Assert.True(shell.IsReady); Assert.False(shell.Operations.HasError);
            Assert.Equal("Before backup", await test.Service.GetPlayerNameAsync());
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
