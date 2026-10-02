using System.Text;
using LyricsX.Core.Parsing;
using LyricsX.Windows.App.LyricFiles;

namespace LyricsX.Windows.App.Tests.LyricFiles;

[TestClass]
public sealed class LocalLyricFileServiceTests
{
    [TestMethod]
    public void Import_Utf8Lrc_ParsesDocument()
    {
        var bytes = Encoding.UTF8.GetBytes("[00:01.00]hello line\n[00:03.00]next");
        var result = new LocalLyricFileService().Import("song.lrc", bytes);

        Assert.AreEqual("local-file", result.ProviderId);
        Assert.AreEqual(2, result.Document.Lines.Count);
        Assert.AreEqual("hello line", result.Document.Lines[0].Content);
    }

    [TestMethod]
    public void Import_Utf16Le_ParsesCorrectly()
    {
        var payload = Encoding.Unicode.GetBytes("[00:01.00]unicode line");
        var bytes = new byte[] { 0xFF, 0xFE }.Concat(payload).ToArray();
        var result = new LocalLyricFileService().Import("song.lrc", bytes);

        Assert.AreEqual("unicode line", result.Document.Lines[0].Content);
    }

    [TestMethod]
    public void Import_LrcxWordTiming_Preserved()
    {
        var bytes = Encoding.UTF8.GetBytes("[00:01.00]<00:01.00>he<00:01.40>llo");
        var result = new LocalLyricFileService().Import("song.lrcx", bytes);

        var line = result.Document.Lines[0];
        Assert.IsNotNull(line.Words);
        Assert.AreEqual(2, line.Words.Count);
    }

    [TestMethod]
    public void Import_OversizedFile_Throws()
    {
        var oversized = new byte[LocalLyricFileService.MaxFileBytes + 1];

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            new LocalLyricFileService().Import("big.lrc", oversized));
    }

    [TestMethod]
    public void Import_InvalidExtension_Throws()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            new LocalLyricFileService().Import("song.txt", Encoding.UTF8.GetBytes("[00:01.00]x")));
    }

    [TestMethod]
    public void Import_MalformedContent_ThrowsFormatException()
    {
        Assert.ThrowsExactly<FormatException>(() =>
            new LocalLyricFileService().Import("song.lrc", Encoding.UTF8.GetBytes("no timestamps")));
    }

    [TestMethod]
    public void Export_EnhancedWords_EmitsLrcxFormat()
    {
        var document = new LyricsX.Core.LyricDocument(
            [new LyricsX.Core.LyricLine(
                TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), "hello", true,
                [new LyricsX.Core.LyricWord(TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(400), "he"),
                 new LyricsX.Core.LyricWord(TimeSpan.FromSeconds(1.4), null, "llo")])],
            new LyricsX.Core.LyricMetadata(null, null, null, null, null,
                new Dictionary<string, string>()),
            TimeSpan.Zero);

        var bytes = new LyricExportService().Export(document, useLrcx: true);
        var text = Encoding.UTF8.GetString(bytes);

        StringAssert.Contains(text, "<00:01.00>he");
        // UTF-8 without BOM
        Assert.IsFalse(bytes.Take(3).SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }));
    }

    [TestMethod]
    public void Export_PlainLines_EmitsLrcFormat()
    {
        var document = new LyricsX.Core.LyricDocument(
            [new LyricsX.Core.LyricLine(TimeSpan.FromSeconds(1), null, "plain line")],
            new LyricsX.Core.LyricMetadata(null, null, null, null, null,
                new Dictionary<string, string>()),
            TimeSpan.Zero);

        var bytes = new LyricExportService().Export(document, useLrcx: false);
        var text = Encoding.UTF8.GetString(bytes);

        StringAssert.Contains(text, "[00:01.00]plain line");
        Assert.IsFalse(text.Contains('<'));
    }
}
