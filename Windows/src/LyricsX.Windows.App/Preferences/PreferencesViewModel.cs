using CommunityToolkit.Mvvm.ComponentModel;
using LyricsX.Core.Storage;
using LyricsX.Platform.Windows.Shell;
using LyricsX.Windows.App.Composition;

namespace LyricsX.Windows.App.Preferences;

/// <summary>
/// Root preferences view model: holds the working copy of settings, validates
/// width/offset/font bounds, persists atomically on Apply, and reflects the
/// login-item registration when the startup toggle changes.
/// </summary>
public partial class PreferencesViewModel : ObservableObject
{
    public const int MinWidthDip = 80;
    public const int MaxWidthDip = 360;
    public const int MaxOffsetMilliseconds = 30_000;
    public const double MinFontSize = 9;
    public const double MaxFontSize = 32;

    private readonly AppSettingsAccessor settingsAccessor;
    private readonly StartupRegistrationService? startup;

    [ObservableProperty]
    public partial AppSettings Working { get; set; }

    [ObservableProperty]
    public partial string? ValidationError { get; set; }

    [ObservableProperty]
    public partial string? StartupError { get; set; }

    public PreferencesViewModel(
        AppSettingsAccessor settingsAccessor,
        StartupRegistrationService? startup = null)
    {
        this.settingsAccessor = settingsAccessor;
        this.startup = startup;
        Working = settingsAccessor.Current;
    }

    public int WidthDip
    {
        get => Working.WidthDip;
        set
        {
            if (Working.WidthDip != value)
            {
                Working = Working with { WidthDip = value };
                OnPropertyChanged(nameof(WidthDip));
            }
        }
    }

    public int UserOffsetMilliseconds
    {
        get => Working.UserOffsetMilliseconds;
        set
        {
            if (Working.UserOffsetMilliseconds != value)
            {
                Working = Working with { UserOffsetMilliseconds = value };
                OnPropertyChanged(nameof(UserOffsetMilliseconds));
            }
        }
    }

    public bool LaunchAtLogin
    {
        get => Working.LaunchAtLogin;
        set
        {
            if (Working.LaunchAtLogin != value)
            {
                Working = Working with { LaunchAtLogin = value };
                OnPropertyChanged(nameof(LaunchAtLogin));
            }
        }
    }

    public bool AutoMatchLyrics
    {
        get => Working.AutoMatchLyrics;
        set
        {
            if (Working.AutoMatchLyrics != value)
            {
                Working = Working with { AutoMatchLyrics = value };
                OnPropertyChanged(nameof(AutoMatchLyrics));
            }
        }
    }

    public bool HideWhenNoMedia
    {
        get => Working.HideWhenNoMedia;
        set
        {
            if (Working.HideWhenNoMedia != value)
            {
                Working = Working with { HideWhenNoMedia = value };
                OnPropertyChanged(nameof(HideWhenNoMedia));
            }
        }
    }

    public bool ShowWhenPaused
    {
        get => Working.ShowWhenPaused;
        set
        {
            if (Working.ShowWhenPaused != value)
            {
                Working = Working with { ShowWhenPaused = value };
                OnPropertyChanged(nameof(ShowWhenPaused));
            }
        }
    }

    public bool ShowNextLine
    {
        get => Working.ShowNextLine;
        set
        {
            if (Working.ShowNextLine != value)
            {
                Working = Working with { ShowNextLine = value };
                OnPropertyChanged(nameof(ShowNextLine));
            }
        }
    }

    public bool AutoScrollLongLines
    {
        get => Working.AutoScrollLongLines;
        set
        {
            if (Working.AutoScrollLongLines != value)
            {
                Working = Working with { AutoScrollLongLines = value };
                OnPropertyChanged(nameof(AutoScrollLongLines));
            }
        }
    }

    public bool FollowSystemAnimation
    {
        get => Working.FollowSystemAnimation;
        set
        {
            if (Working.FollowSystemAnimation != value)
            {
                Working = Working with { FollowSystemAnimation = value };
                OnPropertyChanged(nameof(FollowSystemAnimation));
            }
        }
    }

    public double FontSize
    {
        get => Working.FontSize;
        set
        {
            if (Working.FontSize != value)
            {
                Working = Working with { FontSize = value };
                OnPropertyChanged(nameof(FontSize));
            }
        }
    }

    public bool Validate()
    {
        if (Working.WidthDip is < MinWidthDip or > MaxWidthDip)
        {
            ValidationError = $"歌词宽度需在 {MinWidthDip}–{MaxWidthDip} 之间";
            return false;
        }

        if (Math.Abs(Working.UserOffsetMilliseconds) > MaxOffsetMilliseconds)
        {
            ValidationError = "歌词偏移不能超过 ±30 秒";
            return false;
        }

        if (Working.FontSize is < MinFontSize or > MaxFontSize)
        {
            ValidationError = $"歌词字号需在 {MinFontSize}–{MaxFontSize} 之间";
            return false;
        }

        ValidationError = null;
        return true;
    }

    public async Task<bool> ApplyAsync()
    {
        if (!Validate())
        {
            return false;
        }

        await settingsAccessor.ReplaceAsync(Working);
        await ApplyStartupRegistrationAsync();
        return true;
    }

    private async Task ApplyStartupRegistrationAsync()
    {
        if (startup is null)
        {
            return;
        }

        try
        {
            startup.SetEnabled(Working.LaunchAtLogin);
            StartupError = null;
        }
        catch (Exception exception)
        {
            StartupError = "设置开机自启失败: " + exception.Message;
        }

        await Task.CompletedTask;
    }

    public void ResetToDefaults() => Working = AppSettings.Default;
}
