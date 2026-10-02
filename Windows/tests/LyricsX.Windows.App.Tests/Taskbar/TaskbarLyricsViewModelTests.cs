using LyricsX.Core;
using LyricsX.Core.Playback;
using LyricsX.Windows.App.Services;
using LyricsX.Windows.App.Taskbar;

namespace LyricsX.Windows.App.Tests.Taskbar;

[TestClass]
public sealed class TaskbarLyricsViewModelTests
{
    [TestMethod]
    public void ApplyState_TextAndProgressUpdateFromFrame()
    {
        var viewModel = new TaskbarLyricsViewModel();
        var line = new LyricLine(TimeSpan.Zero, TimeSpan.FromSeconds(4), "current line");
        var frame = new LyricFrame(0, line, null, 0.5, -1, 0);
        var state = LyricsPresentationState.Empty with
        {
            StatusText = "current line",
            Frame = frame
        };

        viewModel.ApplyState(state);

        Assert.AreEqual("current line", viewModel.Text);
        Assert.AreEqual(0.5, viewModel.Progress, 0.001);
        Assert.IsFalse(viewModel.IsSearching);
    }

    [TestMethod]
    public void ApplyState_SearchingState_SetsFlagAndStatusText()
    {
        var viewModel = new TaskbarLyricsViewModel();

        viewModel.ApplyState(LyricsPresentationState.Empty with
        {
            StatusText = "正在搜索歌词…",
            IsSearching = true
        });

        Assert.AreEqual("正在搜索歌词…", viewModel.Text);
        Assert.IsTrue(viewModel.IsSearching);
    }

    [TestMethod]
    public void ApplyState_NextLine_ExposedWhenPresent()
    {
        var viewModel = new TaskbarLyricsViewModel();
        var current = new LyricLine(TimeSpan.Zero, TimeSpan.FromSeconds(2), "one");
        var next = new LyricLine(TimeSpan.FromSeconds(2), null, "two");
        var state = LyricsPresentationState.Empty with
        {
            StatusText = "one",
            Frame = new LyricFrame(0, current, next, 0, -1, 0)
        };

        viewModel.ApplyState(state);

        Assert.AreEqual("two", viewModel.NextText);
    }

    [TestMethod]
    public void ApplyState_NoMedia_HidesOverlay()
    {
        var viewModel = new TaskbarLyricsViewModel();

        viewModel.ApplyState(LyricsPresentationState.Empty);

        Assert.AreEqual("无正在播放的媒体", viewModel.Text);
        Assert.IsFalse(viewModel.IsVisible);
    }

    [TestMethod]
    public void ApplyState_ActiveLyrics_ShowsOverlay()
    {
        var viewModel = new TaskbarLyricsViewModel();
        var line = new LyricLine(TimeSpan.Zero, TimeSpan.FromSeconds(4), "shown");
        var state = LyricsPresentationState.Empty with
        {
            StatusText = "shown",
            Frame = new LyricFrame(0, line, null, 0, -1, 0)
        };

        viewModel.ApplyState(state);

        Assert.IsTrue(viewModel.IsVisible);
    }
}
