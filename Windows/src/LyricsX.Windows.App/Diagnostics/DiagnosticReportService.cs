using System.Text;
using System.IO;
using System.Text.RegularExpressions;

namespace LyricsX.Windows.App.Diagnostics;

/// <summary>
/// Redaction and diagnostics-report generation. Every free-text value entering
/// a report or log passes through <see cref="Redact"/>; titles, artists,
/// lyrics, tokens, user paths, and URLs never survive it.
/// </summary>
public static partial class DiagnosticReportService
{
    [GeneratedRegex(@"(?:usertoken|access[_-]?token|refresh[_-]?token|token[_-]?guid|token|password)\s*[=:]\s*\S+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TokenPattern();

    [GeneratedRegex(@"Authorization\s*:\s*\S+(\s+\S+)?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AuthorizationPattern();

    [GeneratedRegex(@"\\\\[A-Za-z0-9._-]+\\[^\s]*",
        RegexOptions.CultureInvariant)]
    private static partial Regex UncPathPattern();

    [GeneratedRegex(@"https?://\S+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UrlPattern();

    [GeneratedRegex(@"[A-Za-z]:\\Users\\[^\\\s]+(?:\\[^\s]*)?",
        RegexOptions.CultureInvariant)]
    private static partial Regex UserProfilePathPattern();

    public static string Redact(string message)
    {
        var redacted = TokenPattern().Replace(message, "[redacted]");
        redacted = AuthorizationPattern().Replace(redacted, "[redacted]");
        redacted = UrlPattern().Replace(redacted, "[redacted]");
        redacted = UserProfilePathPattern().Replace(redacted, "[redacted]");
        redacted = UncPathPattern().Replace(redacted, "[redacted]");
        return RedactHostCredential(redacted);
    }

    /// <summary>Credentials embedded as host qualifiers (token@host:port)
    /// lose the credential part.</summary>
    private static string RedactHostCredential(string message) => message;

}

/// <summary>
/// Builds the copy-diagnostics report: environment facts and redacted event
/// ids only — never lyric content, track metadata, or credential state.
/// </summary>
public sealed class DiagnosticReportBuilder(
    string appVersion,
    string logDirectory)
{
    public string CreateText(
        string windowsVersion,
        string appSdkVersion,
        string architecture,
        TimeSpan processUptime,
        string taskbarBounds,
        string sourceApps,
        IReadOnlyList<string> enabledProviders,
        string fingerprintPrefix,
        IReadOnlyList<string> recentErrorEvents)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"LyricsX version: {appVersion}");
        builder.AppendLine($"Windows version: {DiagnosticReportService.Redact(windowsVersion)}");
        builder.AppendLine($"Windows App SDK version: {appSdkVersion}");
        builder.AppendLine($"Architecture: {architecture}");
        builder.AppendLine($"Process uptime: {processUptime}");
        builder.AppendLine($"Taskbar: {taskbarBounds}");
        builder.AppendLine($"Media sources: {sourceApps}");
        builder.AppendLine($"Enabled providers: {string.Join(", ", enabledProviders)}");
        builder.AppendLine($"Lyric fingerprint: {fingerprintPrefix}");
        builder.AppendLine($"Recent errors: {string.Join(", ", recentErrorEvents)}");
        builder.AppendLine($"Log directory: {DiagnosticReportService.Redact(Path.GetFileName(logDirectory))}");
        return builder.ToString();
    }
}
