using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LyricsX.Core;
using LyricsX.Core.Parsing;

namespace LyricsX.Providers;

public sealed partial class QQMusicProvider(ProviderHttpClient httpClient) : ILyricsProvider, IDisposable
{
    private static readonly Uri SmartboxUri = new("https://c.y.qq.com/splcloud/fcgi-bin/smartbox_new.fcg");
    private static readonly Uri MusicuUri = new("https://u.y.qq.com/cgi-bin/musicu.fcg");
    private static readonly Uri LyricDownloadUri = new("https://c.y.qq.com/qqmusic/fcgi-bin/lyric_download.fcg");

    private static readonly QrcParser QrcParser = new();
    private static readonly LrcParser LrcParser = new();

    public void Dispose() => httpClient.Dispose();

    public string Id => "qq";

    public async IAsyncEnumerable<LyricCandidate> SearchAsync(
        LyricSearchRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var term = NetEaseProvider.BuildSearchTerm(request.Track);
        var songs = await SearchSongsAsync(term, request.Limit, cancellationToken);

        foreach (var song in songs)
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
                "qq",
                document,
                song.Name,
                song.Artist,
                Album: null,
                Duration: null,
                ProviderRank: 0);
        }
    }

    private async Task<List<QQSong>> SearchSongsAsync(
        string term,
        int limit,
        CancellationToken cancellationToken)
    {
        var smartboxTask = SearchSmartboxAsync(term, cancellationToken);
        var musicuTask = SearchMusicuAsync(term, cancellationToken);
        await Task.WhenAll(smartboxTask, musicuTask);

        var merged = new List<QQSong>();
        var seen = new HashSet<string>();
        foreach (var song in smartboxTask.Result.Concat(musicuTask.Result))
        {
            if (!seen.Add(song.Mid))
            {
                continue;
            }

            merged.Add(song);
            if (merged.Count >= limit)
            {
                return merged;
            }
        }

        return merged;
    }

    private async Task<List<QQSong>> SearchSmartboxAsync(
        string term,
        CancellationToken cancellationToken)
    {
        var json = await httpClient.GetStringAsync(
            new Uri(SmartboxUri, "?key=" + Uri.EscapeDataString(term)), cancellationToken);
        var songs = new List<QQSong>();
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("data", out var data) ||
                !data.TryGetProperty("song", out var songNode) ||
                !songNode.TryGetProperty("itemlist", out var list) ||
                list.ValueKind != JsonValueKind.Array)
            {
                return songs;
            }

            foreach (var element in list.EnumerateArray())
            {
                try
                {
                    var item = element.Deserialize<SmartboxItem>(ProviderJson.Options);
                    if (item is { Mid: not null, Id: not null })
                    {
                        songs.Add(new QQSong(item.Id!, item.Mid!, item.Name ?? "",
                            item.Singer ?? ""));
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

    private async Task<List<QQSong>> SearchMusicuAsync(
        string term,
        CancellationToken cancellationToken)
    {
        var body = JsonSerializer.Serialize(new
        {
            req_1 = new
            {
                method = "DoSearchForQQMusicDesktop",
                module = "music.search.SearchCgiService",
                param = new
                {
                    num_per_page = 20,
                    page_num = 1,
                    query = term,
                    search_type = 0
                }
            }
        }, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

        var request = new HttpRequestMessage(HttpMethod.Post, MusicuUri)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        var json = await httpClient.SendAsync(request, cancellationToken);

        var songs = new List<QQSong>();
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("req_1", out var req) ||
                !req.TryGetProperty("data", out var data) ||
                data.ValueKind != JsonValueKind.Object ||
                !data.TryGetProperty("body", out var bodyNode) ||
                !bodyNode.TryGetProperty("song", out var songNode) ||
                !songNode.TryGetProperty("list", out var list) ||
                list.ValueKind != JsonValueKind.Array)
            {
                return songs;
            }

            foreach (var element in list.EnumerateArray())
            {
                try
                {
                    var item = element.Deserialize<MusicuItem>(ProviderJson.Options);
                    if (item?.Mid is not null)
                    {
                        songs.Add(new QQSong(item.Id?.ToString() ?? "", item.Mid,
                            item.Name ?? "",
                            string.Join(", ", item.Singer?.Select(singer => singer.Name) ?? [])));
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

    private async Task<LyricDocument?> FetchLyricsAsync(
        QQSong song,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, LyricDownloadUri)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["musicid"] = song.Id,
                ["version"] = "15",
                ["miniversion"] = "82",
                ["lrctype"] = "4"
            }),
            Headers =
            {
                Referrer = new Uri("https://c.y.qq.com/"),
            }
        };

        var response = await httpClient.SendAsync(request, cancellationToken);
        var content = QQMusicLyricDecoder.ExtractContent(response);
        if (content is null)
        {
            return null;
        }

        return TryParse(content, QrcParser) ?? TryParse(content, LrcParser);
    }

    private static LyricDocument? TryParse(string content, ILyricParser parser)
    {
        try
        {
            return parser.Parse(content);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    internal sealed record QQSong(string Id, string Mid, string Name, string Artist);

    private sealed record SmartboxItem(string? Mid, string? Name, string? Singer, string? Id);

    private sealed record MusicuItem(string? Mid, string? Name, long? Id, List<MusicuSinger>? Singer);

    private sealed record MusicuSinger(string? Name);
}
