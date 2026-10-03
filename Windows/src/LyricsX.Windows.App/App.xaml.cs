using Microsoft.UI.Xaml;
using LyricsX.Windows.App.Composition;
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

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        // single-instance: primary hosts the command pipe; second instances
        // show preferences in the primary and exit
        singleInstance = new SingleInstanceService();
        if (!singleInstance.TryBecomePrimary())
        {
            singleInstance.SendCommand("show-preferences");
            Environment.Exit(0);
        }

        singleInstance.CommandReceived += command =>
        {
            if (command == "show-preferences")
            {
                dispatcher.TryEnqueue(() => PreferencesWindowService.Show());
            }
        };

        Shell = new MainWindow();
        Shell.Activate();

        Runtime = await AppRuntime.StartAsync(Services, dispatcher);
    }

    private SingleInstanceService? singleInstance;
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
