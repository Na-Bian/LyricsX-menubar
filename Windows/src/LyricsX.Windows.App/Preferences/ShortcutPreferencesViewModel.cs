using CommunityToolkit.Mvvm.ComponentModel;
using LyricsX.Core.Storage;
using LyricsX.Platform.Windows.Shell;
using LyricsX.Windows.App.Composition;

namespace LyricsX.Windows.App.Preferences;

public sealed record HotKeyBinding(string ActionId, HotKeyModifiers Modifiers, uint VirtualKey);

/// <summary>
/// Shortcut page: capture modifier+key bindings, reject bare keys and
/// Windows-reserved combinations, register on apply, and restore prior
/// registrations on cancel.
/// </summary>
public partial class ShortcutPreferencesViewModel : ObservableObject
{
    private static readonly HashSet<uint> WindowsReservedKeys = new()
    {
        (uint)'D', // Win+D show desktop
        (uint)'L', // Win+L lock (blocked at OS level anyway)
        (uint)'E', (uint)'R', (uint)'S', (uint)'I', (uint)0x09 /* VK_TAB */
    };

    private readonly AppSettingsAccessor settingsAccessor;
    private readonly IHotKeyNativeApi native;
    private readonly Dictionary<string, HotKeyBinding> committed = [];
    private readonly Dictionary<string, HotKeyBinding> pending = [];

    [ObservableProperty]
    public partial string? ValidationError { get; set; }

    public ShortcutPreferencesViewModel(AppSettingsAccessor settingsAccessor, IHotKeyNativeApi native)
    {
        this.settingsAccessor = settingsAccessor;
        this.native = native;
    }

    public void SetBinding(string actionId, HotKeyModifiers modifiers, uint virtualKey)
    {
        pending[actionId] = new HotKeyBinding(actionId, modifiers, virtualKey);
    }

    public bool ValidateBinding(HotKeyModifiers modifiers, uint virtualKey)
    {
        if (modifiers == HotKeyModifiers.None)
        {
            ValidationError = "快捷键必须包含修饰键(Ctrl/Alt/Shift)";
            return false;
        }

        if (modifiers.HasFlag(HotKeyModifiers.Windows) && WindowsReservedKeys.Contains(virtualKey))
        {
            ValidationError = "该组合为 Windows 保留快捷键";
            return false;
        }

        ValidationError = null;
        return true;
    }

    public async Task<bool> ApplyAsync()
    {
        foreach (var binding in pending.Values)
        {
            if (!ValidateBinding(binding.Modifiers, binding.VirtualKey))
            {
                return false;
            }

            // registration test before saving; id 0x5100+index is a process-
            // unique registration slot for the settings preview
            if (!native.Register(nint.Zero, 0x5100 + committed.Count,
                    (uint)binding.Modifiers, binding.VirtualKey))
            {
                ValidationError = "快捷键已被其他程序占用";
                return false;
            }
        }

        foreach (var binding in pending)
        {
            committed[binding.Key] = binding.Value;
        }

        var shortcutText = string.Join("|", committed.Values.Select(value =>
            $"{value.ActionId}:{(int)value.Modifiers}:{value.VirtualKey}"));
        await settingsAccessor.ReplaceAsync(settingsAccessor.Current with
        {
            Extra = new Dictionary<string, string> { ["shortcuts"] = shortcutText }
        });
        pending.Clear();
        return true;
    }

    public void Cancel()
    {
        // drop uncommitted bindings; committed registrations stay as-is
        pending.Clear();
        ValidationError = null;
    }
}
