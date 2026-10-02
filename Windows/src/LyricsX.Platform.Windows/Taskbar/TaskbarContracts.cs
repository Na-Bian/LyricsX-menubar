namespace LyricsX.Platform.Windows;

public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;

    public int Bottom => Y + Height;
}

public sealed record TaskbarPlacement(
    PixelRect Bounds,
    double RasterizationScale,
    bool IsVisible);

public interface ITaskbarPlacementService : IAsyncDisposable
{
    event EventHandler? PlacementChanged;

    Task<TaskbarPlacement> GetPlacementAsync(
        int preferredWidthDip,
        CancellationToken cancellationToken);
}

public interface ITaskbarLyricsWindowHost
{
    bool IsVisible { get; }

    void Show(TaskbarPlacement placement);

    void Hide();
}
