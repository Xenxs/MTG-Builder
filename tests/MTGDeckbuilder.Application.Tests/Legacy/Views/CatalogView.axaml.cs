using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MTGDeckbuilder.Desktop.ViewModels;

namespace MTGDeckbuilder.Desktop.Views;

public partial class CatalogView : UserControl
{
    public CatalogView() => InitializeComponent();
    private async void ImportFileClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not CatalogViewModel vm || vm.IsImporting) return;
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null) return;
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Scryfall-catalogus importeren", AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("Scryfall JSON / JSONL / gzip") { Patterns = ["*.json", "*.jsonl", "*.gz"] }]
            });
            var path = files.FirstOrDefault()?.TryGetLocalPath();
            if (path is not null) await vm.ImportFileCommand.ExecuteAsync(path);
        }
        catch (Exception ex) { vm.HandleFilePickerError(ex); }
    }
}
