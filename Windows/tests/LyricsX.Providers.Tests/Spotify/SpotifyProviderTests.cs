using System.Security.Cryptography;
using System.Text;
using LyricsX.Core;
using LyricsX.Providers;
using LyricsX.Providers.Tests.Infrastructure;

namespace LyricsX.Providers.Tests.Spotify;

[TestClass]
public sealed class SpotifyProviderTests
{
    private static readonly MediaTrack SpotifyTrack = new(
        "Spotify", "spot1", "Sunny Window", "测试歌手", null, TimeSpan.FromSeconds(201));

    private sealed class SpotifyStubHandler : HttpMessageHandler
    {
        public int TokenCalls { get; private set; }
        public int LyricsCalls { get; private set; }
        public List<string> BearerTokens { get; } = [];
        public string TokenJson { get; set; } =
            """{ "access_token": "ACC-2", "token_type": "Bearer", "expires_in": 3600, "refresh_token": "REF-2" }""";
        public string LyricsJson { get; set; } = Fixture("color-lyrics.json");
        public bool AlwaysUnauthorized { get; set; }
        public bool TokenEndpointFails { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri!.Host == "accounts.spotify.com")
            {
                TokenCalls++;
                if (TokenEndpointFails)
                {
                    return Task.FromResult(new HttpResponseMessage(
                        System.Net.HttpStatusCode.BadRequest)
                    {
                        Content = new StringContent(
                            """{ "error": "invalid_grant" }""", Encoding.UTF8, "application/json")
                    });
                }

                return Task.FromResult(Ok(TokenJson));
            }

            LyricsCalls++;
            if (AlwaysUnauthorized)
            {
                return Task.FromResult(new HttpResponseMessage(
                    System.Net.HttpStatusCode.Unauthorized));
            }

            BearerTokens.Add(request.Headers.Authorization?.Parameter ?? "");
            return Task.FromResult(Ok(LyricsJson));
        }

        private static HttpResponseMessage Ok(string json) =>
            new(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
    }

    [TestMethod]
    public async Task DisabledProvider_YieldsNothingWithoutRequests()
    {
        var stub = new SpotifyStubHandler();
        var provider = CreateProvider(stub, new MemorySpotifyTokenStore(), enabled: false);

        var count = await CountAsync(provider);

        Assert.AreEqual(0, count);
        Assert.AreEqual(0, stub.TokenCalls + stub.LyricsCalls);
    }

    [TestMethod]
    public async Task MissingClientId_YieldsNothingWithoutRequests()
    {
        var stub = new SpotifyStubHandler();
        var provider = CreateProvider(stub, new MemorySpotifyTokenStore(),
            enabled: true, clientId: null);

        var count = await CountAsync(provider);

        Assert.AreEqual(0, count);
        Assert.AreEqual(0, stub.TokenCalls + stub.LyricsCalls);
    }

    [TestMethod]
    public async Task AuthorizationRequired_WithoutRefreshToken_MakesNoRequests()
    {
        var stub = new SpotifyStubHandler();
        var provider = CreateProvider(stub, new MemorySpotifyTokenStore(), enabled: true);

        var count = await CountAsync(provider);

        Assert.AreEqual(0, count);
        Assert.AreEqual(0, stub.TokenCalls + stub.LyricsCalls);
    }

    [TestMethod]
    public async Task ExpiredAccessToken_RefreshesOnceAndFetchesLyrics()
    {
        var stub = new SpotifyStubHandler();
        var store = new MemorySpotifyTokenStore { Token = "REF-1" };
        var provider = CreateProvider(stub, store, enabled: true);

        var candidates = new List<LyricCandidate>();
        await foreach (var found in provider.SearchAsync(
            new LyricSearchRequest(SpotifyTrack), CancellationToken.None))
        {
            candidates.Add(found);
        }

        Assert.AreEqual(1, stub.TokenCalls);
        Assert.AreEqual(1, stub.LyricsCalls);
        Assert.AreEqual("ACC-2", stub.BearerTokens.Single());
        Assert.AreEqual("REF-2", store.Token);

        var candidate = candidates.Single();
        Assert.AreEqual("spotify", candidate.ProviderId);
        Assert.AreEqual("Sunny Window", candidate.Title);
        Assert.AreEqual("测试歌手", candidate.Artist);
        Assert.AreEqual(5, candidate.ProviderRank);
        Assert.AreEqual(2, candidate.Document.Lines.Count);
        Assert.AreEqual(TimeSpan.FromSeconds(1), candidate.Document.Lines[0].Position);
        Assert.AreEqual("hello window", candidate.Document.Lines[0].Content);
    }

    [TestMethod]
    public async Task NonSpotifyTrack_YieldsNothing()
    {
        var stub = new SpotifyStubHandler();
        var store = new MemorySpotifyTokenStore { Token = "REF-1" };
        var provider = CreateProvider(stub, store, enabled: true);
        var browserTrack = SpotifyTrack with { SourceAppId = "Browser" };

        var count = 0;
        await foreach (var _ in provider.SearchAsync(
            new LyricSearchRequest(browserTrack), CancellationToken.None))
        {
            count++;
        }

        Assert.AreEqual(0, count);
    }

    [TestMethod]
    public async Task UnauthorizedAfterRefresh_StopsAfterOneRetry()
    {
        var stub = new SpotifyStubHandler
        {
            AlwaysUnauthorized = true,
            TokenJson = """{ "access_token": "ACC-2", "token_type": "Bearer", "expires_in": 3600 }"""
        };
        var store = new MemorySpotifyTokenStore { Token = "REF-1" };
        var provider = CreateProvider(stub, store, enabled: true);

        var count = await CountAsync(provider);

        Assert.AreEqual(0, count);
        Assert.AreEqual(2, stub.TokenCalls);
        Assert.AreEqual(2, stub.LyricsCalls);
    }

    [TestMethod]
    public async Task TokenEndpointFailure_NeverLeaksTokensInException()
    {
        var stub = new SpotifyStubHandler
        {
            TokenEndpointFails = true
        };
        var store = new MemorySpotifyTokenStore { Token = "SECRET-REFRESH-1" };
        var provider = CreateProvider(stub, store, enabled: true);

        var count = 0;
        try
        {
            await foreach (var _ in provider.SearchAsync(
                new LyricSearchRequest(SpotifyTrack), CancellationToken.None))
            {
                count++;
            }
        }
        catch (HttpRequestException exception)
        {
            Assert.IsFalse(exception.Message.Contains("SECRET-REFRESH-1"),
                "refresh token leaked: " + exception.Message);
            Assert.IsFalse(exception.ToString().Contains("SECRET-REFRESH-1"),
                "refresh token leaked: " + exception);
        }

        Assert.AreEqual(0, count);
    }

    private static void AssertUrlSafe(string value) =>
        Assert.IsFalse(
            System.Text.RegularExpressions.Regex.IsMatch(value, "[^A-Za-z0-9_-]"),
            $"not url-safe: {value}");

    [TestMethod]
    public void PkceHelpers_ProduceUrlsafeVerifierAndS256Challenge()
    {
        var verifier = SpotifyAuthorizationService.CreateCodeVerifier();
        Assert.IsTrue(verifier.Length >= 43);
        AssertUrlSafe(verifier);

        var challenge = SpotifyAuthorizationService.CreateCodeChallenge(verifier);
        var expected = Convert.ToBase64String(
                SHA256.HashData(Encoding.ASCII.GetBytes(verifier)))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        Assert.AreEqual(expected, challenge);
        AssertUrlSafe(challenge);

        var first = SpotifyAuthorizationService.CreateState();
        var second = SpotifyAuthorizationService.CreateState();
        Assert.AreNotEqual(first, second);
        Assert.IsTrue(SpotifyAuthorizationService.FixedTimeEquals(first, first));
        Assert.IsFalse(SpotifyAuthorizationService.FixedTimeEquals(first, second));
    }

    private static SpotifyLyricsProvider CreateProvider(
        SpotifyStubHandler stub,
        ISpotifyTokenStore store,
        bool enabled,
        string? clientId = "cid123") =>
        new(new ProviderHttpClient("spotify", stub), store, clientId, enabled);

    private static async Task<int> CountAsync(SpotifyLyricsProvider provider)
    {
        var count = 0;
        await foreach (var _ in provider.SearchAsync(
            new LyricSearchRequest(SpotifyTrack), CancellationToken.None))
        {
            count++;
        }

        return count;
    }

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Spotify", name));
}

internal sealed class MemorySpotifyTokenStore : ISpotifyTokenStore
{
    public string? Token { get; set; }

    public Task<string?> GetRefreshTokenAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Token);

    public Task SaveRefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken)
    {
        Token = refreshToken;
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken)
    {
        Token = null;
        return Task.CompletedTask;
    }
}
