using LyricsX.Core;

namespace LyricsX.Platform.Windows.Media;

/// <summary>
/// Tracks the active media session: coalesces SessionsChanged bursts through
/// a serialized refresh loop, publishes only semantically changed snapshots,
/// and synthesizes position between events from ObservedAt while playing.
/// </summary>
public sealed class SelectedMediaSession(
    IMediaSessionSource source,
    MediaSessionRanker ranker,
    TimeSpan? coalesceDelay = null) : ISelectedMediaSession
{
    private static readonly TimeSpan DefaultCoalesce = TimeSpan.FromMilliseconds(100);

    private readonly SemaphoreSlim refreshGate = new(1, 1);
    private int refreshRequested;
    private PlaybackSnapshot current = new(null, PlaybackStatus.Closed, TimeSpan.Zero, 1.0, DateTimeOffset.UtcNow);
    private string? previousSessionId;

    public event EventHandler<PlaybackSnapshot>? PlaybackChanged;

    public PlaybackSnapshot Current => current;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        source.SessionsChanged += OnSessionsChanged;
        await RefreshAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        source.SessionsChanged -= OnSessionsChanged;
        refreshGate.Dispose();
        await ValueTask.CompletedTask;
    }

    private void OnSessionsChanged(object? sender, EventArgs args)
    {
        _ = RefreshAsync(CancellationToken.None);
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        if (!await refreshGate.WaitAsync(0, cancellationToken))
        {
            Interlocked.Exchange(ref refreshRequested, 1);
            return;
        }

        try
        {
            do
            {
                Interlocked.Exchange(ref refreshRequested, 0);
                if (coalesceDelay is { } delay && delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellationToken);
                }

                await RefreshCoreAsync(cancellationToken);
            }
            while (Interlocked.Exchange(ref refreshRequested, 0) == 1);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            refreshGate.Release();
        }
    }

    private async Task RefreshCoreAsync(CancellationToken cancellationToken)
    {
        var sessions = await source.GetSessionsAsync(cancellationToken);
        var selected = ranker.Select(sessions, previousSessionId);
        previousSessionId = selected?.SessionId;

        var snapshot = selected is null
            ? new PlaybackSnapshot(null, PlaybackStatus.Closed, TimeSpan.Zero, 1.0, DateTimeOffset.UtcNow)
            : ToSnapshot(selected);
        if (SemanticEquals(current, snapshot))
        {
            return;
        }

        current = snapshot;
        PlaybackChanged?.Invoke(this, snapshot);
    }

    private static PlaybackSnapshot ToSnapshot(MediaSessionSnapshot session) =>
        new(session.Track, session.Status, session.Position, session.Rate, session.LastUpdatedAt);

    private static bool SemanticEquals(PlaybackSnapshot left, PlaybackSnapshot right)
    {
        if (left.Status != right.Status ||
            left.Position != right.Position ||
            left.Rate != right.Rate)
        {
            return false;
        }

        return (left.Track, right.Track) switch
        {
            (null, null) => true,
            (null, _) or (_, null) => false,
            _ => left.Track.TrackId == right.Track.TrackId
        };
    }
}
