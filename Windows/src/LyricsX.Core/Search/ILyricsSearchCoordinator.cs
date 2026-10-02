namespace LyricsX.Core.Search;

public sealed record LyricSearchState(
    LyricCandidate Candidate,
    bool IsFinal,
    double Score);

public interface ILyricsSearchCoordinator
{
    IAsyncEnumerable<LyricSearchState> SearchAsync(
        LyricSearchRequest request,
        CancellationToken cancellationToken);
}
