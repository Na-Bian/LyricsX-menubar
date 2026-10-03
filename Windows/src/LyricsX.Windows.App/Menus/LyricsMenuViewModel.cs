using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LyricsX.Windows.App.Services;

namespace LyricsX.Windows.App.Menus;

/// <summary>
/// Commands backing the compact (left-click) and full (right-click/tray)
/// lyric menus. UI-bound actions (preferences/exit/pickers/logs/clipboard)
/// surface as events the App layer subscribes to; lyrics-affecting actions
/// call the coordinator directly.
/// </summary>
public partial class LyricsMenuViewModel : ObservableObject
{
    private readonly ILyricsApplicationCoordinator coordinator;

    [ObservableProperty]
    public partial string CurrentTrackTitle { get; set; } = "当前歌曲 — 无正在播放的媒体";

    [ObservableProperty]
    public partial string PlaybackStateText { get; set; } = "播放状态 — 未知";

    [ObservableProperty]
    public partial string VisibilityToggleText { get; set; } = "隐藏任务栏歌词";

    [ObservableProperty]
    public partial bool HasCurrentTrack { get; set; }

    public string PreferencesTitle { get; } = "偏好设置…";

    public string ManualSearchTitle { get; } = "手动搜索歌词…";

    public string ExitTitle { get; } = "退出 LyricsX";

    public event Action? PreferencesRequested;

    public event Action? ExitRequested;

    public event Action? LocalImportRequested;

    public event Action? ExportRequested;

    public LyricsMenuViewModel(ILyricsApplicationCoordinator coordinator)
    {
        this.coordinator = coordinator;
        coordinator.StateChanged += (_, state) => ApplyState(state);
        ApplyState(coordinator.Current);
    }

    [RelayCommand]
    private async Task EarlierByHalfSecondAsync() =>
        await coordinator.AdjustOffsetAsync(TimeSpan.FromMilliseconds(-500), CancellationToken.None);

    [RelayCommand]
    private async Task LaterByHalfSecondAsync() =>
        await coordinator.AdjustOffsetAsync(TimeSpan.FromMilliseconds(500), CancellationToken.None);

    [RelayCommand]
    private Task ResetOffsetAsync() => coordinator.ResetOffsetAsync();

    [RelayCommand]
    private Task ManualSearchAsync()
    {
        LocalImportRequested = null;
        ManualSearchRequested?.Invoke();
        return Task.CompletedTask;
    }

    public event Action? ManualSearchRequested;

    [RelayCommand]
    private async Task ToggleVisibilityAsync()
    {
        await coordinator.ToggleLyricsVisibilityAsync();
        VisibilityToggleText = coordinator.LyricsVisible
            ? "隐藏任务栏歌词"
            : "显示任务栏歌词";
    }

    [RelayCommand]
    private void Preferences() => PreferencesRequested?.Invoke();

    [RelayCommand]
    private void Exit() => ExitRequested?.Invoke();

    [RelayCommand]
    private void OpenLocal() => LocalImportRequested?.Invoke();

    [RelayCommand]
    private void Export() => ExportRequested?.Invoke();

    [RelayCommand]
    private async Task IgnoreTrackAsync() =>
        await coordinator.IgnoreCurrentTrackAsync();

    [RelayCommand]
    private void OpenLogs() => OpenLogsRequested?.Invoke();

    public event Action? OpenLogsRequested;

    [RelayCommand]
    private void CopyDiagnostics() => CopyDiagnosticsRequested?.Invoke();

    public event Action? CopyDiagnosticsRequested;

    private void ApplyState(LyricsPresentationState state)
    {
        var track = state.Playback.Track;
        HasCurrentTrack = track is not null;
        CurrentTrackTitle = track is null
            ? "当前歌曲 — 无正在播放的媒体"
            : $"当前歌曲 — {track.Title} / {track.Artist}";
        PlaybackStateText = $"播放状态 — {state.Playback.Status}";
    }
}
