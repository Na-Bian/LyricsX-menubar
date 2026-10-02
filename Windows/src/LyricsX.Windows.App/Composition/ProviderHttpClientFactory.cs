using System.Net;
using LyricsX.Providers;

namespace LyricsX.Windows.App.Composition;

/// <summary>
/// One HttpClient per provider (plan constraint): shared handler pool with
/// automatic decompression, per-provider name and version.
/// </summary>
public sealed class ProviderHttpClientFactory
{
    private readonly SocketsHttpHandler sharedHandler = new()
    {
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        PooledConnectionLifetime = TimeSpan.FromMinutes(10),
        ConnectTimeout = TimeSpan.FromSeconds(15)
    };

    public ProviderHttpClient Create(string providerName) =>
        new(providerName, sharedHandler, version: ThisAssemblyVersion());

    private static string ThisAssemblyVersion() =>
        typeof(ProviderHttpClientFactory).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";
}
