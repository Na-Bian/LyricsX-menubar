using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using LyricsX.Providers;
using LyricsX.Windows.App.Composition;

namespace LyricsX.Windows.App.Preferences;

/// <summary>
/// Drives the Spotify Authorization Code + PKCE flow end to end: an ephemeral
/// loopback listener receives the browser redirect, the state is compared in
/// fixed time, and the exchanged refresh token lands only in Credential
/// Manager. Nothing is returned or logged on failure but a short reason.
/// </summary>
public sealed class SpotifyConnector(
    ProviderHttpClientFactory httpClientFactory,
    CredentialTokenStore tokenStore)
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(2);

    public bool HasStoredToken { get; private set; }

    public async Task RefreshStateAsync(CancellationToken cancellationToken)
    {
        HasStoredToken = await tokenStore.GetRefreshTokenAsync(cancellationToken) is not null;
    }

    public async Task<(bool Connected, string? Error)> ConnectAsync(
        string clientId,
        CancellationToken cancellationToken)
    {
        clientId = clientId.Trim();
        if (clientId.Length == 0)
        {
            return (false, "请先填写 Spotify Client ID");
        }

        var port = FindFreeLoopbackPort();
        if (port is null)
        {
            return (false, "无法占用本地回环端口");
        }

        var redirectUri = $"http://127.0.0.1:{port}/";
        var authorization = new SpotifyAuthorizationService(
            httpClientFactory.Create("spotify-auth"), tokenStore, clientId);
        var verifier = SpotifyAuthorizationService.CreateCodeVerifier();
        var state = SpotifyAuthorizationService.CreateState();
        var authorizeUrl = authorization.BuildAuthorizeUrl(
            redirectUri, state, SpotifyAuthorizationService.CreateCodeChallenge(verifier));

        var listener = new HttpListener();
        listener.Prefixes.Add(redirectUri);
        try
        {
            listener.Start();
            _ = OpenBrowser(authorizeUrl);

            var context = await listener.GetContextAsync().WaitAsync(DefaultTimeout, cancellationToken);
            var query = context.Request.QueryString;
            await RespondWithClosePage(context);

            var code = query["code"];
            var returnedState = query["state"];
            if (query["error"] is { Length: > 0 } error)
            {
                return (false, "浏览器授权被拒绝: " + error);
            }

            if (string.IsNullOrEmpty(code))
            {
                return (false, "浏览器回调缺少授权码");
            }

            var connected = await authorization.CompleteAuthorizationAsync(
                code, verifier, redirectUri, returnedState ?? string.Empty, state, cancellationToken);
            if (!connected)
            {
                return (false, "授权码交换失败(状态校验或网络错误)");
            }

            await RefreshStateAsync(cancellationToken);
            return (true, null);
        }
        catch (OperationCanceledException)
        {
            return (false, "连接已取消");
        }
        catch (TimeoutException)
        {
            return (false, "等待浏览器授权超时(2 分钟)");
        }
        catch (HttpListenerException)
        {
            return (false, "无法启动本地回环侦听器");
        }
        finally
        {
            listener.Stop();
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        await tokenStore.ClearAsync(cancellationToken);
        HasStoredToken = false;
    }

    private static int? FindFreeLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        try
        {
            listener.Start();
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        catch (SocketException)
        {
            return null;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static async Task RespondWithClosePage(HttpListenerContext context)
    {
        const string closePage =
            "<!doctype html><html><body style=\"font-family:Segoe UI;sans-serif:" +
            "text-align:center;padding-top:3rem\">授权完成,可以关闭此页面返回 LyricsX。</body></html>";
        var buffer = System.Text.Encoding.UTF8.GetBytes(closePage);
        context.Response.ContentLength64 = buffer.Length;
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.OutputStream.WriteAsync(buffer);
        context.Response.Close();
    }

    private static bool OpenBrowser(Uri url)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = url.ToString(),
                UseShellExecute = true
            });
            return process is not null;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
