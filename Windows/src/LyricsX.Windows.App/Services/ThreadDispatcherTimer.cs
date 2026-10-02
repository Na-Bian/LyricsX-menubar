using System.Threading;

namespace LyricsX.Windows.App.Services;

/// <summary>
/// Production <see cref="IDispatcherTimer"/> over a System.Threading.Timer:
/// thread-affinity free (coordinator callbacks arrive on thread-pool threads;
/// the UI layer marshals StateChanged itself). Tests inject fakes.
/// </summary>
public sealed class ThreadDispatcherTimer : IDispatcherTimer, IDisposable
{
    private readonly object gate = new();
    private Timer? timer;

    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(1);

    public bool IsRunning { get; private set; }

    public event EventHandler? Tick;

    public void Start()
    {
        lock (gate)
        {
            if (IsRunning)
            {
                return;
            }

            IsRunning = true;
            timer = new Timer(
                _ => Tick?.Invoke(this, EventArgs.Empty),
                null, Interval, Interval);
        }
    }

    public void Stop()
    {
        lock (gate)
        {
            IsRunning = false;
            timer?.Dispose();
            timer = null;
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
