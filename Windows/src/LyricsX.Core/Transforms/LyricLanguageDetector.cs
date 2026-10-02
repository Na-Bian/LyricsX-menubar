using OpenccNetLib;

namespace LyricsX.Core.Transforms;

public enum LyricLanguage
{
    Unknown,
    Latin,
    SimplifiedChinese,
    TraditionalChinese,
    Japanese,
    Korean,
    Mixed
}

public sealed class LyricLanguageDetector
{
    public LyricLanguage Detect(LyricDocument document)
    {
        var text = string.Concat(document.Lines.Select(line => line.Content));
        var hasJapanese = text.Any(character => character is >= '\u3040' and <= '\u30ff');
        var hasKorean = text.Any(character => character is >= '\uac00' and <= '\ud7af');
        var hasCjk = text.Any(character => character is >= '\u3400' and <= '\u9fff');
        var hasLatin = text.Any(char.IsAsciiLetter);

        if (hasJapanese) return LyricLanguage.Japanese;
        if (hasKorean) return LyricLanguage.Korean;
        if (hasCjk && hasLatin) return LyricLanguage.Mixed;
        if (hasCjk)
        {
            return Opencc.ZhoCheck(text) switch
            {
                1 => LyricLanguage.TraditionalChinese,
                2 => LyricLanguage.SimplifiedChinese,
                _ => LyricLanguage.Mixed
            };
        }

        return hasLatin ? LyricLanguage.Latin : LyricLanguage.Unknown;
    }
}
