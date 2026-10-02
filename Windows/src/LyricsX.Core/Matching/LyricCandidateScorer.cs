using LyricsX.Core.Matching;

namespace LyricsX.Core.Search;

public static class LyricCandidateScorer
{
    public static double Score(MediaTrack track, LyricCandidate candidate)
    {
        var trackTitle = TrackTextNormalizer.Normalize(track.Title);
        var candidateTitle = TrackTextNormalizer.Normalize(candidate.Title);
        if (trackTitle.Length == 0 || candidateTitle.Length == 0)
        {
            return 0;
        }

        var titlesRelate = trackTitle.Contains(candidateTitle, StringComparison.Ordinal) ||
            candidateTitle.Contains(trackTitle, StringComparison.Ordinal);
        if (!titlesRelate)
        {
            return 0;
        }

        var score = trackTitle == candidateTitle ? 40 : 20;

        var trackArtist = TrackTextNormalizer.Normalize(track.Artist);
        var candidateArtist = TrackTextNormalizer.Normalize(candidate.Artist);
        if (trackArtist == candidateArtist)
        {
            score += 30;
        }
        else if (TrackTextNormalizer.Normalize(TrackFingerprint.PrimaryArtist(track.Artist)) ==
            TrackTextNormalizer.Normalize(TrackFingerprint.PrimaryArtist(candidate.Artist)))
        {
            score += 20;
        }

        if (track.Album is not null && candidate.Album is not null &&
            TrackTextNormalizer.Normalize(track.Album) ==
            TrackTextNormalizer.Normalize(candidate.Album))
        {
            score += 15;
        }

        if (track.Duration is not null && candidate.Duration is not null)
        {
            var delta = Math.Abs(
                (track.Duration.Value - candidate.Duration.Value).TotalSeconds);
            if (delta <= 2)
            {
                score += 15;
            }
            else if (delta <= 5)
            {
                score += 8;
            }
        }

        score -= candidate.ProviderRank;
        return Math.Clamp(score, 0, 100);
    }
}
