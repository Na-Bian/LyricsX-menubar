using LyricsX.Core;
using LyricsX.Platform.Windows;
using LyricsX.Platform.Windows.Media;

namespace LyricsX.Platform.Windows.Tests.Media;

[TestClass]
public sealed class SelectedMediaSessionTests
{
    [TestMethod]
    public async Task StartAsync_PublishesPlayingSnapshotFromSource()
    {
        var source = new FakeSessionSource();
        source.Sessions = [Playing("app1", position: 10)];
        await using var selected = new SelectedMediaSession(source, new MediaSessionRanker());

        var received = new TaskCompletionSource<PlaybackSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        selected.PlaybackChanged += (_, snapshot) => received.TrySetResult(snapshot);
        await selected.StartAsync(CancellationToken.None);
        source.RaiseSessionsChanged();

        var snapshot = await Await(received.Task);
        Assert.AreEqual(PlaybackStatus.Playing, snapshot.Status);
        Assert.AreEqual("Sunny Window", snapshot.Track?.Title);
        Assert.AreEqual(TimeSpan.FromSeconds(10), snapshot.Position);
    }

    [TestMethod]
    public async Task PlaybackChanged_SkipsSemanticallyUnchangedBursts()
    {
        var source = new FakeSessionSource();
        source.Sessions = [Playing("app1", position: 10)];
        await using var selected = new SelectedMediaSession(source, new MediaSessionRanker(),
            coalesceDelay: TimeSpan.FromMilliseconds(20));

        var received = new List<PlaybackSnapshot>();
        var receivedEvent = new AutoResetEvent(false);
        selected.PlaybackChanged += (_, snapshot) =>
        {
            received.Add(snapshot);
            receivedEvent.Set();
        };
        await selected.StartAsync(CancellationToken.None);
        source.RaiseSessionsChanged();
        Assert.IsTrue(receivedEvent.WaitOne(TimeSpan.FromSeconds(2)));

        // burst: three identical updates must coalesce into zero extra events
        source.Sessions = [Playing("app1", position: 10, updatedAt: Now(1))];
        source.RaiseSessionsChanged();
        source.Sessions = [Playing("app1", position: 10, updatedAt: Now(2))];
        source.RaiseSessionsChanged();
        source.Sessions = [Playing("app1", position: 10, updatedAt: Now(3))];
        source.RaiseSessionsChanged();
        source.Sessions = [Playing("app1", position: 20, updatedAt: Now(4))];
        source.RaiseSessionsChanged();

        Assert.IsTrue(receivedEvent.WaitOne(TimeSpan.FromSeconds(2)));
        await Task.Delay(100);
        Assert.AreEqual(2, received.Count);
    }

    [TestMethod]
    public async Task DisappearingSession_PublishesClosedSnapshot()
    {
        var source = new FakeSessionSource();
        source.Sessions = [Playing("app1", position: 10)];
        await using var selected = new SelectedMediaSession(source, new MediaSessionRanker());

        var received = new Queue<PlaybackSnapshot>();
        var receivedEvent = new AutoResetEvent(false);
        selected.PlaybackChanged += (_, snapshot) =>
        {
            received.Enqueue(snapshot);
            receivedEvent.Set();
        };
        await selected.StartAsync(CancellationToken.None);
        source.RaiseSessionsChanged();
        Assert.IsTrue(receivedEvent.WaitOne(TimeSpan.FromSeconds(2)));

        source.Sessions = [];
        source.RaiseSessionsChanged();
        Assert.IsTrue(receivedEvent.WaitOne(TimeSpan.FromSeconds(2)));

        Assert.AreEqual(PlaybackStatus.Closed, received.Last().Status);
    }

    [TestMethod]
    public async Task Dispose_UnsubscribesFromSource()
    {
        var source = new FakeSessionSource();
        source.Sessions = [Playing("app1")];
        var selected = new SelectedMediaSession(source, new MediaSessionRanker());
        await selected.StartAsync(CancellationToken.None);

        await selected.DisposeAsync();
        var count = 0;
        selected.PlaybackChanged += (_, _) => count++;
        source.RaiseSessionsChanged();
        await Task.Delay(50);

        Assert.IsFalse(source.HasSubscribers);
        Assert.AreEqual(0, count);
    }

    private static DateTimeOffset Now(int seconds) =>
        new(2026, 1, 1, 12, 0, seconds, TimeSpan.Zero);

    private static MediaSessionSnapshot Playing(
        string appId, double position = 0, DateTimeOffset? updatedAt = null) =>
        new(appId, appId, PlaybackStatus.Playing,
            new MediaTrack(appId, "tid", "Sunny Window", "Artist", null,
                TimeSpan.FromSeconds(200)),
            TimeSpan.FromSeconds(position), 1.0, updatedAt ?? Now(0), appId + "-native");

    private static async Task<T> Await<T>(Task<T> task)
    {
        var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.AreSame(task, completed, "timed out waiting for snapshot");
        return await task;
    }

    private sealed class FakeSessionSource : IMediaSessionSource
    {
        public IReadOnlyList<MediaSessionSnapshot> Sessions { get; set; } = [];

        public bool HasSubscribers => SessionsChanged is not null;

        public event EventHandler? SessionsChanged;

        public void RaiseSessionsChanged() => SessionsChanged?.Invoke(this, EventArgs.Empty);

        public Task<IReadOnlyList<MediaSessionSnapshot>> GetSessionsAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult(Sessions);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
