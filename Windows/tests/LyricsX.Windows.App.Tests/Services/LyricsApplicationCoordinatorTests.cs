using System.Runtime.CompilerServices;
using System.Threading.Channels;
using LyricsX.Core;
using LyricsX.Core.Matching;
using LyricsX.Core.Playback;
using LyricsX.Core.Storage;
using LyricsX.Core.Transforms;
using LyricsX.Windows.App.Composition;
using LyricsX.Platform.Windows;
using LyricsX.Core.Search;
using LyricsX.Windows.App.Services;

namespace LyricsX.Windows.App.Tests.Services;

[TestClass]
public sealed class LyricsApplicationCoordinatorTests
{
    [TestMethod]
    public async Task StartAsync_NoMedia_Searching_FirstLine_BetterLine()
    {
        var root = NewTempRoot();
        try
        {
            var harness = new Harness(root);

            await harness.Coordinator.StartAsync(CancellationToken.None);
            await harness.WaitForStatusAsync("无正在播放的媒体");

            harness.Session.Publish(Track("Test Song", "Artist"), PlaybackStatus.Playing, position: 1);
            await harness.WaitForStatusAsync("正在搜索歌词…");

            harness.SearchCoordinator.Emit(WeakCandidate("first line"));
            await harness.WaitForStatusAsync("first line");

            harness.SearchCoordinator.Emit(StrongCandidate("better line"));
            harness.SearchCoordinator.Complete();
            await harness.WaitForStatusAsync("better line");

            harness.Coordinator.DisposeAsync().AsTask().Wait();
        }
        finally
        {
            Cleanup(root);
        }
    }

    [TestMethod]
    public async Task StartAsync_IgnoredTrack_ReportsWithoutProviderRequests()
    {
        var root = NewTempRoot();
        try
        {
            var harness = new Harness(root);
            var track = Track("Ignored Song", "Artist");
            var fingerprint = TrackFingerprint.Create(track);
            await harness.Settings.ReplaceAsync(harness.Settings.Current with
            {
                IgnoredTrackFingerprints = [fingerprint]
            });

            await harness.Coordinator.StartAsync(CancellationToken.None);
            harness.Session.Publish(track, PlaybackStatus.Playing, position: 0);

            await harness.WaitForStatusAsync("已忽略此歌曲");
            Assert.AreEqual(0, harness.SearchCoordinator.StartCount);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [TestMethod]
    public async Task StartAsync_CacheHit_BypassesNetwork()
    {
        var root = NewTempRoot();
        try
        {
            var harness = new Harness(root);
            var track = Track("Cached Song", "Artist");
            var document = SingleLineDocument("cached line");
            await harness.Cache.StoreAsync(
                TrackFingerprint.Create(track),
                Candidate("netease", document, "Cached Song", "Artist", rank: 1),
                "query", 90, CancellationToken.None);

            await harness.Coordinator.StartAsync(CancellationToken.None);
            harness.Session.Publish(track, PlaybackStatus.Playing, position: 1);

            await harness.WaitForStatusAsync("cached line");
            Assert.AreEqual(0, harness.SearchCoordinator.StartCount);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [TestMethod]
    public async Task AdjustOffsetAsync_ClampsAndRecalculates()
    {
        var root = NewTempRoot();
        try
        {
            var harness = new Harness(root);
            await harness.Coordinator.StartAsync(CancellationToken.None);

            await harness.Coordinator.AdjustOffsetAsync(TimeSpan.FromSeconds(100), CancellationToken.None);
            Assert.AreEqual(30_000, harness.Settings.Current.UserOffsetMilliseconds);

            await harness.Coordinator.AdjustOffsetAsync(TimeSpan.FromSeconds(-100), CancellationToken.None);
            Assert.AreEqual(-30_000, harness.Settings.Current.UserOffsetMilliseconds);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [TestMethod]
    public async Task UseCandidateAsync_RememberTrue_SavesAssociationAndCache()
    {
        var root = NewTempRoot();
        try
        {
            var harness = new Harness(root);
            var track = Track("Pick Song", "Artist");
            await harness.Coordinator.StartAsync(CancellationToken.None);
            harness.Session.Publish(track, PlaybackStatus.Playing, position: 0);
            await harness.WaitForConditionAsync(() =>
                harness.Coordinator.Current.Playback.Track is not null);

            var candidate = StrongCandidate("chosen line");
            await harness.Coordinator.UseCandidateAsync(candidate, remember: true, CancellationToken.None);

            // association maps the track fingerprint to the candidate's own
            // cache entry, which the coordinator resolves on the next play
            var trackFingerprint = TrackFingerprint.Create(track);
            var candidateFingerprint = TrackFingerprint.OfCandidate(candidate);
            var resolved = await harness.Store.TryResolveAsync(
                trackFingerprint, CancellationToken.None);
            Assert.AreEqual(candidateFingerprint, resolved);
            Assert.IsNotNull(await harness.Cache.TryGetAsync(
                candidateFingerprint, CancellationToken.None));
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static MediaTrack Track(string title, string artist) =>
        new("TestPlayer", "id-1", title, artist, "Album", TimeSpan.FromSeconds(200));

    private static LyricDocument SingleLineDocument(string content) =>
        new([new LyricLine(TimeSpan.FromSeconds(1), null, content)],
            new LyricMetadata(null, null, null, null, null, new Dictionary<string, string>()),
            TimeSpan.Zero);

    private static LyricCandidate WeakCandidate(string line) =>
        Candidate("weak", SingleLineDocument(line), "Test Song", "Other", rank: 9);

    private static LyricCandidate StrongCandidate(string line) =>
        Candidate("strong", SingleLineDocument(line), "Test Song", "Artist", rank: 0);

    private static LyricCandidate Candidate(
        string id, LyricDocument document, string title, string artist, int rank) =>
        new(id, document, title, artist, "Album", TimeSpan.FromSeconds(200), rank);

    private static string NewTempRoot() =>
        Path.Combine(Path.GetTempPath(), "lyricsx-app-tests-" + Guid.NewGuid().ToString("N"));

    private static void Cleanup(string root)
    {
        // background pipeline writes can hold a handle briefly after the
        // test body returns; retry instead of failing the cleanup race
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(200);
            }
        }
    }

    private sealed class Harness
    {
        public FakeSessionSource Session { get; } = new();

        public FakeSearchCoordinator SearchCoordinator { get; } = new();

        public LyricCache Cache { get; }

        public AppSettingsAccessor Settings { get; }

        public LyricsApplicationCoordinator Coordinator { get; }

        public ManualAssociationStore Store { get; }

        public Harness(string root)
        {
            Cache = new LyricCache(root);
            Store = new ManualAssociationStore(root);
            var store = new SettingsStore(root);
            Settings = new AppSettingsAccessor(
                store, store.LoadAsync(CancellationToken.None).GetAwaiter().GetResult());
            Coordinator = new LyricsApplicationCoordinator(
                Session,
                SearchCoordinator,
                Cache,
                Store,
                Settings,
                new LyricSynchronizer(),
                new LyricTransformer(),
                timerFactory: interval => new FakeTimer(interval));
        }

        public async Task WaitForStatusAsync(string status)
        {
            await WaitForConditionAsync(() =>
                Coordinator.Current.StatusText == status);
        }

        public async Task WaitForConditionAsync(Func<bool> condition)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                if (condition())
                {
                    return;
                }

                await Task.Delay(20);
            }

            Assert.Fail(
                $"condition not met; last status: '{Coordinator.Current.StatusText}' " +
                $"provider={Coordinator.Current.ProviderId} " +
                $"searchStarts={SearchCoordinator.StartCount} " +
                $"ignored={Settings.Current.IgnoredTrackFingerprints.Count} " +
                $"doc={(Coordinator.Current.Document is null ? "null" : Coordinator.Current.Document.Lines.Count.ToString())}");
        }
    }

    private sealed class FakeTimer(TimeSpan interval) : IDispatcherTimer
    {
        public event EventHandler? Tick;

        public TimeSpan Interval { get; set; } = interval;

        public bool IsRunning { get; private set; }

        public void Start() => IsRunning = true;

        public void Stop() => IsRunning = false;

        public void Fire() => Tick?.Invoke(this, EventArgs.Empty);
    }

    private sealed class FakeSessionSource : ISelectedMediaSession
    {
        public event EventHandler<PlaybackSnapshot>? PlaybackChanged;

        public PlaybackSnapshot Current { get; private set; } =
            new(null, PlaybackStatus.Closed, TimeSpan.Zero, 1.0, DateTimeOffset.UtcNow);

        public void Publish(MediaTrack track, PlaybackStatus status, double position) =>
            Publish(new PlaybackSnapshot(
                track, status, TimeSpan.FromSeconds(position), 1.0, DateTimeOffset.UtcNow));

        public void Publish(PlaybackSnapshot snapshot)
        {
            Current = snapshot;
            PlaybackChanged?.Invoke(this, snapshot);
        }

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeSearchCoordinator : ILyricsSearchCoordinator
    {
        private Channel<LyricCandidate>? channel;
        private LyricCandidate? lastEmitted;

        public int StartCount { get; private set; }

        public void Emit(LyricCandidate candidate)
        {
            lastEmitted = candidate;
            channel?.Writer.TryWrite(candidate);
        }

        public void Complete()
        {
            if (lastEmitted is not null)
            {
                channel?.Writer.TryWrite(lastEmitted); // replayed as the final state
            }

            channel?.Writer.TryComplete();
        }

        public async IAsyncEnumerable<LyricSearchState> SearchAsync(
            LyricSearchRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            StartCount++;
            channel = Channel.CreateUnbounded<LyricCandidate>();
            await foreach (var candidate in channel.Reader.ReadAllAsync(cancellationToken))
            {
                var score = LyricCandidateScorer.Score(request.Track, candidate);
                if (score <= 0)
                {
                    continue;
                }

                yield return new LyricSearchState(candidate, channel.Reader.Completion.IsCompleted, score);
            }
        }
    }
}
