using System.Text;
using System.Text.RegularExpressions;

namespace LyricsX.Providers;

/// <summary>
/// Extracts decodable lyric text from a raw lyric_download response:
/// strips the HTML comment wrapper, pulls <c>content</c>/<c>contentts</c>,
/// decrypts strict-hex QRC payloads, unwraps the nested XML
/// <c>LyricContent</c> attribute with entity unescaping, and normalizes
/// the bracket-timestamp line breaks.
/// </summary>
public static partial class QQMusicLyricDecoder
{
    public static string? ExtractContent(string responseText)
    {
        var text = responseText.Replace("<!--", string.Empty).Replace("-->", string.Empty);
        return ExtractTag(text, "content");
    }

    private static string? ExtractTag(string text, string tagName)
    {
        var open = "<" + tagName + ">";
        var close = "</" + tagName + ">";
        var start = text.IndexOf(open, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return null;
        }

        start += open.Length;
        var end = text.IndexOf(close, start, StringComparison.OrdinalIgnoreCase);
        if (end < 0 || end <= start)
        {
            return null;
        }

        var raw = text[start..end].Trim();
        if (raw.Length == 0)
        {
            return null;
        }

        if (IsStrictHex(raw))
        {
            return QQMusicQrcCipher.DecryptHex(raw);
        }

        if (raw.Contains("<?xml", StringComparison.OrdinalIgnoreCase))
        {
            var attribute = ExtractLyricContentAttribute(raw);
            return attribute is null ? null : NormalizeLineBreaks(attribute);
        }

        return raw;
    }

    private static bool IsStrictHex(string value)
    {
        if (value.Length < 16)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!char.IsAsciiHexDigit(character) && !char.IsWhiteSpace(character))
            {
                return false;
            }
        }

        return true;
    }

    private static string? ExtractLyricContentAttribute(string xml)
    {
        const string marker = "LyricContent=\"";
        var markerIndex = xml.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return null;
        }

        var cursor = markerIndex + marker.Length;
        var builder = new StringBuilder();
        while (cursor < xml.Length)
        {
            var character = xml[cursor];
            if (character == '\\')
            {
                cursor++;
                if (cursor < xml.Length)
                {
                    builder.Append(xml[cursor]);
                    cursor++;
                }

                continue;
            }

            if (character == '"')
            {
                return UnescapeEntities(builder.ToString());
            }

            builder.Append(character);
            cursor++;
        }

        return UnescapeEntities(builder.ToString());
    }

    private static string UnescapeEntities(string value) => value
        .Replace("&#10;", "\n")
        .Replace("&#13;", "\r")
        .Replace("&#32;", " ")
        .Replace("&#39;", "'")
        .Replace("&#40;", "(")
        .Replace("&#41;", ")")
        .Replace("&#45;", "-")
        .Replace("&#46;", ".")
        .Replace("&#58;", ":")
        .Replace("&#64;", "@")
        .Replace("&#95;", "_")
        .Replace("&#124;", "|");

    private static string NormalizeLineBreaks(string content) =>
        TimestampJoin().Replace(
            BracketJoin().Replace(content.Trim(), "]\n[").Trim(),
            "\n");

    [GeneratedRegex(@"\]\s+\[")]
    private static partial Regex BracketJoin();

    [GeneratedRegex(@"[ \t]+(?=\[\d+,\d+\])")]
    private static partial Regex TimestampJoin();
}
