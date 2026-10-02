using LyricsX.Core.Storage;
namespace LyricsX.Windows.App.Composition;

/// <summary>
/// Process-wide view of the current settings, refreshed on save; provider
/// factories read it at construction and on reconnect.
/// </summary>
public sealed class AppSettingsAccessor
{
    private readonly ISettingsStore store;

    public AppSettingsAccessor(ISettingsStore store, AppSettings initial)
    {
        this.store = store;
        Current = initial;
    }

    public AppSettings Current { get; private set; }

    public async Task ReplaceAsync(AppSettings settings)
    {
        await store.SaveAsync(settings, CancellationToken.None);
        Current = settings;
    }

    public bool ProviderEnabled(string id) =>
        Current.Providers.FirstOrDefault(provider => provider.Id == id) is { Enabled: true };
}

public static class AppSettingsExtensions
{
    public static bool ProviderEnabled(this AppSettings settings, string id) =>
        settings.Providers.FirstOrDefault(provider => provider.Id == id) is { Enabled: true };
}
