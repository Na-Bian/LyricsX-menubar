using LyricsX.Core.Transforms;

namespace LyricsX.Core.Tests.Transforms;

[TestClass]
public sealed class LyricLanguageDetectorTests
{
    [TestMethod]
    public void Detect_RecognizesLatin()
    {
        Assert.AreEqual(LyricLanguage.Latin, Detect("hello world"));
    }

    [TestMethod]
    public void Detect_RecognizesJapanese()
    {
        Assert.AreEqual(LyricLanguage.Japanese, Detect("こんにちは世界"));
    }

    [TestMethod]
    public void Detect_RecognizesKorean()
    {
        Assert.AreEqual(LyricLanguage.Korean, Detect("안녕하세요"));
    }

    [TestMethod]
    public void Detect_RecognizesSimplifiedChinese()
    {
        Assert.AreEqual(LyricLanguage.SimplifiedChinese, Detect("汉语同步歌词"));
    }

    [TestMethod]
    public void Detect_RecognizesTraditionalChinese()
    {
        Assert.AreEqual(LyricLanguage.TraditionalChinese, Detect("漢語同步歌詞"));
    }

    [TestMethod]
    public void Detect_RecognizesMixedCjkAndLatin()
    {
        Assert.AreEqual(LyricLanguage.Mixed, Detect("hello 汉语 world"));
    }

    [TestMethod]
    public void Detect_ReturnsUnknownWithoutLetters()
    {
        Assert.AreEqual(LyricLanguage.Unknown, Detect("123 456 ..."));
    }

    private static LyricLanguage Detect(params string[] contents) =>
        new LyricLanguageDetector().Detect(Document(contents));

    private static LyricDocument Document(params string[] contents) =>
        new(
            contents.Select((content, index) => new LyricLine(
                TimeSpan.FromSeconds(index), null, content)).ToArray(),
            new LyricMetadata(null, null, null, null, null,
                new Dictionary<string, string>()),
            TimeSpan.Zero);
}
