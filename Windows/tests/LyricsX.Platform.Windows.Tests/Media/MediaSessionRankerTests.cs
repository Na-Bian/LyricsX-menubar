using LyricsX.Core;
using LyricsX.Platform.Windows;
using LyricsX.Platform.Windows.Media;

namespace LyricsX.Platform.Windows.Tests.Media;

[TestClass]
public sealed class MediaSessionRankerTests
{
    private static DateTimeOffset Now { get; } = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Select_PrefersPlayingThenMostRecentlyUpdated()
    {
        var paused = Session("paused", PlaybackStatus.Paused, Now);
        var oldPlaying = Session("old", PlaybackStatus.Playing, Now.AddSeconds(-5));
        var newPlaying = Session("new", PlaybackStatus.Playing, Now);

        var selected = new MediaSessionRanker().Select(
            [paused, oldPlaying, newPlaying], previousSessionId: "old");

        Assert.AreEqual("new", selected?.SessionId);
    }

    [TestMethod]
    public void Select_ReturnsNullForEmptyList()
    {
        Assert.IsNull(new MediaSessionRanker().Select([], null));
    }

    [TestMethod]
    public void Select_TieBreaksByOrdinalSessionId()
    {
        var first = Session("a", PlaybackStatus.Paused, Now);
        var second = Session("b", PlaybackStatus.Paused, Now);

        var selected = new MediaSessionRanker().Select([second, first], null);

        Assert.AreEqual("a", selected?.SessionId);
    }

    [TestMethod]
    public void Select_PrefersPausedOverStoppedOverClosed()
    {
        var stopped = Session("stopped", PlaybackStatus.Stopped, Now);
        var closed = Session("closed", PlaybackStatus.Closed, Now);
        var paused = Session("paused", PlaybackStatus.Paused, Now);

        var selected = new MediaSessionRanker().Select([closed, stopped, paused], null);

        Assert.AreEqual("paused", selected?.SessionId);
    }

    [TestMethod]
    public void Select_KeepsPreviouslySelectedAmongEqualCandidates()
    {
        var previous = Session("prev", PlaybackStatus.Playing, Now);
        var other = Session("other", PlaybackStatus.Playing, Now);

        var selected = new MediaSessionRanker().Select(
            [other, previous], previousSessionId: "prev");

        Assert.AreEqual("prev", selected?.SessionId);
    }

    [TestMethod]
    public void Select_PreferredSourceRestrictsSelection()
    {
        var playing = Session("playing", PlaybackStatus.Playing, Now, "music.app");
        var paused = Session("paused", PlaybackStatus.Paused, Now, "video.app");

        var selected = new MediaSessionRanker().Select(
            [playing, paused], previousSessionId: null, preferredSourceId: "video.app");

        Assert.AreEqual("paused", selected?.SessionId);
    }

    [TestMethod]
    public void Select_PreferredSourceAbsentFallsBackToAutomatic()
    {
        var playing = Session("playing", PlaybackStatus.Playing, Now, "music.app");

        var selected = new MediaSessionRanker().Select(
            [playing], previousSessionId: null, preferredSourceId: "closed.app");

        Assert.AreEqual("playing", selected?.SessionId);
    }

    [TestMethod]
    public void Select_EmptyPreferredSourceBehavesAutomatically()
    {
        var playing = Session("playing", PlaybackStatus.Playing, Now);

        var selected = new MediaSessionRanker().Select(
            [playing], previousSessionId: null, preferredSourceId: "");

        Assert.AreEqual("playing", selected?.SessionId);
    }

    private static MediaSessionSnapshot Session(
        string id, PlaybackStatus status, DateTimeOffset updated) =>
        Session(id, status, updated, id + ".app");

    private static MediaSessionSnapshot Session(
        string id, PlaybackStatus status, DateTimeOffset updated, string sourceAppId) =>
        new(id, sourceAppId, status,
            new MediaTrack(sourceAppId, "tid", "Title", "Artist", null, TimeSpan.FromSeconds(100)),
            TimeSpan.Zero, 1.0, updated, id + "-native");
}
