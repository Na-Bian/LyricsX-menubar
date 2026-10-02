using System.Runtime.CompilerServices;
using System.Text.Json;
using LyricsX.Core;
using LyricsX.Core.Parsing;

namespace LyricsX.Providers;

public sealed class KugouProvider(ProviderHttpClient httpClient) : ILyricsProvider, IDisposable
{
    private static readonly Uri SearchUri = new("http://mobilecdn.kugou.com/api/v3/search/song");
    private static readonly Uri KrcsSearchUri = new("https://krcs.kugou.com/search");
    private static readonly Uri DownloadUri = new("http://lyrics.kugou.com/download");

    private static readonly KrcParser Parser = new();

    public void Dispose() => httpClient.Dispose();

    public string Id => "kugou";

    public async IAsyncEnumerable<LyricCandidate> SearchAsync(
        LyricSearchRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var term = NetEaseProvider.BuildSearchTerm(request.Track);
        var searchUri = new Uri(SearchUri,
            "?format=json&keyword=" + Uri.EscapeDataString(term) +
            "&page=1&pagesize=20&showtype=1");
        var searchJson = await httpClient.GetStringAsync(searchUri, cancellationToken);

        foreach (var info in DeserializeInfoRows(searchJson).Take(request.Limit))
        {
            if (info?.Hash is not { Length: > 0 })
            {
                continue;
            }

            KugouInfo? fetched;
            try
            {
                fetched = await FetchLyricsAsync(info, cancellationToken);
            }
            catch (HttpRequestException)
            {
                continue;
            }

            if (fetched?.Document is null || fetched.Candidate is null)
            {
                continue;
            }

            var candidate = fetched.Candidate;
            yield return new LyricCandidate(
                "kugou",
                fetched.Document,
                candidate.Song ?? string.Empty,
                candidate.Singer ?? string.Empty,
                Album: null,
                candidate.Duration is > 0
                    ? TimeSpan.FromMilliseconds(candidate.Duration.Value)
                    : null,
                ProviderRank: 2);
        }
    }

    private async Task<KugouInfo?> FetchLyricsAsync(
        KugouInfo info,
        CancellationToken cancellationToken)
    {
        var krcsUri = new Uri(KrcsSearchUri,
            "?ver=1&man=yes&client=mobi&keyword=&duration=&hash=" +
            Uri.EscapeDataString(info.Hash!) +
            "&album_audio_id=" + Uri.EscapeDataString(
                info.AlbumAudioId?.ToString(
                    System.Globalization.CultureInfo.InvariantCulture) ?? ""));
        var krcsJson = await httpClient.GetStringAsync(krcsUri, cancellationToken);

        KugouCandidate? candidate;
        try
        {
            candidate = JsonSerializer.Deserialize<KugouCandidatesResponse>(
                krcsJson, ProviderJson.Options)?.Candidates?.FirstOrDefault();
        }
        catch (JsonException)
        {
            return null;
        }

        if (candidate?.Id is not { Length: > 0 } || candidate.AccessKey is not { Length: > 0 })
        {
            return null;
        }

        var downloadUri = new Uri(DownloadUri,
            "?id=" + Uri.EscapeDataString(candidate.Id) +
            "&accesskey=" + Uri.EscapeDataString(candidate.AccessKey) +
            "&fmt=krc&charset=utf8&client=pc&ver=1");
        var downloadJson = await httpClient.GetStringAsync(downloadUri, cancellationToken);

        string? encrypted;
        try
        {
            encrypted = JsonSerializer.Deserialize<KugouDownloadResponse>(
                downloadJson, ProviderJson.Options)?.Content;
        }
        catch (JsonException)
        {
            return null;
        }

        if (encrypted is not { Length: > 0 })
        {
            return null;
        }

        var krc = KugouKrcCipher.DecryptBase64(encrypted);
        if (krc is null)
        {
            return null;
        }

        try
        {
            var document = Parser.Parse(krc);
            return info with { Candidate = candidate, Document = document };
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static List<KugouInfo?> DeserializeInfoRows(string json)
    {
        var rows = new List<KugouInfo?>();
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("data", out var data) ||
                !data.TryGetProperty("info", out var list) ||
                list.ValueKind != JsonValueKind.Array)
            {
                return rows;
            }

            foreach (var element in list.EnumerateArray())
            {
                try
                {
                    rows.Add(element.Deserialize<KugouInfo>(ProviderJson.Options));
                }
                catch (JsonException)
                {
                    rows.Add(null);
                }
            }
        }
        catch (JsonException)
        {
        }

        return rows;
    }

    internal sealed record KugouInfo(
        string? Hash,
        string? AlbumId,
        [property: System.Text.Json.Serialization.JsonPropertyName("album_audio_id")] long? AlbumAudioId,
        KugouCandidate? Candidate = null,
        LyricDocument? Document = null);

    internal sealed record KugouCandidatesResponse(List<KugouCandidate>? Candidates);

    internal sealed record KugouCandidate(
        string? Id,
        string? AccessKey,
        string? Song,
        string? Singer,
        long? Duration);

    internal sealed record KugouDownloadResponse(string? Content);
}
