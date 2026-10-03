using System.Diagnostics;
using LyricsX.Platform.Windows;
using LyricsX.Platform.Windows.Media;
using LyricsX.Platform.Windows.Shell;
using LyricsX.Windows.App.Taskbar;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace LyricsX.Windows.App.Services;

/// <summary>
/// Wires the plan-defined startup sequence to real services and runs
/// shutdown in reverse. All steps log; a failing optional step (tray,
/// hotkeys, startup registration) is recorded and skipped rather than
/// aborting the launch.
/// </summary>
public sealed class AppRuntime : IAsyncDisposable
{
    private readonly IServiceProvider services;
    private readonly DispatcherQueue dispatcher;
    private readonly string[] log = [];

    public IReadOnlyList<string> StartupLog => log;

    private AppRuntime(IServiceProvider services, DispatcherQueue dispatcher)
    {
        this.services = services;
        this.dispatcher = dispatcher;
    }

    public static async Task<AppRuntime> StartAsync(
        IServiceProvider services, DispatcherQueue dispatcher)
    {
        var runtime = new AppRuntime(services, dispatcher);
        await runtime.RunStartupAsync();
        return runtime;
    }

    private async Task RunStartupAsync()
    {
        var logger = services.GetRequiredService<Diagnostics.AppLogger>();
        logger.Log(Diagnostics.LogLevel.Info, "lifetime", "L0001", "startup begin");

        // 1. settings are loaded inside ServiceRegistration.BuildServices
        // 2. logging started above
        // 3. shell window (this process's hidden message surface) exists — App
        // 4. tray
        TryStep("start-tray", () =>
        {
            var tray = services.GetRequiredService<TrayIconService>();
            tray.Show();
            tray.OpenPreferencesRequested += (_, _) =>
                dispatcher.TryEnqueue(() => PreferencesWindowService.Show());
        });

        // 5-7. media pipeline
        var mediaSource = services.GetRequiredService<GlobalMediaSessionSource>();
        var selected = services.GetRequiredService<ISelectedMediaSession>();
        var coordinator = services.GetRequiredService<ILyricsApplicationCoordinator>();

        await StepAsync("start-media-source",
            () => mediaSource.StartAsync(CancellationToken.None));
        await StepAsync("start-selected-session",
            () => selected.StartAsync(CancellationToken.None));
        await StepAsync("start-lyrics-coordinator",
            () => coordinator.StartAsync(CancellationToken.None));

        // 8. taskbar lyric window + host
        TaskbarLyricsWindow? lyricWindow = null;
        TryStep("create-taskbar-window", () =>
        {
            lyricWindow = new TaskbarLyricsWindow();
            var presenter = services.GetRequiredService<LyricsWindowPresenter>();
            presenter.Attach(lyricWindow);
            coordinator.StateChanged += (_, state) =>
                presenter.ApplyState(state);
        });

        // 9. placement observation drives window visibility
        TryStep("start-placement", () =>
        {
            _ = services.GetRequiredService<ITaskbarPlacementService>().RunAsync(
                CancellationToken.None);
            services.GetRequiredService<LyricsWindowPresenter>().Enable();
        });

        // 10. hotkeys (this thread must own a message pump: the WinUI
        //     dispatcher thread qualifies)
        TryStep("register-hotkeys", () =>
        {
            var hotkeys = services.GetRequiredService<GlobalHotKeyService>();
            hotkeys.TryRegister(1, HotKeyModifiers.Control | HotKeyModifiers.Alt, 'L');
            hotkeys.Pressed += (_, id) =>
                dispatcher.TryEnqueue(() => PreferencesWindowService.Show());
        });

        // 11. startup registration reflects settings
        TryStep("register-startup", () =>
        {
            var startup = services.GetRequiredService<StartupRegistrationService>();
            if (services.GetRequiredService<LyricsX.Windows.App.Composition.AppSettingsAccessor>().Current.LaunchAtLogin)
            {
                startup.SetEnabled(true);
            }
        });

        logger.Log(Diagnostics.LogLevel.Info, "lifetime", "L0002", "startup complete");
    }

    public async Task ShutdownAsync()
    {
        var logger = services.GetRequiredService<Diagnostics.AppLogger>();
        logger.Log(Diagnostics.LogLevel.Info, "lifetime", "L0003", "shutdown begin");
        logger.Dispose(); // flush + enforce retention (7 days / 50 MiB)

        await SafeAsync("dispose-coordinator",
            () => services.GetRequiredService<ILyricsApplicationCoordinator>().DisposeAsync().AsTask());
        await SafeAsync("dispose-media",
            () => services.GetRequiredService<ISelectedMediaSession>().DisposeAsync().AsTask());
        await SafeAsync("dispose-media-source",
            () => services.GetRequiredService<GlobalMediaSessionSource>().DisposeAsync().AsTask());
        Safe("remove-tray", () => services.GetRequiredService<TrayIconService>().Dispose());

        logger.Log(Diagnostics.LogLevel.Info, "lifetime", "L0004", "shutdown complete");
    }

    private void TryStep(string step, Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            services.GetRequiredService<Diagnostics.AppLogger>().Log(
                Diagnostics.LogLevel.Warning, "lifetime", "W01" + step.Length,
                step + " skipped: " + exception.Message);
        }
    }

    private async Task StepAsync(string step, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            services.GetRequiredService<Diagnostics.AppLogger>().Log(
                Diagnostics.LogLevel.Warning, "lifetime", "W02" + step.Length,
                step + " failed: " + exception.Message);
        }
    }

    private async Task SafeAsync(string step, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            services.GetRequiredService<Diagnostics.AppLogger>().Log(
                Diagnostics.LogLevel.Warning, "lifetime", "W03" + step.Length,
                step + " failed: " + exception.Message);
        }
    }

    private void Safe(string step, Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            services.GetRequiredService<Diagnostics.AppLogger>().Log(
                Diagnostics.LogLevel.Warning, "lifetime", "W04" + step.Length,
                step + " failed: " + exception.Message);
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
