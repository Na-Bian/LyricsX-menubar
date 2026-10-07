using LyricsX.Core;

namespace LyricsX.Platform.Windows.Media;

/// <summary>
/// Deterministic active-session ranking: Playing beats everything, then most
/// recently updated, then the previously selected session, then status order,
/// then ordinal session id. A preferred source restricts selection to that
/// application's sessions when one exists; otherwise ranking falls back to
/// automatic. Pure logic, fully unit-testable.
/// </summary>
public sealed class MediaSessionRanker
{
    public MediaSessionSnapshot? Select(
        IReadOnlyList<MediaSessionSnapshot> sessions,
        string? previousSessionId,
        string? preferredSourceId = null)
    {
        if (sessions.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(preferredSourceId))
        {
            var preferred = sessions
                .Where(session => session.SourceAppId == preferredSourceId)
                .ToArray();
            // the preferred player not running/visible: automatic fallback
            // keeps lyrics alive instead of silently blanking them
            if (preferred.Length > 0)
            {
                sessions = preferred;
            }
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
