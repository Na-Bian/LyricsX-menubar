using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LyricsX.Core;
using LyricsX.Core.Matching;
using LyricsX.Core.Search;
using LyricsX.Core.Storage;
using LyricsX.Windows.App.Services;

namespace LyricsX.Windows.App.Search;

/// <summary>One row in the manual-search result list.</summary>
public sealed partial class LyricCandidateViewModel : ObservableObject
{
    public LyricCandidate Candidate { get; }

    public string ProviderId => Candidate.ProviderId;

    public string Title => Candidate.Title;

    public string Artist => Candidate.Artist;

    public string? Album => Candidate.Album;

    public string Duration => Candidate.Duration is { } duration
        ? $"{(int)duration.TotalMinutes:00}:{duration.Seconds:00}"
        : "--:--";

    public string MatchSummary { get; }

    public LyricCandidateViewModel(LyricCandidate candidate, double score)
    {
        Candidate = candidate;
        MatchSummary = ((int)score).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}

/// <summary>
/// Manual lyric search: prefilled metadata, cancel-on-new-query, incremental
/// provider results, one-time use vs remembered association, and association
/// removal that leaves cached lyrics intact.
/// </summary>
public partial class ManualSearchViewModel : ObservableObject
{
    private readonly ILyricsSearchCoordinator searchCoordinator;
    private readonly ManualAssociationStore associations;
    private readonly LyricCache cache;
    private readonly ILyricsApplicationCoordinator? coordinator;
    private CancellationTokenSource? currentSearch;

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Artist { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsSearching { get; set; }

    [ObservableProperty]
    public partial System.Collections.ObjectModel.ObservableCollection<LyricCandidateViewModel> Results { get; set; } = [];

    public ManualSearchViewModel(
        ILyricsSearchCoordinator searchCoordinator,
        ManualAssociationStore associations,
        LyricCache cache,
        ILyricsApplicationCoordinator? coordinator = null)
    {
        this.searchCoordinator = searchCoordinator;
        this.associations = associations;
        this.cache = cache;
        this.coordinator = coordinator;
    }

    [RelayCommand]
    public async Task SearchAsync(CancellationToken cancellationToken)
    {
        currentSearch?.Cancel();
        var search = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        currentSearch = search;

        Results.Clear();
        IsSearching = true;
        try
        {
            var track = new MediaTrack(
                "manual", "manual", Title, Artist, null, null);
            await foreach (var state in searchCoordinator.SearchAsync(
                new LyricSearchRequest(track), search.Token))
            {
                Results.Add(new LyricCandidateViewModel(state.Candidate, state.Score));
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            IsSearching = false;
        }
    }

    public async Task UseOnceAsync(LyricCandidate candidate)
    {
        if (coordinator is not null)
        {
            await coordinator.UseCandidateAsync(candidate, remember: false, CancellationToken.None);
        }
    }

    public async Task AlwaysUseAsync(LyricCandidate candidate)
    {
        // association key = the CURRENT track's fingerprint, value = the
        // candidate's own fingerprint (the cache file key) — same scheme the
        // coordinator resolves on the next play of the same track
        var trackFingerprint = CurrentTrackFingerprint ?? TrackFingerprint.OfCandidate(candidate);
        var candidateFingerprint = TrackFingerprint.OfCandidate(candidate);
        await cache.StoreAsync(candidateFingerprint, candidate, "manual", 100, CancellationToken.None);
        await associations.SetAsync(trackFingerprint, candidateFingerprint, CancellationToken.None);
        if (coordinator is not null)
        {
            await coordinator.UseCandidateAsync(candidate, remember: true, CancellationToken.None);
        }
    }

    public async Task RemoveAssociationAsync(LyricCandidate candidate)
    {
        var trackFingerprint = CurrentTrackFingerprint ?? TrackFingerprint.OfCandidate(candidate);
        await associations.RemoveAsync(trackFingerprint, CancellationToken.None);
    }

    /// <summary>Fingerprint of the currently playing track, supplied by the
    /// caller so "for this song" always keys on the playing track.</summary>
    public string? CurrentTrackFingerprint { get; set; }
}
