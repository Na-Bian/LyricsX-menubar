using System.Text;
using LyricsX.Core;
using LyricsX.Providers;
using LyricsX.Providers.Tests.Infrastructure;

namespace LyricsX.Providers.Tests.Musixmatch;

[TestClass]
public sealed class MusixmatchProviderTests
{
    private static readonly MediaTrack Track = new(
        "Browser", "id", "Sunny Window", "测试歌手", "测试专辑", TimeSpan.FromSeconds(201));

    private sealed class MusixmatchStubHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        public string SearchJson { get; set; } = Fixture("search-hit.json");
        public string SubtitlesJson { get; set; } = Fixture("macro-subtitles.json");

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var path = request.RequestUri!.AbsolutePath;
            var payload = path.Contains("macro.subtitles.get") ? SubtitlesJson : SearchJson;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            });
        }
    }

    [TestMethod]
    public async Task SearchAsync_MapsCandidateAndSkipsMalformedRows()
    {
        var stub = new MusixmatchStubHandler();
        var candidates = new List<LyricCandidate>();
        var provider = CreateProvider(stub);
        await foreach (var found in provider.SearchAsync(
            new LyricSearchRequest(Track), CancellationToken.None))
        {
            candidates.Add(found);
        }

        var search = stub.Requests.First(request =>
            request.RequestUri!.AbsolutePath.Contains("track.search"));
        StringAssert.Contains(search.RequestUri!.Query, Uri.EscapeDataString("Sunny Window"));
        StringAssert.Contains(search.RequestUri!.Query, "app_id=web-desktop-app-v1.0");
        StringAssert.Contains(search.RequestUri!.Query, "f_has_lyrics=1");
        StringAssert.Contains(
            stub.Requests.Last().RequestUri!.Query, "namespace=lyrics_richsynched");

        var candidate = candidates.Single();
        Assert.AreEqual("musixmatch", candidate.ProviderId);
        Assert.AreEqual("Sunny Window", candidate.Title);
        Assert.AreEqual("测试歌手", candidate.Artist);
        Assert.AreEqual("测试专辑", candidate.Album);
        Assert.AreEqual(TimeSpan.FromSeconds(201), candidate.Duration);
        Assert.AreEqual(3, candidate.ProviderRank);
        Assert.AreEqual(2, candidate.Document.Lines.Count);
        Assert.AreEqual("hello window", candidate.Document.Lines[0].Content);
        Assert.AreEqual(TimeSpan.FromSeconds(1), candidate.Document.Lines[0].Position);
        Assert.AreEqual(2.5, candidate.Document.Lines[0].Duration!.Value.TotalSeconds, 0.001);
    }

    [TestMethod]
    public async Task SearchAsync_SkipsInstrumentalTracksWithoutSubtitleCalls()
    {
        var stub = new MusixmatchStubHandler
        {
            SearchJson = Fixture("search-instrumental.json")
        };

        var candidates = new List<LyricCandidate>();
        var provider = CreateProvider(stub);
        await foreach (var found in provider.SearchAsync(
            new LyricSearchRequest(Track), CancellationToken.None))
        {
            candidates.Add(found);
        }

        Assert.AreEqual(0, candidates.Count);
        Assert.AreEqual(1, stub.Requests.Count);
    }

    [TestMethod]
    public async Task SearchAsync_FailsClosedOnTokenRenewHint()
    {
        var stub = new MusixmatchStubHandler
        {
            SearchJson = Fixture("token-renew.json")
        };
        var provider = CreateProvider(stub);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in provider.SearchAsync(
                new LyricSearchRequest(Track), CancellationToken.None))
            {
            }
        });
    }

    [TestMethod]
    public async Task SearchAsync_AppendsUserTokenWhenConfigured()
    {
        var stub = new MusixmatchStubHandler();

        var provider = new MusixmatchProvider(
            new ProviderHttpClient("musixmatch", stub), userToken: "tok123");
        await foreach (var _ in provider.SearchAsync(
            new LyricSearchRequest(Track), CancellationToken.None))
        {
        }

        StringAssert.Contains(
            stub.Requests.First().RequestUri!.Query, "usertoken=tok123");
    }

    [TestMethod]
    public async Task SearchAsync_PropagatesCancellationToHttpClient()
    {
        var stub = new MusixmatchStubHandler();
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
    public async Task SearchAsync_ToleratesNonObjectMacroBody()
    {
        var stub = new MusixmatchStubHandler
        {
            SubtitlesJson = """{ "message": { "header": { "status_code": 200 }, "body": [] } }"""
        };

        var count = 0;
        var provider = CreateProvider(stub);
        await foreach (var _ in provider.SearchAsync(
            new LyricSearchRequest(Track), CancellationToken.None))
        {
            count++;
        }

        Assert.AreEqual(0, count);
    }

    [TestMethod]
    public async Task SearchAsync_ToleratesEmptySubtitleList()
    {
        var stub = new MusixmatchStubHandler
        {
            SubtitlesJson = """
{
  "message": {
    "header": { "status_code": 200 },
    "body": {
      "macro_calls": {
        "matcher.track.get": {
          "message": { "header": { "status_code": 200 }, "body": { "track": {} } }
        },
        "track.subtitles.get": {
          "message": { "header": { "status_code": 200 }, "body": { "subtitle_list": [] } }
        }
      }
    }
  }
}
"""
        };

        var count = 0;
        var provider = CreateProvider(stub);
        await foreach (var _ in provider.SearchAsync(
            new LyricSearchRequest(Track), CancellationToken.None))
        {
            count++;
        }

        Assert.AreEqual(0, count);
    }

    private static MusixmatchProvider CreateProvider(MusixmatchStubHandler stub) =>
        new(new ProviderHttpClient("musixmatch", stub));

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Musixmatch", name));
}
