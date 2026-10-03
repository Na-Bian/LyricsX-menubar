using LyricsX.Platform.Windows;
using LyricsX.Windows.App.Taskbar;
using Microsoft.UI.Dispatching;

namespace LyricsX.Windows.App.Services;

/// <summary>
/// Bridges placement events and coordinator state to the taskbar lyric
/// window on the UI thread; Show only when state and placement agree.
/// </summary>
public sealed class LyricsWindowPresenter
{
    private readonly ITaskbarPlacementService placementService;
    private readonly DispatcherQueue dispatcher;
    private TaskbarLyricsWindow? window;
    private TaskbarPlacement? lastPlacement;
    private LyricsPresentationState? lastState;
    private bool enabled;

    public LyricsWindowPresenter(
        ITaskbarPlacementService placementService,
        DispatcherQueue dispatcher)
    {
        this.placementService = placementService;
        this.dispatcher = dispatcher;
    }

    public void Attach(TaskbarLyricsWindow window) => this.window = window;

    public void Enable()
    {
        enabled = true;
        placementService.PlacementChanged += OnPlacementChanged;
    }

    private void OnPlacementChanged(object? sender, EventArgs args)
    {
        _ = dispatcher.TryEnqueue(async () =>
        {
            lastPlacement = await placementService.GetPlacementAsync(260, CancellationToken.None);
            Show();
        });
    }

    public void ApplyState(LyricsPresentationState state)
    {
        lastState = state;
        if (enabled)
        {
            _ = dispatcher.TryEnqueue(Show);
        }
    }

    private void Show()
    {
        if (window is null || lastPlacement is null)
        {
            return;
        }

        window.ApplyPlacement(lastPlacement);
        if (lastState is { } state)
        {
            window.ApplyState(state);
        }
    }
}
