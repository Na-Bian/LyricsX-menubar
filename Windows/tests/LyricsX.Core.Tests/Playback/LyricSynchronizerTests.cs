using LyricsX.Core.Playback;

namespace LyricsX.Core.Tests.Playback;

[TestClass]
public sealed class LyricSynchronizerTests
{
    [TestMethod]
    public void GetFrame_AppliesDocumentAndUserOffsets()
    {
        var document = new LyricDocument(
            [
                new LyricLine(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), "one"),
                new LyricLine(TimeSpan.FromSeconds(3), null, "two")
            ],
            new LyricMetadata(null, null, null, null, null,
                new Dictionary<string, string>()),
            TimeSpan.FromMilliseconds(250));

        var frame = new LyricSynchronizer().GetFrame(
            document,
            TimeSpan.FromSeconds(2.75),
            TimeSpan.FromMilliseconds(-500));

        Assert.AreEqual("two", frame.Current?.Content);
        Assert.AreEqual(1, frame.CurrentIndex);
    }

    [TestMethod]
    public void GetFrame_ReturnsNoLineBeforeFirstTimestamp()
    {
        var document = Document(
            new LyricLine(TimeSpan.FromSeconds(1), null, "one"));

        var frame = new LyricSynchronizer().GetFrame(
            document, TimeSpan.FromSeconds(0.5), TimeSpan.Zero);

        Assert.AreEqual(-1, frame.CurrentIndex);
        Assert.IsNull(frame.Current);
        Assert.IsNull(frame.Next);
    }

    [TestMethod]
    public void GetFrame_ReportsNextLineAndLineProgress()
    {
        var document = Document(
            new LyricLine(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), "one"),
            new LyricLine(TimeSpan.FromSeconds(3), null, "two"));

        var frame = new LyricSynchronizer().GetFrame(
            document, TimeSpan.FromSeconds(2), TimeSpan.Zero);

        Assert.AreEqual("one", frame.Current?.Content);
        Assert.AreEqual("two", frame.Next?.Content);
        Assert.AreEqual(0.5, frame.Progress, 0.001);
    }

    [TestMethod]
    public void GetFrame_PausesStableAtSamePosition()
    {
        var document = Document(
            new LyricLine(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), "one"),
            new LyricLine(TimeSpan.FromSeconds(3), null, "two"));

        var synchronizer = new LyricSynchronizer();
        var first = synchronizer.GetFrame(document, TimeSpan.FromSeconds(2), TimeSpan.Zero);
        var second = synchronizer.GetFrame(document, TimeSpan.FromSeconds(2), TimeSpan.Zero);

        Assert.AreEqual(first.CurrentIndex, second.CurrentIndex);
        Assert.AreEqual(first.Progress, second.Progress, 0.0001);
    }

    [TestMethod]
    public void GetFrame_AfterLastLineClampsProgress()
    {
        var document = Document(
            new LyricLine(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), "one"));

        var frame = new LyricSynchronizer().GetFrame(
            document, TimeSpan.FromSeconds(30), TimeSpan.Zero);

        Assert.AreEqual("one", frame.Current?.Content);
        Assert.AreEqual(1.0, frame.Progress, 0.0001);
    }

    [TestMethod]
    public void GetFrame_UsesNextLinePositionWhenDurationMissing()
    {
        var document = Document(
            new LyricLine(TimeSpan.FromSeconds(1), null, "one"),
            new LyricLine(TimeSpan.FromSeconds(3), null, "two"));

        var frame = new LyricSynchronizer().GetFrame(
            document, TimeSpan.FromSeconds(2), TimeSpan.Zero);

        Assert.AreEqual("one", frame.Current?.Content);
        Assert.AreEqual(0.5, frame.Progress, 0.001);
    }

    [TestMethod]
    public void GetFrame_TracksEnhancedWordSelectionAndProgress()
    {
        var words = new List<LyricWord>
        {
            new(TimeSpan.FromSeconds(1), TimeSpan.FromMilliseconds(400), "Win"),
            new(TimeSpan.FromSeconds(1.4), TimeSpan.FromMilliseconds(600), "dows")
        };
        var document = Document(
            new LyricLine(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), "Windows", true, words));

        var synchronizer = new LyricSynchronizer();

        var first = synchronizer.GetFrame(document, TimeSpan.FromSeconds(1.2), TimeSpan.Zero);
        Assert.AreEqual(0, first.CurrentWordIndex);
        Assert.AreEqual(0.5, first.WordProgress, 0.01);

        var second = synchronizer.GetFrame(document, TimeSpan.FromSeconds(1.7), TimeSpan.Zero);
        Assert.AreEqual(1, second.CurrentWordIndex);
        Assert.AreEqual(0.5, second.WordProgress, 0.01);
    }

    [TestMethod]
    public void GetFrame_UsesDocumentDelayAndUserOffsetIndependently()
    {
        var document = new LyricDocument(
            [new LyricLine(TimeSpan.FromSeconds(10), null, "line")],
            new LyricMetadata(null, null, null, null, null,
                new Dictionary<string, string>()),
            TimeSpan.FromSeconds(2));

        var synchronizer = new LyricSynchronizer();

        var before = synchronizer.GetFrame(document, TimeSpan.FromSeconds(11.9), TimeSpan.Zero);
        var atLine = synchronizer.GetFrame(document, TimeSpan.FromSeconds(12), TimeSpan.Zero);
        var withUserOffset = synchronizer.GetFrame(
            document, TimeSpan.FromSeconds(13), TimeSpan.FromSeconds(1));

        Assert.AreEqual(-1, before.CurrentIndex);
        Assert.AreEqual(0, atLine.CurrentIndex);
        Assert.AreEqual(0, withUserOffset.CurrentIndex);
    }

    private static LyricDocument Document(params LyricLine[] lines) =>
        new(lines,
            new LyricMetadata(null, null, null, null, null,
                new Dictionary<string, string>()),
            TimeSpan.Zero);
}
