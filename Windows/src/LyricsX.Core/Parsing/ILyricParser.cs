namespace LyricsX.Core.Parsing;

public interface ILyricParser
{
    LyricDocument Parse(string content);
}
