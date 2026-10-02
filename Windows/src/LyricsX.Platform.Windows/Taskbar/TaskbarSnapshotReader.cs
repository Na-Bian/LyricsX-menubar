using System.IO;

namespace LyricsX.Platform.Windows.Taskbar;

/// <summary>
/// Builds a <see cref="TaskbarSnapshot"/> from the native and automation
/// seams. Handles absent Explorer, restarted Explorer (new HWND), denied or
/// failing automation (falls back to fixed reservations), and off-screen or
/// stale controls.
/// </summary>
public class TaskbarSnapshotReader(
    ITaskbarNativeApi nativeApi,
    ITaskbarAutomationApi automationApi,
    IFullscreenDetector fullscreenDetector)
{
    // fallback reservations (DIPs) when automation yields nothing usable
    private const int FallbackLeftReserveDip = 64;
    private const int FallbackCenterReserveDip = 520;

    public nint CurrentTaskbarHwnd { get; private set; }

    /// <summary>Raised by the host when shell/DPI/display events suggest the
    /// taskbar geometry changed; the placement service listens to it.</summary>
    public event EventHandler? PlacementChanged;

    /// <summary>Signals a probable geometry change (DPI change, display
    /// change, Explorer restart, shell setting change).</summary>
    public void NotifyChange() => PlacementChanged?.Invoke(this, EventArgs.Empty);

    public virtual async Task<TaskbarSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken)
    {
        var hwnd = nativeApi.FindPrimaryTaskbar();
        CurrentTaskbarHwnd = hwnd;
        if (hwnd == nint.Zero)
        {
            // absent Explorer: nothing to place against
            return new TaskbarSnapshot(default, TaskbarEdge.Bottom, [], 1.0, false, false);
        }

        var bounds = nativeApi.GetWindowRect(hwnd);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return new TaskbarSnapshot(default, TaskbarEdge.Bottom, [], 1.0, false, false);
        }

        var scale = nativeApi.GetRasterizationScale(hwnd);

        var occupied = await Task.Run(
            () => ReadOccupiedWithRetry(hwnd, bounds, scale), cancellationToken);
        return new TaskbarSnapshot(
            bounds,
            nativeApi.GetEdge(hwnd),
            occupied,
            scale,
            nativeApi.IsAutoHideEnabled(),
            fullscreenDetector.IsForegroundFullscreen(bounds));
    }

    private IReadOnlyList<PixelRect> ReadOccupiedWithRetry(
        nint hwnd, PixelRect taskbarBounds, double scale)
    {
        try
        {
            var rects = automationApi.GetVisibleInteractiveBounds(hwnd);
            var usable = rects.Where(rect => Intersects(rect, taskbarBounds)).ToList();
            return usable.Count > 0
                ? usable
                : FallbackReservations(taskbarBounds, scale);
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException or InvalidOperationException or IOException)
        {
            // stale elements: retry once with a fresh root
            try
            {
                var rects = automationApi.GetVisibleInteractiveBounds(hwnd);
                var usable = rects.Where(rect => Intersects(rect, taskbarBounds)).ToList();
                return usable.Count > 0
                    ? usable
                    : FallbackReservations(taskbarBounds, scale);
            }
            catch (Exception)
            {
                // denied or unexpected Explorer tree: safe fallback reservations
                return FallbackReservations(taskbarBounds, scale);
            }
        }
    }

    private static bool Intersects(PixelRect rect, PixelRect bounds) =>
        rect.Width >= 4 && rect.Height >= 4 &&
        rect.Right > bounds.X && rect.X < bounds.Right &&
        rect.Bottom > bounds.Y && rect.Y < bounds.Bottom;

    private static IReadOnlyList<PixelRect> FallbackReservations(
        PixelRect taskbarBounds, double scale)
    {
        var left = (int)Math.Round(FallbackLeftReserveDip * scale);
        var center = (int)Math.Round(FallbackCenterReserveDip * scale);
        var centerStart = taskbarBounds.X + (taskbarBounds.Width - center) / 2;
        return
        [
            new PixelRect(taskbarBounds.X, taskbarBounds.Y, left, taskbarBounds.Height),
            new PixelRect(centerStart, taskbarBounds.Y, center, taskbarBounds.Height),
            new PixelRect(taskbarBounds.Right - 140, taskbarBounds.Y, 140, taskbarBounds.Height)
        ];
    }
}
