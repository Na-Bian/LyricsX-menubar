using Microsoft.UI.Xaml;
using LyricsX.Windows.App.Composition;
using LyricsX.Windows.App.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LyricsX.Windows.App;

public partial class App : Application
{
    internal static IServiceProvider Services { get; private set; } = null!;

    internal MainWindow? Shell { get; private set; }

    public App()
    {
        InitializeComponent();
        Services = ServiceRegistration.BuildServices(AppDataRoot.Resolve());
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Shell = new MainWindow();
        Shell.Activate();
    }
}
