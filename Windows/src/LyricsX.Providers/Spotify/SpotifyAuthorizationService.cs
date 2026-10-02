using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LyricsX.Providers;

/// <summary>
/// Spotify Authorization Code with PKCE: cryptographically random verifier
/// and state, SHA-256 URL-safe challenge, loopback redirect URIs, refresh
/// token persisted only through <see cref="ISpotifyTokenStore"/>, and access
/// tokens kept in memory. No client secret is ever embedded.
/// </summary>
public sealed class SpotifyAuthorizationService(
    ProviderHttpClient httpClient,
    ISpotifyTokenStore tokenStore,
    string? clientId = null)
{
    private const string AuthorizeUrl = "https://accounts.spotify.com/authorize";
    private const string TokenUrl = "https://accounts.spotify.com/api/token";

    private string? accessToken;
    private DateTimeOffset accessTokenExpiry = DateTimeOffset.MinValue;

    public static string CreateCodeVerifier()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Base64Url(bytes);
    }

    public static string CreateCodeChallenge(string verifier) =>
        Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    public static string CreateState() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

    public static bool FixedTimeEquals(string left, string right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(left),
            Encoding.UTF8.GetBytes(right));
    }

    public Uri BuildAuthorizeUrl(string redirectUri, string state, string codeChallenge) =>
        new(AuthorizeUrl +
            "?client_id=" + Uri.EscapeDataString(clientId ?? string.Empty) +
            "&response_type=code" +
            "&redirect_uri=" + Uri.EscapeDataString(redirectUri) +
            "&code_challenge_method=S256" +
            "&code_challenge=" + codeChallenge +
            "&state=" + state);

    /// <summary>
    /// Completes the authorization-code exchange after the loopback callback,
    /// comparing states in fixed time. Returns false without touching the
    /// token store when the state check or the exchange fails.
    /// </summary>
    public async Task<bool> CompleteAuthorizationAsync(
        string code,
        string verifier,
        string redirectUri,
        string returnedState,
        string expectedState,
        CancellationToken cancellationToken)
    {
        if (!FixedTimeEquals(expectedState, returnedState))
        {
            return false;
        }

        var tokens = await ExchangeAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = clientId ?? string.Empty,
            ["code_verifier"] = verifier
        }, cancellationToken);
        if (tokens is null)
        {
            return false;
        }

        await ApplyTokensAsync(tokens, cancellationToken);
        return true;
    }

    public async Task<string?> GetValidAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (accessToken is not null &&
            DateTimeOffset.UtcNow < accessTokenExpiry - TimeSpan.FromSeconds(30))
        {
            return accessToken;
        }

        return await RefreshAccessTokenAsync(cancellationToken);
    }

    public async Task<string?> RefreshAccessTokenAsync(CancellationToken cancellationToken)
    {
        var refresh = await tokenStore.GetRefreshTokenAsync(cancellationToken);
        if (refresh is null || string.IsNullOrEmpty(clientId))
        {
            return null;
        }

        var tokens = await ExchangeAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refresh,
            ["client_id"] = clientId
        }, cancellationToken);
        if (tokens is null)
        {
            return null;
        }

        await ApplyTokensAsync(tokens, cancellationToken);
        return accessToken;
    }

    public void ClearAccessToken()
    {
        accessToken = null;
        accessTokenExpiry = DateTimeOffset.MinValue;
    }

    private async Task ApplyTokensAsync(
        SpotifyTokens tokens,
        CancellationToken cancellationToken)
    {
        accessToken = tokens.AccessToken;
        accessTokenExpiry = DateTimeOffset.UtcNow +
            TimeSpan.FromSeconds(Math.Max(tokens.ExpiresIn ?? 3600, 60));
        if (tokens.RefreshToken is { Length: > 0 })
        {
            await tokenStore.SaveRefreshTokenAsync(tokens.RefreshToken, cancellationToken);
        }
    }

    private async Task<SpotifyTokens?> ExchangeAsync(
        IReadOnlyDictionary<string, string> form,
        CancellationToken cancellationToken)
    {
        string json;
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl)
            {
                Content = new FormUrlEncodedContent(form)
            };
            // failure bodies carry only status, never the exchanged tokens
            json = await httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }

        try
        {
            var tokens = JsonSerializer.Deserialize<SpotifyTokens>(json, ProviderJson.Options);
            return string.IsNullOrEmpty(tokens?.AccessToken) ? null : tokens;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private sealed record SpotifyTokens(
        [property: System.Text.Json.Serialization.JsonPropertyName("access_token")] string? AccessToken,
        [property: System.Text.Json.Serialization.JsonPropertyName("token_type")] string? TokenType,
        [property: System.Text.Json.Serialization.JsonPropertyName("expires_in")] int? ExpiresIn,
        [property: System.Text.Json.Serialization.JsonPropertyName("refresh_token")] string? RefreshToken);
}
