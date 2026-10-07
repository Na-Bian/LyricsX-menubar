using System.Diagnostics;
using LyricsX.Platform.Windows;
using LyricsX.Platform.Windows.Media;
using LyricsX.Platform.Windows.Shell;
using LyricsX.Windows.App.Preferences;
using LyricsX.Windows.App.Search;
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
    private readonly Stopwatch uptime = Stopwatch.StartNew();
    private ILyricsApplicationCoordinator? coordinator;

    public IReadOnlyList<string> StartupLog => log;

    public LyricsPresentationState Current => services
        .GetRequiredService<ILyricsApplicationCoordinator>().Current;

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
        coordinator = services.GetRequiredService<ILyricsApplicationCoordinator>();

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

            // click menus: compact (left) and full (right) flyouts over the
            // lyric surface, plus their UI-facing actions
            var menu = services.GetRequiredService<LyricsX.Windows.App.Menus.LyricsMenuViewModel>();
            menu.PreferencesRequested += () =>
                dispatcher.TryEnqueue(() => PreferencesWindowService.Show());
            menu.ManualSearchRequested += () => dispatcher.TryEnqueue(() =>
                ManualSearchWindowService.Show(lyricWindow.AppWindow.Id));
            menu.LocalImportRequested += () => dispatcher.TryEnqueue(() =>
                ManualSearchWindowService.Show(lyricWindow.AppWindow.Id));
            menu.ExportRequested += () => dispatcher.TryEnqueue(() =>
                _ = ExportCurrentLyricsAsync(coordinator, lyricWindow));
            menu.OpenLogsRequested += OpenLogsFolder;
            menu.CopyDiagnosticsRequested += () => dispatcher.TryEnqueue(() =>
                _ = CopyDiagnosticsAsync());
            menu.ExitRequested += () => dispatcher.TryEnqueue(() => _ = ShutdownAsync());
            lyricWindow.AttachMenus(menu);
        });

        // 9. placement observation drives window visibility
        TryStep("start-placement", () =>
        {
            _ = services.GetRequiredService<ITaskbarPlacementService>().RunAsync(
                CancellationToken.None);
            services.GetRequiredService<LyricsWindowPresenter>().Enable();
        });

        // 10. hotkeys (this thread must own a message pump: the WinUI
        //     dispatcher thread qualifies). Persisted bindings win; the
        //     default Ctrl+Alt+L covers a fresh install.
        TryStep("register-hotkeys", () =>
        {
            var hotkeys = services.GetRequiredService<GlobalHotKeyService>();
            var settings = services.GetRequiredService<LyricsX.Windows.App.Composition.AppSettingsAccessor>();
            var bindings = LyricsX.Windows.App.Preferences.HotKeySettings.Parse(
                settings.Current.Extra is not null &&
                settings.Current.Extra.TryGetValue(LyricsX.Windows.App.Preferences.HotKeySettings.SettingsKey, out var serialized)
                    ? serialized
                    : null)
                .ToDictionary(binding => binding.ActionId);

            var showPreferences = bindings.GetValueOrDefault(
                LyricsX.Windows.App.Preferences.HotKeySettings.ShowPreferencesAction);
            hotkeys.TryRegister(1,
                showPreferences?.Modifiers ?? (HotKeyModifiers.Control | HotKeyModifiers.Alt),
                showPreferences?.VirtualKey ?? 'L');
            hotkeys.Pressed += (_, id) => dispatcher.TryEnqueue(() =>
                _ = RouteHotKeyAsync(id));

            static bool TryRegisterOptional(GlobalHotKeyService hotkeys, int id, HotKeyBinding? binding)
            {
                if (binding is null)
                {
                    return true;
                }

                return hotkeys.TryRegister(id, binding.Modifiers, binding.VirtualKey);
            }

            var registeredToggle = TryRegisterOptional(hotkeys, 2, bindings.GetValueOrDefault(
                LyricsX.Windows.App.Preferences.HotKeySettings.ToggleVisibilityAction));
            var registeredEarlier = TryRegisterOptional(hotkeys, 3, bindings.GetValueOrDefault(
                LyricsX.Windows.App.Preferences.HotKeySettings.EarlierOffsetAction));
            var registeredLater = TryRegisterOptional(hotkeys, 4, bindings.GetValueOrDefault(
                LyricsX.Windows.App.Preferences.HotKeySettings.LaterOffsetAction));
            if (!registeredToggle || !registeredEarlier || !registeredLater)
            {
                services.GetRequiredService<Diagnostics.AppLogger>().Log(
                    Diagnostics.LogLevel.Warning, "hotkeys", "W0101",
                    "one or more custom shortcuts conflicted and were skipped");
            }
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

    private static void OpenLogsFolder()
    {
        try
        {
            var logs = Path.Combine(AppDataRoot.Resolve(), "logs");
            Directory.CreateDirectory(logs);
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = logs,
                UseShellExecute = true
            });
        }
        catch (Exception)
        {
            // explorer failures are non-fatal; logs remain on disk
        }
    }

    private async Task CopyDiagnosticsAsync()
    {
        try
        {
            var report = new Diagnostics.DiagnosticReportBuilder(
                typeof(AppRuntime).Assembly.GetName().Version?.ToString(3) ?? "0.1.0",
                Path.Combine(AppDataRoot.Resolve(), "logs"))
                .CreateText(
                    Environment.OSVersion.VersionString,
                    typeof(Microsoft.UI.Xaml.Application).Assembly.GetName().Version?.ToString(3) ?? "?",
                    System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
                    processUptime: uptime.Elapsed,
                    "redacted",
                    "redacted",
                    [],
                    coordinator?.CurrentFingerprint is { Length: > 1 }
                        ? coordinator.CurrentFingerprint[..2] + "…"
                        : "none",
                    []);
            var package = new global::Windows.ApplicationModel.DataTransfer.DataPackage();
            package.SetText(report);
            global::Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
        }
        catch (Exception)
        {
            // clipboard can be locked by the foreground app; nothing to recover
        }

        await Task.CompletedTask;
    }

    private static async Task ExportCurrentLyricsAsync(
        ILyricsApplicationCoordinator coordinator,
        TaskbarLyricsWindow window)
    {
        var document = coordinator.Current.Document;
        if (document is null)
        {
            return;
        }

        try
        {
            var picker = new global::Windows.Storage.Pickers.FileSavePicker
            {
                SuggestedStartLocation = global::Windows.Storage.Pickers.PickerLocationId.MusicLibrary
            };
            picker.FileTypeChoices.Add("LRCX 歌词", new List<string> { ".lrcx" });
            picker.FileTypeChoices.Add("LRC 歌词", new List<string> { ".lrc" });
            WinRT.Interop.InitializeWithWindow.Initialize(
                picker, WinRT.Interop.WindowNative.GetWindowHandle(window));

            var file = await picker.PickSaveFileAsync();
            if (file is null)
            {
                return;
            }

            var useLrcx = file.FileType.Equals(".lrcx", StringComparison.OrdinalIgnoreCase);
            var bytes = new LyricsX.Windows.App.LyricFiles.LyricExportService()
                .Export(document, useLrcx);
            await global::Windows.Storage.FileIO.WriteBytesAsync(file, bytes);
        }
        catch (Exception)
        {
            // picker cancellation and IO failures both land here silently
        }
    }

    private async Task RouteHotKeyAsync(int id)
    {
        var coordinator = services.GetRequiredService<ILyricsApplicationCoordinator>();
        switch (id)
        {
            case 1:
                PreferencesWindowService.Show();
                break;
            case 2:
                await coordinator.ToggleLyricsVisibilityAsync();
                break;
            case 3:
                await coordinator.AdjustOffsetAsync(TimeSpan.FromMilliseconds(-500), CancellationToken.None);
                break;
            case 4:
                await coordinator.AdjustOffsetAsync(TimeSpan.FromMilliseconds(500), CancellationToken.None);
                break;
        }
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
