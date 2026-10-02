using CommunityToolkit.Mvvm.ComponentModel;
using LyricsX.Core;
using LyricsX.Windows.App.Services;

namespace LyricsX.Windows.App.Taskbar;

/// <summary>
/// Presentation state for the taskbar lyric surface: current text, progress,
/// next line, and overlay visibility. Pure view model, unit-testable.
/// </summary>
public partial class TaskbarLyricsViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string Text { get; set; } = "无正在播放的媒体";

    [ObservableProperty]
    public partial string? NextText { get; set; }

    [ObservableProperty]
    public partial double Progress { get; set; }

    [ObservableProperty]
    public partial bool IsSearching { get; set; }

    [ObservableProperty]
    public partial bool IsVisible { get; set; }

    public void ApplyState(LyricsPresentationState state)
    {
        Text = state.StatusText;
        Progress = state.Frame.Progress;
        NextText = state.Frame.Next?.Content;
        IsSearching = state.IsSearching;
        // visible whenever there is something meaningful to show: an active
        // line, an in-flight search, or a status other than the idle text
        IsVisible = state.Frame.Current is not null ||
            state.IsSearching ||
            (!string.IsNullOrEmpty(state.StatusText) &&
             state.StatusText is not "无正在播放的媒体");
    }
}
