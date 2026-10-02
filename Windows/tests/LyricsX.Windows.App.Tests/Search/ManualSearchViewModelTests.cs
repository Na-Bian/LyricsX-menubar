using System.Runtime.CompilerServices;
using System.Threading.Channels;
using LyricsX.Core;
using LyricsX.Core.Search;
using LyricsX.Core.Storage;
using LyricsX.Windows.App.Search;
using LyricsX.Windows.App.Services;

namespace LyricsX.Windows.App.Tests.Search;

[TestClass]
public sealed class ManualSearchViewModelTests
{
    [TestMethod]
    public async Task SearchAsync_PrefillsAndStreamsIncrementalResults()
    {
        var searchCoordinator = new FakeSearch();
        var viewModel = new ManualSearchViewModel(searchCoordinator,
            new ManualAssociationStore(NewTempRoot()), new LyricCache(NewTempRoot()));
        viewModel.Title = "晴天";
        viewModel.Artist = "周杰伦";

        var task = viewModel.SearchAsync(CancellationToken.None);
        searchCoordinator.Emit(Candidate("netease", "晴天", "周杰伦", 60));
        searchCoordinator.Emit(Candidate("qq", "晴天 Live", "周杰伦", 40));
        searchCoordinator.Complete();
        await task;

        Assert.AreEqual(2, viewModel.Results.Count);
        Assert.AreEqual("netease", viewModel.Results[0].ProviderId);
        StringAssert.Contains(viewModel.Results[0].MatchSummary, "70");
    }

    [TestMethod]
    public async Task NewSearch_CancelsPriorQuery()
    {
        var searchCoordinator = new FakeSearch();
        var viewModel = new ManualSearchViewModel(searchCoordinator,
            new ManualAssociationStore(NewTempRoot()), new LyricCache(NewTempRoot()));

        var first = viewModel.SearchAsync(CancellationToken.None);
        var second = viewModel.SearchAsync(CancellationToken.None);
        searchCoordinator.Complete();
        await Task.WhenAll(first, second);

        Assert.AreEqual(2, searchCoordinator.StartCount);
    }

    [TestMethod]
    public async Task UseOnce_UpdatesCoordinatorWithoutAssociation()
    {
        var root = NewTempRoot();
        var store = new ManualAssociationStore(root);
        var cache = new LyricCache(root);
        var coordinator = new FakeAppCoordinator();
        var viewModel = new ManualSearchViewModel(new FakeSearch(), store, cache, coordinator);

        var candidate = Candidate("netease", "Song", "Artist", 80);
        await viewModel.UseOnceAsync(candidate);

        Assert.AreSame(candidate, coordinator.LastUsed);
        Assert.IsNull(await store.TryResolveAsync("song|artist|200", CancellationToken.None));
    }

    [TestMethod]
    public async Task AlwaysUse_SavesCacheAndAssociation()
    {
        var root = NewTempRoot();
        var store = new ManualAssociationStore(root);
        var cache = new LyricCache(root);
        var viewModel = new ManualSearchViewModel(new FakeSearch(), store, cache);

        var candidate = Candidate("qq", "Song", "Artist", 80);
        await viewModel.AlwaysUseAsync(candidate);

        var fingerprint = TrackFingerprintOf(candidate);
        Assert.IsNotNull(await store.TryResolveAsync(fingerprint, CancellationToken.None));
        Assert.IsNotNull(await cache.TryGetAsync(fingerprint, CancellationToken.None));
    }

    [TestMethod]
    public async Task RemoveAssociation_RemovesOnlyMapping()
    {
        var root = NewTempRoot();
        var store = new ManualAssociationStore(root);
        var cache = new LyricCache(root);
        var viewModel = new ManualSearchViewModel(new FakeSearch(), store, cache);

        var candidate = Candidate("qq", "Song", "Artist", 80);
        await viewModel.AlwaysUseAsync(candidate);
        await viewModel.RemoveAssociationAsync(candidate);

        var fingerprint = TrackFingerprintOf(candidate);
        Assert.IsNull(await store.TryResolveAsync(fingerprint, CancellationToken.None));
        Assert.IsNotNull(await cache.TryGetAsync(fingerprint, CancellationToken.None));
    }

    private static string TrackFingerprintOf(LyricCandidate candidate) =>
        $"{candidate.Title.ToLowerInvariant()}|{candidate.Artist.ToLowerInvariant()}|200";

    private static string NewTempRoot() =>
        Path.Combine(Path.GetTempPath(), "search-" + Guid.NewGuid().ToString("N"));

    private static LyricCandidate Candidate(
        string provider, string title, string artist, double score) =>
        new(provider,
            new LyricDocument(
                [new LyricLine(TimeSpan.FromSeconds(1), null, "line one")],
                new LyricMetadata(title, artist, null, null, TimeSpan.FromSeconds(200),
                    new Dictionary<string, string>()),
                TimeSpan.Zero),
            title, artist, null, TimeSpan.FromSeconds(200), 0);

    private sealed class FakeAppCoordinator : ILyricsApplicationCoordinator
    {
        public LyricCandidate? LastUsed { get; private set; }

#pragma warning disable CS0067
        public event EventHandler<LyricsPresentationState>? StateChanged;
#pragma warning restore CS0067

        public LyricsPresentationState Current { get; } = LyricsPresentationState.Empty;

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SearchManuallyAsync(string title, string artist, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task UseCandidateAsync(LyricCandidate candidate, bool remember, CancellationToken cancellationToken)
        {
            LastUsed = candidate;
            return Task.CompletedTask;
        }

        public Task AdjustOffsetAsync(TimeSpan delta, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public bool LyricsVisible { get; set; } = true;

        public Task ToggleLyricsVisibilityAsync()
        {
            LyricsVisible = !LyricsVisible;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeSearch : ILyricsSearchCoordinator
    {
        private Channel<LyricCandidate>? channel;

        public int StartCount { get; private set; }

        public void Emit(LyricCandidate candidate) => channel?.Writer.TryWrite(candidate);

        public void Complete() => channel?.Writer.TryComplete();

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

                yield return new LyricSearchState(candidate, false, score);
            }
        }
    }
}
