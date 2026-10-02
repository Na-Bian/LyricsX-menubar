using System.Runtime.CompilerServices;
using System.Text.Json;
using LyricsX.Core;

namespace LyricsX.Providers;

/// <summary>
/// Spotify synced-lyrics provider over the color-lyrics endpoint. Disabled by
/// default, only attempted for tracks whose source app is Spotify, and capped
/// at one token refresh per search when the lyrics endpoint answers 401.
/// </summary>
public sealed class SpotifyLyricsProvider(
    ProviderHttpClient httpClient,
    ISpotifyTokenStore tokenStore,
    string? clientId = null,
    bool enabled = false) : ILyricsProvider, IDisposable
{
    private const string LyricsBaseUrl = "https://spclient.wg.spotify.com/color-lyrics/v2/track/";

    private readonly SpotifyAuthorizationService authorization =
        new(httpClient, tokenStore, clientId);

    public string Id => "spotify";

    public void Dispose() => httpClient.Dispose();

    public async IAsyncEnumerable<LyricCandidate> SearchAsync(
        LyricSearchRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!enabled || string.IsNullOrEmpty(clientId))
        {
            yield break;
        }

        if (!request.Track.SourceAppId.Equals("spotify", StringComparison.OrdinalIgnoreCase))
        {
            yield break;
        }

        var accessToken = await authorization.GetValidAccessTokenAsync(cancellationToken);
        if (accessToken is null)
        {
            yield break;
        }

        var json = await TryGetLyricsAsync(
            accessToken, request.Track.TrackId, allowRefresh: true, cancellationToken);
        if (json is null)
        {
            yield break;
        }

        var document = ParseLyrics(json);
        if (document is null)
        {
            yield break;
        }

        yield return new LyricCandidate(
            "spotify",
            document,
            request.Track.Title,
            request.Track.Artist,
            request.Track.Album,
            request.Track.Duration,
            ProviderRank: 5);
    }

    private async Task<string?> TryGetLyricsAsync(
        string accessToken,
        string trackId,
        bool allowRefresh,
        CancellationToken cancellationToken)
    {
        var response = await SendLyricsRequestAsync(accessToken, trackId, cancellationToken);
        if (response.StatusCode == 401 && allowRefresh)
        {
            authorization.ClearAccessToken();
            var refreshed = await authorization.RefreshAccessTokenAsync(cancellationToken);
            if (refreshed is null)
            {
                return null;
            }

            response = await SendLyricsRequestAsync(refreshed, trackId, cancellationToken);
        }

        return response.StatusCode is >= 200 and < 300
            ? response.Body
            : null;
    }

    private Task<ProviderHttpResponse> SendLyricsRequestAsync(
        string accessToken,
        string trackId,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            LyricsBaseUrl + Uri.EscapeDataString(trackId) + "?format=json&vocalLoudness=true");
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        return httpClient.TrySendAsync(request, cancellationToken);
    }

    private static LyricDocument? ParseLyrics(string json)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<ColorLyricsResponse>(
                json, ProviderJson.Options);
            if (payload?.Lyrics is not { Lines.Count: > 0 } lyrics ||
                !string.Equals(lyrics.SyncType, "LINE_SYNCED", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var lines = new List<LyricLine>(lyrics.Lines.Count);
            for (var i = 0; i < lyrics.Lines.Count; i++)
            {
                var line = lyrics.Lines[i];
                var position = TimeSpan.FromMilliseconds(line.StartTimeMs ?? 0);
                TimeSpan? duration = i + 1 < lyrics.Lines.Count
                    ? TimeSpan.FromMilliseconds(lyrics.Lines[i + 1].StartTimeMs ?? 0) - position
                    : null;
                lines.Add(new LyricLine(position, duration, line.Words ?? string.Empty));
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

    private sealed record ColorLyricsResponse(ColorLyricsBody? Lyrics);

    private sealed record ColorLyricsBody(string? SyncType, List<ColorLyricsLine> Lines);

    private sealed record ColorLyricsLine(long? StartTimeMs, string? Words);
}
