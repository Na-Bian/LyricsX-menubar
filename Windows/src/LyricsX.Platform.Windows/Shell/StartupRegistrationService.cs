using Microsoft.Win32;

namespace LyricsX.Platform.Windows.Shell;

public interface IStartupRegistrationService
{
    bool IsEnabled { get; }

    void SetEnabled(bool enabled);
}

/// <summary>
/// Per-user startup registration under HKCU Run; never elevates, only ever
/// touches its own value name. Exposes the raw write for testability.
/// </summary>
public sealed class StartupRegistrationService(string valueName = "LyricsX") : IStartupRegistrationService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(valueName) is not null;
        }
    }

    public void SetEnabled(bool enabled) =>
        SetEnabled(enabled, AppContext.BaseDirectory + "LyricsX.Windows.App.exe");

    public void SetEnabled(bool enabled, string executablePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
        {
            key.SetValue(valueName, "\"" + executablePath + "\" --startup");
        }
        else
        {
            key.DeleteValue(valueName, throwOnMissingValue: false);
        }
    }
}
