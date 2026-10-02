using LyricsX.Platform.Windows;
using LyricsX.Platform.Windows.Taskbar;

namespace LyricsX.Platform.Windows.Tests.Taskbar;

[TestClass]
public sealed class TaskbarPlacementServiceTests
{
    [TestMethod]
    public async Task GetPlacementAsync_ReturnsCalculatedPlacement()
    {
        var reader = new FakeReader(new TaskbarSnapshot(
            new PixelRect(0, 1040, 1920, 40),
            TaskbarEdge.Bottom,
            [new PixelRect(0, 1040, 48, 40), new PixelRect(760, 1040, 400, 40), new PixelRect(1650, 1040, 270, 40)],
            1.0, false, false));
        await using var service = new TaskbarPlacementService(
            reader, new TaskbarIntervalCalculator(), pollInterval: Timeout.InfiniteTimeSpan);

        var placement = await service.GetPlacementAsync(260, CancellationToken.None);

        Assert.IsTrue(placement.IsVisible);
        Assert.AreEqual(new PixelRect(52, 1040, 260, 40), placement.Bounds);
    }

    [TestMethod]
    public async Task Polling_PlacementChanged_FiresOncePerSemanticChange()
    {
        var snapshot = new TaskbarSnapshot(
            new PixelRect(0, 1040, 1920, 40),
            TaskbarEdge.Bottom,
            [new PixelRect(760, 1040, 400, 40), new PixelRect(1650, 1040, 270, 40)],
            1.0, false, false);
        var reader = new FakeReader(snapshot);
        var changed = new SemaphoreSlim(0, int.MaxValue);
        await using var service = new TaskbarPlacementService(
            reader, new TaskbarIntervalCalculator(), pollInterval: TimeSpan.FromMilliseconds(20));
        service.PlacementChanged += (_, _) => changed.Release();

        _ = service.RunAsync(CancellationToken.None);
        await Task.Delay(150);
        reader.Snapshot = snapshot with
        {
            OccupiedBounds = [new PixelRect(0, 1040, 48, 40), new PixelRect(760, 1040, 400, 40), new PixelRect(1650, 1040, 270, 40)]
        };
        reader.RaiseChanged();
        await Task.Delay(150);

        Assert.IsTrue(await changed.WaitAsync(TimeSpan.Zero));
        Assert.IsTrue(await changed.WaitAsync(TimeSpan.Zero));
        Assert.IsFalse(await changed.WaitAsync(TimeSpan.Zero),
            "placement changed fired more than twice for one semantic change");
    }

    [TestMethod]
    public async Task GetPlacementAsync_ReaderThrows_ReturnsHiddenPlacement()
    {
        var reader = new FakeReader(null) { ThrowOnRead = true };
        await using var service = new TaskbarPlacementService(
            reader, new TaskbarIntervalCalculator(), pollInterval: Timeout.InfiniteTimeSpan);

        var placement = await service.GetPlacementAsync(260, CancellationToken.None);

        Assert.IsFalse(placement.IsVisible);
    }

    private sealed class FakeReader(TaskbarSnapshot? snapshot) : TaskbarSnapshotReader(
        new TestNativeApi(), new TestAutomationApi(), new TestFullscreenDetector())
    {
        public TaskbarSnapshot? Snapshot { get; set; } = snapshot;

        public bool ThrowOnRead { get; set; }

        public event EventHandler? Changed;

        public void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

        public override Task<TaskbarSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken)
        {
            if (ThrowOnRead)
            {
                throw new InvalidOperationException("explorer gone");
            }

            return Task.FromResult(Snapshot ?? new TaskbarSnapshot(
                default, TaskbarEdge.Bottom, [], 1.0, false, false));
        }
    }

    private sealed class TestNativeApi : ITaskbarNativeApi
    {
        public nint FindPrimaryTaskbar() => 1;

        public PixelRect GetWindowRect(nint window) => default;

        public TaskbarEdge GetEdge(nint window) => TaskbarEdge.Bottom;

        public bool IsAutoHideEnabled() => false;

        public double GetRasterizationScale(nint window) => 1.0;

        public bool IsForegroundFullscreen(PixelRect monitorBounds) => false;
    }

    private sealed class TestAutomationApi : ITaskbarAutomationApi
    {
        public IReadOnlyList<PixelRect> GetVisibleInteractiveBounds(nint taskbarWindow) => [];
    }

    private sealed class TestFullscreenDetector : IFullscreenDetector
    {
        public bool IsForegroundFullscreen(PixelRect monitorBounds) => false;
    }
}
