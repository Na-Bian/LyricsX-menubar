namespace LyricsX.Core.Playback;

public sealed record LyricFrame(
    int CurrentIndex,
    LyricLine? Current,
    LyricLine? Next,
    double Progress,
    int CurrentWordIndex,
    double WordProgress);

public sealed class LyricSynchronizer
{
    public LyricFrame GetFrame(
        LyricDocument document,
        TimeSpan playbackPosition,
        TimeSpan userOffset)
    {
        // The plan's own test fixes the sign convention:
        // document delay and a positive user offset both make lines appear later.
        var effective = playbackPosition - document.Delay - userOffset;
        var lines = document.Lines;
        if (lines.Count == 0)
        {
            return new LyricFrame(-1, null, null, 0, -1, 0);
        }

        var index = SearchLatest(lines, effective);
        if (index < 0)
        {
            return new LyricFrame(-1, null, null, 0, -1, 0);
        }

        var current = lines[index];
        var next = index + 1 < lines.Count ? lines[index + 1] : null;
        var duration = current.Duration ??
            (next is not null ? next.Position - current.Position : null);
        var progress = duration is null || duration.Value <= TimeSpan.Zero
            ? 1.0
            : Math.Clamp(
                (effective - current.Position).TotalSeconds /
                duration.Value.TotalSeconds, 0, 1);

        var (wordIndex, wordProgress) = current.Words is { Count: > 0 } words
            ? SelectWord(words, effective)
            : (-1, 0.0);

        return new LyricFrame(index, current, next, progress, wordIndex, wordProgress);
    }

    private static (int Index, double Progress) SelectWord(
        IReadOnlyList<LyricWord> words,
        TimeSpan effective)
    {
        var index = SearchLatestWord(words, effective);
        if (index < 0)
        {
            return (-1, 0.0);
        }

        var word = words[index];
        var progress = word.Duration is null || word.Duration.Value <= TimeSpan.Zero
            ? 1.0
            : Math.Clamp(
                (effective - word.Position).TotalSeconds /
                word.Duration.Value.TotalSeconds, 0, 1);
        return (index, progress);
    }

    private static int SearchLatest(IReadOnlyList<LyricLine> lines, TimeSpan time)
    {
        var low = 0;
        var high = lines.Count - 1;
        var result = -1;
        while (low <= high)
        {
            var middle = low + (high - low) / 2;
            if (lines[middle].Position <= time)
            {
                result = middle;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return result;
    }

    private static int SearchLatestWord(IReadOnlyList<LyricWord> words, TimeSpan time)
    {
        var low = 0;
        var high = words.Count - 1;
        var result = -1;
        while (low <= high)
        {
            var middle = low + (high - low) / 2;
            if (words[middle].Position <= time)
            {
                result = middle;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return result;
    }
}
