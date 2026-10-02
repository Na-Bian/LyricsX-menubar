using System.Net;
using LyricsX.Core;
using LyricsX.Providers;

namespace LyricsX.Providers.Tests;

[TestClass]
public sealed class LiveProviderSmokeTests
{
    private static readonly MediaTrack Request = new(
        "Live", "smoke", "晴天", "周杰伦", "叶惠美", TimeSpan.FromSeconds(269));

    private static ProviderHttpClient LiveClient(string provider) =>
        new(provider, new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        });

    [TestCategory("LiveProvider")]
    [TestMethod]
    public async Task LrclibLive_ReturnsWithoutProtocolFailure()
    {
        using var provider = new LrclibProvider(LiveClient("lrclib"));
        await AssertCompletesWithoutProtocolFailure(provider);
    }

    [TestCategory("LiveProvider")]
    [TestMethod]
    public async Task NetEaseLive_ReturnsWithoutProtocolFailure()
    {
        using var provider = new NetEaseProvider(LiveClient("netease"));
        await AssertCompletesWithoutProtocolFailure(provider);
    }

    [TestCategory("LiveProvider")]
    [TestMethod]
    public async Task QQMusicLive_ReturnsWithoutProtocolFailure()
    {
        using var provider = new QQMusicProvider(LiveClient("qq"));
        await AssertCompletesWithoutProtocolFailure(provider);
    }

    [TestCategory("LiveProvider")]
    [TestMethod]
    public async Task KugouLive_ReturnsWithoutProtocolFailure()
    {
        using var provider = new KugouProvider(LiveClient("kugou"));
        await AssertCompletesWithoutProtocolFailure(provider);
    }

    private static async Task AssertCompletesWithoutProtocolFailure(ILyricsProvider provider)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var count = 0;
        await foreach (var candidate in provider
            .SearchAsync(new LyricSearchRequest(Request), timeout.Token)
            .WithCancellation(timeout.Token))
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(candidate.ProviderId));
            Assert.IsNotNull(candidate.Document);
            Assert.IsTrue(candidate.Document.Lines.Count > 0);
            count++;
        }

        Assert.IsTrue(count is >= 0 and <= 5);
    }
}
