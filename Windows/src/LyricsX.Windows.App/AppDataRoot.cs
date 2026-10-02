namespace LyricsX.Windows.App;

/// <summary>Resolves the per-user application data root.</summary>
public static class AppDataRoot
{
    public static string Resolve()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "LyricsX");
    }
}
