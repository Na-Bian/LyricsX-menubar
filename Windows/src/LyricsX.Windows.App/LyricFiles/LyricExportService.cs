using System.Text;
using LyricsX.Core;
using LyricsX.Core.Parsing;

namespace LyricsX.Windows.App.LyricFiles;

/// <summary>
/// Serializes the active lyric document for export: enhanced-word lines as
/// LRCX, plain lines as LRC; UTF-8 without BOM.
/// </summary>
public sealed class LyricExportService
{
    private static readonly LrcSerializer Serializer = new();

    public byte[] Export(LyricDocument document, bool useLrcx)
    {
        var lines = useLrcx ? document.Lines : StripWords(document.Lines);
        var text = Serializer.Serialize(document with { Lines = lines });
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(text);
    }

    private static IReadOnlyList<LyricLine> StripWords(IReadOnlyList<LyricLine> lines) =>
        lines.Select(line => line with { Words = null }).ToArray();
}
