namespace LyricsX.Providers;

public interface ISpotifyTokenStore
{
    Task<string?> GetRefreshTokenAsync(CancellationToken cancellationToken);

    Task SaveRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken);

    Task ClearAsync(CancellationToken cancellationToken);
}
