using LyricsX.Core;
using LyricsX.Core.Playback;
using LyricsX.Windows.App.Services;
using LyricsX.Windows.App.Composition;
using LyricsX.Windows.App.Menus;

namespace LyricsX.Windows.App.Tests.Menus;

[TestClass]
public sealed class LyricsMenuViewModelTests
{
    [TestMethod]
    public async Task OffsetCommands_CallCoordinator()
    {
        var coordinator = new FakeCoordinator();
        var viewModel = new LyricsMenuViewModel(coordinator);

        await viewModel.EarlierByHalfSecondCommand.ExecuteAsync(null);
        await viewModel.LaterByHalfSecondCommand.ExecuteAsync(null);

        CollectionAssert.AreEqual(
            new[] { TimeSpan.FromMilliseconds(-500), TimeSpan.FromMilliseconds(500) },
            coordinator.OffsetCalls.ToArray());
    }

    [TestMethod]
    public async Task ToggleVisibility_ReportsHidden()
    {
        var coordinator = new FakeCoordinator();
        var viewModel = new LyricsMenuViewModel(coordinator);

        await viewModel.ToggleVisibilityCommand.ExecuteAsync(null);

        Assert.IsTrue(coordinator.VisibilityToggled);
        Assert.AreEqual("显示任务栏歌词", viewModel.VisibilityToggleText);
    }

    [TestMethod]
    public void MenuTexts_ContainRequiredWording()
    {
        var viewModel = new LyricsMenuViewModel(new FakeCoordinator());

        StringAssert.Contains(viewModel.PreferencesTitle, "偏好设置…");
        StringAssert.Contains(viewModel.ManualSearchTitle, "手动搜索歌词…");
    }

    private sealed class FakeCoordinator : ILyricsApplicationCoordinator
    {
        public List<TimeSpan> OffsetCalls { get; } = [];

        public bool VisibilityToggled { get; private set; }

#pragma warning disable CS0067
        public event EventHandler<LyricsPresentationState>? StateChanged;
#pragma warning restore CS0067

        public LyricsPresentationState Current { get; private set; } =
            LyricsPresentationState.Empty with
            {
                Playback = new PlaybackSnapshot(
                    new MediaTrack("p", "id", "Song", "Artist", null, null),
                    PlaybackStatus.Playing, TimeSpan.Zero, 1.0, DateTimeOffset.UtcNow)
            };

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SearchManuallyAsync(string title, string artist, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task UseCandidateAsync(LyricCandidate candidate, bool remember, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task AdjustOffsetAsync(TimeSpan delta, CancellationToken cancellationToken)
        {
            OffsetCalls.Add(delta);
            return Task.CompletedTask;
        }

        public bool LyricsVisible { get; private set; } = true;

        public Task ToggleLyricsVisibilityAsync()
        {
            VisibilityToggled = true;
            LyricsVisible = !LyricsVisible;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
