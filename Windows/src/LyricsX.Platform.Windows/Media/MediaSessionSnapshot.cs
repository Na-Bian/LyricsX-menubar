using LyricsX.Core;

namespace LyricsX.Platform.Windows;

public sealed record MediaSessionSnapshot(
    string SessionId,
    string SourceAppId,
    PlaybackStatus Status,
    MediaTrack? Track,
    TimeSpan Position,
    double Rate,
    DateTimeOffset LastUpdatedAt,
    object NativeSession);

public interface IMediaSessionSource : IAsyncDisposable
{
    event EventHandler? SessionsChanged;

    Task<IReadOnlyList<MediaSessionSnapshot>> GetSessionsAsync(
        CancellationToken cancellationToken);
}

public interface ISelectedMediaSession : IAsyncDisposable
{
    event EventHandler<PlaybackSnapshot>? PlaybackChanged;

    PlaybackSnapshot Current { get; }

    Task StartAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Test seam for GSMTC playback states: the WinRT enum is only available on
/// Windows, so the mapper and its tests speak this neutral spelling.
/// </summary>
public enum GlobalPlaybackStatus
{
    Closed,
    Opened,
    Paused,
    Playing,
    Stopped
}
