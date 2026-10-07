using LyricsX.Core.Transforms;

namespace LyricsX.Core.Storage;

public sealed record ProviderSetting(string Id, bool Enabled, int Priority);

public sealed record AppSettings(
    int SchemaVersion,
    bool LaunchAtLogin,
    bool ShowWhenPaused,
    int WidthDip,
    int UserOffsetMilliseconds,
    ChineseConversion ChineseConversion,
    IReadOnlyList<ProviderSetting> Providers,
    IReadOnlyList<string> IgnoredTrackFingerprints,
    IReadOnlyList<string> IgnoredAlbumFingerprints,
    string MusixmatchToken = "",
    string SpotifyClientId = "",
    bool AutoMatchLyrics = true,
    bool HideWhenNoMedia = true,
    bool ShowNextLine = true,
    bool AutoScrollLongLines = true,
    bool FollowSystemAnimation = true,
    string FontName = "Segoe UI Variable Text",
    double FontSize = 13,
    string FontWeight = "SemiBold",
    bool StrictSearch = false,
    IReadOnlyList<string> BlockedPhrases = null!,
    string PreferredPlayerSourceId = "",
    IReadOnlyDictionary<string, string> Extra = null!)
{

    // IReadOnlyList fields get reference equality from the generated record
    // members; JSON round-trips produce different list instances, so compare
    // the sequences structurally instead.
    public bool Equals(AppSettings? other) =>
        other is not null &&
        SchemaVersion == other.SchemaVersion &&
        LaunchAtLogin == other.LaunchAtLogin &&
        ShowWhenPaused == other.ShowWhenPaused &&
        WidthDip == other.WidthDip &&
        UserOffsetMilliseconds == other.UserOffsetMilliseconds &&
        ChineseConversion == other.ChineseConversion &&
        Providers.SequenceEqual(other.Providers) &&
        IgnoredTrackFingerprints.SequenceEqual(other.IgnoredTrackFingerprints) &&
        IgnoredAlbumFingerprints.SequenceEqual(other.IgnoredAlbumFingerprints) &&
        MusixmatchToken == other.MusixmatchToken &&
        SpotifyClientId == other.SpotifyClientId &&
        AutoMatchLyrics == other.AutoMatchLyrics &&
        HideWhenNoMedia == other.HideWhenNoMedia &&
        ShowNextLine == other.ShowNextLine &&
        AutoScrollLongLines == other.AutoScrollLongLines &&
        FollowSystemAnimation == other.FollowSystemAnimation &&
        FontName == other.FontName &&
        FontSize == other.FontSize &&
        FontWeight == other.FontWeight &&
        StrictSearch == other.StrictSearch &&
        PreferredPlayerSourceId == other.PreferredPlayerSourceId &&
        (BlockedPhrases ?? []).SequenceEqual(other.BlockedPhrases ?? []) &&
        ((Extra == null || Extra.Count == 0) ==
            (other.Extra == null || other.Extra.Count == 0));

    public override int GetHashCode() => HashCode.Combine(
        SchemaVersion, LaunchAtLogin, ShowWhenPaused, WidthDip,
        UserOffsetMilliseconds, ChineseConversion, MusixmatchToken, SpotifyClientId);

    public static AppSettings Default { get; } = new(
        SchemaVersion: 1,
        LaunchAtLogin: false,
        ShowWhenPaused: true,
        WidthDip: 260,
        UserOffsetMilliseconds: 0,
        ChineseConversion: ChineseConversion.None,
        Providers:
        [
            new ProviderSetting("qq", true, 0),
            new ProviderSetting("netease", true, 1),
            new ProviderSetting("kugou", true, 2),
            new ProviderSetting("musixmatch", true, 3),
            new ProviderSetting("lrclib", true, 4),
            new ProviderSetting("spotify", false, 5)
        ],
        IgnoredTrackFingerprints: [],
        IgnoredAlbumFingerprints: []);
}

public interface ISettingsStore
{
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken);
}

public sealed class SettingsStore(string rootDirectory) : ISettingsStore
{
    private const string FileName = "settings.json";
    private readonly JsonFileStore<AppSettings> store = new(rootDirectory, () => AppSettings.Default);

    public Task<AppSettings> LoadAsync(CancellationToken cancellationToken) =>
        store.LoadAsync(FileName, cancellationToken);

    public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken) =>
        store.SaveAsync(FileName, settings, cancellationToken);
}
