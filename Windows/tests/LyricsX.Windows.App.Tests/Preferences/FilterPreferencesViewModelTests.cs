using LyricsX.Core.Storage;
using LyricsX.Windows.App.Composition;
using LyricsX.Windows.App.Preferences;

namespace LyricsX.Windows.App.Tests.Preferences;

[TestClass]
public sealed class FilterPreferencesViewModelTests
{
    private static (FilterPreferencesViewModel ViewModel, AppSettingsAccessor Accessor) Create(
        AppSettings? initial = null)
    {
        var root = Path.Combine(Path.GetTempPath(), "filter-prefs-" + Guid.NewGuid().ToString("N"));
        var store = new SettingsStore(root);
        var accessor = new AppSettingsAccessor(
            store, initial ?? store.LoadAsync(CancellationToken.None).GetAwaiter().GetResult());
        return (new FilterPreferencesViewModel(accessor), accessor);
    }

    [TestMethod]
    public void AddPhrase_TrimsAndRejectsDuplicates()
    {
        var (viewModel, _) = Create();

        viewModel.AddPhrase("  纯音乐  ");
        viewModel.AddPhrase("纯音乐");

        Assert.AreEqual(1, viewModel.BlockedPhrases.Count);
        Assert.AreEqual("纯音乐", viewModel.BlockedPhrases[0]);
        Assert.IsNotNull(viewModel.ValidationError);
    }

    [TestMethod]
    public void AddPhrase_EmptyInputSetsValidationError()
    {
        var (viewModel, _) = Create();

        viewModel.AddPhrase("   ");

        Assert.AreEqual(0, viewModel.BlockedPhrases.Count);
        Assert.IsNotNull(viewModel.ValidationError);
    }

    [TestMethod]
    public async Task Apply_PersistsStrictSearchAndPhrases()
    {
        var (viewModel, accessor) = Create();
        viewModel.StrictSearch = true;
        viewModel.AddPhrase("作词");
        viewModel.AddPhrase("作曲");

        Assert.IsTrue(await viewModel.ApplyAsync());

        Assert.IsTrue(accessor.Current.StrictSearch);
        CollectionAssert.AreEquivalent(
            new[] { "作词", "作曲" },
            accessor.Current.BlockedPhrases.ToArray());
    }

    [TestMethod]
    public void RemovePhrase_RemovesOnlyTarget()
    {
        var (viewModel, _) = Create();
        viewModel.AddPhrase("作词");
        viewModel.AddPhrase("作曲");

        viewModel.RemovePhrase("作词");

        CollectionAssert.AreEquivalent(new[] { "作曲" }, viewModel.BlockedPhrases.ToArray());
    }

    [TestMethod]
    public async Task ClearIgnored_EmptiesBothListsAndRefreshesCounts()
    {
        var (viewModel, accessor) = Create(AppSettings.Default with
        {
            IgnoredTrackFingerprints = ["t1", "t2"],
            IgnoredAlbumFingerprints = ["a1"]
        });
        Assert.AreEqual(2, viewModel.IgnoredTrackCount);
        Assert.AreEqual(1, viewModel.IgnoredAlbumCount);

        Assert.IsTrue(await viewModel.ClearIgnoredAsync());

        Assert.AreEqual(0, viewModel.IgnoredTrackCount);
        Assert.AreEqual(0, viewModel.IgnoredAlbumCount);
        Assert.AreEqual(0, accessor.Current.IgnoredTrackFingerprints.Count);
        Assert.AreEqual(0, accessor.Current.IgnoredAlbumFingerprints.Count);
    }

    [TestMethod]
    public async Task Apply_DoesNotTouchIgnoredLists()
    {
        var (viewModel, accessor) = Create(AppSettings.Default with
        {
            IgnoredTrackFingerprints = ["t1"]
        });

        Assert.IsTrue(await viewModel.ApplyAsync());

        Assert.AreEqual(1, accessor.Current.IgnoredTrackFingerprints.Count);
    }
}
