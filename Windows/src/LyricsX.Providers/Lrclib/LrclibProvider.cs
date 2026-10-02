using System.Runtime.CompilerServices;
using System.Text.Json;
using LyricsX.Core;
using LyricsX.Core.Parsing;

namespace LyricsX.Providers;

public sealed class LrclibProvider(ProviderHttpClient httpClient) : ILyricsProvider, IDisposable
{
    private static readonly LrcParser Parser = new();

    public void Dispose() => httpClient.Dispose();

    public string Id => "lrclib";

    public async IAsyncEnumerable<LyricCandidate> SearchAsync(
        LyricSearchRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var query = "track_name=" + Uri.EscapeDataString(request.Track.Title);
        if (!string.IsNullOrWhiteSpace(request.Track.Artist))
        {
            query += "&artist_name=" + Uri.EscapeDataString(request.Track.Artist);
        }

        var json = await httpClient.GetStringAsync(
            new Uri("https://lrclib.net/api/search?" + query), cancellationToken);

        foreach (var item in ProviderJson
            .DeserializeRows<LrclibSearchItem>(json)
            .Take(request.Limit))
        {
            if (item is null ||
                item.SyncedLyrics is not { Length: > 0 } synced ||
                item.Instrumental == true)
            {
                continue;
            }

            LyricDocument document;
            try
            {
                document = Parser.Parse(synced);
            }
            catch (FormatException)
            {
                continue;
            }

            yield return new LyricCandidate(
                "lrclib",
                document,
                item.TrackName ?? string.Empty,
                item.ArtistName ?? string.Empty,
                item.AlbumName,
                item.Duration is > 0 ? TimeSpan.FromSeconds(item.Duration.Value) : null,
                ProviderRank: 4);
        }
    }

    private sealed record LrclibSearchItem(
        int? Id,
        string? Name,
        string? TrackName,
        string? ArtistName,
        string? AlbumName,
        double? Duration,
        bool? Instrumental,
        string? PlainLyrics,
        string? SyncedLyrics);
}
