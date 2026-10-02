using System.Globalization;
using System.Text;

namespace LyricsX.Core.Parsing;

public sealed class LrcSerializer
{
    public string Serialize(LyricDocument document)
    {
        var builder = new StringBuilder();
        var metadata = document.Metadata;
        if (metadata.Title is not null)
        {
            builder.Append("[ti:").Append(metadata.Title).Append(']').AppendLine();
        }

        if (metadata.Artist is not null)
        {
            builder.Append("[ar:").Append(metadata.Artist).Append(']').AppendLine();
        }

        if (metadata.Album is not null)
        {
            builder.Append("[al:").Append(metadata.Album).Append(']').AppendLine();
        }

        if (metadata.Author is not null)
        {
            builder.Append("[by:").Append(metadata.Author).Append(']').AppendLine();
        }

        if (document.Delay != TimeSpan.Zero)
        {
            builder.Append("[offset:")
                .Append((int)document.Delay.TotalMilliseconds)
                .Append(']')
                .AppendLine();
        }

        foreach (var line in document.Lines)
        {
            builder.Append('[').Append(FormatTimestamp(line.Position)).Append(']');
            if (line.Words is { Count: > 0 })
            {
                foreach (var word in line.Words)
                {
                    builder.Append('<')
                        .Append(FormatTimestamp(word.Position))
                        .Append('>')
                        .Append(word.Content);
                }
            }
            else
            {
                builder.Append(line.Content);
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string FormatTimestamp(TimeSpan value)
    {
        var totalMinutes = (int)value.TotalMinutes;
        return FormattableString.Invariant(
            $"{totalMinutes:00}:{value.Seconds:00}.{value.Milliseconds / 10:00}");
    }
}
