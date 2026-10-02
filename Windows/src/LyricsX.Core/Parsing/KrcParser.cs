using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace LyricsX.Core.Parsing;

/// <summary>
/// Parses Kugou KRC lyrics: lines of <c>[startMs,durationMs]text</c> with
/// inline word tags <c>&lt;relativeStartMs,durationMs,0&gt;fragment</c>.
/// </summary>
public sealed partial class KrcParser : ILyricParser
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
            var (text, words) = ParseBody(body, position);
            parsed.Add(new LyricLine(position, duration, text, true, words));
        }

        return new LyricDocument(parsed, metadata, delay);
    }

    private static (string Text, List<LyricWord>? Words) ParseBody(string body, TimeSpan linePosition)
    {
        var tags = WordTag().Matches(body);
        if (tags.Count == 0)
        {
            return (body.Trim(), null);
        }

        var builder = new StringBuilder();
        var words = new List<LyricWord>(tags.Count);
        foreach (Match tag in tags)
        {
            if (!EnhancedTimestampHelper.TryParseMilliseconds(
                    tag.Groups[1].Value, out var relativeStart) ||
                !EnhancedTimestampHelper.TryParseMilliseconds(
                    tag.Groups[2].Value, out var duration))
            {
                continue;
            }

            var start = linePosition + relativeStart;
            var fragment = tag.Groups[3].Value;
            if (fragment.Length > 0)
            {
                builder.Append(fragment);
            }

            words.Add(new LyricWord(start, duration, fragment));
        }

        return (builder.ToString(), words);
    }

    [GeneratedRegex(@"<(\d+),(\d+),0>([^<]*)")]
    private static partial Regex WordTag();
}
