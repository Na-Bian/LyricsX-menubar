using System.Text;
using LyricsX.Core;
using LyricsX.Providers;
using LyricsX.Providers.Tests.Infrastructure;

namespace LyricsX.Providers.Tests.NetEase;

[TestClass]
public sealed class NetEaseProviderTests
{
    private static readonly MediaTrack Track = new(
        "Browser", "id", "晴天", "测试歌手", null, TimeSpan.FromSeconds(269));

    private sealed class NetEaseStubHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        public string SearchJson { get; set; } = Fixture("search.json");
        public string LyricJson { get; set; } = Fixture("lyric.json");

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var host = request.RequestUri!.Host;
            string json;
            if (host == "music.163.com")
            {
                json = SearchJson;
            }
            else
            {
                Assert.AreEqual("interface3.music.163.com", host);
                Assert.IsTrue(request.Content is FormUrlEncodedContent,
                    "eapi body must be form-encoded");
                var body = request.Content!.ReadAsStringAsync(cancellationToken).Result;
                StringAssert.StartsWith(body, "params=");
                var hex = body["params=".Length..];
                var decrypted = NetEaseEapiCrypto.DecryptParams(hex);
                StringAssert.Contains(decrypted, "/api/song/lyric/v1");
                StringAssert.Contains(decrypted, "777");
                json = LyricJson;
            }

            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            response.Headers.Add(
                "Set-Cookie", "NMTID=abc123; Path=/; Max-Age=31536000");
            return Task.FromResult(response);
        }
    }

    [TestMethod]
    public async Task SearchAsync_EncodesUtf8QueryAppliesCookieAndMapsCandidate()
    {
        var stub = new NetEaseStubHandler();
        var candidates = new List<LyricCandidate>();
        var provider = CreateProvider(stub);
        {
            await foreach (var found in provider.SearchAsync(
                new LyricSearchRequest(Track), CancellationToken.None))
            {
                candidates.Add(found);
            }
        }

        // first pass extracts the cookie, second pass carries it, third is eapi
        Assert.AreEqual(3, stub.Requests.Count);
        var search = stub.Requests.Take(2).ToList();
        Assert.IsTrue(search.All(request =>
            request.RequestUri!.Host == "music.163.com" &&
            request.RequestUri.Query.Contains(Uri.EscapeDataString("晴天")) &&
            request.RequestUri.Query.Contains(Uri.EscapeDataString("测试歌手"))));
        StringAssert.StartsWith(
            stub.Requests[1].Headers.GetValues("Cookie").Single(), "NMTID=abc123");
        // the eapi request carries its own synthetic cookie, not the search one
        StringAssert.Contains(
            stub.Requests[2].Headers.GetValues("Cookie").Single(), "MUSIC_U=");

        var lyric = candidates.Single();
        Assert.AreEqual("netease", lyric.ProviderId);
        Assert.AreEqual("晴天", lyric.Title);
        Assert.AreEqual("测试歌手", lyric.Artist);
        Assert.AreEqual("测试专辑", lyric.Album);
        Assert.AreEqual(TimeSpan.FromSeconds(269), lyric.Duration);
        Assert.AreEqual(1, lyric.ProviderRank);
        // the [00:01:00] colon time tag must be fixed to [00:01.00]
        Assert.AreEqual(2, lyric.Document.Lines.Count);
        Assert.AreEqual("first line", lyric.Document.Lines[0].Content);
    }

    [TestMethod]
    public async Task SearchAsync_SkipsEmptyLyricsAndMalformedRows()
    {
        var stub = new NetEaseStubHandler
        {
            SearchJson = Fixture("search-malformed.json"),
            LyricJson = Fixture("lyric-empty.json")
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
        var stub = new NetEaseStubHandler();
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

    private static NetEaseProvider CreateProvider(NetEaseStubHandler stub) =>
        new(new ProviderHttpClient("netease", stub));

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "NetEase", name));
}
