using LyricsX.Platform.Windows;
using LyricsX.Platform.Windows.Taskbar;

namespace LyricsX.Platform.Windows.Tests.Taskbar;

[TestClass]
public sealed class TaskbarIntervalCalculatorTests
{
    [TestMethod]
    public void Calculate_ReturnsFirstGapAfterLeftSystemButtons()
    {
        var snapshot = new TaskbarSnapshot(
            new PixelRect(0, 1040, 1920, 40),
            TaskbarEdge.Bottom,
            [
                new PixelRect(0, 1040, 48, 40),
                new PixelRect(760, 1040, 400, 40),
                new PixelRect(1650, 1040, 270, 40)
            ],
            1.0,
            AutoHide: false,
            IsForegroundFullscreen: false);

        var result = new TaskbarIntervalCalculator().Calculate(snapshot, 260, 80, 360);

        Assert.AreEqual(new PixelRect(52, 1040, 260, 40), result.Bounds);
        Assert.IsTrue(result.IsVisible);
    }

    [TestMethod]
    public void Calculate_WidgetsOnLeft_SecondGapIsUsable()
    {
        var snapshot = new TaskbarSnapshot(
            new PixelRect(0, 1040, 1920, 40),
            TaskbarEdge.Bottom,
            [
                new PixelRect(0, 1040, 120, 40),
                new PixelRect(760, 1040, 400, 40),
                new PixelRect(1650, 1040, 270, 40)
            ],
            1.0,
            AutoHide: false,
            IsForegroundFullscreen: false);

        var result = new TaskbarIntervalCalculator().Calculate(snapshot, 260, 80, 360);

        Assert.AreEqual(new PixelRect(124, 1040, 260, 40), result.Bounds);
        Assert.IsTrue(result.IsVisible);
    }

    [TestMethod]
    public void Calculate_NoCenteredIcons_UsesLeftEdge()
    {
        var snapshot = new TaskbarSnapshot(
            new PixelRect(0, 1040, 1920, 40),
            TaskbarEdge.Bottom,
            [
                new PixelRect(0, 1040, 48, 40),
                new PixelRect(1650, 1040, 270, 40)
            ],
            1.0,
            AutoHide: false,
            IsForegroundFullscreen: false);

        var result = new TaskbarIntervalCalculator().Calculate(snapshot, 260, 80, 360);

        Assert.AreEqual(new PixelRect(52, 1040, 260, 40), result.Bounds);
    }

    [TestMethod]
    public void Calculate_NarrowGap_ClampsToGapWidth()
    {
        var snapshot = new TaskbarSnapshot(
            new PixelRect(0, 1040, 1920, 40),
            TaskbarEdge.Bottom,
            [
                new PixelRect(0, 1040, 48, 40),
                new PixelRect(400, 1040, 400, 40),
                new PixelRect(1650, 1040, 270, 40)
            ],
            1.0,
            AutoHide: false,
            IsForegroundFullscreen: false);

        // gap 52..396 after inflation = 344px; width = min(260, 360, 344) = 260
        var result = new TaskbarIntervalCalculator().Calculate(snapshot, 260, 80, 360);

        Assert.AreEqual(new PixelRect(52, 1040, 260, 40), result.Bounds);
    }

    [TestMethod]
    public void Calculate_GapNarrowerThanPreferred_ClampsToGapWidth()
    {
        var snapshot = new TaskbarSnapshot(
            new PixelRect(0, 1040, 1920, 40),
            TaskbarEdge.Bottom,
            [
                new PixelRect(0, 1040, 48, 40),
                new PixelRect(240, 1040, 1680, 40)
            ],
            1.0,
            AutoHide: false,
            IsForegroundFullscreen: false);

        // gap 52..236 = 184px; width = min(260, 360, 184) = 184
        var result = new TaskbarIntervalCalculator().Calculate(snapshot, 260, 80, 360);

        Assert.AreEqual(new PixelRect(52, 1040, 184, 40), result.Bounds);
    }

    [TestMethod]
    public void Calculate_AllGapsBelowMinimum_Hides()
    {
        var snapshot = new TaskbarSnapshot(
            new PixelRect(0, 1040, 1920, 40),
            TaskbarEdge.Bottom,
            [
                new PixelRect(0, 1040, 48, 40),
                new PixelRect(196, 1040, 1724, 40)
            ],
            1.0,
            AutoHide: false,
            IsForegroundFullscreen: false);

        // gap 52..192 = 140px < 200px minimum
        var result = new TaskbarIntervalCalculator().Calculate(snapshot, 260, 200, 360);

        Assert.IsFalse(result.IsVisible);
    }

    [TestMethod]
    public void Calculate_ScaleAppliesToDipConversion()
    {
        var snapshot = new TaskbarSnapshot(
            new PixelRect(0, 1352, 2560, 68),
            TaskbarEdge.Bottom,
            [
                new PixelRect(0, 1352, 82, 68),
                new PixelRect(1013, 1352, 534, 68),
                new PixelRect(2200, 1352, 360, 68)
            ],
            1.7,
            AutoHide: false,
            IsForegroundFullscreen: false);

        // 260 preferred DIPs -> 442px at 1.7; gap starts at 86
        var result = new TaskbarIntervalCalculator().Calculate(snapshot, 260, 80, 360);

        Assert.IsTrue(result.IsVisible);
        Assert.AreEqual(86, result.Bounds.X);
        Assert.AreEqual(442, result.Bounds.Width);
        Assert.AreEqual(68, result.Bounds.Height);
        Assert.AreEqual(1.7, result.RasterizationScale);
    }

    [TestMethod]
    public void Calculate_VerticalTaskbar_Hides()
    {
        var snapshot = new TaskbarSnapshot(
            new PixelRect(0, 0, 40, 1080),
            TaskbarEdge.Left,
            [new PixelRect(0, 0, 40, 48)],
            1.0,
            AutoHide: false,
            IsForegroundFullscreen: false);

        var result = new TaskbarIntervalCalculator().Calculate(snapshot, 260, 80, 360);

        Assert.IsFalse(result.IsVisible);
    }

    [TestMethod]
    public void Calculate_TopEdge_IsUsableAsHorizontalEdge()
    {
        var snapshot = new TaskbarSnapshot(
            new PixelRect(0, 0, 1920, 40),
            TaskbarEdge.Top,
            [new PixelRect(0, 0, 48, 40)],
            1.0,
            AutoHide: false,
            IsForegroundFullscreen: false);

        var result = new TaskbarIntervalCalculator().Calculate(snapshot, 260, 80, 360);

        Assert.AreEqual(new PixelRect(52, 0, 260, 40), result.Bounds);
        Assert.IsTrue(result.IsVisible);
    }

    [TestMethod]
    public void Calculate_AutoHide_Hides()
    {
        var snapshot = new TaskbarSnapshot(
            new PixelRect(0, 1040, 1920, 40),
            TaskbarEdge.Bottom,
            [],
            1.0,
            AutoHide: true,
            IsForegroundFullscreen: false);

        Assert.IsFalse(new TaskbarIntervalCalculator()
            .Calculate(snapshot, 260, 80, 360).IsVisible);
    }

    [TestMethod]
    public void Calculate_ForegroundFullscreen_Hides()
    {
        var snapshot = new TaskbarSnapshot(
            new PixelRect(0, 1040, 1920, 40),
            TaskbarEdge.Bottom,
            [],
            1.0,
            AutoHide: false,
            IsForegroundFullscreen: true);

        Assert.IsFalse(new TaskbarIntervalCalculator()
            .Calculate(snapshot, 260, 80, 360).IsVisible);
    }

    [TestMethod]
    public void Calculate_OverlappingOccupiedRects_MergeBeforeGapScan()
    {
        var snapshot = new TaskbarSnapshot(
            new PixelRect(0, 1040, 1920, 40),
            TaskbarEdge.Bottom,
            [
                new PixelRect(0, 1040, 48, 40),
                new PixelRect(44, 1040, 48, 40),
                new PixelRect(1650, 1040, 270, 40)
            ],
            1.0,
            AutoHide: false,
            IsForegroundFullscreen: false);

        var result = new TaskbarIntervalCalculator().Calculate(snapshot, 260, 80, 360);

        // [0..48]+[44..92] inflate to [-4..52]+[40..96], merged to 0..96
        Assert.AreEqual(new PixelRect(96, 1040, 260, 40), result.Bounds);
    }

    [TestMethod]
    public void Calculate_ZeroTaskbarSize_Hides()
    {
        var snapshot = new TaskbarSnapshot(
            new PixelRect(0, 0, 0, 0),
            TaskbarEdge.Bottom,
            [],
            1.0,
            AutoHide: false,
            IsForegroundFullscreen: false);

        Assert.IsFalse(new TaskbarIntervalCalculator()
            .Calculate(snapshot, 260, 80, 360).IsVisible);
    }
}
