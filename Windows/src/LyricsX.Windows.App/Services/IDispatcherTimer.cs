namespace LyricsX.Windows.App.Services;

/// <summary>Dispatcher timer seam so coordinator ticking is unit-testable.</summary>
public interface IDispatcherTimer
{
    TimeSpan Interval { get; set; }

    bool IsRunning { get; }

    event EventHandler? Tick;

    void Start();

    void Stop();
}
