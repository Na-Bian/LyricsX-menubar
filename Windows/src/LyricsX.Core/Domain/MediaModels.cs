namespace LyricsX.Core;

public enum PlaybackStatus
{
    Closed,
    Stopped,
    Paused,
    Playing
}

public sealed record MediaTrack(
    string SourceAppId,
    string TrackId,
    string Title,
    string Artist,
    string? Album,
    TimeSpan? Duration);

public sealed record PlaybackSnapshot(
    MediaTrack? Track,
    PlaybackStatus Status,
    TimeSpan Position,
    double Rate,
    DateTimeOffset ObservedAt);
