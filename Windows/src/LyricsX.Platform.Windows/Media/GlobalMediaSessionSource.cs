using LyricsX.Core;
using Windows.Media.Control;

namespace LyricsX.Platform.Windows.Media;

/// <summary>
/// Reads GSMTC sessions in an unpackaged full-trust process. Native session
/// objects are retained only inside the snapshots and never persisted.
/// </summary>
public sealed class GlobalMediaSessionSource : IMediaSessionSource
{
    private GlobalSystemMediaTransportControlsSessionManager? manager;

    public event EventHandler? SessionsChanged;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        manager = await GlobalSystemMediaTransportControlsSessionManager
            .RequestAsync()
            .AsTask(cancellationToken);
        manager.SessionsChanged += OnSessionsChanged;
        manager.CurrentSessionChanged += OnCurrentSessionChanged;
    }

    public async Task<IReadOnlyList<MediaSessionSnapshot>> GetSessionsAsync(
        CancellationToken cancellationToken)
    {
        if (manager is null)
        {
            return [];
        }

        var snapshots = new List<MediaSessionSnapshot>();
        foreach (var session in manager.GetSessions().ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var info = session.GetPlaybackInfo();
                var timeline = session.GetTimelineProperties();
                var media = await session.TryGetMediaPropertiesAsync().AsTask(cancellationToken);
                var sessionId = session.SourceAppUserModelId ?? string.Empty;

                snapshots.Add(GsmtcMapper.Map(
                    sessionId: sessionId,
                    sourceAppUserModelId: session.SourceAppUserModelId,
                    playbackStatus: ToNeutral(info.PlaybackStatus),
                    title: media.Title,
                    artist: media.Artist,
                    album: media.AlbumTitle,
                    positionSeconds: timeline.Position.TotalSeconds,
                    timelineEndSeconds: timeline.EndTime.TotalSeconds,
                    lastUpdatedAt: DateTimeOffset.UtcNow,
                    nativeSession: session));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (
                exception is UnauthorizedAccessException ||
                exception is InvalidOperationException ||
                exception is ArgumentException ||
                exception.GetType().FullName == "System.Runtime.InteropServices.COMException")
            {
                // session disappeared mid-enumeration or denied access: skip it
            }
        }

        return snapshots;
    }

    public async ValueTask DisposeAsync()
    {
        if (manager is not null)
        {
            manager.SessionsChanged -= OnSessionsChanged;
            manager.CurrentSessionChanged -= OnCurrentSessionChanged;
        }

        manager = null;
        await ValueTask.CompletedTask;
    }

    private void OnSessionsChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        SessionsChangedEventArgs args) =>
        SessionsChanged?.Invoke(this, EventArgs.Empty);

    private void OnCurrentSessionChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        CurrentSessionChangedEventArgs args) =>
        SessionsChanged?.Invoke(this, EventArgs.Empty);

    private static GlobalPlaybackStatus ToNeutral(
        GlobalSystemMediaTransportControlsSessionPlaybackStatus status) => status switch
    {
        GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => GlobalPlaybackStatus.Playing,
        GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => GlobalPlaybackStatus.Paused,
        GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped => GlobalPlaybackStatus.Stopped,
        GlobalSystemMediaTransportControlsSessionPlaybackStatus.Opened => GlobalPlaybackStatus.Opened,
        _ => GlobalPlaybackStatus.Closed
    };
}
