using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;

namespace LyricsX.Windows.App.Services;

/// <summary>
/// One process per user: a named mutex elects the primary; second instances
/// send "show-preferences" or "shutdown" over a named pipe and wait for "ok".
/// </summary>
public sealed class SingleInstanceService : IDisposable
{
    public const string MutexName = @"Local\LyricsX.Windows.App";
    public const string PipeName = "LyricsX.Windows.App.Command";

    private Mutex? mutex;
    private CancellationTokenSource? serverCts;
    private Task? serverTask;

    public bool IsPrimary { get; private set; }

    public event Action<string>? CommandReceived;

    public bool TryBecomePrimary()
    {
        mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (!createdNew)
        {
            mutex.Dispose();
            mutex = null;
            return false;
        }

        IsPrimary = true;
        serverCts = new CancellationTokenSource();
        serverTask = Task.Run(() => PipeServerLoop(serverCts.Token));
        return true;
    }

    public bool SendCommand(string command)
    {
        // the primary's server loop may not have opened its first pipe yet
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut);
                client.Connect(500);
                using var writer = new StreamWriter(client) { AutoFlush = true };
                using var reader = new StreamReader(client);
                writer.WriteLine(command);
                return reader.ReadLine() == "ok";
            }
            catch (TimeoutException)
            {
            }
            catch (IOException)
            {
            }
            catch (ObjectDisposedException)
            {
                // server-side accept/handshake race: retry the connection
            }
            catch (UnauthorizedAccessException exception)
            {
                LastServerError = exception.Message;
            }

            Thread.Sleep(100);
        }

        return false;
    }

    public static bool RequestShutdown() => new SingleInstanceService().SendCommand("shutdown");

    public string? LastServerError { get; private set; }

    private async Task PipeServerLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            NamedPipeServerStream server;
            try
            {
                server = new NamedPipeServerStream(
                    PipeName, PipeDirection.InOut, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            }
            catch (Exception exception)
            {
                LastServerError = exception.GetType().Name + ": " + exception.Message;
                await Task.Delay(200, cancellationToken);
                continue;
            }

            try
            {
                await server.WaitForConnectionAsync(cancellationToken);
                using (server)
                {
                    using var reader = new StreamReader(server);
                    using var writer = new StreamWriter(server) { AutoFlush = true };
                    var line = await reader.ReadLineAsync(cancellationToken);
                    if (line is not null)
                    {
                        CommandReceived?.Invoke(line);
                        await writer.WriteLineAsync("ok");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                // a faulting subscriber or client must not kill the loop:
                // log-and-continue keeps second-instance handling alive
                LastServerError = exception.GetType().Name + ": " + exception.Message;
            }
        }
    }

    public void Dispose()
    {
        serverCts?.Cancel();
        try
        {
            serverTask?.Wait(2000);
        }
        catch (AggregateException)
        {
        }

        serverCts?.Dispose();
        if (mutex is { } owned)
        {
            try
            {
                owned.ReleaseMutex();
            }
            catch (ApplicationException)
            {
            }

            owned.Dispose();
        }
    }
}
