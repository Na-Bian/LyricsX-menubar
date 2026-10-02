using LyricsX.Core.Parsing;

namespace LyricsX.Core.Tests.Parsing;

[TestClass]
public sealed class LrcParserTests
{
    private const string Sample =
        "[ti:Window Song]\n[ar:Codex]\n[offset:250]\n" +
        "[00:01.00][00:03.00]Hello\n[00:05.20]<00:05.20>Win<00:05.60>dows";

    [TestMethod]
    public void Parse_ReadsMetadataOffsetAndRepeatedLines()
    {
        var document = new LrcParser().Parse(Sample);

        Assert.AreEqual("Window Song", document.Metadata.Title);
        Assert.AreEqual(TimeSpan.FromMilliseconds(250), document.Delay);
        Assert.AreEqual(3, document.Lines.Count);
        Assert.AreEqual(TimeSpan.FromSeconds(3), document.Lines[1].Position);
        Assert.AreEqual("Windows", document.Lines[2].Content);
        Assert.AreEqual(2, document.Lines[2].Words?.Count);
        Assert.AreEqual(TimeSpan.FromMilliseconds(400),
            document.Lines[2].Words?[0].Duration);
    }

    [TestMethod]
    public void Serialize_RoundTripsLinePositions()
    {
        var parser = new LrcParser();
        var first = parser.Parse(Sample);
        var second = parser.Parse(new LrcSerializer().Serialize(first));

        CollectionAssert.AreEqual(
            first.Lines.Select(line => line.Position).ToArray(),
            second.Lines.Select(line => line.Position).ToArray());
        CollectionAssert.AreEqual(
            first.Lines[2].Words?.Select(word => word.Position).ToArray(),
            second.Lines[2].Words?.Select(word => word.Position).ToArray());
    }
}
