using System.Security.Cryptography;
using System.Text;
using LyricsX.Core;

namespace LyricsX.Platform.Windows.Media;

/// <summary>
/// Pure mapping from GSMTC session data to the stable platform contracts.
/// No WinRT types cross this boundary: the WinRT-facing source adapts enum
/// values into <see cref="GlobalPlaybackStatus"/> and passes primitives.
/// </summary>
public static class GsmtcMapper
{
    public static MediaSessionSnapshot Map(
        string sessionId,
        string? sourceAppUserModelId,
        GlobalPlaybackStatus playbackStatus,
        string? title,
        string? artist,
        string? album,
        double positionSeconds,
        double timelineEndSeconds,
        DateTimeOffset lastUpdatedAt,
        object? nativeSession = null)
    {
        TimeSpan? duration = timelineEndSeconds > 0
            ? TimeSpan.FromSeconds(timelineEndSeconds)
            : null;
        var track = new MediaTrack(
            SourceAppId: sourceAppUserModelId ?? string.Empty,
            TrackId: CreateTrackId(
                sourceAppUserModelId ?? string.Empty,
                title ?? string.Empty,
                artist ?? string.Empty,
                album,
                duration),
            Title: title ?? string.Empty,
            Artist: artist ?? string.Empty,
            Album: string.IsNullOrWhiteSpace(album) ? null : album,
            Duration: duration);

        return new MediaSessionSnapshot(
            SessionId: sessionId,
            SourceAppId: sourceAppUserModelId ?? string.Empty,
            Status: MapStatus(playbackStatus),
            Track: track,
            Position: TimeSpan.FromSeconds(Math.Max(0, positionSeconds)),
            Rate: 1.0,
            LastUpdatedAt: lastUpdatedAt,
            NativeSession: nativeSession ?? sessionId);
    }

    private static PlaybackStatus MapStatus(GlobalPlaybackStatus status) => status switch
    {
        GlobalPlaybackStatus.Playing => PlaybackStatus.Playing,
        GlobalPlaybackStatus.Paused => PlaybackStatus.Paused,
        GlobalPlaybackStatus.Stopped => PlaybackStatus.Stopped,
        GlobalPlaybackStatus.Opened => PlaybackStatus.Stopped,
        _ => PlaybackStatus.Closed
    };

    private static string CreateTrackId(
        string sourceAppId,
        string title,
        string artist,
        string? album,
        TimeSpan? duration)
    {
        var normalizedTitle = Normalize(title);
        var normalizedArtist = Normalize(artist);
        var normalizedAlbum = Normalize(album ?? string.Empty);
        var roundedSeconds = duration is null
            ? string.Empty
            : Math.Round(duration.Value.TotalSeconds)
                .ToString(System.Globalization.CultureInfo.InvariantCulture);
        var payload = FormattableString.Invariant(
            $"{sourceAppId}|{normalizedTitle}|{normalizedArtist}|{normalizedAlbum}|{roundedSeconds}");
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    private static string Normalize(string value) =>
        value.Normalize(NormalizationForm.FormKC).Trim().ToLowerInvariant();
}
