using CommunityToolkit.Mvvm.ComponentModel;
using LyricsX.Core.Storage;
using LyricsX.Windows.App.Composition;

namespace LyricsX.Windows.App.Preferences;

public sealed partial class ProviderItemViewModel : ObservableObject
{
    public string Id { get; }

    public string DisplayName { get; }

    [ObservableProperty]
    public partial bool Enabled { get; set; }

    public int Priority { get; set; }

    public ProviderItemViewModel(string id, string displayName, bool enabled, int priority)
    {
        Id = id;
        DisplayName = displayName;
        Enabled = enabled;
        Priority = priority;
    }
}

/// <summary>
/// Source page: reorderable provider list (order is the tie-breaker after
/// match quality), at least one public source enabled, Spotify credentials
/// held only in Credential Manager.
/// </summary>
public partial class SourcePreferencesViewModel : ObservableObject
{
    private static readonly Dictionary<string, string> DisplayNames = new()
    {
        ["qq"] = "QQ 音乐",
        ["netease"] = "网易云音乐",
        ["kugou"] = "酷狗音乐",
        ["musixmatch"] = "Musixmatch",
        ["lrclib"] = "LRCLIB",
        ["spotify"] = "Spotify"
    };

    private readonly AppSettingsAccessor settingsAccessor;

    [ObservableProperty]
    public partial System.Collections.ObjectModel.ObservableCollection<ProviderItemViewModel> Providers { get; set; }

    [ObservableProperty]
    public partial string? ValidationError { get; set; }

    [ObservableProperty]
    public partial string SpotifyClientId { get; set; } = string.Empty;

    public string SpotifyConnectLabel { get; } = "连接 Spotify…";

    public string SpotifyDisconnectLabel { get; } = "断开连接";

    public SourcePreferencesViewModel(AppSettingsAccessor settingsAccessor)
    {
        this.settingsAccessor = settingsAccessor;
        Providers = new(settingsAccessor.Current.Providers
            .OrderBy(provider => provider.Priority)
            .Select(provider => new ProviderItemViewModel(
                provider.Id,
                DisplayNames.GetValueOrDefault(provider.Id, provider.Id),
                provider.Enabled,
                provider.Priority)));
    }

    public void MoveUp(ProviderItemViewModel item)
    {
        var index = Providers.IndexOf(item);
        if (index > 0)
        {
            Providers.Move(index, index - 1);
        }
    }

    public void MoveDown(ProviderItemViewModel item)
    {
        var index = Providers.IndexOf(item);
        if (index >= 0 && index < Providers.Count - 1)
        {
            Providers.Move(index, index + 1);
        }
    }

    public async Task<bool> ApplyAsync()
    {
        if (Providers.All(provider => !provider.Enabled))
        {
            ValidationError = "至少需要启用一个歌词来源";
            return false;
        }

        ValidationError = null;
        var providers = Providers
            .Select((provider, index) => new ProviderSetting(provider.Id, provider.Enabled, index))
            .ToArray();
        await settingsAccessor.ReplaceAsync(settingsAccessor.Current with { Providers = providers });
        return true;
    }
}
