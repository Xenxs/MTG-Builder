using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;

namespace MTGDeckbuilder.Desktop.ViewModels;

public sealed partial class UiOperations(ILogger<UiOperations> logger) : ObservableObject
{
    [ObservableProperty] private bool isBusy;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasError))] private string? errorMessage;
    [ObservableProperty] private string status = "Database voorbereiden…";
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public static int ReadQuantity(decimal value)
    {
        if (value != decimal.Truncate(value) || value < 0 || value > 9999)
            throw new ArgumentException("Gebruik een geheel aantal kaarten tussen 0 en 9999.");
        return (int)value;
    }

    public async Task RunAsync(Func<Task> action, string success)
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = null;
        Status = "Bezig…";
        try { await action(); Status = success; }
        catch (ArgumentException ex) { ErrorMessage = ex.Message; Status = "Controleer de invoer"; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Operation failed");
            ErrorMessage = "De bewerking is mislukt. Probeer opnieuw. Technische details staan in het logbestand bij Instellingen.";
            Status = "Bewerking mislukt";
        }
        finally { IsBusy = false; }
    }
}
