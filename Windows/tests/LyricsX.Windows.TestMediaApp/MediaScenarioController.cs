using System.IO;
using System.IO.Pipes;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage.Streams;

namespace LyricsX.Windows.TestMediaApp;

/// <summary>
/// Publishes three deterministic tracks through a MediaPlayer (whose SMTC is
/// registered globally, making the session visible to GSMTC readers) and
/// serves a newline-command automation pipe: play, pause, next,
/// position:seconds, exit. Replies "ok" after playback state updates.
/// </summary>
public sealed class MediaScenarioController
{
    public static readonly (string Title, string Artist, TimeSpan Duration)[] Tracks =
    [
        ("Windows Test One", "LyricsX Fixture", TimeSpan.FromSeconds(120)),
        ("Windows Test Two", "LyricsX Fixture", TimeSpan.FromSeconds(180)),
        ("Windows Test Three", "Second Fixture", TimeSpan.FromSeconds(240))
    ];

    private readonly MediaPlayer player = new();
    private readonly Microsoft.UI.Dispatching.DispatcherQueue dispatcher;
    private int trackIndex;

    public event Action<string>? Log;

    public MediaScenarioController(Microsoft.UI.Dispatching.DispatcherQueue dispatcher)
    {
        this.dispatcher = dispatcher;
        player.CommandManager.IsEnabled = false;
        ApplyTrack();
    }

    public static string? PipeNameFromCommandLine()
    {
        var args = Environment.GetCommandLineArgs();
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals("--automation-pipe", StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    public void ApplyInitialTrack() => ApplyTrack();

    public void Play() => player.Play();

    public void Pause() => player.Pause();

    public void Next()
    {
        trackIndex = (trackIndex + 1) % Tracks.Length;
        ApplyTrack();
        Log?.Invoke($"next -> {CurrentTrack.Title}");
    }

    public void SetPosition(TimeSpan value) =>
        player.PlaybackSession.Position = value;

    public void StartAutomationPipe(string pipeName)
    {
        var thread = new Thread(() => PipeLoop(pipeName))
        {
            IsBackground = true,
            Name = "LyricsX-AutomationPipe"
        };
        thread.Start();
    }

    private (string Title, string Artist, TimeSpan Duration) CurrentTrack =>
        Tracks[trackIndex];

    private void ApplyTrack()
    {
        var track = CurrentTrack;
        var source = MediaSource.CreateFromStream(
            CreateSilence(track.Duration), "audio/wav");
        var item = new MediaPlaybackItem(source);
        var properties = item.GetDisplayProperties();
        properties.Type = global::Windows.Media.MediaPlaybackType.Music;
        properties.MusicProperties.Title = track.Title;
        properties.MusicProperties.Artist = track.Artist;
        properties.MusicProperties.AlbumTitle = "LyricsX Test Album";
        item.ApplyDisplayProperties(properties);
        player.Source = item;
    }

    private static IRandomAccessStream CreateSilence(TimeSpan duration)
    {
        const int sampleRate = 8000;
        var seconds = Math.Max(1, (int)Math.Ceiling(duration.TotalSeconds));
        var dataSize = sampleRate * seconds;

        using var wav = new MemoryStream();
        using (var writer = new BinaryWriter(wav))
        {
            writer.Write("RIFF"u8);
            writer.Write(36 + dataSize);
            writer.Write("WAVE"u8);
            writer.Write("fmt "u8);
            writer.Write(16);
            writer.Write((short)1); // PCM
            writer.Write((short)1); // mono
            writer.Write(sampleRate);
            writer.Write(sampleRate); // byte rate
            writer.Write((short)1); // block align
            writer.Write((short)8); // bits per sample
            writer.Write("data"u8);
            writer.Write(dataSize);
            writer.Write(new byte[dataSize]);
        }

        var stream = new InMemoryRandomAccessStream();
        using (var dataWriter = new DataWriter(stream.GetOutputStreamAt(0)))
        {
            dataWriter.WriteBytes(wav.ToArray());
            _ = dataWriter.StoreAsync().AsTask().GetAwaiter().GetResult();
        }

        stream.Seek(0);
        return stream;
    }

    private void PipeLoop(string pipeName)
    {
        while (true)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    pipeName, PipeDirection.InOut, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                server.WaitForConnection();

                using var reader = new StreamReader(server);
                using var writer = new StreamWriter(server) { AutoFlush = true };
                string? line;
                while ((line = reader.ReadLine()) is not null)
                {
                    var handled = new TaskCompletionSource<bool>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                    var ok = dispatcher.TryEnqueue(() =>
                    {
                        try
                        {
                            Execute(line);
                            handled.SetResult(true);
                        }
                        catch (Exception exception)
                        {
                            handled.SetException(exception);
                        }
                    });
                    if (!ok)
                    {
                        writer.WriteLine("error dispatcher-gone");
                        return;
                    }

                    try
                    {
                        _ = handled.Task.GetAwaiter().GetResult();
                        writer.WriteLine("ok");
                        if (line.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
                        {
                            Environment.Exit(0);
                        }
                    }
                    catch (Exception exception)
                    {
                        writer.WriteLine("error " + exception.Message);
                    }
                }
            }
            catch (IOException)
            {
                // client disconnected: serve the next connection
            }
        }
    }

    private void Execute(string line)
    {
        var command = line.Trim();
        if (command.Equals("play", StringComparison.OrdinalIgnoreCase))
        {
            Play();
        }
        else if (command.Equals("pause", StringComparison.OrdinalIgnoreCase))
        {
            Pause();
        }
        else if (command.Equals("next", StringComparison.OrdinalIgnoreCase))
        {
            Next();
        }
        else if (command.StartsWith("position:", StringComparison.OrdinalIgnoreCase))
        {
            var seconds = double.Parse(
                command["position:".Length..],
                System.Globalization.CultureInfo.InvariantCulture);
            SetPosition(TimeSpan.FromSeconds(seconds));
        }
        else if (command.Equals("exit", StringComparison.OrdinalIgnoreCase))
        {
            // handled by the caller after replying
        }
        else
        {
            throw new InvalidOperationException("unknown command: " + command);
        }
    }
}
