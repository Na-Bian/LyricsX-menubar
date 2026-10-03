using LyricsX.Core;
using LyricsX.Core.Playback;
using LyricsX.Platform.Windows;

namespace LyricsX.Windows.App.Services;

public interface ILyricsApplicationCoordinator : IAsyncDisposable
{
    event EventHandler<LyricsPresentationState>? StateChanged;

    LyricsPresentationState Current { get; }

    Task StartAsync(CancellationToken cancellationToken);

    Task SearchManuallyAsync(string title, string artist, CancellationToken cancellationToken);

    Task UseCandidateAsync(LyricCandidate candidate, bool remember, CancellationToken cancellationToken);

    Task AdjustOffsetAsync(TimeSpan delta, CancellationToken cancellationToken);

    Task ResetOffsetAsync();

    Task ToggleLyricsVisibilityAsync();

    bool LyricsVisible { get; }

    Task IgnoreCurrentTrackAsync();

    string? CurrentFingerprint { get; }
}

public sealed record LyricsPresentationState(
    PlaybackSnapshot Playback,
    LyricDocument? Document,
    LyricFrame Frame,
    string StatusText,
    bool IsSearching,
    string? ProviderId)
{
    public static LyricsPresentationState Empty { get; } = new(
        new PlaybackSnapshot(null, PlaybackStatus.Closed, TimeSpan.Zero, 1.0, DateTimeOffset.UtcNow),
        null,
        new LyricFrame(-1, null, null, 0, -1, 0),
        "无正在播放的媒体",
        false,
        null);
}
