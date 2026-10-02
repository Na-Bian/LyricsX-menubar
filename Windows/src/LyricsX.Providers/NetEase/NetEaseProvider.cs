using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LyricsX.Core;
using LyricsX.Core.Parsing;

namespace LyricsX.Providers;

public sealed partial class NetEaseProvider(ProviderHttpClient httpClient) : ILyricsProvider, IDisposable
{
    private const string SearchUrl = "http://music.163.com/api/search/pc";
    private const string LyricEapiUrl = "https://interface3.music.163.com/eapi/song/lyric/v1";
    private const string BrowserUserAgent =
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 " +
        "(KHTML, like Gecko) Version/15.4 Safari/15.4";
    private const string EapiUserAgent =
        "Mozilla/5.0 (Linux; Android 9; PCT-AL10) AppleWebKit/537.36 " +
        "(KHTML, like Gecko) Chrome/70.0.3538.64 HuaweiBrowser/10.0.3.311 Mobile Safari/537.36";

    private static readonly LrcParser Parser = new();

    public void Dispose() => httpClient.Dispose();

    public string Id => "netease";

    public async IAsyncEnumerable<LyricCandidate> SearchAsync(
        LyricSearchRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var songs = await SearchSongsAsync(request, cancellationToken);
        foreach (var song in songs.Take(request.Limit))
        {
            LyricDocument? document;
            try
            {
                document = await FetchLyricsAsync(song, cancellationToken);
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
                "netease",
                document,
                song.Name ?? string.Empty,
                song.Artists?.FirstOrDefault()?.Name ?? string.Empty,
                song.Album?.Name,
                song.Duration is > 0 ? TimeSpan.FromMilliseconds(song.Duration.Value) : null,
                ProviderRank: 1);
        }
    }

    private async Task<List<NetEaseSong>> SearchSongsAsync(
        LyricSearchRequest request,
        CancellationToken cancellationToken)
    {
        var term = BuildSearchTerm(request.Track);
        var query = $"s={Uri.EscapeDataString(term)}&offset=0&limit=10&type=1";

        // First pass extracts the anti-bot cookie, second pass carries it.
        var first = BuildSearchRequest(query);
        var firstResponse = await httpClient.SendWithHeadersAsync(first, cancellationToken);
        var second = BuildSearchRequest(query);
        var cookie = ExtractCookie(firstResponse.Headers);
        if (cookie is not null)
        {
            second.Headers.Add("Cookie", cookie);
        }

        var json = await httpClient.SendAsync(second, cancellationToken);

        var songs = new List<NetEaseSong>();
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("result", out var result) ||
                result.ValueKind != JsonValueKind.Object ||
                !result.TryGetProperty("songs", out var songArray) ||
                songArray.ValueKind != JsonValueKind.Array)
            {
                return songs;
            }

            foreach (var element in songArray.EnumerateArray())
            {
                try
                {
                    var song = element.Deserialize<NetEaseSong>(ProviderJson.Options);
                    if (song?.Id is not null && song.Name is not null)
                    {
                        songs.Add(song);
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

        return songs;
    }

    private static HttpRequestMessage BuildSearchRequest(string query) =>
        new(HttpMethod.Post, SearchUrl + "?" + query)
        {
            Headers =
            {
                Referrer = new Uri("http://music.163.com/"),
            },
        };

    private static string? ExtractCookie(System.Net.Http.Headers.HttpResponseHeaders headers)
    {
        if (!headers.TryGetValues("Set-Cookie", out var values))
        {
            return null;
        }

        var cookie = string.Join("; ", values);
        var separator = cookie.IndexOf(';');
        return separator >= 0 ? cookie[..separator] : (cookie.Length > 0 ? cookie : null);
    }

    private async Task<LyricDocument?> FetchLyricsAsync(
        NetEaseSong song,
        CancellationToken cancellationToken)
    {
        var header = BuildEapiHeader();
        var payload = new Dictionary<string, string>
        {
            ["id"] = song.Id!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["cp"] = "false",
            ["lv"] = "0",
            ["kv"] = "0",
            ["tv"] = "0",
            ["rv"] = "0",
            ["yv"] = "0",
            ["ytv"] = "0",
            ["yrv"] = "0",
            ["csrf_token"] = "",
            ["header"] = JsonSerializer.Serialize(header)
        };

        var hex = NetEaseEapiCrypto.EncryptParams(LyricEapiUrl, payload);
        var request = new HttpRequestMessage(HttpMethod.Post, LyricEapiUrl)
        {
            Content = new FormUrlEncodedContent(
                new Dictionary<string, string> { ["params"] = hex }),
            Headers =
            {
                Referrer = new Uri("https://music.163.com/"),
            }
        };
        request.Headers.Add("User-Agent", EapiUserAgent);
        request.Headers.Add("Cookie", string.Join("; ",
            header.Select(pair => $"{pair.Key}={pair.Value}")));

        var json = await httpClient.SendAsync(request, cancellationToken);
        string? rawLrc;
        try
        {
            rawLrc = JsonSerializer.Deserialize<NetEaseLyricsResponse>(
                json, ProviderJson.Options)?.Lrc?.Lyric;
        }
        catch (JsonException)
        {
            return null;
        }

        if (string.IsNullOrEmpty(rawLrc))
        {
            return null;
        }

        var fixedLrc = ColonTimeTag().Replace(rawLrc, "$1.$2");
        try
        {
            return Parser.Parse(fixedLrc);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static Dictionary<string, string> BuildEapiHeader()
    {
        var now = DateTimeOffset.UtcNow;
        return new Dictionary<string, string>
        {
            ["__csrf"] = "",
            ["appver"] = "8.0.0",
            ["buildver"] = ((long)now.ToUnixTimeSeconds()).ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            ["channel"] = "",
            ["deviceId"] = "",
            ["mobilename"] = "",
            ["resolution"] = "1920x1080",
            ["os"] = "android",
            ["osver"] = "",
            ["requestId"] = $"{now.ToUnixTimeMilliseconds():0}_{Random.Shared.Next(0, 9999):0000}",
            ["versioncode"] = "140",
            ["MUSIC_U"] = ""
        };
    }

    internal static string BuildSearchTerm(MediaTrack track)
    {
        var term = track.Title;
        if (!string.IsNullOrWhiteSpace(track.Artist))
        {
            term += " " + track.Artist;
        }

        return term.Trim();
    }

    [GeneratedRegex(@"(\[\d+:\d+):(\d+\])")]
    private static partial Regex ColonTimeTag();

    internal sealed record NetEaseSong(
        string? Name,
        long? Id,
        long? Duration,
        List<NetEaseArtist>? Artists,
        NetEaseAlbum? Album);

    internal sealed record NetEaseArtist(string? Name, long? Id);

    internal sealed record NetEaseAlbum(string? Name, long? Id, Uri? PicUrl);

    private sealed record NetEaseLyricsResponse(NetEaseLyricBody? Lrc);

    private sealed record NetEaseLyricBody(string? Lyric);
}
