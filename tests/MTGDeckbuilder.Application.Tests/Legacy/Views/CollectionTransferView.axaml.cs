using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MTGDeckbuilder.Desktop.ViewModels;

namespace MTGDeckbuilder.Desktop.Views;

public partial class CollectionTransferView : UserControl
{
    public CollectionTransferView() => InitializeComponent();
    private async void PrepareClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not CollectionTransferViewModel vm || vm.IsWorking) return;
        try
        {
            var top = TopLevel.GetTopLevel(this);
            if (top is null) return;
            var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Collectiebestand kiezen", AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("MTGDeckbuilder collectie JSON") { Patterns = ["*.json"] }]
            });
            var path = files.FirstOrDefault()?.TryGetLocalPath();
            if (path is not null) await vm.PrepareCommand.ExecuteAsync(path);
        }
        catch (Exception ex) { vm.HandleFilePickerError(ex); }
    }
    private async void ExportClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not CollectionTransferViewModel vm || vm.IsWorking) return;
        try
        {
            var top = TopLevel.GetTopLevel(this);
            if (top is null) return;
            var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Collectie exporteren", SuggestedFileName = "mijn-collectie.json", DefaultExtension = "json",
                FileTypeChoices = [new FilePickerFileType("MTGDeckbuilder collectie JSON") { Patterns = ["*.json"] }], ShowOverwritePrompt = true
            });
            var path = file?.TryGetLocalPath();
            if (path is not null) await vm.ExportCommand.ExecuteAsync(path);
        }
        catch (Exception ex) { vm.HandleFilePickerError(ex); }
    }
}
