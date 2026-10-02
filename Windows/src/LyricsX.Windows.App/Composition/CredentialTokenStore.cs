using LyricsX.Platform.Windows.Security;
using LyricsX.Providers;

namespace LyricsX.Windows.App.Composition;

/// <summary>
/// Placeholder seam kept for future in-app credential flows; the production
/// token store is WindowsCredentialStore (Credential Manager) directly.
/// </summary>
public sealed class CredentialTokenStore : ISpotifyTokenStore
{
    private readonly WindowsCredentialStore inner = new();

    public Task<string?> GetRefreshTokenAsync(CancellationToken cancellationToken) =>
        inner.GetRefreshTokenAsync(cancellationToken);

    public Task SaveRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken) =>
        inner.SaveRefreshTokenAsync(refreshToken, cancellationToken);

    public Task ClearAsync(CancellationToken cancellationToken) =>
        inner.ClearAsync(cancellationToken);
}
