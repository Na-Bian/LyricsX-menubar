using LyricsX.Core.Parsing;

namespace LyricsX.Core.Tests.Parsing;

[TestClass]
public sealed class QrcParserTests
{
    private const string Sample =
        "[ti:Qrc Song]\n[ar:QQ]\n[offset:500]\n" +
        "[1000,2000]Hello(1200,300) wo(1500,400)rld\n" +
        "[3200,800]Bye";

    [TestMethod]
    public void Parse_ReadsLinesWordsOffsetAndMetadata()
    {
        var document = new QrcParser().Parse(Sample);

        Assert.AreEqual("Qrc Song", document.Metadata.Title);
        Assert.AreEqual("QQ", document.Metadata.Artist);
        Assert.AreEqual(TimeSpan.FromMilliseconds(500), document.Delay);
        Assert.AreEqual(2, document.Lines.Count);

        var first = document.Lines[0];
        Assert.AreEqual(TimeSpan.FromSeconds(1), first.Position);
        Assert.AreEqual(TimeSpan.FromSeconds(2), first.Duration);
        Assert.AreEqual("Hello world", first.Content);

        Assert.IsNotNull(first.Words);
        Assert.AreEqual(2, first.Words!.Count);
        Assert.AreEqual(TimeSpan.FromMilliseconds(1200), first.Words[0].Position);
        Assert.AreEqual(TimeSpan.FromMilliseconds(300), first.Words[0].Duration);
        Assert.AreEqual("Hello", first.Words[0].Content);
        Assert.AreEqual(TimeSpan.FromMilliseconds(1500), first.Words[1].Position);
        Assert.AreEqual(TimeSpan.FromMilliseconds(400), first.Words[1].Duration);
        Assert.AreEqual(" wo", first.Words[1].Content);

        Assert.AreEqual("Bye", document.Lines[1].Content);
        Assert.IsNull(document.Lines[1].Words);
    }

    [TestMethod]
    public void Parse_ThrowsWhenNoTimedLineExists()
    {
        Assert.ThrowsExactly<FormatException>(() =>
            new QrcParser().Parse("[ti:only metadata]"));
    }
}

[TestClass]
public sealed class KrcParserTests
{
    private const string Sample =
        "[offset:250]\n" +
        "[1000,2000]<0,500,0>你<500,1500,0>好\n" +
        "[3200,800]世";

    [TestMethod]
    public void Parse_ReadsLinesAndRelativeAngleWordTags()
    {
        var document = new KrcParser().Parse(Sample);

        Assert.AreEqual(TimeSpan.FromMilliseconds(250), document.Delay);
        Assert.AreEqual(2, document.Lines.Count);

        var first = document.Lines[0];
        Assert.AreEqual(TimeSpan.FromSeconds(1), first.Position);
        Assert.AreEqual(TimeSpan.FromSeconds(2), first.Duration);
        Assert.AreEqual("你好", first.Content);

        Assert.IsNotNull(first.Words);
        Assert.AreEqual(2, first.Words!.Count);
        Assert.AreEqual(TimeSpan.FromSeconds(1), first.Words[0].Position);
        Assert.AreEqual(TimeSpan.FromMilliseconds(500), first.Words[0].Duration);
        Assert.AreEqual(TimeSpan.FromSeconds(1.5), first.Words[1].Position);
        Assert.AreEqual(TimeSpan.FromMilliseconds(1500), first.Words[1].Duration);

        Assert.AreEqual("世", document.Lines[1].Content);
    }

    [TestMethod]
    public void Parse_ThrowsWhenNoTimedLineExists()
    {
        Assert.ThrowsExactly<FormatException>(() =>
            new KrcParser().Parse("[id:kugou]"));
    }
}

[TestClass]
public sealed class EnhancedTimestampMalformedInputTests
{
    [TestMethod]
    public void QrcParser_SkipsLinesWithAbsurdTimestampsButKeepsValidOnes()
    {
        var document = new QrcParser().Parse(
            "[99999999999999999999999,1]bad\n[1000,2000]good(1200,300)\n");

        Assert.AreEqual(1, document.Lines.Count);
        Assert.AreEqual("good", document.Lines[0].Content);
    }

    [TestMethod]
    public void KrcParser_SkipsLinesWithAbsurdTimestampsButKeepsValidOnes()
    {
        var document = new KrcParser().Parse(
            "[99999999999999999999999,1]bad\n[1000,2000]<0,500,0>好\n");

        Assert.AreEqual(1, document.Lines.Count);
        Assert.AreEqual("好", document.Lines[0].Content);
    }

    [TestMethod]
    public void QrcParser_SkipsLinesBeyondOneDay()
    {
        var document = new QrcParser().Parse(
            "[86400000001,1]too-far\n[1000,2000]near\n");

        Assert.AreEqual(1, document.Lines.Count);
        Assert.AreEqual("near", document.Lines[0].Content);
    }
}
