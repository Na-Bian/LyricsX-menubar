using System.Globalization;
using System.Text.RegularExpressions;

namespace LyricsX.Core.Parsing;

public sealed partial class LrcParser : ILyricParser
{
    public LyricDocument Parse(string content)
    {
        var text = (content ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        string? title = null;
        string? artist = null;
        string? album = null;
        string? author = null;
        TimeSpan? length = null;
        var delay = TimeSpan.Zero;
        var entries = new List<(TimeSpan Position, string Content)>();

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var metadata = MetadataLine().Match(line);
            if (metadata.Success)
            {
                ApplyMetadata(
                    metadata.Groups[1].Value.ToLowerInvariant(),
                    metadata.Groups[2].Value.Trim(),
                    ref title, ref artist, ref album, ref author, ref length, ref delay);
                continue;
            }

            var rest = line;
            var positions = new List<TimeSpan>();
            while (TimestampTag().Match(rest) is { Success: true } match)
            {
                if (!TryParseTimestamp(match.Groups[1].Value, out var stamp))
                {
                    break;
                }

                positions.Add(stamp);
                rest = rest[match.Length..];
            }

            if (positions.Count == 0)
            {
                continue;
            }

            foreach (var position in positions)
            {
                entries.Add((position, rest));
            }
        }

        if (entries.Count == 0)
        {
            throw new FormatException("The lyric document contains no timed lines.");
        }

        var ordered = entries
            .Select((entry, index) => (entry.Position, entry.Content, Index: index))
            .OrderBy(item => item.Position)
            .ThenBy(item => item.Index)
            .ToList();

        var lines = new List<LyricLine>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            var (position, lineContent, _) = ordered[i];
            var duration = NextDistinctGap(ordered, i, position);
            lines.Add(CreateLine(position, duration, lineContent));
        }

        var metadataRecord = new LyricMetadata(title, artist, album, author, length,
            new Dictionary<string, string>());
        return new LyricDocument(lines, metadataRecord, delay);
    }

    private static void ApplyMetadata(
        string key,
        string value,
        ref string? title,
        ref string? artist,
        ref string? album,
        ref string? author,
        ref TimeSpan? length,
        ref TimeSpan delay)
    {
        switch (key)
        {
            case "ti" when value.Length > 0:
                title = value;
                break;
            case "ar" when value.Length > 0:
                artist = value;
                break;
            case "al" when value.Length > 0:
                album = value;
                break;
            case "by" when value.Length > 0:
                author = value;
                break;
            case "length" when TryParseTimestamp(value, out var parsed):
                length = parsed;
                break;
            case "offset" when int.TryParse(value, NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out var milliseconds):
                delay = TimeSpan.FromMilliseconds(milliseconds);
                break;
        }
    }

    private static TimeSpan? NextDistinctGap(
        List<(TimeSpan Position, string Content, int Index)> ordered,
        int index,
        TimeSpan position)
    {
        for (var j = index + 1; j < ordered.Count; j++)
        {
            if (ordered[j].Position > position)
            {
                return ordered[j].Position - position;
            }
        }

        return null;
    }

    private static LyricLine CreateLine(TimeSpan position, TimeSpan? duration, string content)
    {
        var words = ParseWords(content, position, duration);
        var visible = words is null
            ? content
            : string.Concat(words.Select(word => word.Content));
        return new LyricLine(position, duration, visible, true, words);
    }

    private static IReadOnlyList<LyricWord>? ParseWords(
        string content,
        TimeSpan linePosition,
        TimeSpan? lineDuration)
    {
        var tags = WordTag().Matches(content);
        if (tags.Count == 0)
        {
            return null;
        }

        var words = new List<LyricWord>(tags.Count);
        for (var i = 0; i < tags.Count; i++)
        {
            var tag = tags[i];
            if (!TryParseTimestamp(tag.Groups[1].Value, out var wordPosition))
            {
                continue;
            }

            var textStart = tag.Index + tag.Length;
            var textEnd = i + 1 < tags.Count ? tags[i + 1].Index : content.Length;
            words.Add(new LyricWord(wordPosition, null, content[textStart..textEnd]));
        }

        for (var i = 0; i < words.Count; i++)
        {
            TimeSpan? wordDuration = i + 1 < words.Count
                ? words[i + 1].Position - words[i].Position
                : lineDuration is null
                    ? null
                    : linePosition + lineDuration.Value - words[i].Position;
            words[i] = words[i] with { Duration = wordDuration };
        }

        return words;
    }

    private static bool TryParseTimestamp(string value, out TimeSpan result)
    {
        result = default;
        var parts = value.Split(':', 2);
        if (parts.Length != 2 ||
            !int.TryParse(parts[0], out var minutes) ||
            !decimal.TryParse(parts[1], NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var seconds))
        {
            return false;
        }

        result = TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds((double)seconds);
        return result >= TimeSpan.Zero;
    }

    [GeneratedRegex(@"^\[(ti|ar|al|by|length|offset):(.*)\]$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MetadataLine();

    [GeneratedRegex(@"^\[(\d{1,4}:\d{1,2}(?:\.\d{1,3})?)\]")]
    private static partial Regex TimestampTag();

    [GeneratedRegex(@"<(\d{1,4}:\d{1,2}(?:\.\d{1,3})?)>")]
    private static partial Regex WordTag();
}
