using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace LyricsX.Core.Parsing;

/// <summary>
/// Parses QQMusic QRC lyrics: lines of <c>[startMs,durationMs]text</c> with
/// inline word tags <c>fragment(absoluteStartMs,durationMs)</c>.
/// </summary>
public sealed partial class QrcParser : ILyricParser
{
    public LyricDocument Parse(string content)
    {
        var lines = EnhancedTimestampHelper.ParseLines(content, out var metadata, out var delay);
        if (lines.Count == 0)
        {
            throw new FormatException("The lyric document contains no timed lines.");
        }

        var parsed = new List<LyricLine>(lines.Count);
        foreach (var (position, duration, body) in lines)
        {
            var (text, words) = ParseBody(body);
            parsed.Add(new LyricLine(position, duration, text, true, words));
        }

        return new LyricDocument(parsed, metadata, delay);
    }

    private static (string Text, List<LyricWord>? Words) ParseBody(string body)
    {
        var tags = WordTag().Matches(body);
        if (tags.Count == 0)
        {
            return (body.Trim(), null);
        }

        var builder = new StringBuilder();
        var words = new List<LyricWord>(tags.Count);
        var tailStart = 0;
        foreach (Match tag in tags)
        {
            var fragment = tag.Groups[1].Value;
            if (fragment.Length > 0)
            {
                builder.Append(fragment);
            }

            var start = TimeSpan.FromMilliseconds(
                long.Parse(tag.Groups[2].Value, CultureInfo.InvariantCulture));
            var duration = TimeSpan.FromMilliseconds(
                long.Parse(tag.Groups[3].Value, CultureInfo.InvariantCulture));
            words.Add(new LyricWord(start, duration, fragment));
            tailStart = tag.Index + tag.Length;
        }

        var tail = body[tailStart..];
        if (tail.Length > 0)
        {
            builder.Append(tail);
        }

        return (builder.ToString(), words);
    }

    [GeneratedRegex(@"^\[(\d+),(\d+)\](.*)")]
    private static partial Regex LineTagRegex();

    [GeneratedRegex(@"(.*?)\((\d+),(\d+)\)")]
    private static partial Regex WordTag();
}
