namespace LyricsX.Windows.App.Composition;

/// <summary>Per-user directories beneath the injected data root.</summary>
public sealed record AppDirectories(string Root)
{
    public string Cache => Path.Combine(Root, "cache");

    public string Logs => Path.Combine(Root, "logs");

    public string Backups => Path.Combine(Root, "backups");
}
