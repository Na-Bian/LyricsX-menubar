using LyricsX.Core;

namespace LyricsX.Platform.Windows.Media;

/// <summary>
/// Deterministic active-session ranking: Playing beats everything, then most
/// recently updated, then the previously selected session, then status order,
/// then ordinal session id. Pure logic, fully unit-testable.
/// </summary>
public sealed class MediaSessionRanker
{
    public MediaSessionSnapshot? Select(
        IReadOnlyList<MediaSessionSnapshot> sessions,
        string? previousSessionId)
    {
        if (sessions.Count == 0)
        {
            return null;
        }

        return sessions
            .OrderByDescending(session => session.Status == PlaybackStatus.Playing)
            .ThenByDescending(session => session.LastUpdatedAt)
            .ThenByDescending(session =>
                previousSessionId is not null && session.SessionId == previousSessionId)
            .ThenByDescending(session => session.Status switch
            {
                PlaybackStatus.Paused => 3,
                PlaybackStatus.Stopped => 2,
                _ => 1
            })
            .ThenBy(session => session.SessionId, StringComparer.Ordinal)
            .First();
    }
}
