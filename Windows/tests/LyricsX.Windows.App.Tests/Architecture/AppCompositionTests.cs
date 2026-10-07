using LyricsX.Windows.App.Composition;
using LyricsX.Windows.App.Preferences;
using LyricsX.Windows.App.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LyricsX.Windows.App.Tests.Architecture;

[TestClass]
public sealed class AppCompositionTests
{
    [TestMethod]
    public async Task BuildServices_ResolvesCoordinatorAndPreferencesViewModel()
    {
        await using var provider = ServiceRegistration.BuildServices(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        Assert.IsNotNull(provider.GetRequiredService<ILyricsApplicationCoordinator>());
        Assert.IsNotNull(provider.GetRequiredService<PreferencesViewModel>());
    }

    [TestMethod]
    public async Task BuildServices_ResolvesAllPreferencesSectionsAndMenus()
    {
        await using var provider = ServiceRegistration.BuildServices(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        Assert.IsNotNull(provider.GetRequiredService<SourcePreferencesViewModel>());
        Assert.IsNotNull(provider.GetRequiredService<PlayerPreferencesViewModel>());
        Assert.IsNotNull(provider.GetRequiredService<FilterPreferencesViewModel>());
        Assert.IsNotNull(provider.GetRequiredService<ShortcutPreferencesViewModel>());
        Assert.IsNotNull(provider.GetRequiredService<SpotifyConnector>());
        Assert.IsNotNull(provider.GetRequiredService<LyricsX.Windows.App.Menus.LyricsMenuViewModel>());
        Assert.IsNotNull(provider.GetRequiredService<LyricsX.Platform.Windows.Media.SelectedMediaSession>());
        Assert.IsNotNull(provider.GetRequiredService<LyricsX.Platform.Windows.Shell.IHotKeyNativeApi>());
    }

    [TestMethod]
    public async Task BuildServices_RegistersEnabledProvidersInPriorityOrder()
    {
        await using var provider = ServiceRegistration.BuildServices(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        // spotify is disabled by default settings and is not registered;
        // the rest follow the settings priority order
        var providers = provider.GetServices<LyricsX.Core.ILyricsProvider>().ToArray();
        CollectionAssert.AreEquivalent(
            new[] { "qq", "netease", "kugou", "musixmatch", "lrclib" },
            providers.Select(p => p.Id).ToArray());
    }

    [TestMethod]
    public void BuildServices_CoreStaysFreeOfPlatformTypes()
    {
        var references = typeof(LyricsX.Core.MediaTrack)
            .Assembly.GetReferencedAssemblies()
            .Select(name => name.Name)
            .ToArray();

        CollectionAssert.DoesNotContain(references, "LyricsX.Windows.App");
    }
}
