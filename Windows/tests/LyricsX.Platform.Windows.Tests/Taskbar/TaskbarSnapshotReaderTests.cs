using LyricsX.Platform.Windows;
using LyricsX.Platform.Windows.Taskbar;

namespace LyricsX.Platform.Windows.Tests.Taskbar;

[TestClass]
public sealed class TaskbarSnapshotReaderTests
{
    private const nint TaskbarHwnd = 0x1234;

    [TestMethod]
    public async Task ReadSnapshot_BuildsSnapshotFromNativeApi()
    {
        var native = new FakeNativeApi
        {
            TaskbarRect = new PixelRect(0, 1738, 2940, 108),
            Scale = 2.25
        };
        var automation = new FakeAutomationApi
        {
            Bounds = [new PixelRect(1200, 1738, 700, 108), new PixelRect(2800, 1738, 140, 108)]
        };
        var reader = CreateReader(native, automation);

        var snapshot = await reader.ReadSnapshotAsync(CancellationToken.None);

        Assert.AreEqual(TaskbarHwnd, reader.CurrentTaskbarHwnd);
        Assert.AreEqual(new PixelRect(0, 1738, 2940, 108), snapshot.TaskbarBounds);
        Assert.AreEqual(TaskbarEdge.Bottom, snapshot.Edge);
        Assert.AreEqual(2.25, snapshot.RasterizationScale);
        Assert.IsFalse(snapshot.AutoHide);
        Assert.AreEqual(2, snapshot.OccupiedBounds.Count);
    }

    [TestMethod]
    public async Task ReadSnapshot_AbsentExplorer_ReturnsHiddenPlacement()
    {
        var native = new FakeNativeApi { TaskbarFound = false };
        var reader = CreateReader(native, new FakeAutomationApi());

        var snapshot = await reader.ReadSnapshotAsync(CancellationToken.None);

        Assert.AreEqual(0, snapshot.TaskbarBounds.Width);
        Assert.AreEqual(TaskbarEdge.Bottom, snapshot.Edge);
    }

    [TestMethod]
    public async Task ReadSnapshot_ExplorerRestarted_PicksUpNewHwnd()
    {
        var native = new FakeNativeApi { TaskbarRect = new PixelRect(0, 1738, 2940, 108) };
        var automation = new FakeAutomationApi();
        var reader = CreateReader(native, automation);

        await reader.ReadSnapshotAsync(CancellationToken.None);
        native.HwndOverride = 0x5555;
        await reader.ReadSnapshotAsync(CancellationToken.None);

        Assert.AreEqual(0x5555, reader.CurrentTaskbarHwnd);
    }

    [TestMethod]
    public async Task ReadSnapshot_AutomationDenied_UsesFallbackReservations()
    {
        var native = new FakeNativeApi
        {
            TaskbarRect = new PixelRect(0, 1738, 2940, 108),
            Scale = 1.0
        };
        var automation = new FakeAutomationApi { DenyAccess = true };
        var reader = CreateReader(native, automation);

        var snapshot = await reader.ReadSnapshotAsync(CancellationToken.None);

        // fallback: leftmost 64 DIPs + centered 520 DIPs + right 140px at scale 1.0
        Assert.AreEqual(3, snapshot.OccupiedBounds.Count);
        Assert.AreEqual(64, snapshot.OccupiedBounds[0].Width);
        Assert.AreEqual(520, snapshot.OccupiedBounds[1].Width);
        Assert.AreEqual(2940 - 140, snapshot.OccupiedBounds[2].X);
        Assert.AreEqual(140, snapshot.OccupiedBounds[2].Width);
    }

    [TestMethod]
    public async Task ReadSnapshot_OffScreenControls_Discarded()
    {
        var native = new FakeNativeApi
        {
            TaskbarRect = new PixelRect(0, 1738, 2940, 108),
            Scale = 1.0
        };
        var automation = new FakeAutomationApi
        {
            Bounds = [new PixelRect(-50, 0, 200, 100), new PixelRect(500, 1738, 300, 108)]
        };
        var reader = CreateReader(native, automation);

        var snapshot = await reader.ReadSnapshotAsync(CancellationToken.None);

        Assert.AreEqual(1, snapshot.OccupiedBounds.Count);
        Assert.AreEqual(500, snapshot.OccupiedBounds[0].X);
    }

    [TestMethod]
    public async Task ReadSnapshot_StaleElements_AutomationThrows_RetriesWithFreshRoot()
    {
        var native = new FakeNativeApi
        {
            TaskbarRect = new PixelRect(0, 1738, 2940, 108),
            Scale = 1.0
        };
        var automation = new FakeAutomationApi
        {
            ThrowStaleOnce = true,
            Bounds = [new PixelRect(500, 1738, 300, 108)]
        };
        var reader = CreateReader(native, automation);

        var snapshot = await reader.ReadSnapshotAsync(CancellationToken.None);

        Assert.AreEqual(2, automation.AccessCount);
        Assert.AreEqual(1, snapshot.OccupiedBounds.Count);
    }

    [TestMethod]
    public async Task ReadSnapshot_AutomationAlwaysFails_FallsBackAndRemainsUsable()
    {
        var native = new FakeNativeApi
        {
            TaskbarRect = new PixelRect(0, 1738, 2940, 108),
            Scale = 1.0
        };
        var automation = new FakeAutomationApi { ThrowStaleAlways = true };
        var reader = CreateReader(native, automation);

        var snapshot = await reader.ReadSnapshotAsync(CancellationToken.None);

        Assert.AreEqual(3, snapshot.OccupiedBounds.Count);
    }

    [TestMethod]
    public async Task ReadSnapshot_DpiChange_ReportsNewScale()
    {
        var native = new FakeNativeApi
        {
            TaskbarRect = new PixelRect(0, 1738, 2940, 108),
            Scale = 2.25
        };
        var reader = CreateReader(native, new FakeAutomationApi());

        await reader.ReadSnapshotAsync(CancellationToken.None);
        native.Scale = 1.5;
        native.TriggerChange();
        var snapshot = await reader.ReadSnapshotAsync(CancellationToken.None);

        Assert.AreEqual(1.5, snapshot.RasterizationScale);
    }

    private static TaskbarSnapshotReader CreateReader(
        FakeNativeApi native,
        FakeAutomationApi automation) =>
        new(native, automation, new FakeFullscreenDetector { IsFullscreen = false });

    private sealed class FakeNativeApi : ITaskbarNativeApi
    {
        public bool TaskbarFound { get; set; } = true;

        public nint HwndOverride { get; set; }

        public PixelRect TaskbarRect { get; set; }

        public double Scale { get; set; } = 1.0;

        public bool AutoHide { get; set; }

        public nint FindPrimaryTaskbar() => TaskbarFound ? (HwndOverride != 0 ? HwndOverride : TaskbarHwnd) : 0;

        public PixelRect GetWindowRect(nint window) => TaskbarRect;

        public TaskbarEdge GetEdge(nint window) => TaskbarEdge.Bottom;

        public bool IsAutoHideEnabled() => AutoHide;

        public double GetRasterizationScale(nint window) => Scale;

        public bool IsForegroundFullscreen(PixelRect monitorBounds) => false;

        public void TriggerChange() { }
    }

    private sealed class FakeAutomationApi : ITaskbarAutomationApi
    {
        public bool DenyAccess { get; set; }

        public bool ThrowStaleOnce { get; set; }

        public bool ThrowStaleAlways { get; set; }

        public int AccessCount { get; private set; }

        public IReadOnlyList<PixelRect> Bounds { get; set; } = [];

        public IReadOnlyList<PixelRect> GetVisibleInteractiveBounds(nint taskbarWindow)
        {
            AccessCount++;
            if (DenyAccess)
            {
                throw new UnauthorizedAccessException("automation denied");
            }

            if (ThrowStaleAlways || (ThrowStaleOnce && AccessCount == 1))
            {
                throw new InvalidOperationException("element not available");
            }

            return Bounds;
        }
    }

    private sealed class FakeFullscreenDetector : IFullscreenDetector
    {
        public bool IsFullscreen { get; set; }

        public bool IsForegroundFullscreen(PixelRect monitorBounds) => IsFullscreen;
    }
}
