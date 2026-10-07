using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LyricsX.Core.Storage;
using LyricsX.Windows.App.Composition;

namespace LyricsX.Windows.App.Preferences;

/// <summary>
/// Player page: GSMTC sources are discovered live, so the choice is between
/// automatic ranking (default) and pinning one application's sessions. An
/// empty id means automatic; a pinned source that is not running falls back
/// to automatic inside the ranker.
/// </summary>
public partial class PlayerPreferencesViewModel : ObservableObject
{
    public const string AutomaticSourceId = "";

    private readonly AppSettingsAccessor settingsAccessor;

    [ObservableProperty]
    public partial string? ValidationError { get; set; }

    [ObservableProperty]
    public partial string SelectedSourceId { get; set; }

    public ObservableCollection<string> KnownSources { get; } = [];

    public string AutomaticLabel { get; } = "自动选择正在播放的会话";

    public PlayerPreferencesViewModel(AppSettingsAccessor settingsAccessor)
    {
        this.settingsAccessor = settingsAccessor;
        SelectedSourceId = settingsAccessor.Current.PreferredPlayerSourceId;
    }

    /// <summary>
    /// Merges live-observed source ids into the list; the pinned id stays
    /// selectable even when its app is not currently publishing a session.
    /// </summary>
    public void UpdateKnownSources(IEnumerable<string> sourceIds)
    {
        foreach (var source in sourceIds
            .Where(source => !string.IsNullOrEmpty(source))
            .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!KnownSources.Contains(source, StringComparer.OrdinalIgnoreCase))
            {
                KnownSources.Add(source);
            }
        }

        if (!string.IsNullOrEmpty(SelectedSourceId) &&
            !KnownSources.Contains(SelectedSourceId, StringComparer.OrdinalIgnoreCase))
        {
            KnownSources.Add(SelectedSourceId);
        }
    }

    public async Task<bool> ApplyAsync()
    {
        ValidationError = null;
        await settingsAccessor.ReplaceAsync(settingsAccessor.Current with
        {
            PreferredPlayerSourceId = SelectedSourceId.Trim()
        });
        return true;
    }
}
