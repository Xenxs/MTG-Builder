using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MTGDeckbuilder.Desktop.ViewModels;

namespace MTGDeckbuilder.Desktop.Views;

public partial class PaperView : UserControl
{
    public PaperView() => InitializeComponent();
    private async void OpenWizardClicked(object? sender,RoutedEventArgs e)
    {if(TopLevel.GetTopLevel(this)?.DataContext is ShellViewModel shell)await shell.NavigateCommand.ExecuteAsync("wizard");}
    private async void ChooseCsvClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not PaperViewModel vm || vm.Operations.IsBusy) return;
        try
        {
            var top = TopLevel.GetTopLevel(this); if (top is null) return;
            var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            { Title = "Fysieke collectie / ManaBox CSV", AllowMultiple = false, FileTypeFilter = [new FilePickerFileType("CSV") { Patterns = ["*.csv"] }] });
            var path = files.FirstOrDefault()?.TryGetLocalPath();
            if (path is not null) await vm.PrepareCsvCommand.ExecuteAsync(path);
        }
        catch (Exception ex) { await vm.PickerErrorAsync(ex); }
    }
}

public partial class DeckWizardView : UserControl {public DeckWizardView()=>InitializeComponent();}

public partial class SettingsView : UserControl
{
    public SettingsView() => InitializeComponent();
    private async void ChooseBackupClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm || vm.Operations.IsBusy) return;
        try
        {
            var top = TopLevel.GetTopLevel(this); if (top is null) return;
            var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            { Title = "SQLite-back-up kiezen", AllowMultiple = false, FileTypeFilter = [new FilePickerFileType("SQLite-back-up") { Patterns = ["*.db"] }] });
            var path = files.FirstOrDefault()?.TryGetLocalPath();
            if (path is not null) await vm.InspectBackupFileCommand.ExecuteAsync(path);
        }
        catch (Exception ex) { await vm.HandleBackupPickerErrorAsync(ex); }
    }
}
