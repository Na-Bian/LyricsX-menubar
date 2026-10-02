using LyricsX.Platform.Windows.Media;

// Redacted GSMTC probe: prints source app ID, playback status, position, and
// track title/artist once per session. Never prints album art URIs, account
// identifiers, or track IDs (which mix in media properties).
var source = new GlobalMediaSessionSource();
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
await source.StartAsync(timeout.Token);
var sessions = await source.GetSessionsAsync(timeout.Token);

if (sessions.Count == 0)
{
    Console.WriteLine("no-media-sessions");
    return;
}

foreach (var session in sessions)
{
    var track = session.Track;
    Console.WriteLine(
        $"app={session.SourceAppId} status={session.Status} " +
        $"position={session.Position.TotalSeconds:0.#}s " +
        $"title={(track?.Title.Length > 0 ? track.Title : "(none)")} " +
        $"artist={(track?.Artist.Length > 0 ? track.Artist : "(none)")}");
}
