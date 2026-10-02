using LyricsX.Core;
using LyricsX.Platform.Windows.Media;

namespace LyricsX.Platform.Windows.Tests.Media;

[TestClass]
public sealed class GsmtcMapperTests
{
    private static DateTimeOffset Now { get; } = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Map_PlayingSession_ProducesPlayingSnapshotWithTrack()
    {
        var snapshot = GsmtcMapper.Map(
            sessionId: "s1",
            sourceAppUserModelId: "Spotify.exe",
            playbackStatus: GlobalPlaybackStatus.Playing,
            title: "Sunny Window",
            artist: "测试歌手",
            album: "测试专辑",
            positionSeconds: 42,
            timelineEndSeconds: 201,
            lastUpdatedAt: Now);

        Assert.AreEqual("s1", snapshot.SessionId);
        Assert.AreEqual("Spotify.exe", snapshot.SourceAppId);
        Assert.AreEqual(PlaybackStatus.Playing, snapshot.Status);
        Assert.IsNotNull(snapshot.Track);
        Assert.AreEqual("Sunny Window", snapshot.Track!.Title);
        Assert.AreEqual("测试歌手", snapshot.Track.Artist);
        Assert.AreEqual("测试专辑", snapshot.Track.Album);
        Assert.AreEqual(TimeSpan.FromSeconds(42), snapshot.Position);
        Assert.AreEqual(TimeSpan.FromSeconds(201), snapshot.Track.Duration);
        Assert.AreEqual(1.0, snapshot.Rate);
        Assert.AreEqual(Now, snapshot.LastUpdatedAt);
        // without an explicit native session the id itself is retained
        Assert.AreEqual("s1", (string)snapshot.NativeSession);
    }

    [TestMethod]
    public void Map_PausedAndStopped_PreserveStatus()
    {
        var paused = GsmtcMapper.Map("s", "a", GlobalPlaybackStatus.Paused,
            "t", "ar", "al", 1, 2, Now);
        var closed = GsmtcMapper.Map("s", "a", GlobalPlaybackStatus.Closed,
            "t", "ar", "al", 1, 2, Now);

        Assert.AreEqual(PlaybackStatus.Paused, paused.Status);
        Assert.AreEqual(PlaybackStatus.Closed, closed.Status);
    }

    [TestMethod]
    public void Map_MissingMetadata_UsesEmptyStringsAndNullDuration()
    {
        var snapshot = GsmtcMapper.Map("s", "a", GlobalPlaybackStatus.Playing,
            title: "", artist: null, album: null, positionSeconds: 0,
            timelineEndSeconds: 0, lastUpdatedAt: Now);

        Assert.AreEqual(string.Empty, snapshot.Track!.Title);
        Assert.AreEqual(string.Empty, snapshot.Track.Artist);
        Assert.IsNull(snapshot.Track.Album);
        Assert.IsNull(snapshot.Track.Duration);
    }

    [TestMethod]
    public void Map_RepeatedCallsWithSameData_ProduceIdenticalTrackId()
    {
        var first = GsmtcMapper.Map("s1", "app", GlobalPlaybackStatus.Playing,
            "Song", "Artist", "Album", 0, 200, Now);
        var second = GsmtcMapper.Map("s2", "app", GlobalPlaybackStatus.Paused,
            "Song", "Artist", "Album", 10, 200, Now.AddMinutes(1));

        Assert.AreEqual(first.Track!.TrackId, second.Track!.TrackId);
        Assert.AreNotEqual(first.SessionId, second.SessionId);
    }

    [TestMethod]
    public void Map_DifferentDurations_ProduceDifferentTrackIds()
    {
        var first = GsmtcMapper.Map("s", "app", GlobalPlaybackStatus.Playing,
            "Song", "Artist", "Album", 0, 200, Now);
        var second = GsmtcMapper.Map("s", "app", GlobalPlaybackStatus.Playing,
            "Song", "Artist", "Album", 0, 240, Now);

        Assert.AreNotEqual(first.Track!.TrackId, second.Track!.TrackId);
    }

    [TestMethod]
    public void Map_SourceAppIds_PreservedVerbatim()
    {
        foreach (var appId in new[]
        {
            "AppleInc.AppleMusicWin_nzyj5cx40ttqa!App",
            "Netease Cloud Music",
            "QQMusic.exe",
            "chrome.exe",
            "Microsoft.ZuneMusic_8wekyb3d8bbwe!Microsoft.ZuneMusic"
        })
        {
            var snapshot = GsmtcMapper.Map("s", appId, GlobalPlaybackStatus.Playing,
                "t", "a", null, 0, 1, Now);
            Assert.AreEqual(appId, snapshot.SourceAppId);
        }
    }

    [TestMethod]
    public void Map_NonPlayingStatus_DoesNotReportRateAboveOne()
    {
        var snapshot = GsmtcMapper.Map("s", "a", GlobalPlaybackStatus.Paused,
            "t", "ar", null, 5, 10, Now);

        Assert.AreEqual(1.0, snapshot.Rate);
    }
}
