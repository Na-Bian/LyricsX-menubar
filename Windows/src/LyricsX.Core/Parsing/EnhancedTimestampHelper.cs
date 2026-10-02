using System.Globalization;
using System.Text.RegularExpressions;

namespace LyricsX.Core.Parsing;

/// <summary>
/// Shared line/metadata grammar for the <c>[startMs,durationMs]</c> lyric
/// family (QRC, KRC): metadata id-tags outside timed lines, timed lines in
/// input order, and the LRC-style offset tag mapped to document delay.
/// </summary>
internal static partial class EnhancedTimestampHelper
{
    public static List<(TimeSpan Position, TimeSpan Duration, string Body)> ParseLines(
        string content,
        out LyricMetadata metadata,
        out TimeSpan delay)
    {
        var text = (content ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        string? title = null;
        string? artist = null;
        string? album = null;
        string? author = null;
        delay = TimeSpan.Zero;
        var lines = new List<(TimeSpan, TimeSpan, string)>();

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var stamp = LineTag().Match(line);
            if (stamp.Success)
            {
                var position = TimeSpan.FromMilliseconds(
                    long.Parse(stamp.Groups[1].Value, CultureInfo.InvariantCulture));
                var duration = TimeSpan.FromMilliseconds(
                    long.Parse(stamp.Groups[2].Value, CultureInfo.InvariantCulture));
                lines.Add((position, duration, stamp.Groups[3].Value));
                continue;
            }

            var id3 = Id3Tag().Match(line);
            if (!id3.Success)
            {
                continue;
            }

            var key = id3.Groups[1].Value.ToLowerInvariant();
            var value = id3.Groups[2].Value.Trim();
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
                case "offset" when int.TryParse(value, NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture, out var milliseconds):
                    delay = TimeSpan.FromMilliseconds(milliseconds);
                    break;
            }
        }

        metadata = new LyricMetadata(title, artist, album, author, null,
            new Dictionary<string, string>());
        return lines;
    }

    [GeneratedRegex(@"^\[(\d+),(\d+)\](.*)")]
    private static partial Regex LineTag();

    [GeneratedRegex(@"^\[([A-Za-z_][A-Za-z0-9_-]*):(.*)\]$")]
    private static partial Regex Id3Tag();
}
