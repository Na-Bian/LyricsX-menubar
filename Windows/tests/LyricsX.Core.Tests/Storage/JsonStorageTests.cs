using System.Text;
using LyricsX.Core.Matching;
using LyricsX.Core.Storage;
using LyricsX.Core.Transforms;

namespace LyricsX.Core.Tests.Storage;

[TestClass]
public sealed class JsonStorageTests
{
    [TestMethod]
    public async Task LoadAsync_MissingFileReturnsDefaults()
    {
        var root = NewTempRoot();
        try
        {
            var settings = await new SettingsStore(root).LoadAsync(CancellationToken.None);

            Assert.AreEqual(260, settings.WidthDip);
            Assert.IsTrue(settings.ShowWhenPaused);
            Assert.IsFalse(settings.LaunchAtLogin);
            Assert.AreEqual(ChineseConversion.None, settings.ChineseConversion);
            CollectionAssert.AreEqual(
                new[] { "qq", "netease", "kugou", "musixmatch", "lrclib", "spotify" },
                settings.Providers.Select(provider => provider.Id).ToArray());
            Assert.IsFalse(settings.Providers.Single(p => p.Id == "spotify").Enabled);
            Assert.IsTrue(settings.Providers.Where(p => p.Id != "spotify").All(p => p.Enabled));
            Assert.AreEqual(0, settings.IgnoredTrackFingerprints.Count);
            Assert.AreEqual(0, settings.IgnoredAlbumFingerprints.Count);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [TestMethod]
    public async Task SaveThenLoad_RoundTripsCustomSettings()
    {
        var root = NewTempRoot();
        try
        {
            var store = new SettingsStore(root);
            var custom = new AppSettings(
                1, true, false, 320, -250, ChineseConversion.Traditional,
                [new ProviderSetting("netease", false, 1)],
                ["song|artist|200"],
                ["artist|album"]);
            await store.SaveAsync(custom, CancellationToken.None);

            var loaded = await store.LoadAsync(CancellationToken.None);

            Assert.AreEqual(custom, loaded);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [TestMethod]
    public async Task Save_WritesCamelCaseUtf8WithoutBomAndLeavesNoTempFiles()
    {
        var root = NewTempRoot();
        try
        {
            var store = new SettingsStore(root);
            await store.SaveAsync(AppSettings.Default, CancellationToken.None);

            var path = Path.Join(root, "settings.json");
            var bytes = await File.ReadAllBytesAsync(path);
            byte[] utf8Bom = [0xEF, 0xBB, 0xBF];
            Assert.IsFalse(bytes.Take(3).SequenceEqual(utf8Bom));
            var text = Encoding.UTF8.GetString(bytes);
            StringAssert.Contains(text, "\"chineseConversion\":\"None\"");
            StringAssert.Contains(text, "\"widthDip\":260");
            Assert.AreEqual(0, Directory.GetFiles(root, "*.tmp").Length);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [TestMethod]
    public async Task LoadAsync_MalformedJsonIsBackedUpAndDefaultsReturned()
    {
        var root = NewTempRoot();
        try
        {
            Directory.CreateDirectory(root);
            await File.WriteAllTextAsync(Path.Join(root, "settings.json"), "{ not json");
            var store = new SettingsStore(root);

            var settings = await store.LoadAsync(CancellationToken.None);

            Assert.AreEqual(260, settings.WidthDip);
            Assert.IsFalse(File.Exists(Path.Join(root, "settings.json")));
            var backups = Directory.GetFiles(Path.Join(root, "backups"), "*.invalid.json");
            Assert.AreEqual(1, backups.Length);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [TestMethod]
    public async Task LyricCache_StoreThenTryGet_RoundTripsDocument()
    {
        var root = NewTempRoot();
        try
        {
            var cache = new LyricCache(root);
            var document = new LyricDocument(
                [
                    new LyricLine(TimeSpan.FromSeconds(1), null, "one"),
                    new LyricLine(TimeSpan.FromSeconds(2), null, "two")
                ],
                new LyricMetadata("t", "a", null, null, null, new Dictionary<string, string>()),
                TimeSpan.FromMilliseconds(150));
            var candidate = new LyricCandidate(
                "netease", document, "t", "a", null, TimeSpan.FromSeconds(3), 1);

            await cache.StoreAsync("fp", candidate, "original query", 88.5, CancellationToken.None);

            var restored = await cache.TryGetAsync("fp", CancellationToken.None);
            Assert.IsNotNull(restored);
            Assert.AreEqual(2, restored.Lines.Count);
            Assert.AreEqual(TimeSpan.FromSeconds(2), restored.Lines[1].Position);
            Assert.AreEqual("two", restored.Lines[1].Content);
            Assert.AreEqual(TimeSpan.FromMilliseconds(150), restored.Delay);
            Assert.IsTrue(File.Exists(Path.Join(root, "cache", Sha256("fp") + ".json")));
        }
        finally
        {
            Cleanup(root);
        }
    }

    [TestMethod]
    public async Task LyricCache_TryGetReturnsNullForUnknownFingerprint()
    {
        var root = NewTempRoot();
        try
        {
            var restored = await new LyricCache(root).TryGetAsync("nope", CancellationToken.None);
            Assert.IsNull(restored);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [TestMethod]
    public async Task ManualAssociation_ResolvesSameNormalizedFingerprint()
    {
        var root = NewTempRoot();
        try
        {
            var store = new ManualAssociationStore(root);
            var saved = new MediaTrack(
                "Player", "1", "Song", "Artist", "Album", TimeSpan.FromSeconds(200));
            var lookup = new MediaTrack(
                "OtherPlayer", "2", " Song ", "Artist feat. Someone", "Album",
                TimeSpan.FromSeconds(200));

            await store.SetAsync(
                TrackFingerprint.Create(saved), "cache-key", CancellationToken.None);

            Assert.AreEqual(
                "cache-key",
                await store.TryResolveAsync(
                    TrackFingerprint.Create(lookup), CancellationToken.None));

            await store.RemoveAsync(TrackFingerprint.Create(saved), CancellationToken.None);
            Assert.IsNull(await store.TryResolveAsync(
                TrackFingerprint.Create(lookup), CancellationToken.None));
        }
        finally
        {
            Cleanup(root);
        }
    }

    [TestMethod]
    public void TrackFingerprint_AlbumWithoutAlbumIsNotIgnorable()
    {
        var track = new MediaTrack("Player", "1", "Song", "Artist", null, null);

        Assert.IsNull(TrackFingerprint.CreateAlbum(track));
    }

    private static string NewTempRoot() =>
        Path.Combine(Path.GetTempPath(), "lyricsx-tests-" + Guid.NewGuid().ToString("N"));

    private static void Cleanup(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }

    private static string Sha256(string value) =>
        Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();
}
