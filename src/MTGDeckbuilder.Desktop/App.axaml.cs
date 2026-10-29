using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MTGDeckbuilder.Desktop.ViewModels;
using MTGDeckbuilder.Infrastructure;
using MTGDeckbuilder.Application;
using Serilog;

namespace MTGDeckbuilder.Desktop;

public partial class App : Avalonia.Application
{
    private ServiceProvider? _services;
    private DataDirectoryLease? _lease;
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var paths = new AppPaths(Environment.GetEnvironmentVariable("MTGDECKBUILDER_DATA_DIR"));
            Directory.CreateDirectory(Path.GetDirectoryName(paths.LogPath)!);
            Log.Logger = new LoggerConfiguration().MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
                .WriteTo.File(paths.LogPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14, shared: true)
                .CreateLogger();
            try { _lease = DataDirectoryLease.Acquire(paths); }
            catch (ArgumentException ex)
            {
                Log.Warning(ex, "Data directory already in use");
                desktop.MainWindow = new Window { Title = "MTG Deckbuilder — al geopend", Width = 540, Height = 180,
                    Content = new TextBlock { Text = ex.Message, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Margin = new Thickness(24) } };
                base.OnFrameworkInitializationCompleted(); return;
            }
            AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Fatal(e.ExceptionObject as Exception, "Unhandled application error");
            TaskScheduler.UnobservedTaskException += (_, e) => Log.Error(e.Exception, "Unobserved task error");
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.ClearProviders().AddSerilog(dispose: false));
            services.AddDeckbuilder(paths);
            services.AddSingleton<IDatabaseInitializer>(sp => new RecoveringDatabaseInitializer(
                sp.GetRequiredService<DatabaseInitializer>(), sp.GetRequiredService<SqliteBackupService>(), _lease!));
            services.AddSingleton<UiOperations>();
            services.AddSingleton<PaperViewModel>();
            services.AddSingleton<DeckWizardViewModel>();
            services.AddSingleton<SettingsViewModel>();
            services.AddSingleton<ShellViewModel>();
            _services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
            var shell = _services.GetRequiredService<ShellViewModel>();
            var window = new MainWindow { DataContext = shell };
            window.Opened += async (_, _) => await shell.InitializeCommand.ExecuteAsync(null);
            desktop.MainWindow = window;
            desktop.Exit += (_, _) => { Log.Information("Application closed"); _services.Dispose(); _lease.Dispose(); };
            Log.Information("Application {Version} started", shell.Version);
        }
        base.OnFrameworkInitializationCompleted();
    }
}
