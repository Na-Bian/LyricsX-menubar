using Microsoft.UI.Xaml;
using LyricsX.Windows.App.Composition;
using LyricsX.Platform.Windows.Shell;
using LyricsX.Windows.App.Services;
using Microsoft.Extensions.DependencyInjection;
using System.IO;

namespace LyricsX.Windows.App;

public partial class App : Application
{
    internal static IServiceProvider Services { get; private set; } = null!;

    internal MainWindow? Shell { get; private set; }

    public App()
    {
        try
        {
            InitializeComponent();
            Services = ServiceRegistration.BuildServices(AppDataRoot.Resolve());
        }
        catch (Exception initError)
        {
            StartupDiagnostics.TraceFile("init: " + initError);
            throw;
        }
    }

    internal static AppRuntime? Runtime { get; private set; }

    internal static Services.SingleInstanceService? SingletonService { get; set; }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        // single-instance election already happened in Program.Main; the
        // primary's pipe forwards second-instance commands here
        SingletonService!.CommandReceived += command =>
        {
            if (command == "show-preferences")
            {
                dispatcher.TryEnqueue(() => PreferencesWindowService.Show());
            }
            else if (command == "shutdown")
            {
                // uninstaller / second-instance shutdown: release and exit
                dispatcher.TryEnqueue(() => _ = ShutdownAsync());
            }
        };

        Shell = new MainWindow();
        Shell.Activate();

        Services.GetRequiredService<TrayIconService>().ExitRequested += (_, _) =>
            dispatcher.TryEnqueue(() => _ = ShutdownAsync());

        Runtime = await AppRuntime.StartAsync(Services, dispatcher);
    }

    private async Task ShutdownAsync()
    {
        if (Runtime is not null)
        {
            await Runtime.ShutdownAsync();
        }

        SingletonService?.Dispose();
        Environment.Exit(0);
    }
}


internal static class StartupDiagnostics
{
    internal static void TraceFile(string message)
    {
        try
        {
            Directory.CreateDirectory(AppDataRoot.Resolve());
            File.AppendAllText(
                Path.Combine(AppDataRoot.Resolve(), "startup-crash.txt"),
                DateTime.Now.ToString("o") + " " + message + Environment.NewLine);
        }
        catch
        {
            // last-resort diagnostics must never throw
        }
    }
}
