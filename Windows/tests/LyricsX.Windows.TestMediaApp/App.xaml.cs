using Microsoft.UI.Xaml;

namespace LyricsX.Windows.TestMediaApp;

public partial class App : Application
{
    private MainWindow? mainWindow;

    public App()
    {
        InitializeComponent();
        Trace("app ctor");
        UnhandledException += (_, args) =>
            Trace("unhandled: " + args.Message + " / " + args.Exception?.StackTrace);
    }

    internal static void Trace(string message)
    {
        try
        {
            File.AppendAllText(@"C:\Users\31576\testapp-log.txt",
                DateTime.Now.ToString("o") + " " + message + Environment.NewLine);
        }
        catch
        {
        }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Trace("onlaunched");
        mainWindow = new MainWindow();
        Trace("window constructed");
        mainWindow.Activate();
        Trace("activated");
    }
}


