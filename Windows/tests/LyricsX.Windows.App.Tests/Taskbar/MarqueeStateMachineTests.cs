using LyricsX.Windows.App.Taskbar;

namespace LyricsX.Windows.App.Tests.Taskbar;

[TestClass]
public sealed class MarqueeStateMachineTests
{
    [TestMethod]
    public void Advance_OverflowTextPausesMovesAndRepeats()
    {
        // cycle: 0.8 s initial pause, 100 px at 30 px/s, 0.5 s end pause
        var marquee = new MarqueeStateMachine(
            viewportWidth: 100, textWidth: 160, speedPixelsPerSecond: 30);

        Assert.AreEqual(0, marquee.Advance(TimeSpan.FromMilliseconds(799)).Offset);
        Assert.AreEqual(0, marquee.Advance(TimeSpan.FromMilliseconds(1)).Offset);
        Assert.AreEqual(-30, marquee.Advance(TimeSpan.FromSeconds(1)).Offset, 0.01);
        // travel completes at exactly one viewport, seam holds at the start
        Assert.AreEqual(0, marquee.Advance(TimeSpan.FromSeconds(2.34)).Offset, 0.01);
        // end pause (0.5 s) then the cycle restarts with the initial pause
        Assert.AreEqual(0, marquee.Advance(TimeSpan.FromMilliseconds(500)).Offset);
        Assert.AreEqual(0, marquee.Advance(TimeSpan.FromMilliseconds(400)).Offset);
        // second cycle travels at the same speed
        Assert.AreEqual(-24.2, marquee.Advance(
            TimeSpan.FromMilliseconds(1_200)).Offset, 0.1);
    }

    [TestMethod]
    public void Advance_FittingText_NeverMoves()
    {
        var marquee = new MarqueeStateMachine(
            viewportWidth: 200, textWidth: 160, speedPixelsPerSecond: 30);

        for (var i = 0; i < 20; i++)
        {
            Assert.AreEqual(0, marquee.Advance(TimeSpan.FromMilliseconds(100)).Offset);
        }
    }

    [TestMethod]
    public void Advance_InitialPauseLasts800Milliseconds()
    {
        var marquee = new MarqueeStateMachine(100, 160, 30);

        Assert.AreEqual(0, marquee.Advance(TimeSpan.FromMilliseconds(800)).Offset);
        Assert.IsTrue(marquee.Advance(TimeSpan.FromMilliseconds(50)).Offset < 0);
    }

    [TestMethod]
    public void Advance_TravelsAtConfiguredSpeed()
    {
        var marquee = new MarqueeStateMachine(100, 160, 30);
        _ = marquee.Advance(TimeSpan.FromSeconds(0.8)); // initial pause

        var state = marquee.Advance(TimeSpan.FromSeconds(1));
        Assert.AreEqual(-30, state.Offset, 0.01);
    }

    [TestMethod]
    public void Advance_EndPauseThenRepeat()
    {
        var marquee = new MarqueeStateMachine(100, 160, 30);

        // travel the full cycle (0.8 s pause + 100 px travel)
        _ = marquee.Advance(TimeSpan.FromMilliseconds(800));
        _ = marquee.Advance(TimeSpan.FromSeconds(100.0 / 30));
        // end pause holds the seam at the start position
        Assert.AreEqual(0, marquee.Advance(TimeSpan.FromMilliseconds(400)).Offset, 0.01);
        // after the end pause the cycle restarts with a fresh initial pause
        Assert.AreEqual(0, marquee.Advance(TimeSpan.FromMilliseconds(100)).Offset, 0.01);
        Assert.AreEqual(0, marquee.Advance(TimeSpan.FromMilliseconds(700)).Offset);
    }

    [TestMethod]
    public void Reset_RestartsFromInitialPause()
    {
        var marquee = new MarqueeStateMachine(100, 160, 30);
        _ = marquee.Advance(TimeSpan.FromMilliseconds(800));
        _ = marquee.Advance(TimeSpan.FromSeconds(2));

        marquee.Reset();

        Assert.AreEqual(0, marquee.Advance(TimeSpan.FromMilliseconds(799)).Offset);
    }

    [TestMethod]
    public void Advance_ReducedMotion_AlwaysAtZero()
    {
        var marquee = new MarqueeStateMachine(100, 160, 30, reducedMotion: true);

        Assert.AreEqual(0, marquee.Advance(TimeSpan.FromSeconds(10)).Offset);
        Assert.IsFalse(marquee.ShouldAnimate);
    }
}
