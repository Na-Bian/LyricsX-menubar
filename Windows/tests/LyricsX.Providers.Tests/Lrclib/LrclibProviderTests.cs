using System.Text.Json;
using LyricsX.Core;
using LyricsX.Providers;
using LyricsX.Providers.Tests.Infrastructure;

namespace LyricsX.Providers.Tests.Lrclib;

[TestClass]
public sealed class LrclibProviderTests
{
    private static readonly MediaTrack Track = new(
        "Browser", "id", "晴天的歌", "测试歌手", "专辑", TimeSpan.FromSeconds(200));

    [TestMethod]
    public async Task SearchAsync_EncodesQueryParametersAsUtf8AndMapsCandidate()
    {
        var stub = new StubHttpMessageHandler(_ => Ok(Fixture("search-hit.json")));
        var candidates = await CollectAsync(stub);

        var request = stub.Requests.Single();
        StringAssert.Contains(request.RequestUri!.Query, "track_name=");
        StringAssert.Contains(request.RequestUri!.Query, UrlEncode("晴天的歌"));
        StringAssert.Contains(request.RequestUri!.Query, UrlEncode("测试歌手"));

        var candidate = candidates.Single();
        Assert.AreEqual("lrclib", candidate.ProviderId);
        Assert.AreEqual("Sunny Day Song", candidate.Title);
        Assert.AreEqual("Test Artist", candidate.Artist);
        Assert.AreEqual("Test Album", candidate.Album);
        Assert.AreEqual(TimeSpan.FromSeconds(201.5), candidate.Duration);
        Assert.AreEqual(4, candidate.ProviderRank);
        Assert.AreEqual(2, candidate.Document.Lines.Count);
        Assert.AreEqual("hello window", candidate.Document.Lines[0].Content);
    }

    [TestMethod]
    public async Task SearchAsync_SkipsInstrumentalAndUnsyncedEntries()
    {
        var stub = new StubHttpMessageHandler(_ => Ok(Fixture("search-instrumental.json")));

        var candidates = await CollectAsync(stub);

        Assert.AreEqual(0, candidates.Count);
        Assert.AreEqual(1, stub.Requests.Count);
    }

    [TestMethod]
    public async Task SearchAsync_SkipsMalformedRowsButKeepsValidOnes()
    {
        var stub = new StubHttpMessageHandler(_ => Ok(Fixture("search-malformed.json")));

        var candidates = await CollectAsync(stub);

        Assert.AreEqual(1, candidates.Count);
        Assert.AreEqual("Valid Song", candidates[0].Title);
    }

    [TestMethod]
    public async Task SearchAsync_ReturnsNothingForEmptyResult()
    {
        var stub = new StubHttpMessageHandler(_ => Ok(Fixture("search-empty.json")));

        var candidates = await CollectAsync(stub);

        Assert.AreEqual(0, candidates.Count);
    }

    [TestMethod]
    public async Task SearchAsync_PropagatesCancellationToHttpClient()
    {
        var stub = new StubHttpMessageHandler(_ => Ok(Fixture("search-hit.json")));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var provider = CreateProvider(stub);

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(async () =>
        {
            await foreach (var _ in provider.SearchAsync(
                new LyricSearchRequest(Track), canceled.Token))
            {
            }
        });
    }

    [TestMethod]
    public async Task SearchAsync_ThrowsHttpRequestExceptionOnHttpError()
    {
        var stub = new StubHttpMessageHandler(
            _ => new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError));

        await Assert.ThrowsExactlyAsync<HttpRequestException>(async () =>
        {
            await foreach (var _ in CreateProvider(stub).SearchAsync(
                new LyricSearchRequest(Track), CancellationToken.None))
            {
            }
        });
    }

    private static LrclibProvider CreateProvider(StubHttpMessageHandler stub) =>
        new(new ProviderHttpClient("lrclib", stub));

    private static async Task<List<LyricCandidate>> CollectAsync(StubHttpMessageHandler stub)
    {
        var provider = CreateProvider(stub);
        var results = new List<LyricCandidate>();
        await foreach (var found in provider.SearchAsync(
            new LyricSearchRequest(Track), CancellationToken.None))
        {
            results.Add(found);
        }

        return results;
    }

    private static HttpResponseMessage Ok(string json) =>
        new(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Lrclib", name));

    private static string UrlEncode(string value) => Uri.EscapeDataString(value);
}
