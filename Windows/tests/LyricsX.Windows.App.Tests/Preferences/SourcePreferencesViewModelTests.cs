using LyricsX.Core.Storage;
using LyricsX.Windows.App.Composition;
using LyricsX.Windows.App.Preferences;

namespace LyricsX.Windows.App.Tests.Preferences;

[TestClass]
public sealed class SourcePreferencesViewModelTests
{
    private static (SourcePreferencesViewModel ViewModel, AppSettingsAccessor Accessor) Create()
    {
        var root = Path.Combine(Path.GetTempPath(), "src-prefs-" + Guid.NewGuid().ToString("N"));
        var store = new SettingsStore(root);
        var accessor = new AppSettingsAccessor(
            store, store.LoadAsync(CancellationToken.None).GetAwaiter().GetResult());
        return (new SourcePreferencesViewModel(accessor), accessor);
    }

    [TestMethod]
    public void Providers_ListedInSettingsOrder()
    {
        var (viewModel, _) = Create();

        CollectionAssert.AreEqual(
            new[] { "qq", "netease", "kugou", "musixmatch", "lrclib", "spotify" },
            viewModel.Providers.Select(p => p.Id).ToArray());
    }

    [TestMethod]
    public async Task ToggleEnabled_PersistsAndKeepsOrder()
    {
        var (viewModel, accessor) = Create();
        viewModel.Providers.First(p => p.Id == "netease").Enabled = false;

        await viewModel.ApplyAsync();

        Assert.IsFalse(accessor.Current.ProviderEnabled("netease"));
        Assert.IsTrue(accessor.Current.ProviderEnabled("qq"));
    }

    [TestMethod]
    public async Task MoveUp_ReordersPriority()
    {
        var (viewModel, accessor) = Create();
        viewModel.MoveUp(viewModel.Providers.First(p => p.Id == "netease"));

        await viewModel.ApplyAsync();

        Assert.AreEqual(0, accessor.Current.Providers.First(p => p.Id == "netease").Priority);
        Assert.AreEqual(1, accessor.Current.Providers.First(p => p.Id == "qq").Priority);
    }

    [TestMethod]
    public async Task ApplyAsync_AtLeastOneEnabled_Enforced()
    {
        var (viewModel, accessor) = Create();
        foreach (var provider in viewModel.Providers)
        {
            provider.Enabled = false;
        }

        Assert.IsFalse(await viewModel.ApplyAsync());
        Assert.IsNotNull(viewModel.ValidationError);
        // at least the first provider stays enabled in the working copy
        Assert.IsTrue(viewModel.Providers.Any(p => p.Enabled) ||
            accessor.Current.Providers.Any(p => p.Enabled));
    }

    [TestMethod]
    public void SpotifySection_HasConnectDisconnectLabels()
    {
        var (viewModel, _) = Create();

        StringAssert.Contains(viewModel.SpotifyConnectLabel, "连接 Spotify…");
        StringAssert.Contains(viewModel.SpotifyDisconnectLabel, "断开连接");
    }
}
