using Avalonia;
using Serilog;

namespace MTGDeckbuilder.Desktop;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        try { return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args); }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
            return 1;
        }
        finally { Log.CloseAndFlush(); }
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect();
}
