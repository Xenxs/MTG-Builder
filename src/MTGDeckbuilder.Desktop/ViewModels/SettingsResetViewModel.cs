using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MTGDeckbuilder.Application;
namespace MTGDeckbuilder.Desktop.ViewModels;
public sealed partial class SettingsViewModel
{
    [ObservableProperty,NotifyPropertyChangedFor(nameof(CanReset))]private DatabaseResetReview? resetReview;
    [ObservableProperty,NotifyPropertyChangedFor(nameof(CanReset))]private string resetPhrase="";
    [ObservableProperty]private string resetResult="";
    public bool CanReset=>ResetReview is not null&&ResetPhrase=="WISSEN";
    [RelayCommand]private Task ReviewResetAsync()=>operations.RunAsync(async()=>
    {ResetReview=null;ResetPhrase="";ResetResult="";ResetReview=await reset.ReviewAsync();},"Wisvoorbeeld geladen; er is nog niets verwijderd");
    [RelayCommand]private Task ClearDatabaseAsync()=>operations.RunAsync(async()=>
    {if(ResetReview is null)throw new ArgumentException("Controleer eerst het wisvoorbeeld.");var result=await reset.ClearAsync(ResetReview.Id,ResetPhrase);ResetResult="Database geleegd. Voorbeelden blijven uitgeschakeld. Reservekopie: "+result.BackupPath;ResetReview=null;ResetPhrase="";await RefreshAsync();},"Database veilig geleegd; reservekopie behouden");
}
