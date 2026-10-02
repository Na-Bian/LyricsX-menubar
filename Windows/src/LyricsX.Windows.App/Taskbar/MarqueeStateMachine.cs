namespace LyricsX.Windows.App.Taskbar;

/// <summary>
/// Deterministic marquee phases: 800 ms initial pause → leftward travel at a
/// fixed pixel rate → 500 ms end pause → repeat. Pure state, unit-testable;
/// the view maps <see cref="Advance"/> offsets onto a translate transform.
/// </summary>
public sealed class MarqueeStateMachine
{
    private static readonly TimeSpan InitialPause = TimeSpan.FromMilliseconds(800);
    private static readonly TimeSpan EndPause = TimeSpan.FromMilliseconds(500);

    private readonly double viewportWidth;
    private readonly double textWidth;
    private readonly double speedPixelsPerSecond;
    private readonly bool reducedMotion;

    private TimeSpan clock;

    public MarqueeStateMachine(
        double viewportWidth,
        double textWidth,
        double speedPixelsPerSecond,
        bool reducedMotion = false)
    {
        this.viewportWidth = viewportWidth;
        this.textWidth = textWidth;
        this.speedPixelsPerSecond = speedPixelsPerSecond;
        this.reducedMotion = reducedMotion;
    }

    /// <summary>
    /// Seconds of leftward travel per cycle: the seam travels exactly one
    /// viewport, where the duplicated text block takes the original place.
    /// </summary>
    public double CycleTravelSeconds =>
        textWidth > viewportWidth
            ? viewportWidth / speedPixelsPerSecond
            : 0;

    public bool ShouldAnimate => !reducedMotion && textWidth > viewportWidth;

    public double TotalCycleSeconds => CycleTravelSeconds + InitialPause.TotalSeconds + EndPause.TotalSeconds;

    public (double Offset, bool Animating) Advance(TimeSpan delta)
    {
        if (!ShouldAnimate)
        {
            return (0, false);
        }

        var cycleSeconds = TotalCycleSeconds;
        clock += delta;
        if (clock.TotalSeconds >= cycleSeconds)
        {
            // re-align the clock at the cycle boundary so floating-point
            // residue never leaks into the next cycle's initial pause
            clock = TimeSpan.FromSeconds(clock.TotalSeconds % cycleSeconds);
        }

        var cycle = clock.TotalSeconds;

        if (cycle < InitialPause.TotalSeconds)
        {
            return (0, true);
        }

        var afterInitial = cycle - InitialPause.TotalSeconds;
        if (afterInitial < CycleTravelSeconds)
        {
            return (-afterInitial * speedPixelsPerSecond, true);
        }

        // end pause: the text has fully exited the viewport, so the seam
        // shows the start position again before the cycle repeats
        return (0, true);
    }

    public void Reset() => clock = TimeSpan.Zero;
}
