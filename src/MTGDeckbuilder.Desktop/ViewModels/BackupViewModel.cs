using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MTGDeckbuilder.Application;

namespace MTGDeckbuilder.Desktop.ViewModels;

public sealed partial class SettingsViewModel
{
    public ObservableCollection<BackupFile> Backups { get; } = [];
    [ObservableProperty] private BackupFile? selectedBackup;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasRestoreReview)), NotifyPropertyChangedFor(nameof(CanScheduleRestore))] private RestoreReview? restoreReview;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanScheduleRestore))] private bool confirmRestore;
    [ObservableProperty] private RecoveryStatus? recoveryStatus;
    public bool HasRestoreReview => RestoreReview is not null;
    public bool CanScheduleRestore => RestoreReview is not null && ConfirmRestore && RecoveryStatus?.Scheduled != true;
    public string BackupsDirectory => paths.BackupsDirectory;
    partial void OnSelectedBackupChanged(BackupFile? value) { RestoreReview = null; ConfirmRestore = false; }
    private async Task RefreshBackupsAsync()
    {
        var previous = SelectedBackup?.Path; var list = await backups.ListAsync();
        Backups.Clear(); foreach (var file in list) Backups.Add(file);
        SelectedBackup = Backups.FirstOrDefault(x => x.Path == previous) ?? Backups.FirstOrDefault();
        RecoveryStatus = await backups.StatusAsync(); OnPropertyChanged(nameof(CanScheduleRestore));
    }
    [RelayCommand]
    private Task CreateBackupAsync() => operations.RunAsync(async () =>
    {
        var file = await backups.CreateAsync(); await RefreshBackupsAsync(); SelectedBackup = Backups.Single(x => x.Path == file.Path);
    }, "Gecontroleerde back-up gemaakt. Bewaar zelf een extra kopie buiten deze gegevensmap.");
    [RelayCommand]
    private Task ReloadBackupsAsync() => operations.RunAsync(RefreshBackupsAsync, "Back-ups en herstelstatus vernieuwd");
    [RelayCommand]
    private Task InspectBackupAsync() => operations.RunAsync(async () =>
    {
        var file = SelectedBackup ?? throw new ArgumentException("Kies eerst een back-up.");
        RestoreReview = null; ConfirmRestore = false;
        var review = await backups.InspectAsync(file.Path);
        if (SelectedBackup == file) RestoreReview = review;
    }, "Herstelvoorbeeld gecontroleerd; er is niets teruggezet.");
    [RelayCommand]
    private Task InspectBackupFileAsync(string path) => operations.RunAsync(async () =>
    {
        RestoreReview = null; ConfirmRestore = false; SelectedBackup = null;
        RestoreReview = await backups.InspectAsync(path);
    }, "Externe back-up gecontroleerd; er is niets teruggezet.");
    [RelayCommand]
    private Task ScheduleRestoreAsync() => operations.RunAsync(async () =>
    {
        if (RestoreReview is not { } review) throw new ArgumentException("Controleer eerst een herstelvoorbeeld.");
        await backups.ScheduleAsync(review.Id, ConfirmRestore);
        RestoreReview = null; ConfirmRestore = false; await RefreshBackupsAsync();
    }, "Herstel gepland. Sluit de app en start opnieuw om de database terug te zetten.");
    [RelayCommand]
    private Task CancelRestoreAsync() => operations.RunAsync(async () =>
    {
        await backups.CancelAsync(); RestoreReview = null; ConfirmRestore = false; await RefreshBackupsAsync();
    }, "Gepland herstel geannuleerd; de database is niet teruggezet.");
    public Task HandleBackupPickerErrorAsync(Exception ex) => operations.RunAsync(() => Task.FromException(ex), "");
}
