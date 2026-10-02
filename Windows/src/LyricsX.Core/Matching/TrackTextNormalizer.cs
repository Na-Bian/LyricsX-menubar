using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace LyricsX.Core.Matching;

public static partial class TrackTextNormalizer
{
    public static string Normalize(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormKC).ToLowerInvariant();
        normalized = EditionNoise().Replace(normalized, " ");
        normalized = FeaturedArtist().Replace(normalized, " ");
        normalized = Separators().Replace(normalized, " ");
        return Whitespace().Replace(normalized, " ").Trim();
    }

    [GeneratedRegex(@"[\(\[（【].*?(live|remaster(ed)?|version|edit|deluxe|豪华版|伴奏|现场).*?[\)\]）】]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EditionNoise();

    [GeneratedRegex(@"\s+(feat\.?|ft\.?|featuring)\s+.+$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FeaturedArtist();

    [GeneratedRegex(@"[‐‑‒–—―·•・/\\|]+")]
    private static partial Regex Separators();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
