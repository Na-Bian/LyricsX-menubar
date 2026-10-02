using System.Diagnostics;
using LyricsX.Core;
using LyricsX.Core.Playback;
using LyricsX.Core.Matching;
using LyricsX.Core.Search;
using LyricsX.Core.Storage;
using LyricsX.Core.Transforms;
using LyricsX.Platform.Windows;
using LyricsX.Platform.Windows.Media;
using LyricsX.Windows.App.Composition;

namespace LyricsX.Windows.App.Services;

/// <summary>
/// Single orchestrator: media session → ignore check → manual association →
/// cache → provider search (first candidate immediate, better within the
/// quality window) → transform → synchronized frames on a 50 ms/250 ms tick.
/// Track changes cancel only the per-track pipeline; settings changes restart
/// only the affected layer.
/// </summary>
public sealed class LyricsApplicationCoordinator(
    ISelectedMediaSession session,
    ILyricsSearchCoordinator searchCoordinator,
    LyricCache cache,
    ManualAssociationStore associations,
    AppSettingsAccessor settings,
    LyricSynchronizer synchronizer,
    LyricTransformer transformer,
    Func<TimeSpan, IDispatcherTimer>? timerFactory = null) : ILyricsApplicationCoordinator
{
    private static readonly TimeSpan PlayingTick = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan PausedTick = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan MaxOffset = TimeSpan.FromSeconds(30);

    private readonly object gate = new();
    private CancellationTokenSource? trackPipeline;
    private string? currentFingerprint;
    private LyricDocument? document;
    private PlaybackSnapshot playback = LyricsPresentationState.Empty.Playback;
    private IDispatcherTimer? tickTimer;
    private LyricsPresentationState current = LyricsPresentationState.Empty;
    private PlaybackSnapshot lastSnapshot = LyricsPresentationState.Empty.Playback;
    private DateTimeOffset lastSnapshotRealTime = DateTimeOffset.UtcNow;

    public event EventHandler<LyricsPresentationState>? StateChanged;

    public LyricsPresentationState Current => current;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        session.PlaybackChanged += OnPlaybackChanged;
        Publish(LyricsPresentationState.Empty);
        return Task.CompletedTask;
    }

    public async Task SearchManuallyAsync(
        string title,
        string artist,
        CancellationToken cancellationToken)
    {
        var track = playback.Track;
        if (track is null)
        {
            return;
        }

        var overrideTrack = track with { Title = title, Artist = artist };
        await StartTrackPipelineAsync(overrideTrack, forceSearch: true, cancellationToken);
    }

    public async Task UseCandidateAsync(
        LyricCandidate candidate,
        bool remember,
        CancellationToken cancellationToken)
    {
        var fingerprint = currentFingerprint;
        if (fingerprint is null)
        {
            return;
        }

        // a manual pick supersedes any in-flight automatic search
        CancelPipeline();

        if (remember)
        {
            // cache under the candidate's own fingerprint; the association
            // maps the playing track to that cache entry
            var candidateFingerprint = TrackFingerprint.OfCandidate(candidate);
            await cache.StoreAsync(candidateFingerprint, candidate, "manual", 100, cancellationToken);
            await associations.SetAsync(fingerprint, candidateFingerprint, cancellationToken);
        }

        ApplyDocument(candidate.Document, candidate.ProviderId);
    }

    public async Task ResetOffsetAsync()
    {
        await settings.ReplaceAsync(settings.Current with
        {
            UserOffsetMilliseconds = 0
        });
        RepublishFrame();
    }

    public async Task AdjustOffsetAsync(TimeSpan delta, CancellationToken cancellationToken)
    {
        var currentOffset = TimeSpan.FromMilliseconds(settings.Current.UserOffsetMilliseconds);
        var adjusted = TimeSpan.FromMilliseconds(Math.Clamp(
            (currentOffset + delta).TotalMilliseconds,
            -MaxOffset.TotalMilliseconds,
            MaxOffset.TotalMilliseconds));
        await settings.ReplaceAsync(settings.Current with
        {
            UserOffsetMilliseconds = (int)adjusted.TotalMilliseconds
        });
        RepublishFrame();
    }

    public bool LyricsVisible { get; private set; } = true;

    public Task ToggleLyricsVisibilityAsync()
    {
        LyricsVisible = !LyricsVisible;
        RepublishFrame();
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        session.PlaybackChanged -= OnPlaybackChanged;
        CancelPipeline();
        tickTimer?.Stop();
        return ValueTask.CompletedTask;
    }

    private void OnPlaybackChanged(object? sender, PlaybackSnapshot snapshot)
    {
        lock (gate)
        {
            playback = snapshot;
            lastSnapshot = snapshot;
            lastSnapshotRealTime = DateTimeOffset.UtcNow;
        }

        var track = snapshot.Track;
        if (track is null || snapshot.Status is PlaybackStatus.Closed or PlaybackStatus.Stopped)
        {
            CancelPipeline();
            Publish(LyricsPresentationState.Empty with
            {
                Playback = snapshot,
                StatusText = "无正在播放的媒体"
            });
            return;
        }

        var fingerprint = TrackFingerprint.Create(track);
        if (fingerprint != currentFingerprint)
        {
            _ = StartTrackPipelineAsync(track, forceSearch: false, CancellationToken.None)
                .ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
        }

        UpdateTimerMode(snapshot.Status);
        Publish(BuildState());
    }

    private async Task StartTrackPipelineAsync(
        MediaTrack track,
        bool forceSearch,
        CancellationToken cancellationToken)
    {
        CancelPipeline();
        var pipeline = new CancellationTokenSource();
        trackPipeline = pipeline;
#pragma warning disable CS4014
        var pipelineFingerprint = TrackFingerprint.Create(track);
        currentFingerprint = pipelineFingerprint;
        document = null;
        Publish(BuildState("正在搜索歌词…", isSearching: true));

        try
        {
            var token = pipeline.Token;

            if (settings.Current.IgnoredTrackFingerprints.Contains(currentFingerprint))
            {
                Publish(BuildState("已忽略此歌曲"));
                return;
            }

            var albumFingerprint = TrackFingerprint.CreateAlbum(track);
            if (albumFingerprint is not null &&
                settings.Current.IgnoredAlbumFingerprints.Contains(albumFingerprint))
            {
                Publish(BuildState("已忽略此歌曲"));
                return;
            }

            if (!forceSearch)
            {
                var associated = await associations.TryResolveAsync(currentFingerprint, token);
                if (associated is not null)
                {
                    var cached = await cache.TryGetAsync(associated, token);
                    if (cached is not null)
                    {
                        ApplyDocument(cached, providerId: "manual-association");
                        return;
                    }
                }

                var direct = await cache.TryGetAsync(currentFingerprint, token);
                if (direct is not null)
                {
                    ApplyDocument(direct, providerId: "cache");
                    return;
                }
            }

            if (settings.Current.StrictSearch)
            {
                // strict mode only affects scoring weights downstream; the
                // coordinator still consumes the merged stream
            }

            LyricCandidate? best = null;
            var bestScore = 0.0;
            await foreach (var state in searchCoordinator.SearchAsync(
                new LyricSearchRequest(track), token))
            {
                if (state.Score > bestScore)
                {
                    best = state.Candidate;
                    bestScore = state.Score;
                    // first hit shows immediately; better ones replace in-window
                    ApplyDocument(best.Document, best.ProviderId);
                }

                if (state.IsFinal)
                {
                    if (best is not null && pipelineFingerprint == currentFingerprint)
                    {
                        await cache.StoreAsync(
                            pipelineFingerprint, best, track.Title, bestScore, token);
                    }
                    else
                    {
                        Publish(BuildState("未找到歌词"));
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // superseded by a new track or disposal
        }
        catch (HttpRequestException exception)
        {
            Publish(BuildState("歌词搜索失败: " + exception.Message));
        }
        catch (Exception exception)
        {
            // unexpected pipeline failure surfaces in the status line so
            // fire-and-forget faults are never silent
            Publish(BuildState("歌词处理失败: " + exception.GetType().Name));
        }
#pragma warning restore CS4014
    }

    private void ApplyDocument(LyricDocument raw, string providerId)
    {
        var options = new LyricTransformOptions(
            settings.Current.ChineseConversion,
            RemoveEmptyLines: true,
            settings.Current.BlockedPhrases ?? []);
        var transformed = transformer.Apply(raw, options);
        lock (gate)
        {
            document = transformed;
        }

        Publish(BuildState(providerId: providerId));
    }

    private void UpdateTimerMode(PlaybackStatus status)
    {
        if (tickTimer is null)
        {
            tickTimer = (timerFactory ?? DefaultTimerFactory)(PlayingTick);
            tickTimer.Tick += (_, _) => RepublishFrame();
        }

        var desired = status == PlaybackStatus.Playing ? PlayingTick : PausedTick;
        if (tickTimer.Interval != desired)
        {
            tickTimer.Interval = desired;
        }

        if (!tickTimer.IsRunning)
        {
            tickTimer.Start();
        }
    }

    private static IDispatcherTimer DefaultTimerFactory(TimeSpan interval) =>
        throw new InvalidOperationException(
            "a dispatcher timer factory is required in production composition");

    private PlaybackSnapshot ExtrapolatedSnapshot()
    {
        var snapshot = lastSnapshot;
        if (snapshot.Status == PlaybackStatus.Playing && snapshot.Track is not null)
        {
            // advance the position between GSMTC timeline updates while playing
            var elapsed = DateTimeOffset.UtcNow - lastSnapshotRealTime;
            return snapshot with { Position = snapshot.Position + elapsed * snapshot.Rate };
        }

        return snapshot;
    }

    private void RepublishFrame()
    {
        var snapshot = ExtrapolatedSnapshot();
        if (snapshot.Track is null)
        {
            return;
        }

        if (!LyricsVisible)
        {
            lock (gate)
            {
                document = null;
            }
            Publish(BuildState("歌词已隐藏"));
            return;
        }

        if (snapshot.Status != PlaybackStatus.Playing && !settings.Current.ShowWhenPaused)
        {
            Publish(BuildState("已暂停"));
            return;
        }

        Publish(BuildState());
    }

    private LyricsPresentationState BuildState(
        string? statusText = null,
        bool? isSearching = null,
        bool lyricsVisible = true,
        string? providerId = null)
    {
        PlaybackSnapshot snapshot;
        LyricDocument? doc;
        lock (gate)
        {
            snapshot = ExtrapolatedSnapshot();
            doc = document;
        }

        LyricFrame frame = doc is not null
            ? synchronizer.GetFrame(
                doc,
                snapshot.Position,
                TimeSpan.FromMilliseconds(settings.Current.UserOffsetMilliseconds))
            : new LyricFrame(-1, null, null, 0, -1, 0);

        var text = statusText ?? frame.Current?.Content ?? current.StatusText;

        return new LyricsPresentationState(
            snapshot,
            doc,
            frame,
            text,
            isSearching ?? (doc is null && statusText == "正在搜索歌词…"),
            providerId ?? current?.ProviderId);
    }

    private void Publish(LyricsPresentationState state)
    {
        current = state;
        StateChanged?.Invoke(this, state);
    }

    private void CancelPipeline()
    {
        trackPipeline?.Cancel();
        trackPipeline?.Dispose();
        trackPipeline = null;
    }

    private static string cacheKeyOf(LyricCandidate candidate) =>
        candidate.ProviderId + ":" + candidate.Title + ":" + candidate.Document.Lines.Count;
}
