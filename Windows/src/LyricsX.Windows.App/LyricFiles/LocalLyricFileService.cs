using System.Text;
using LyricsX.Core;
using LyricsX.Core.Parsing;

namespace LyricsX.Windows.App.LyricFiles;

/// <summary>
/// Imports local LRC/LRCX files picked through WinUI pickers: encodings
/// UTF-8/BOM/UTF-16, 4 MiB cap, provider id "local-file". The original file
/// path is never retained.
/// </summary>
public sealed class LocalLyricFileService
{
    public const int MaxFileBytes = 4 * 1024 * 1024;

    private static readonly string[] AllowedExtensions = [".lrc", ".lrcx"];
    private static readonly LrcParser Parser = new();

    public LyricCandidate Import(string fileName, byte[] content)
    {
        var extension = Path.GetExtension(fileName);
        if (!AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only .lrc and .lrcx files are supported.");
        }

        if (content.Length > MaxFileBytes)
        {
            throw new InvalidOperationException("Lyric file exceeds the 4 MiB limit.");
        }

        var text = Decode(content);
        var document = Parser.Parse(text);
        return new LyricCandidate(
            "local-file",
            document,
            Path.GetFileNameWithoutExtension(fileName),
            Artist: string.Empty,
            Album: null,
            Duration: null,
            ProviderRank: 0);
    }

    private static string Decode(byte[] bytes)
    {
        if (bytes.Length >= 3 &&
            bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }

        if (bytes.Length >= 2 &&
            bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        }

        if (bytes.Length >= 2 &&
            bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        }

        return Encoding.UTF8.GetString(bytes);
    }
}
