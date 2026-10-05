using LyricsX.Core;
using LyricsX.Core.Parsing;
using LyricsX.Core.Playback;
using LyricsX.Core.Search;
using LyricsX.Core.Storage;
using LyricsX.Core.Transforms;
using LyricsX.Platform.Windows;
using LyricsX.Platform.Windows.Media;
using LyricsX.Platform.Windows.Security;
using LyricsX.Platform.Windows.Shell;
using LyricsX.Platform.Windows.Taskbar;
using LyricsX.Providers;
using LyricsX.Windows.App.Preferences;
using LyricsX.Windows.App.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LyricsX.Windows.App.Composition;

/// <summary>
/// Wires the full object graph: one HttpClient handler shared by six
/// providers, the core engine, and application-layer services. Tests inject
/// an isolated data root; production uses %LOCALAPPDATA%\LyricsX.
/// </summary>
public static class ServiceRegistration
{
    public static ServiceProvider BuildServices(string dataRoot)
    {
        var services = new ServiceCollection();

        var store = new SettingsStore(dataRoot);
        var settings = new AppSettingsAccessor(
            store, store.LoadAsync(CancellationToken.None).GetAwaiter().GetResult());

        services.AddSingleton(settings);
        services.AddSingleton<ISettingsStore>(store);
        services.AddSingleton(_ => new ManualAssociationStore(dataRoot));
        services.AddSingleton(_ => new LyricCache(dataRoot));
        services.AddSingleton<LrcParser>();
        services.AddSingleton<LrcSerializer>();
        services.AddSingleton<LyricTransformer>();
        services.AddSingleton<LyricLanguageDetector>();
        services.AddSingleton<LyricSynchronizer>();
        services.AddSingleton<ILyricsSearchCoordinator>(sp =>
            new LyricsSearchCoordinator(
                sp.GetServices<ILyricsProvider>().ToArray(),
                qualityWindow: TimeSpan.FromSeconds(5)));

        // platform services (plan 02): media, taskbar, shell
        services.AddSingleton<GlobalMediaSessionSource>();
        services.AddSingleton<IMediaSessionSource>(sp => sp.GetRequiredService<GlobalMediaSessionSource>());
        services.AddSingleton<ITaskbarNativeApi, TaskbarNativeApi>();
        services.AddSingleton<ITaskbarAutomationApi, TaskbarAutomationReader>();
        services.AddSingleton<IFullscreenDetector, ShellFullscreenDetector>();
        services.AddSingleton<TaskbarSnapshotReader>();
        services.AddSingleton<TaskbarIntervalCalculator>();
        services.AddSingleton<ITaskbarPlacementService, TaskbarPlacementService>();
        services.AddSingleton<MediaSessionRanker>();
        services.AddSingleton<SelectedMediaSession>();
        services.AddSingleton<ISelectedMediaSession>(sp => sp.GetRequiredService<SelectedMediaSession>());

        foreach (var provider in CreateProviders(settings))
        {
            services.AddSingleton(provider);
        }

        services.AddSingleton<ILyricsApplicationCoordinator, LyricsApplicationCoordinator>();
        services.AddSingleton<PreferencesViewModel>();

        services.AddSingleton<Diagnostics.AppLogger>(sp =>
            new Diagnostics.AppLogger(Path.Combine(dataRoot, "logs")));
        services.AddSingleton<TrayIconService>();
        services.AddSingleton<GlobalHotKeyService>(sp =>
            new GlobalHotKeyService(new Win32HotKeyNativeApi(), 0x5100));
        services.AddSingleton<StartupRegistrationService>();
        services.AddSingleton<LyricsWindowPresenter>();

        return services.BuildServiceProvider();
    }

    private static ILyricsProvider[] CreateProviders(AppSettingsAccessor settings)
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.GZip |
                System.Net.DecompressionMethods.Deflate,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            ConnectTimeout = TimeSpan.FromSeconds(15)
        };
        var version = typeof(ServiceRegistration).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";

        ProviderHttpClient Client(string name) =>
            new(name, handler, version: version);

        // only enabled providers are registered, in the user's priority order:
        // the search coordinator pumps whatever it is given
        var byId = new Dictionary<string, Func<ILyricsProvider>>
        {
            ["qq"] = () => new QQMusicProvider(Client("qq")),
            ["netease"] = () => new NetEaseProvider(Client("netease")),
            ["kugou"] = () => new KugouProvider(Client("kugou")),
            ["musixmatch"] = () => new MusixmatchProvider(
                Client("musixmatch"), settings.Current.MusixmatchToken),
            ["lrclib"] = () => new LrclibProvider(Client("lrclib")),
            ["spotify"] = () => new SpotifyLyricsProvider(
                Client("spotify"),
                new WindowsCredentialStore(),
                settings.Current.SpotifyClientId,
                enabled: true),
        };

        return settings.Current.Providers
            .Where(provider => provider.Enabled && byId.ContainsKey(provider.Id))
            .OrderBy(provider => provider.Priority)
            .Select(provider => byId[provider.Id]())
            .ToArray();
    }
}
