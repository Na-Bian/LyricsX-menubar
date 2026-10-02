using System.Text.Json;
using System.Text.Json.Serialization;

namespace LyricsX.Windows.App.Diagnostics;

/// <summary>
/// JSON-Lines logger with rotation (5 MiB per file), retention (7 days), and
/// a directory cap (50 MiB). Messages are redacted before hitting disk.
/// </summary>
public sealed class AppLogger(string logDirectory) : IDisposable
{
    private const long RotateBytes = 5 * 1024 * 1024;
    private const int RetainDays = 7;
    private const long MaxDirectoryBytes = 50 * 1024 * 1024;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly object gate = new();

    public void Log(LogLevel level, string component, string eventId, string message)
    {
        lock (gate)
        {
            Directory.CreateDirectory(logDirectory);
            var path = Path.Join(logDirectory,
                $"LyricsX-{DateTime.UtcNow:yyyyMMdd}.jsonl");
            if (File.Exists(path) && new FileInfo(path).Length >= RotateBytes)
            {
                Rotate(path);
            }

            var entry = JsonSerializer.Serialize(new
            {
                timestamp = DateTime.UtcNow,
                level,
                component,
                eventId,
                message = DiagnosticReportService.Redact(message)
            }, Options);
            File.AppendAllText(path, entry + Environment.NewLine);
        }
    }

    private static void Rotate(string path)
    {
        var stamp = DateTime.UtcNow.ToString("HHmmss");
        File.Move(path, path.Replace(".jsonl", $".{stamp}.jsonl"));
    }

    public void Dispose() => new LogRetentionService(
        logDirectory, RetainDays, MaxDirectoryBytes).Enforce();
}

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

/// <summary>Deletes logs older than the retention window and trims the
/// directory below its size cap (oldest first).</summary>
public sealed class LogRetentionService(
    string logDirectory,
    int retentionDays,
    long maxDirectoryBytes)
{
    public void Enforce()
    {
        if (!Directory.Exists(logDirectory))
        {
            return;
        }

        var files = Directory.GetFiles(logDirectory, "LyricsX-*.jsonl*")
            .Select(path => new { Path = path, Info = new FileInfo(path), Stamp = StampOf(path) })
            .OrderBy(entry => entry.Stamp)
            .ToArray();
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);

        var total = 0L;
        foreach (var entry in files)
        {
            // the stamp in the file name is authoritative: file creation
            // times survive copies and are useless for retention
            if (entry.Stamp < cutoff)
            {
                entry.Info.Delete();
            }
            else
            {
                total += entry.Info.Length;
            }
        }

        foreach (var entry in files.Where(entry => entry.Info.Exists))
        {
            if (total <= maxDirectoryBytes)
            {
                break;
            }

            total -= entry.Info.Length;
            entry.Info.Delete();
        }
    }

    private static DateTime StampOf(string path)
    {
        var name = Path.GetFileName(path);
        var start = name.IndexOf('-') + 1;
        var stamp = name.Substring(start, Math.Min(8, name.Length - start));
        return DateTime.TryParseExact(
            stamp, "yyyyMMdd", null,
            System.Globalization.DateTimeStyles.AssumeUniversal |
            System.Globalization.DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : DateTime.UtcNow;
    }
}
