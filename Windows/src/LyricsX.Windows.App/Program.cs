using LyricsX.Windows.App.Services;

namespace LyricsX.Windows.App;

/// <summary>
/// Hand-rolled entry point: OS version gate before any WinUI init, single
/// instance enforcement via named mutex + command pipe, --startup passthrough.
/// Exit codes: 0 normal, 10 unsupported OS, 20 fatal init failure.
/// </summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            NativeMessageBox.Show(
                "LyricsX",
                "LyricsX 需要 Windows 11 (build 22000) 或更高版本。\n" +
                "LyricsX requires Windows 11 (build 22000) or later.");
            return 10;
        }

        if (args.Any(arg => arg.Equals("--shutdown", StringComparison.OrdinalIgnoreCase)))
        {
            return SingleInstanceService.RequestShutdown() ? 0 : 0;
        }

        WinRT.ComWrappersSupport.InitializeComWrappers();
        try
        {
            var singleInstance = new SingleInstanceService();
            App.SingletonService = singleInstance;
            if (!singleInstance.TryBecomePrimary())
            {
                var requested = singleInstance.SendCommand("show-preferences");
                return requested ? 0 : 1;
            }

            Microsoft.UI.Xaml.Application.Start(_ =>
            {
                var context = new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                    Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                new App();
            });

            return 0;
        }
        catch (Exception)
        {
            return 20;
        }
    }
}
