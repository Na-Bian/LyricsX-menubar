using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace LyricsX.Core.Search;

public sealed class LyricsSearchCoordinator(
    IReadOnlyList<ILyricsProvider> providers,
    TimeSpan qualityWindow,
    int retryCount = 2,
    TimeSpan? retryDelay = null) : ILyricsSearchCoordinator
{
    public async IAsyncEnumerable<LyricSearchState> SearchAsync(
        LyricSearchRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = Channel.CreateBounded<LyricCandidate>(
            new BoundedChannelOptions(64)
            {
                SingleReader = true,
                SingleWriter = false
            });
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.WhenAll(providers.Select(provider =>
                    PumpProviderAsync(channel.Writer, provider, request, cancellation.Token)));
            }
            finally
            {
                channel.Writer.TryComplete();
            }
        });

        LyricCandidate? best = null;
        var bestScore = 0.0;
        var reader = channel.Reader;
        try
        {
            while (true)
            {
                if (!reader.TryRead(out var candidate))
                {
                    try
                    {
                        if (!await reader.WaitToReadAsync(cancellation.Token))
                        {
                            break;
                        }

                        continue;
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }

                var score = LyricCandidateScorer.Score(request.Track, candidate);
                if (score <= 0)
                {
                    continue;
                }

                if (best is null)
                {
                    best = candidate;
                    bestScore = score;
                    cancellation.CancelAfter(qualityWindow);
                    yield return new LyricSearchState(candidate, IsFinal: false, Score: score);
                }
                else if (score > bestScore)
                {
                    best = candidate;
                    bestScore = score;
                }
            }
        }
        finally
        {
            // disposing the iterator (consumer break / track change) must stop
            // the pumps too, not only the linked token source
            cancellation.Cancel();
        }

        if (best is not null)
        {
            yield return new LyricSearchState(best, IsFinal: true, Score: bestScore);
        }
    }

    private async Task PumpProviderAsync(
        ChannelWriter<LyricCandidate> writer,
        ILyricsProvider provider,
        LyricSearchRequest request,
        CancellationToken cancellationToken)
    {
        var attempts = retryCount + 1;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                await foreach (var candidate in provider
                    .SearchAsync(request, cancellationToken)
                    .WithCancellation(cancellationToken))
                {
                    await writer.WriteAsync(candidate, cancellationToken);
                }

                return;
            }
            catch (Exception exception) when (IsTransient(exception, cancellationToken) &&
                attempt < attempts)
            {
                if (retryDelay is { } delay && delay > TimeSpan.Zero)
                {
                    try
                    {
                        await Task.Delay(delay, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private static bool IsTransient(Exception exception, CancellationToken cancellationToken) =>
        !cancellationToken.IsCancellationRequested &&
        (exception is HttpRequestException || exception is TaskCanceledException);
}
