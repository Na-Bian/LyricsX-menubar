using System.Text;
using LyricsX.Core;
using LyricsX.Providers;
using LyricsX.Providers.Tests.Infrastructure;

namespace LyricsX.Providers.Tests.QQMusic;

[TestClass]
public sealed class QQMusicProviderTests
{
    private static readonly MediaTrack Track = new(
        "Browser", "id", "晴天", "测试歌手", null, TimeSpan.FromSeconds(269));

    private sealed class QQStubHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        public string SmartboxJson { get; set; } = Fixture("smartbox.json");
        public string MusicuJson { get; set; } = Fixture("musicu-search.json");
        public string LyricBody { get; set; } = Fixture("lyric-download.txt");

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var path = request.RequestUri!.AbsolutePath;
            string payload;
            string contentType = "application/json";
            if (path.Contains("smartbox_new"))
            {
                payload = SmartboxJson;
            }
            else if (path.Contains("musicu.fcg"))
            {
                payload = MusicuJson;
            }
            else
            {
                Assert.IsTrue(path.Contains("lyric_download"));
                contentType = "text/plain";
                payload = LyricBody;
            }

            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, contentType)
            });
        }
    }

    [TestMethod]
    public async Task SearchAsync_QueriesBothEndpointsConcurrentlyAndMapsCandidates()
    {
        var stub = new QQStubHandler();
        var candidates = new List<LyricCandidate>();
        var provider = CreateProvider(stub);
        {
            await foreach (var found in provider.SearchAsync(
                new LyricSearchRequest(Track), CancellationToken.None))
            {
                candidates.Add(found);
            }
        }

        var searchRequests = stub.Requests
            .Where(request => !request.RequestUri!.AbsolutePath.Contains("lyric_download"))
            .ToList();
        Assert.AreEqual(2, searchRequests.Count);
        var smartbox = searchRequests.Single(request =>
            request.RequestUri!.AbsolutePath.Contains("smartbox_new"));
        StringAssert.Contains(smartbox.RequestUri!.Query, Uri.EscapeDataString("晴天"));
        var musicu = searchRequests.Single(request =>
            request.RequestUri!.AbsolutePath.Contains("musicu.fcg"));
        var body = await musicu.Content!.ReadAsStringAsync();
        StringAssert.Contains(body, "晴天");

        var parsed = candidates.Where(candidate => candidate.Title == "晴天歌").ToList();
        Assert.AreEqual(1, parsed.Count);
        var candidate = parsed[0];
        Assert.AreEqual("qq", candidate.ProviderId);
        Assert.AreEqual("测试歌手", candidate.Artist);
        Assert.IsNull(candidate.Album);
        Assert.AreEqual(0, candidate.ProviderRank);
        var line = candidate.Document.Lines.Single();
        Assert.AreEqual("晴天好", line.Content);
        Assert.AreEqual(2, line.Words!.Count);
        Assert.AreEqual(TimeSpan.FromMilliseconds(1200), line.Words[0].Position);
    }

    [TestMethod]
    public async Task SearchAsync_DecryptsHexQrcContent()
    {
        var qrc = "[ti:Encrypted]\n[1000,2000]Enc(1200,300)rypted\n";
        var body = "<content>" + QQMusicQrcCipher.EncryptToHex(qrc) + "</content>";
        var stub = new QQStubHandler
        {
            MusicuJson = """{ "req_1": { "code": 0, "data": { "body": { "song": { "list": [] } } } } }""",
            LyricBody = body
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

        Assert.AreEqual(1, candidates.Count);
        Assert.AreEqual("Encrypted", candidates[0].Document.Metadata.Title);
        Assert.AreEqual("Encrypted", candidates[0].Document.Lines[0].Content);
    }

    [TestMethod]
    public async Task SearchAsync_SkipsSongsWithoutDecodableLyrics()
    {
        var stub = new QQStubHandler
        {
            MusicuJson = """{ "req_1": { "code": 0, "data": { "body": { "song": { "list": [] } } } } }""",
            LyricBody = "<content></content><contentts></contentts>"
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
        var stub = new QQStubHandler();
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

    private static QQMusicProvider CreateProvider(QQStubHandler stub) =>
        new(new ProviderHttpClient("qq", stub));

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "QQMusic", name));
}
