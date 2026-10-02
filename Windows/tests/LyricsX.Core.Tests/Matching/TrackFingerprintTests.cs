using LyricsX.Core;
using LyricsX.Core.Matching;

namespace LyricsX.Core.Tests.Matching;

[TestClass]
public sealed class TrackFingerprintTests
{
    [TestMethod]
    public void Create_RemovesFeaturedArtistAndEditionNoise()
    {
        var track = new MediaTrack(
            "AppleMusic", "1", "Song（Live） feat. Guest", "Artist & Guest",
            "Album", TimeSpan.FromSeconds(201));

        Assert.AreEqual("song|artist|201", TrackFingerprint.Create(track));
    }

    [TestMethod]
    public void Create_NormalizesFullWidthAndWhitespace()
    {
        var track = new MediaTrack(
            "Browser", "2", " Ｈｅｌｌｏ　World ", " Test  Artist ",
            null, null);

        Assert.AreEqual("hello world|test artist|?", TrackFingerprint.Create(track));
    }

    [TestMethod]
    public void CreateAlbum_UsesPrimaryArtistAndAlbum()
    {
        var track = new MediaTrack(
            "QQMusic", "3", "Song", "Artist feat. Guest",
            "Album（Deluxe）", null);

        Assert.AreEqual("artist|album", TrackFingerprint.CreateAlbum(track));
    }
}
