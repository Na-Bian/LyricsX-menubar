namespace LyricsX.Platform.Windows.Taskbar;

/// <summary>
/// Pure leftmost-gap selection for a horizontal taskbar. Occupied rectangles
/// are inflated, clipped, and merged into intervals; the first gap wide
/// enough for the minimum DIP width wins, clamped to preferred and maximum.
/// </summary>
public sealed class TaskbarIntervalCalculator
{
    private const int HorizontalInflationPx = 4;
    private const double MinRasterizationScale = 0.5;

    public TaskbarPlacementResult Calculate(
        TaskbarSnapshot snapshot,
        int preferredWidthDip,
        int minimumWidthDip,
        int maximumWidthDip)
    {
        if (snapshot.AutoHide ||
            snapshot.IsForegroundFullscreen ||
            snapshot.Edge is not (TaskbarEdge.Bottom or TaskbarEdge.Top) ||
            snapshot.TaskbarBounds.Width <= 0 ||
            snapshot.TaskbarBounds.Height <= 0 ||
            snapshot.RasterizationScale < MinRasterizationScale)
        {
            return new TaskbarPlacementResult(snapshot.TaskbarBounds, snapshot.RasterizationScale, false);
        }

        var scale = snapshot.RasterizationScale;
        var minimumPx = (int)Math.Ceiling(minimumWidthDip * scale);
        var preferredPx = Math.Min(
            (int)Math.Round(preferredWidthDip * scale),
            (int)Math.Round(maximumWidthDip * scale));

        foreach (var gap in Gaps(snapshot))
        {
            if (gap.Width < minimumPx)
            {
                continue;
            }

            var width = Math.Min(preferredPx, gap.Width);
            return new TaskbarPlacementResult(
                new PixelRect(gap.X, snapshot.TaskbarBounds.Y, width, snapshot.TaskbarBounds.Height),
                scale,
                true);
        }

        return new TaskbarPlacementResult(snapshot.TaskbarBounds, scale, false);
    }

    private static IEnumerable<PixelRect> Gaps(TaskbarSnapshot snapshot)
    {
        var taskbar = snapshot.TaskbarBounds;
        var intervals = new List<(int Start, int End)>();
        foreach (var occupied in snapshot.OccupiedBounds)
        {
            var left = Math.Max(taskbar.X, occupied.X - HorizontalInflationPx);
            var right = Math.Min(taskbar.Right, occupied.Right + HorizontalInflationPx);
            if (right <= left)
            {
                continue;
            }

            intervals.Add((left, right));
        }

        intervals.Sort((a, b) => a.Start != b.Start
            ? a.Start.CompareTo(b.Start)
            : a.End.CompareTo(b.End));

        var cursor = taskbar.X;
        foreach (var (start, end) in Merge(intervals))
        {
            if (start > cursor)
            {
                yield return new PixelRect(cursor, taskbar.Y, start - cursor, taskbar.Height);
            }

            cursor = Math.Max(cursor, end);
        }

        if (cursor < taskbar.Right)
        {
            yield return new PixelRect(cursor, taskbar.Y, taskbar.Right - cursor, taskbar.Height);
        }
    }

    private static IEnumerable<(int Start, int End)> Merge(List<(int Start, int End)> intervals)
    {
        if (intervals.Count == 0)
        {
            yield break;
        }

        var current = intervals[0];
        foreach (var interval in intervals.Skip(1))
        {
            if (interval.Start <= current.End)
            {
                current = (current.Start, Math.Max(current.End, interval.End));
            }
            else
            {
                yield return current;
                current = interval;
            }
        }

        yield return current;
    }
}
