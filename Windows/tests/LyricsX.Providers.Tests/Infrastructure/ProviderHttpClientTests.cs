using System.Net;
using LyricsX.Providers;

namespace LyricsX.Providers.Tests.Infrastructure;

[TestClass]
public sealed class ProviderHttpClientTests
{
    [TestMethod]
    public async Task SendAsync_AppliesProviderUserAgentAndLanguageHeaders()
    {
        var stub = new StubHttpMessageHandler(_ => JsonResponse("{}"));
        using var client = Create(stub);

        await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, TestUri), CancellationToken.None);

        var request = stub.Requests.Single();
        Assert.AreEqual("LyricsX-Windows/1.2.3",
            request.Headers.UserAgent.ToString());
        Assert.AreEqual("zh-CN, zh;q=0.9, en;q=0.5",
            string.Join(", ", request.Headers.AcceptLanguage.Select(
                language => language.Value + (language.Quality.HasValue
                    ? ";q=" + language.Quality.Value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                    : string.Empty))));
    }

    [TestMethod]
    public async Task SendAsync_RequestsGzipAndDeflate()
    {
        var stub = new StubHttpMessageHandler(_ => JsonResponse("{}"));
        using var client = Create(stub);

        await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, TestUri), CancellationToken.None);

        StringAssert.Contains(
            stub.Requests.Single().Headers.AcceptEncoding.ToString(),
            "gzip");
        StringAssert.Contains(
            stub.Requests.Single().Headers.AcceptEncoding.ToString(),
            "deflate");
    }

    [TestMethod]
    public async Task SendAsync_ThrowsHttpRequestExceptionOnNonSuccess()
    {
        var stub = new StubHttpMessageHandler(
            _ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        using var client = Create(stub);

        await Assert.ThrowsExactlyAsync<HttpRequestException>(() =>
            client.SendAsync(
                new HttpRequestMessage(HttpMethod.Get, TestUri), CancellationToken.None));
    }

    [TestMethod]
    public async Task SendAsync_RespectsConfiguredTimeout()
    {
        var stub = new DelayedHandler(TimeSpan.FromSeconds(5));
        using var client = new ProviderHttpClient(
            "test", stub, timeout: TimeSpan.FromMilliseconds(100));

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() =>
            client.SendAsync(
                new HttpRequestMessage(HttpMethod.Get, TestUri), CancellationToken.None));
        stopwatch.Stop();

        Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(2),
            $"timeout took {stopwatch.Elapsed}");
    }

    [TestMethod]
    public void DefaultTimeout_IsFifteenSeconds()
    {
        Assert.AreEqual(TimeSpan.FromSeconds(15), ProviderHttpClient.DefaultTimeout);
    }

    [TestMethod]
    public async Task SendAsync_PropagatesCallerCancellation()
    {
        var stub = new StubHttpMessageHandler(_ => JsonResponse("{}"));
        using var client = Create(stub);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() =>
            client.SendAsync(
                new HttpRequestMessage(HttpMethod.Get, TestUri), canceled.Token));

        Assert.AreEqual(0, stub.Requests.Count);
    }

    [TestMethod]
    public async Task SendAsync_ThrottlesToTwoConcurrentRequestsPerProvider()
    {
        var handler = new CountingDelayedHandler(TimeSpan.FromMilliseconds(150));
        using var client = new ProviderHttpClient("test", handler);

        var sends = Enumerable.Range(0, 4).Select(_ => client.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, TestUri), CancellationToken.None));
        await Task.WhenAll(sends);

        Assert.AreEqual(4, handler.Completed);
        Assert.AreEqual(2, handler.MaxConcurrent);
        Assert.IsTrue(handler.MaxConcurrent <= 2);
    }

    [TestMethod]
    public async Task SendAsync_RejectsBodiesBeyondTheReadLimit()
    {
        var stub = new StubHttpMessageHandler(
            _ => JsonResponse(new string('x', 64)));
        using var client = new ProviderHttpClient(
            "test", stub, maxResponseBytes: 16);

        await Assert.ThrowsExactlyAsync<HttpRequestException>(() =>
            client.SendAsync(
                new HttpRequestMessage(HttpMethod.Get, TestUri), CancellationToken.None));
    }

    [TestMethod]
    public async Task SendWithHeadersAsync_ExposesResponseHeaders()
    {
        var stub = new StubHttpMessageHandler(_ =>
        {
            var response = JsonResponse("{}");
            response.Headers.Add("Set-Cookie", "NMTID=abc123; Path=/; Max-Age=31536000");
            return response;
        });
        using var client = Create(stub);

        var response = await client.SendWithHeadersAsync(
            new HttpRequestMessage(HttpMethod.Get, TestUri), CancellationToken.None);

        Assert.AreEqual(200, response.StatusCode);
        StringAssert.Contains(
            string.Join("; ", response.Headers.GetValues("Set-Cookie")),
            "NMTID=abc123");
        Assert.AreEqual("{}", response.Body);
    }

    private static ProviderHttpClient Create(StubHttpMessageHandler stub) =>
        new("test", stub, version: "1.2.3");

    private static Uri TestUri => new("https://provider.example/api");

    private static HttpResponseMessage JsonResponse(string content) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
        };

    private sealed class DelayedHandler(TimeSpan delay) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await Task.Delay(delay, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private sealed class CountingDelayedHandler(TimeSpan delay) : HttpMessageHandler
    {
        private int concurrent;
        private int completed;

        public int MaxConcurrent { get; private set; }

        public int Completed => completed;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var current = Interlocked.Increment(ref concurrent);
            MaxConcurrent = Math.Max(MaxConcurrent, current);
            try
            {
                await Task.Delay(delay, cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            finally
            {
                Interlocked.Decrement(ref concurrent);
                Interlocked.Increment(ref completed);
            }
        }
    }
}
