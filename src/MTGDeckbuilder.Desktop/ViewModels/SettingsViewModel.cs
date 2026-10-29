using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MTGDeckbuilder.Application;
using MTGDeckbuilder.Domain;
using MTGDeckbuilder.Infrastructure;

namespace MTGDeckbuilder.Desktop.ViewModels;

public sealed partial class SettingsViewModel(DeckbuilderService service, UiOperations operations, AppPaths paths, IBackupService backups, IDatabaseResetService reset) : PageViewModel
{
    public UiOperations Operations => operations;
    public string DatabasePath => paths.DatabasePath;
    public string LogDirectory => Path.GetDirectoryName(paths.LogPath)!;
    [ObservableProperty] private string playerName = "";
    public override async Task RefreshAsync()
    {
        PlayerName = await service.GetPlayerNameAsync();
        await RefreshBackupsAsync();
    }
    [RelayCommand]
    private Task SaveAsync() => operations.RunAsync(async () => await service.SavePlayerNameAsync(PlayerName), "Instellingen opgeslagen");
}
