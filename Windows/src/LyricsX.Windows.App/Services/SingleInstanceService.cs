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
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut);
            client.Connect(3000);
            using var writer = new StreamWriter(client) { AutoFlush = true };
            using var reader = new StreamReader(client);
            writer.WriteLine(command);
            return reader.ReadLine() == "ok";
        }
        catch (TimeoutException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    public static bool RequestShutdown() => new SingleInstanceService().SendCommand("shutdown");

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
            catch (IOException)
            {
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
            catch (IOException)
            {
                // client aborted mid-handshake: serve the next one
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
