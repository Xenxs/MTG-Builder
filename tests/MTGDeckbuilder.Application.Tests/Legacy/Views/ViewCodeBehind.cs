using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MTGDeckbuilder.Desktop.ViewModels;

namespace MTGDeckbuilder.Desktop.Views;

public partial class DashboardView : UserControl { public DashboardView() => InitializeComponent(); }
public partial class CollectionView : UserControl { public CollectionView() => InitializeComponent(); }
public partial class ArchetypePoolView : UserControl { public ArchetypePoolView() => InitializeComponent(); }
public partial class DeckGenerationView : UserControl { public DeckGenerationView() => InitializeComponent(); }
public partial class DecksView : UserControl
{
    public DecksView() => InitializeComponent();
    private async void DeckSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is DecksViewModel vm) await vm.LoadDeckCommand.ExecuteAsync(null);
    }
    private async void ExportDeckClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not DecksViewModel vm || vm.Operations.IsBusy) return;
        try
        {
            var top = TopLevel.GetTopLevel(this); if (top is null) return;
            var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Decktekst opslaan", SuggestedFileName = "mijn-deck.txt", DefaultExtension = "txt", ShowOverwritePrompt = true,
                FileTypeChoices = [new FilePickerFileType("Arena-decktekst") { Patterns = ["*.txt"] }]
            });
            var path = file?.TryGetLocalPath();
            if (path is not null) await vm.SaveExportFileCommand.ExecuteAsync(path);
        }
        catch (Exception ex) { await vm.HandleExportFilePickerErrorAsync(ex); }
    }
}
