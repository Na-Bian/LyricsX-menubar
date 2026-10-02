using OpenccNetLib;

namespace LyricsX.Core.Transforms;

public enum ChineseConversion { None, Simplified, Traditional }

public sealed record LyricTransformOptions(
    ChineseConversion ChineseConversion,
    bool RemoveEmptyLines,
    IReadOnlyList<string> BlockedPhrases);

public sealed class LyricTransformer
{
    public LyricDocument Apply(LyricDocument document, LyricTransformOptions options)
    {
        Opencc? converter = options.ChineseConversion switch
        {
            ChineseConversion.Simplified => new Opencc(OpenccConfig.T2S),
            ChineseConversion.Traditional => new Opencc(OpenccConfig.S2T),
            _ => null
        };

        var lines = document.Lines
            .Where(line => !options.RemoveEmptyLines ||
                !string.IsNullOrWhiteSpace(line.Content))
            .Where(line => !options.BlockedPhrases.Any(phrase =>
                line.Content.Contains(phrase, StringComparison.OrdinalIgnoreCase)))
            .Select(line => line with
            {
                Content = converter?.Convert(line.Content) ?? line.Content
            })
            .ToArray();

        return document with { Lines = lines };
    }
}
