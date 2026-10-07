using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LyricsX.Core.Storage;
using LyricsX.Windows.App.Composition;

namespace LyricsX.Windows.App.Preferences;

/// <summary>
/// Filter page: strict search matching, blocked phrases (removed from every
/// fetched lyric by the transform), and the ignore lists populated by
/// "忽略这首歌". Fingerprints are opaque, so they surface as counts with a
/// single clear action.
/// </summary>
public partial class FilterPreferencesViewModel : ObservableObject
{
    private readonly AppSettingsAccessor settingsAccessor;

    [ObservableProperty]
    public partial string? ValidationError { get; set; }

    [ObservableProperty]
    public partial bool StrictSearch { get; set; }

    [ObservableProperty]
    public partial string? NewPhrase { get; set; }

    [ObservableProperty]
    public partial int IgnoredTrackCount { get; private set; }

    [ObservableProperty]
    public partial int IgnoredAlbumCount { get; private set; }

    public ObservableCollection<string> BlockedPhrases { get; } = [];

    public FilterPreferencesViewModel(AppSettingsAccessor settingsAccessor)
    {
        this.settingsAccessor = settingsAccessor;
        StrictSearch = settingsAccessor.Current.StrictSearch;
        foreach (var phrase in settingsAccessor.Current.BlockedPhrases ?? [])
        {
            BlockedPhrases.Add(phrase);
        }

        RefreshIgnoredCounts();
    }

    public void AddPhrase(string? phrase)
    {
        phrase = phrase?.Trim();
        if (string.IsNullOrEmpty(phrase))
        {
            ValidationError = "屏蔽词不能为空";
            return;
        }

        if (BlockedPhrases.Contains(phrase, StringComparer.Ordinal))
        {
            ValidationError = "该屏蔽词已存在";
            return;
        }

        ValidationError = null;
        BlockedPhrases.Add(phrase);
        NewPhrase = string.Empty;
    }

    public void RemovePhrase(string phrase)
    {
        BlockedPhrases.Remove(phrase);
    }

    public async Task<bool> ClearIgnoredAsync()
    {
        ValidationError = null;
        await settingsAccessor.ReplaceAsync(settingsAccessor.Current with
        {
            IgnoredTrackFingerprints = [],
            IgnoredAlbumFingerprints = []
        });
        RefreshIgnoredCounts();
        return true;
    }

    public async Task<bool> ApplyAsync()
    {
        ValidationError = null;
        await settingsAccessor.ReplaceAsync(settingsAccessor.Current with
        {
            StrictSearch = StrictSearch,
            BlockedPhrases = BlockedPhrases.Distinct(StringComparer.Ordinal).ToArray()
        });
        return true;
    }

    private void RefreshIgnoredCounts()
    {
        IgnoredTrackCount = settingsAccessor.Current.IgnoredTrackFingerprints.Count;
        IgnoredAlbumCount = settingsAccessor.Current.IgnoredAlbumFingerprints.Count;
    }
}
