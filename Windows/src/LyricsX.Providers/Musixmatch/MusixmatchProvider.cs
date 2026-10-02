using System.Runtime.CompilerServices;
using System.Text.Json;
using LyricsX.Core;

namespace LyricsX.Providers;

/// <summary>
/// Musixmatch desktop-app endpoint provider: tokenless anonymous access with
/// an optional user token, line-level synchronized subtitles, and a fail-closed
/// diagnostic when the upstream protocol demands token renewal.
/// </summary>
public sealed class MusixmatchProvider(
    ProviderHttpClient httpClient,
    string? userToken = null) : ILyricsProvider, IDisposable
{
    private const string Host = "https://apic-desktop.musixmatch.com";
    private const string SearchPath = "/ws/1.1/track.search";
    private const string LyricsPath = "/ws/1.1/macro.subtitles.get";

    public string Id => "musixmatch";

    public void Dispose() => httpClient.Dispose();

    public async IAsyncEnumerable<LyricCandidate> SearchAsync(
        LyricSearchRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var tracks = await SearchTracksAsync(request, cancellationToken);
        foreach (var track in tracks.Take(request.Limit))
        {
            if (track.HasSubtitles != 1 || track.Instrumental == 1)
            {
                continue;
            }

            LyricDocument? document;
            try
            {
                document = await FetchSubtitlesAsync(track, cancellationToken);
            }
            catch (HttpRequestException)
            {
                continue;
            }

            if (document is null)
            {
                continue;
            }

            yield return new LyricCandidate(
                "musixmatch",
                document,
                track.TrackName ?? string.Empty,
                track.ArtistName ?? string.Empty,
                track.AlbumName,
                track.TrackLength is > 0 ? TimeSpan.FromSeconds(track.TrackLength.Value) : null,
                ProviderRank: 3);
        }
    }

    private async Task<List<MxmTrack>> SearchTracksAsync(
        LyricSearchRequest request,
        CancellationToken cancellationToken)
    {
        var query = CommonQuery() +
            "&q_track=" + Uri.EscapeDataString(request.Track.Title) +
            "&q_artist=" + Uri.EscapeDataString(request.Track.Artist) +
            "&f_has_lyrics=1";
        var json = await httpClient.GetStringAsync(
            new Uri(Host + SearchPath + "?" + query), cancellationToken);

        var header = TryReadHeader(json);
        FailClosedOnRenew(header);

        var tracks = new List<MxmTrack>();
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("message", out var message) ||
                !message.TryGetProperty("body", out var body) ||
                body.ValueKind != JsonValueKind.Object ||
                !body.TryGetProperty("track_list", out var list) ||
                list.ValueKind != JsonValueKind.Array)
            {
                return tracks;
            }

            foreach (var element in list.EnumerateArray())
            {
                try
                {
                    if (element.TryGetProperty("track", out var trackNode))
                    {
                        var track = trackNode.Deserialize<MxmTrack>(ProviderJson.Options);
                        if (track?.TrackName is not null)
                        {
                            tracks.Add(track);
                        }
                    }
                }
                catch (JsonException)
                {
                    // skip malformed row
                }
            }
        }
        catch (JsonException)
        {
        }

        return tracks;
    }

    private async Task<LyricDocument?> FetchSubtitlesAsync(
        MxmTrack track,
        CancellationToken cancellationToken)
    {
        var query = CommonQuery() +
            "&q_album=" + Uri.EscapeDataString(track.AlbumName ?? string.Empty) +
            "&q_artist=" + Uri.EscapeDataString(track.ArtistName ?? string.Empty) +
            "&q_artists=" + Uri.EscapeDataString(track.ArtistName ?? string.Empty) +
            "&q_track=" + Uri.EscapeDataString(track.TrackName ?? string.Empty) +
            "&track_spotify_id=" + Uri.EscapeDataString(track.TrackSpotifyId ?? string.Empty) +
            "&q_duration=" + TrackLengthString(track) +
            "&f_subtitle_length=" + FloorTrackLengthString(track);
        var json = await httpClient.GetStringAsync(
            new Uri(Host + LyricsPath + "?" + query), cancellationToken);

        var header = TryReadHeader(json);
        FailClosedOnRenew(header);

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (!root.TryGetProperty("message", out var message) ||
                !message.TryGetProperty("body", out var body))
            {
                return null;
            }

            var macroCalls = body.TryGetProperty("macro_calls", out var calls)
                ? calls
                : default(JsonElement?);
            if (macroCalls is null)
            {
                return null;
            }

            if (StatusCodeOf(macroCalls.Value, "matcher.track.get") != 200 ||
                StatusCodeOf(macroCalls.Value, "track.subtitles.get") != 200)
            {
                return null;
            }

            var subtitleBody = macroCalls.Value
                .GetProperty("track.subtitles.get")
                .GetProperty("message")
                .GetProperty("body")
                .GetProperty("subtitle_list")[0]
                .GetProperty("subtitle")
                .GetProperty("subtitle_body")
                .GetString();
            return subtitleBody is null ? null : ParseSubtitleBody(subtitleBody);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (KeyNotFoundException)
        {
            return null;
        }
        catch (IndexOutOfRangeException)
        {
            return null;
        }
    }

    private static LyricDocument? ParseSubtitleBody(string subtitleBody)
    {
        try
        {
            var items = JsonSerializer.Deserialize<List<MxmSubtitleItem>>(
                subtitleBody, ProviderJson.Options);
            if (items is not { Count: > 0 })
            {
                return null;
            }

            var lines = new List<LyricLine>(items.Count);
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var position = TimeSpan.FromSeconds(item.Time?.Total ?? 0);
                TimeSpan? duration = i + 1 < items.Count
                    ? TimeSpan.FromSeconds(items[i + 1].Time?.Total ?? 0) - position
                    : null;
                lines.Add(new LyricLine(position, duration, item.Text ?? string.Empty));
            }

            return new LyricDocument(lines,
                new LyricMetadata(null, null, null, null, null, new Dictionary<string, string>()),
                TimeSpan.Zero);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private string CommonQuery()
    {
        var query =
            "format=json&namespace=lyrics_richsynched&subtitle_format=mxm&app_id=web-desktop-app-v1.0";
        if (!string.IsNullOrEmpty(userToken))
        {
            query += "&usertoken=" + Uri.EscapeDataString(userToken);
        }

        return query;
    }

    private static void FailClosedOnRenew(JsonElement? header)
    {
        if (header is { } element &&
            element.TryGetProperty("status_code", out var code) &&
            code.GetInt32() != 200 &&
            element.TryGetProperty("hint", out var hint) &&
            hint.GetString() == "renew")
        {
            throw new InvalidOperationException(
                "Musixmatch rejected the request: the token must be renewed.");
        }
    }

    private static JsonElement? TryReadHeader(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("message", out var message) &&
                message.TryGetProperty("header", out var header))
            {
                return header.Clone();
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

    private static int StatusCodeOf(JsonElement macroCalls, string key)
    {
        try
        {
            return macroCalls.GetProperty(key)
                .GetProperty("message")
                .GetProperty("header")
                .GetProperty("status_code")
                .GetInt32();
        }
        catch (KeyNotFoundException)
        {
            return -1;
        }
    }

    private static string TrackLengthString(MxmTrack track) =>
        (track.TrackLength ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string FloorTrackLengthString(MxmTrack track) =>
        Math.Floor((double)(track.TrackLength ?? 0)).ToString("0", System.Globalization.CultureInfo.InvariantCulture);

    internal sealed record MxmTrack(
        [property: System.Text.Json.Serialization.JsonPropertyName("track_name")] string? TrackName,
        [property: System.Text.Json.Serialization.JsonPropertyName("artist_name")] string? ArtistName,
        [property: System.Text.Json.Serialization.JsonPropertyName("album_name")] string? AlbumName,
        [property: System.Text.Json.Serialization.JsonPropertyName("track_length")] int? TrackLength,
        [property: System.Text.Json.Serialization.JsonPropertyName("has_subtitles")] int? HasSubtitles,
        [property: System.Text.Json.Serialization.JsonPropertyName("instrumental")] int? Instrumental,
        [property: System.Text.Json.Serialization.JsonPropertyName("track_spotify_id")] string? TrackSpotifyId);

    private sealed record MxmSubtitleItem(string? Text, MxmSubtitleTime? Time);

    private sealed record MxmSubtitleTime(double? Total, int? Minutes, int? Seconds, int? Hundredths);
}
