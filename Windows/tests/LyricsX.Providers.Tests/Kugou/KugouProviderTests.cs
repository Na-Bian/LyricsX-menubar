using System.Text;
using System.Text.Json;
using LyricsX.Core;
using LyricsX.Providers;
using LyricsX.Providers.Tests.Infrastructure;

namespace LyricsX.Providers.Tests.Kugou;

[TestClass]
public sealed class KugouProviderTests
{
    private static readonly MediaTrack Track = new(
        "Browser", "id", "晴天", "测试歌手", null, TimeSpan.FromSeconds(269));

    private sealed class KugouStubHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        public string SearchJson { get; set; } = Fixture("search.json");
        public string KrcsJson { get; set; } = Fixture("krcs-candidates.json");
        public string KrcText { get; set; } = Fixture("sample.krc");

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var host = request.RequestUri!.Host;
            string payload;
            if (host == "mobilecdn.kugou.com")
            {
                payload = SearchJson;
            }
            else if (host == "krcs.kugou.com")
            {
                payload = KrcsJson;
            }
            else
            {
                Assert.AreEqual("lyrics.kugou.com", host);
                var download = JsonSerializer.Serialize(
                    new DownloadResponse(KugouKrcCipher.EncryptToBase64(KrcText)),
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                payload = download;
            }

            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            });
        }

        private sealed record DownloadResponse(string Content);
    }

    [TestMethod]
    public async Task SearchAsync_ChainsSearchCandidatesAndDecryptsKrc()
    {
        var stub = new KugouStubHandler();
        var candidates = new List<LyricCandidate>();
        var provider = CreateProvider(stub);
        {
            await foreach (var found in provider.SearchAsync(
                new LyricSearchRequest(Track), CancellationToken.None))
            {
                candidates.Add(found);
            }
        }

        var search = stub.Requests.Single(request =>
            request.RequestUri!.Host == "mobilecdn.kugou.com");
        StringAssert.Contains(search.RequestUri!.Query, Uri.EscapeDataString("晴天"));
        var krcs = stub.Requests.Single(request =>
            request.RequestUri!.Host == "krcs.kugou.com");
        StringAssert.Contains(krcs.RequestUri!.Query, "hash=abc123hash");
        var download = stub.Requests.Single(request =>
            request.RequestUri!.Host == "lyrics.kugou.com");
        StringAssert.Contains(download.RequestUri!.Query, "fmt=krc");

        var candidate = candidates.Single();
        Assert.AreEqual("kugou", candidate.ProviderId);
        Assert.AreEqual("晴天", candidate.Title);
        Assert.AreEqual("测试歌手", candidate.Artist);
        Assert.AreEqual(TimeSpan.FromSeconds(269), candidate.Duration);
        Assert.AreEqual(2, candidate.ProviderRank);
        Assert.AreEqual(TimeSpan.FromMilliseconds(250), candidate.Document.Delay);
        var line = candidate.Document.Lines[0];
        Assert.AreEqual("晴天", line.Content);
        Assert.AreEqual(2, line.Words!.Count);
        Assert.AreEqual(TimeSpan.FromMilliseconds(1500), line.Words[1].Position);
    }

    [TestMethod]
    public async Task SearchAsync_SkipsWhenNoKrcCandidates()
    {
        var stub = new KugouStubHandler
        {
            KrcsJson = """{ "status": 200, "candidates": [] }"""
        };

        var candidates = new List<LyricCandidate>();
        var provider = CreateProvider(stub);
        {
            await foreach (var found in provider.SearchAsync(
                new LyricSearchRequest(Track), CancellationToken.None))
            {
                candidates.Add(found);
            }
        }

        Assert.AreEqual(0, candidates.Count);
    }

    [TestMethod]
    public async Task SearchAsync_PropagatesCancellationToHttpClient()
    {
        var stub = new KugouStubHandler();
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

    private static KugouProvider CreateProvider(KugouStubHandler stub) =>
        new(new ProviderHttpClient("kugou", stub));

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Kugou", name));
}
