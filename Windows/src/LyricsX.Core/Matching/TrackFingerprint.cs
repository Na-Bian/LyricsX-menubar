namespace LyricsX.Core.Matching;

public static class TrackFingerprint
{
    public static string Create(MediaTrack track)
    {
        var title = TrackTextNormalizer.Normalize(track.Title);
        var artist = TrackTextNormalizer.Normalize(PrimaryArtist(track.Artist));
        var seconds = track.Duration is null
            ? "?"
            : Math.Round(track.Duration.Value.TotalSeconds).ToString(
                System.Globalization.CultureInfo.InvariantCulture);
        return $"{title}|{artist}|{seconds}";
    }

    public static string? CreateAlbum(MediaTrack track)
    {
        if (string.IsNullOrWhiteSpace(track.Album))
        {
            return null;
        }

        var artist = TrackTextNormalizer.Normalize(PrimaryArtist(track.Artist));
        var album = TrackTextNormalizer.Normalize(track.Album);
        return $"{artist}|{album}";
    }

    internal static string PrimaryArtist(string artist)
    {
        var separators = new[] { " feat. ", " ft. ", " featuring ", " & ", "、", "," };
        var index = separators
            .Select(separator => artist.IndexOf(separator, StringComparison.OrdinalIgnoreCase))
            .Where(value => value >= 0)
            .DefaultIfEmpty(artist.Length)
            .Min();
        return artist[..index];
    }
}
