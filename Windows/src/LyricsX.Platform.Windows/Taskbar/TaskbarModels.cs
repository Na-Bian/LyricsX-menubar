namespace LyricsX.Platform.Windows.Taskbar;

public enum TaskbarEdge
{
    Bottom,
    Top,
    Left,
    Right
}

public sealed record TaskbarSnapshot(
    PixelRect TaskbarBounds,
    TaskbarEdge Edge,
    IReadOnlyList<PixelRect> OccupiedBounds,
    double RasterizationScale,
    bool AutoHide,
    bool IsForegroundFullscreen);

public sealed record TaskbarPlacementResult(
    PixelRect Bounds,
    double RasterizationScale,
    bool IsVisible);
