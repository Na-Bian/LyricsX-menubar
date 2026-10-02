using System.Diagnostics;
using System.Net.Http;
using System.Runtime.CompilerServices;
using LyricsX.Core.Search;

namespace LyricsX.Core.Tests.Search;

[TestClass]
public sealed class LyricsSearchCoordinatorTests
{
    [TestMethod]
    public async Task SearchAsync_YieldsFirstCandidateThenBetterCandidateAsFinal()
    {
        var track = new MediaTrack(
            "Browser", "id", "Exact Song", "Exact Artist", "Exact Album",
            TimeSpan.FromSeconds(200));
        var weak = Candidate("weak", "Exact Song", "Other", 0);
        var exact = Candidate("exact", "Exact Song", "Exact Artist", 1);
        var coordinator = new LyricsSearchCoordinator(
            [new FakeProvider("p", weak, exact)],
            TimeSpan.FromMilliseconds(50));

        var results = new List<LyricSearchState>();
        await foreach (var state in coordinator.SearchAsync(
            new LyricSearchRequest(track), CancellationToken.None))
        {
            results.Add(state);
        }

        Assert.AreEqual(2, results.Count);
        Assert.AreSame(weak, results[0].Candidate);
        Assert.IsFalse(results[0].IsFinal);
        Assert.AreSame(exact, results[1].Candidate);
        Assert.IsTrue(results[1].IsFinal);
    }

    [TestMethod]
    public async Task SearchAsync_MergesConcurrentProviders()
    {
        var track = Track("Exact Song", "Exact Artist");
        var weak = Candidate("weak", "Exact Song", "Other", 0);
        var exact = Candidate("exact", "Exact Song", "Exact Artist", 1);
        var delayed = new FakeProvider("p1", TimeSpan.FromMilliseconds(80), exact);
        var immediate = new FakeProvider("p2", weak);
        var coordinator = new LyricsSearchCoordinator(
            [delayed, immediate], TimeSpan.FromMilliseconds(150));

        var results = await CollectAsync(coordinator, track);

        Assert.AreEqual(2, results.Count);
        Assert.AreSame(weak, results[0].Candidate);
        Assert.AreSame(exact, results[^1].Candidate);
        Assert.IsTrue(results[^1].IsFinal);
    }

    [TestMethod]
    public async Task SearchAsync_RetriesTransientProviderFailures()
    {
        var track = Track("Exact Song", "Exact Artist");
        var candidate = Candidate("c", "Exact Song", "Exact Artist", 0);
        var provider = new FlakyProvider(2, candidate);
        var coordinator = new LyricsSearchCoordinator([provider],
            TimeSpan.FromMilliseconds(30), retryCount: 2, retryDelay: TimeSpan.Zero);

        var results = await CollectAsync(coordinator, track);

        Assert.AreEqual(3, provider.Calls);
        Assert.AreEqual(1, results.Count(state => !state.IsFinal));
        Assert.IsTrue(results[^1].IsFinal);
    }

    [TestMethod]
    public async Task SearchAsync_CompletesWithoutResultWhenNoCandidateMatchesTitle()
    {
        var track = Track("Exact Song", "Exact Artist");
        var mismatched = Candidate("m", "Totally Different", "Whoever", 0);
        var coordinator = new LyricsSearchCoordinator(
            [new FakeProvider("p", mismatched)], TimeSpan.FromMilliseconds(30));

        var results = await CollectAsync(coordinator, track);

        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public async Task SearchAsync_CompletesWhenProvidersAreEmpty()
    {
        var track = Track("Exact Song", "Exact Artist");
        var coordinator = new LyricsSearchCoordinator(
            [new FakeProvider("empty")], TimeSpan.FromMilliseconds(30));

        var results = await CollectAsync(coordinator, track);

        Assert.AreEqual(0, results.Count);
    }

    [TestMethod]
    public async Task SearchAsync_StopsPromptlyWhenCallerCancels()
    {
        var track = Track("Exact Song", "Exact Artist");
        var candidate = Candidate("c", "Exact Song", "Exact Artist", 0);
        var coordinator = new LyricsSearchCoordinator(
            [new FakeProvider("slow", TimeSpan.FromSeconds(5), candidate)],
            TimeSpan.FromSeconds(5));

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));
        var stopwatch = Stopwatch.StartNew();
        var terminated = false;
        try
        {
            await foreach (var _ in coordinator.SearchAsync(
                new LyricSearchRequest(track), cts.Token))
            {
            }

            terminated = true;
        }
        catch (OperationCanceledException)
        {
            terminated = true;
        }

        Assert.IsTrue(terminated);
        Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(2),
            $"enumeration took {stopwatch.Elapsed}");
    }

    [TestMethod]
    public void Score_RewardsExactMetadataAndDurationProximity()
    {
        var track = Track("Exact Song", "Exact Artist", album: "Exact Album");
        Assert.AreEqual(100, LyricCandidateScorer.Score(track,
            Candidate("c", "Exact Song", "Exact Artist", 0, album: "Exact Album")));

        Assert.AreEqual(93, LyricCandidateScorer.Score(track,
            Candidate("c", "Exact Song", "Exact Artist", 0,
                album: "Exact Album", duration: 204)));

        Assert.AreEqual(85, LyricCandidateScorer.Score(track,
            Candidate("c", "Exact Song", "Exact Artist", 0,
                album: "Exact Album", duration: 210)));
    }

    [TestMethod]
    public void Score_RejectsMismatchedTitles()
    {
        var track = Track("Exact Song", "Exact Artist");
        var other = Candidate("c", "Different Song", "Exact Artist", 0);

        Assert.AreEqual(0, LyricCandidateScorer.Score(track, other));
    }

    [TestMethod]
    public void Score_TitleContainmentScoresLowerThanExact()
    {
        var track = Track("Hello", "Artist");
        var contained = Candidate("c", "Hello World", "Artist", 0);

        Assert.AreEqual(65, LyricCandidateScorer.Score(track, contained));
    }

    [TestMethod]
    public void Score_PrimaryArtistMatchScoresLowerThanExactArtist()
    {
        // "feat." is stripped by the normalizer itself, so the 20-point
        // primary-artist branch is reachable through "&"-style separators.
        var track = new MediaTrack(
            "Browser", "id", "Exact Song", "Artist & Guest", null,
            TimeSpan.FromSeconds(200));

        Assert.AreEqual(75, LyricCandidateScorer.Score(track,
            Candidate("c", "Exact Song", "Artist", 0)));
    }

    private static MediaTrack Track(
        string title, string artist, string? album = null) =>
        new("Browser", "id", title, artist, album, TimeSpan.FromSeconds(200));

    private static async Task<List<LyricSearchState>> CollectAsync(
        LyricsSearchCoordinator coordinator, MediaTrack track)
    {
        var results = new List<LyricSearchState>();
        await foreach (var state in coordinator.SearchAsync(
            new LyricSearchRequest(track), CancellationToken.None))
        {
            results.Add(state);
        }

        return results;
    }

    private static LyricCandidate Candidate(
        string id, string title, string artist, int rank,
        string? album = null, int duration = 200) =>
        new(id,
            new LyricDocument(
                [new LyricLine(TimeSpan.Zero, null, "line")],
                new LyricMetadata(title, artist, null, null, null,
                    new Dictionary<string, string>()),
                TimeSpan.Zero),
            title, artist, album, TimeSpan.FromSeconds(duration), rank);

    private sealed class FakeProvider : ILyricsProvider
    {
        private readonly TimeSpan delay;
        private readonly LyricCandidate[] candidates;

        public FakeProvider(string id, params LyricCandidate[] candidates)
            : this(id, TimeSpan.Zero, candidates)
        {
        }

        public FakeProvider(string id, TimeSpan delay, params LyricCandidate[] candidates)
        {
            Id = id;
            this.delay = delay;
            this.candidates = candidates;
        }

        public string Id { get; }

        public async IAsyncEnumerable<LyricCandidate> SearchAsync(
            LyricSearchRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }

            foreach (var candidate in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return candidate;
                await Task.Yield();
            }
        }
    }

    private sealed class FlakyProvider(
        int failuresBeforeSuccess,
        params LyricCandidate[] candidates) : ILyricsProvider
    {
        public int Calls { get; private set; }

        public string Id => "flaky";

        public async IAsyncEnumerable<LyricCandidate> SearchAsync(
            LyricSearchRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Calls++;
            if (Calls <= failuresBeforeSuccess)
            {
                throw new HttpRequestException("transient failure");
            }

            foreach (var candidate in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return candidate;
                await Task.Yield();
            }
        }
    }
}
