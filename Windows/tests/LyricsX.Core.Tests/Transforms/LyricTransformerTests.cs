using LyricsX.Core.Transforms;

namespace LyricsX.Core.Tests.Transforms;

[TestClass]
public sealed class LyricTransformerTests
{
    [TestMethod]
    public void Apply_RemovesEmptyLinesWhenRequested()
    {
        var document = Document("one", "  ", "", "two");
        var options = new LyricTransformOptions(
            ChineseConversion.None, RemoveEmptyLines: true, []);

        var result = new LyricTransformer().Apply(document, options);

        CollectionAssert.AreEqual(
            new[] { "one", "two" },
            result.Lines.Select(line => line.Content).ToArray());
    }

    [TestMethod]
    public void Apply_KeepsEmptyLinesWhenNotRequested()
    {
        var document = Document("one", "");
        var options = new LyricTransformOptions(
            ChineseConversion.None, RemoveEmptyLines: false, []);

        var result = new LyricTransformer().Apply(document, options);

        Assert.AreEqual(2, result.Lines.Count);
    }

    [TestMethod]
    public void Apply_FiltersBlockedPhrasesCaseInsensitively()
    {
        var document = Document("作词：某人", "纯音乐，请欣赏", "正常歌词行");
        var options = new LyricTransformOptions(
            ChineseConversion.None, RemoveEmptyLines: false, ["纯音乐"]);

        var result = new LyricTransformer().Apply(document, options);

        Assert.AreEqual(2, result.Lines.Count);
        Assert.AreEqual("作词：某人", result.Lines[0].Content);
        Assert.AreEqual("正常歌词行", result.Lines[1].Content);
    }

    [TestMethod]
    public void Apply_ConvertsSimplifiedToTraditional()
    {
        var document = Document("汉字歌词");
        var options = new LyricTransformOptions(
            ChineseConversion.Traditional, RemoveEmptyLines: false, []);

        var result = new LyricTransformer().Apply(document, options);

        Assert.AreEqual("漢字歌詞", result.Lines.Single().Content);
    }

    [TestMethod]
    public void Apply_LeavesContentUntouchedWithoutConversion()
    {
        var document = Document("汉字歌词");
        var options = new LyricTransformOptions(
            ChineseConversion.Simplified, RemoveEmptyLines: false, []);

        var result = new LyricTransformer().Apply(document, options);

        Assert.AreEqual("汉字歌词", result.Lines.Single().Content);
    }

    [TestMethod]
    public void Apply_PreservesPositionsAndMetadata()
    {
        var metadata = new LyricMetadata("t", "a", null, null, null,
            new Dictionary<string, string>());
        var document = new LyricDocument(
            [new LyricLine(TimeSpan.FromSeconds(1), null, "歌词", true,
                [new LyricWord(TimeSpan.FromSeconds(1), null, "歌")])],
            metadata,
            TimeSpan.FromMilliseconds(120));
        var options = new LyricTransformOptions(
            ChineseConversion.None, RemoveEmptyLines: false, []);

        var result = new LyricTransformer().Apply(document, options);

        Assert.AreEqual(TimeSpan.FromSeconds(1), result.Lines[0].Position);
        Assert.AreEqual(metadata, result.Metadata);
        Assert.AreEqual(TimeSpan.FromMilliseconds(120), result.Delay);
        Assert.IsNotNull(result.Lines[0].Words);
        Assert.AreEqual(1, result.Lines[0].Words!.Count);
    }

    private static LyricDocument Document(params string[] contents) =>
        new(
            contents.Select((content, index) => new LyricLine(
                TimeSpan.FromSeconds(index), null, content)).ToArray(),
            new LyricMetadata(null, null, null, null, null,
                new Dictionary<string, string>()),
            TimeSpan.Zero);
}
