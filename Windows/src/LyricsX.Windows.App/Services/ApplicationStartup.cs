namespace LyricsX.Windows.App.Services;

/// <summary>
/// The canonical startup/shutdown ordering, shared by the real lifetime
/// orchestration and its tests. Shutdown is the exact reverse of startup.
/// Exit codes: 0 success, 10 unsupported OS, 20 fatal init failure.
/// </summary>
public static class ApplicationStartup
{
    public static readonly string[] Sequence =
    [
        "load-settings",
        "start-logging",
        "create-shell-window",
        "start-tray",
        "start-media-source",
        "start-selected-session",
        "start-lyrics-coordinator",
        "create-taskbar-window",
        "start-placement",
        "register-hotkeys",
        "register-startup"
    ];

    public static string[] ShutdownSequence => Sequence.Reverse().ToArray();

    public const int ExitCodeSuccess = 0;
    public const int ExitCodeUnsupportedOs = 10;
    public const int ExitCodeFatalInit = 20;
}
