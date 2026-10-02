using System.Text;

namespace LyricsX.Providers;

public sealed record ProviderHttpResponse(
    int StatusCode,
    System.Net.Http.Headers.HttpResponseHeaders Headers,
    string Body);

public sealed class ProviderHttpClient : IDisposable
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);
    public const int DefaultMaxResponseBytes = 4 * 1024 * 1024;

    private readonly string providerName;
    private readonly HttpClient httpClient;
    private readonly int maxResponseBytes;
    private readonly SemaphoreSlim throttle = new(2, 2);

    public ProviderHttpClient(
        string providerName,
        HttpMessageHandler handler,
        TimeSpan? timeout = null,
        int? maxResponseBytes = null,
        string version = "0.1.0")
    {
        this.providerName = providerName;
        this.maxResponseBytes = maxResponseBytes ?? DefaultMaxResponseBytes;
        httpClient = new HttpClient(handler, disposeHandler: true)
        {
            Timeout = timeout ?? DefaultTimeout
        };
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"LyricsX-Windows/{version}");
        httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("zh-CN,zh;q=0.9,en;q=0.5");
        httpClient.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip, deflate");
    }

    public Task<string> GetStringAsync(Uri uri, CancellationToken cancellationToken) =>
        SendAsync(new HttpRequestMessage(HttpMethod.Get, uri), cancellationToken);

    public Task<string> PostAsync(
        Uri uri,
        HttpContent content,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = content };
        return SendAsync(request, cancellationToken);
    }

    public async Task<string> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = await SendCoreAsync(request, cancellationToken);
        EnsureSuccess(response, providerName);
        return response.Body;
    }

    public async Task<ProviderHttpResponse> SendWithHeadersAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var response = await SendCoreAsync(request, cancellationToken);
        EnsureSuccess(response, providerName);
        return response;
    }

    private async Task<ProviderHttpResponse> SendCoreAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        await throttle.WaitAsync(cancellationToken);
        try
        {
            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            await using var stream = await response.Content
                .ReadAsStreamAsync(cancellationToken);
            var body = await ReadBoundedAsync(stream, cancellationToken);
            return new ProviderHttpResponse(
                (int)response.StatusCode,
                response.Headers,
                body);
        }
        finally
        {
            throttle.Release();
        }
    }

    private static void EnsureSuccess(ProviderHttpResponse response, string providerName)
    {
        if (response.StatusCode < 200 || response.StatusCode >= 300)
        {
            throw new HttpRequestException(
                $"{providerName} returned {response.StatusCode}.");
        }
    }

    private async Task<string> ReadBoundedAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var bounded = new MemoryStream();
        var buffer = new byte[8192];
        var total = 0;
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > maxResponseBytes)
            {
                throw new HttpRequestException(
                    $"{providerName} response exceeded {maxResponseBytes} bytes.");
            }

            bounded.Write(buffer, 0, read);
        }

        return Encoding.UTF8.GetString(bounded.ToArray());
    }

    public void Dispose()
    {
        httpClient.Dispose();
        throttle.Dispose();
    }
}
