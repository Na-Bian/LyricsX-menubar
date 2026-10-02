using CommunityToolkit.Mvvm.ComponentModel;
using LyricsX.Core.Storage;
using LyricsX.Windows.App.Composition;

namespace LyricsX.Windows.App.Preferences;

/// <summary>
/// Root preferences view model: holds the working copy of settings, validates
/// width/offset/font bounds, and persists atomically on Apply.
/// </summary>
public partial class PreferencesViewModel : ObservableObject
{
    public const int MinWidthDip = 80;
    public const int MaxWidthDip = 360;
    public const int MaxOffsetMilliseconds = 30_000;

    private readonly AppSettingsAccessor settingsAccessor;

    [ObservableProperty]
    public partial AppSettings Working { get; set; }

    [ObservableProperty]
    public partial string? ValidationError { get; set; }

    [ObservableProperty]
    public partial string? StartupError { get; set; }

    public PreferencesViewModel(AppSettingsAccessor settingsAccessor)
    {
        this.settingsAccessor = settingsAccessor;
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
        return true;
    }

    public void ResetToDefaults() => Working = AppSettings.Default;
}
