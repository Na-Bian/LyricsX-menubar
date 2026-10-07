using LyricsX.Platform.Windows.Shell;

namespace LyricsX.Windows.App.Preferences;

/// <summary>
/// Parses the persisted shortcut list ("actionId:modifiers:vk|…", stored in
/// settings Extra["shortcuts"]) back into bindings. Malformed segments are
/// skipped, never fatal, so a hand-edited settings file cannot break startup.
/// </summary>
public static class HotKeySettings
{
    public const string SettingsKey = "shortcuts";

    public const string ShowPreferencesAction = "show-preferences";

    public const string ToggleVisibilityAction = "toggle-visibility";

    public const string EarlierOffsetAction = "earlier-offset";

    public const string LaterOffsetAction = "later-offset";

    public static string Serialize(IEnumerable<HotKeyBinding> bindings) => string.Join("|",
        bindings.Select(binding =>
            $"{binding.ActionId}:{(int)binding.Modifiers}:{binding.VirtualKey}"));

    public static IReadOnlyList<HotKeyBinding> Parse(string? serialized)
    {
        if (string.IsNullOrWhiteSpace(serialized))
        {
            return [];
        }

        // composite flag values (e.g. Ctrl+Alt) are not enum members, so
        // validation checks that every bit is a known modifier flag
        var knownFlags = (int)(HotKeyModifiers.Alt | HotKeyModifiers.Control |
            HotKeyModifiers.Shift | HotKeyModifiers.Windows);

        var bindings = new List<HotKeyBinding>();
        foreach (var segment in serialized.Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = segment.Split(':');
            if (parts.Length != 3)
            {
                continue;
            }

            if (!int.TryParse(parts[1], out var modifiers) ||
                !uint.TryParse(parts[2], out var virtualKey) ||
                modifiers == 0 ||
                (modifiers & ~knownFlags) != 0)
            {
                continue;
            }

            bindings.Add(new HotKeyBinding(
                parts[0], (HotKeyModifiers)modifiers, virtualKey));
        }

        return bindings;
    }
}
