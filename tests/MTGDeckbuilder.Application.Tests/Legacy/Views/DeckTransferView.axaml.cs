using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MTGDeckbuilder.Desktop.ViewModels;

namespace MTGDeckbuilder.Desktop.Views;

public partial class DeckTransferView : UserControl
{
    public DeckTransferView() => InitializeComponent();
    private async void ReadFileClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not DeckTransferViewModel vm || vm.IsWorking) return;
        try
        {
            var top = TopLevel.GetTopLevel(this); if (top is null) return;
            var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Decktekst kiezen", AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("Arena-decktekst") { Patterns = ["*.txt"] }]
            });
            var path = files.FirstOrDefault()?.TryGetLocalPath();
            if (path is not null) await vm.ReadFileCommand.ExecuteAsync(path);
        }
        catch (Exception ex) { vm.HandleFilePickerError(ex); }
    }
}
