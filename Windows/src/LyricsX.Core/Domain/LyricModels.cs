namespace LyricsX.Core;

public sealed record LyricWord(
    TimeSpan Position,
    TimeSpan? Duration,
    string Content);

public sealed record LyricLine(
    TimeSpan Position,
    TimeSpan? Duration,
    string Content,
    bool Enabled = true,
    IReadOnlyList<LyricWord>? Words = null);

public sealed record LyricMetadata(
    string? Title,
    string? Artist,
    string? Album,
    string? Author,
    TimeSpan? Duration,
    IReadOnlyDictionary<string, string> Extra);

public sealed record LyricDocument(
    IReadOnlyList<LyricLine> Lines,
    LyricMetadata Metadata,
    TimeSpan Delay);

public sealed record LyricSearchRequest(MediaTrack Track, int Limit = 5);

public sealed record LyricCandidate(
    string ProviderId,
    LyricDocument Document,
    string Title,
    string Artist,
    string? Album,
    TimeSpan? Duration,
    int ProviderRank);

public interface ILyricsProvider
{
    string Id { get; }

    IAsyncEnumerable<LyricCandidate> SearchAsync(
        LyricSearchRequest request,
        CancellationToken cancellationToken);
}
