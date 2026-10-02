using System.Diagnostics;

namespace LyricsX.Platform.Windows.Taskbar;

/// <summary>
/// Publishes lyric-window placements: pure calculation on demand, plus an
/// observation loop that wakes on either the poll interval or a reader
/// geometry-change signal, debounces, and repositions only on semantic
/// placement changes.
/// </summary>
public sealed class TaskbarPlacementService(
    TaskbarSnapshotReader reader,
    TaskbarIntervalCalculator calculator,
    TimeSpan? pollInterval = null,
    TimeSpan? debounceDelay = null) : ITaskbarPlacementService
{
    private static readonly TimeSpan DefaultPoll = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan DefaultDebounce = TimeSpan.FromMilliseconds(100);

    private readonly TimeSpan pollInterval = pollInterval ?? DefaultPoll;
    private readonly TimeSpan debounceDelay = debounceDelay ?? DefaultDebounce;
    private readonly SemaphoreSlim wakeSignal = new(0, int.MaxValue);
    private int preferredWidthDip = 260;
    private TaskbarPlacement lastPlacement = new(default, 1.0, false);
    private int disposed;

    public event EventHandler? PlacementChanged;

    public async Task<TaskbarPlacement> GetPlacementAsync(
        int preferredWidthDip,
        CancellationToken cancellationToken)
    {
        this.preferredWidthDip = preferredWidthDip;
        return await ComputePlacementAsync(cancellationToken);
    }

    /// <summary>
    /// Runs the observation loop until cancelled. Reader geometry signals and
    /// the poll timer both trigger debounced recomputation.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        reader.PlacementChanged += OnReaderChanged;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var pollTask = Task.Delay(pollInterval, cancellationToken);
                var wakeTask = wakeSignal.WaitAsync(cancellationToken);
                try
                {
                    await Task.WhenAny(pollTask, wakeTask);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                try
                {
                    await Task.Delay(debounceDelay, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                await ComputeAndPublishAsync(cancellationToken);
            }
        }
        finally
        {
            reader.PlacementChanged -= OnReaderChanged;
        }
    }

    private void OnReaderChanged(object? sender, EventArgs args)
    {
        try
        {
            wakeSignal.Release();
        }
        catch (SemaphoreFullException)
        {
        }
    }

    private async Task ComputeAndPublishAsync(CancellationToken cancellationToken)
    {
        var placement = await ComputePlacementAsync(cancellationToken);
        if (placement.Bounds == lastPlacement.Bounds &&
            placement.IsVisible == lastPlacement.IsVisible &&
            Math.Abs(placement.RasterizationScale - lastPlacement.RasterizationScale) < 0.001)
        {
            return;
        }

        lastPlacement = placement;
        PlacementChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task<TaskbarPlacement> ComputePlacementAsync(CancellationToken cancellationToken)
    {
        TaskbarSnapshot snapshot;
        try
        {
            snapshot = await reader.ReadSnapshotAsync(cancellationToken);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or UnauthorizedAccessException)
        {
            return new TaskbarPlacement(default, 1.0, false);
        }

        var result = calculator.Calculate(snapshot, preferredWidthDip, minimumWidthDip: 80, maximumWidthDip: 360);
        return new TaskbarPlacement(result.Bounds, result.RasterizationScale, result.IsVisible);
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0)
        {
            wakeSignal.Dispose();
        }

        return ValueTask.CompletedTask;
    }
}
