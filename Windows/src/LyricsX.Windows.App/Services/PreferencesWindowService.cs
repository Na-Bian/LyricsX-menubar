using LyricsX.Core.Storage;
using LyricsX.Platform.Windows;
using LyricsX.Platform.Windows.Media;
using LyricsX.Platform.Windows.Shell;
using LyricsX.Windows.App.Composition;
using LyricsX.Windows.App.Preferences;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace LyricsX.Windows.App.Services;

/// <summary>
/// Single-instance preferences window over the shared view models: six
/// sections (general, display, source, player, filter, shortcut), one Apply
/// bar with inline validation, Esc closes. Source/shortcut sections carry
/// their own side effects (provider rebuild notice, hotkey registration).
/// </summary>
public static class PreferencesWindowService
{
    private static Window? window;

    public static void Show()
    {
        if (window is not null)
        {
            try
            {
                window.Activate();
                return;
            }
            catch (Exception)
            {
                window = null; // closed-window Activate throws; rebuild below
            }
        }

        window = BuildWindow();
        window.Activate();
    }

    private static Window BuildWindow()
    {
        var window = new Window { Title = "LyricsX 偏好设置" };
        window.Closed += (_, _) => window = null;

        var preferences = App.Services.GetRequiredService<PreferencesViewModel>();
        var source = App.Services.GetRequiredService<SourcePreferencesViewModel>();
        var player = App.Services.GetRequiredService<PlayerPreferencesViewModel>();
        var filter = App.Services.GetRequiredService<FilterPreferencesViewModel>();
        var shortcut = App.Services.GetRequiredService<ShortcutPreferencesViewModel>();
        var spotify = App.Services.GetRequiredService<SpotifyConnector>();
        var accessor = App.Services.GetRequiredService<AppSettingsAccessor>();

        var error = new InfoBar { IsOpen = false, Severity = InfoBarSeverity.Error };

        var pivot = new Pivot();
        pivot.Items.Add(Page("通用", GeneralSection(preferences)));
        pivot.Items.Add(Page("显示", DisplaySection(preferences)));
        pivot.Items.Add(Page("来源", SourceSection(source, spotify, accessor)));
        pivot.Items.Add(Page("播放器", PlayerSection(player)));
        pivot.Items.Add(Page("过滤", FilterSection(filter)));
        pivot.Items.Add(Page("快捷键", ShortcutSection(shortcut, accessor)));

        var apply = new Button { Content = "应用", Style = ApplyButtonStyle() };
        apply.Click += async (_, _) =>
        {
            if (!await preferences.ApplyAsync())
            {
                ShowError(error, preferences.ValidationError);
                return;
            }

            if (!string.IsNullOrEmpty(preferences.StartupError))
            {
                ShowError(error, preferences.StartupError);
                return;
            }

            if (!await source.ApplyAsync())
            {
                ShowError(error, source.ValidationError);
                return;
            }

            if (!await player.ApplyAsync())
            {
                ShowError(error, player.ValidationError);
                return;
            }

            if (!await filter.ApplyAsync())
            {
                ShowError(error, filter.ValidationError);
                return;
            }

            ShowInfo(error, "已保存。来源与播放器改动在下次启动 LyricsX 时完全生效。");
        };

        var bar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        bar.Children.Add(apply);

        var root = new Grid { Padding = new Thickness(0) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(pivot, 0);
        Grid.SetRowSpan(pivot, 2);
        Grid.SetRow(error, 0);
        Grid.SetRow(bar, 2);
        root.Children.Add(pivot);
        root.Children.Add(error);
        root.Children.Add(bar);

        var escape = new KeyboardAccelerator { Key = VirtualKey.Escape };
        escape.Invoked += (_, _) => window.Close();
        root.KeyboardAccelerators.Add(escape);

        window.Content = root;
        window.AppWindow?.ResizeClient(new global::Windows.Graphics.SizeInt32(720, 660));

        _ = RefreshPlayerSourcesAsync(player);
        _ = spotify.RefreshStateAsync(CancellationToken.None);
        return window;
    }

    private static PivotItem Page(string header, UIElement content) =>
        new()
        {
            Header = header,
            Content = new ScrollViewer
            {
                Content = new StackPanel { Spacing = 14, Padding = new Thickness(12, 12, 24, 24) }
                .Also(panel => panel.Children.Add(content))
            }
        };

    private static UIElement GeneralSection(PreferencesViewModel viewModel)
    {
        var launch = new ToggleSwitch
        {
            Header = "登录时启动 LyricsX",
            IsOn = viewModel.LaunchAtLogin,
            OnContent = "开",
            OffContent = "关"
        };
        var autoMatch = new ToggleSwitch
        {
            Header = "自动搜索并匹配歌词",
            IsOn = viewModel.AutoMatchLyrics,
            OnContent = "开",
            OffContent = "关"
        };
        var hideNoMedia = new ToggleSwitch
        {
            Header = "无播放媒体时隐藏歌词条",
            IsOn = viewModel.HideWhenNoMedia,
            OnContent = "开",
            OffContent = "关"
        };
        launch.Toggled += (_, _) => viewModel.LaunchAtLogin = launch.IsOn;
        autoMatch.Toggled += (_, _) => viewModel.AutoMatchLyrics = autoMatch.IsOn;
        hideNoMedia.Toggled += (_, _) => viewModel.HideWhenNoMedia = hideNoMedia.IsOn;

        var panel = new StackPanel { Spacing = 14 };
        panel.Children.Add(launch);
        panel.Children.Add(autoMatch);
        panel.Children.Add(hideNoMedia);
        panel.Children.Add(new TextBlock
        {
            Text = "开机自启写入当前用户 Run 注册表项,卸载时自动移除。",
            Opacity = 0.7,
            TextWrapping = TextWrapping.Wrap
        });
        return panel;
    }

    private static UIElement DisplaySection(PreferencesViewModel viewModel)
    {
        var width = new NumberBox
        {
            Header = "任务栏歌词宽度 (80–360)",
            Value = viewModel.WidthDip,
            Minimum = PreferencesViewModel.MinWidthDip,
            Maximum = PreferencesViewModel.MaxWidthDip,
            Width = 260,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact
        };
        var offset = new NumberBox
        {
            Header = "歌词时间偏移(毫秒,±30000)",
            Value = viewModel.UserOffsetMilliseconds,
            Minimum = -PreferencesViewModel.MaxOffsetMilliseconds,
            Maximum = PreferencesViewModel.MaxOffsetMilliseconds,
            Width = 260,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact
        };
        var font = new NumberBox
        {
            Header = "歌词字号 (9–32)",
            Value = viewModel.FontSize,
            Minimum = PreferencesViewModel.MinFontSize,
            Maximum = PreferencesViewModel.MaxFontSize,
            Width = 260
        };
        var whenPaused = new ToggleSwitch
        {
            Header = "暂停时仍显示歌词",
            IsOn = viewModel.ShowWhenPaused,
            OnContent = "开",
            OffContent = "关"
        };
        var nextLine = new ToggleSwitch
        {
            Header = "显示下一句歌词",
            IsOn = viewModel.ShowNextLine,
            OnContent = "开",
            OffContent = "关"
        };
        var scroll = new ToggleSwitch
        {
            Header = "超长歌词跑马灯滚动",
            IsOn = viewModel.AutoScrollLongLines,
            OnContent = "开",
            OffContent = "关"
        };
        var animation = new ToggleSwitch
        {
            Header = "跟随系统动画设置(关闭时省略号截断)",
            IsOn = viewModel.FollowSystemAnimation,
            OnContent = "开",
            OffContent = "关"
        };

        var toggles = new[] { whenPaused, nextLine, scroll, animation };
        foreach (var toggle in toggles)
        {
            toggle.Toggled += (_, _) => ApplyDisplayValues();
        }
        width.ValueChanged += (_, _) => ApplyDisplayValues();
        offset.ValueChanged += (_, _) => ApplyDisplayValues();
        font.ValueChanged += (_, _) => ApplyDisplayValues();

        void ApplyDisplayValues()
        {
            viewModel.WidthDip = (int)width.Value;
            viewModel.UserOffsetMilliseconds = (int)offset.Value;
            viewModel.FontSize = font.Value;
            viewModel.ShowWhenPaused = whenPaused.IsOn;
            viewModel.ShowNextLine = nextLine.IsOn;
            viewModel.AutoScrollLongLines = scroll.IsOn;
            viewModel.FollowSystemAnimation = animation.IsOn;
        }

        var panel = new StackPanel { Spacing = 14 };
        panel.Children.Add(width);
        panel.Children.Add(offset);
        panel.Children.Add(font);
        foreach (var toggle in toggles)
        {
            panel.Children.Add(toggle);
        }
        return panel;
    }

    private static UIElement SourceSection(
        SourcePreferencesViewModel viewModel,
        SpotifyConnector spotify,
        AppSettingsAccessor accessor)
    {
        var listHost = new StackPanel { Spacing = 6 };
        void Render()
        {
            listHost.Children.Clear();
            foreach (var provider in viewModel.Providers)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                var toggle = new CheckBox
                {
                    Content = provider.DisplayName,
                    IsChecked = provider.Enabled,
                    MinWidth = 200
                };
                toggle.Checked += (_, _) => provider.Enabled = true;
                toggle.Unchecked += (_, _) => provider.Enabled = false;
                var up = new Button { Content = "上移" };
                up.Click += (_, _) => { viewModel.MoveUp(provider); Render(); };
                var down = new Button { Content = "下移" };
                down.Click += (_, _) => { viewModel.MoveDown(provider); Render(); };
                row.Children.Add(toggle);
                row.Children.Add(up);
                row.Children.Add(down);
                listHost.Children.Add(row);
            }
        }

        Render();

        var musixmatch = new PasswordBox
        {
            Header = "Musixmatch User Token(可选)",
            Password = accessor.Current.MusixmatchToken
        };
        var clientId = new TextBox
        {
            Header = "Spotify Client ID",
            Text = accessor.Current.SpotifyClientId,
            Width = 360,
            PlaceholderText = "Spotify 开发者后台获取"
        };
        var spotifyState = new TextBlock { Opacity = 0.7, TextWrapping = TextWrapping.Wrap };
        var connect = new Button { Content = viewModel.SpotifyConnectLabel };
        var disconnect = new Button { Content = viewModel.SpotifyDisconnectLabel };
        connect.IsEnabled = false;
        disconnect.IsEnabled = false;

        _ = UpdateSpotifyStateAsync();

        async Task UpdateSpotifyStateAsync()
        {
            await spotify.RefreshStateAsync(CancellationToken.None);
            spotifyState.Text = spotify.HasStoredToken
                ? "已连接:refresh token 保存在 Windows 凭据管理器。"
                : "未连接。";
            connect.IsEnabled = true;
            disconnect.IsEnabled = spotify.HasStoredToken;
        }

        connect.Click += async (_, _) =>
        {
            connect.IsEnabled = false;
            spotifyState.Text = "已打开浏览器,请完成 Spotify 授权…";
            var (connected, connectError) = await spotify.ConnectAsync(
                clientId.Text, CancellationToken.None);
            if (connected)
            {
                await accessor.ReplaceAsync(accessor.Current with
                {
                    SpotifyClientId = clientId.Text.Trim()
                });
            }

            spotifyState.Text = connected
                ? "已连接:refresh token 保存在 Windows 凭据管理器。"
                : connectError ?? "连接失败。";
            connect.IsEnabled = true;
            disconnect.IsEnabled = spotify.HasStoredToken;
        };
        disconnect.Click += async (_, _) =>
        {
            await spotify.DisconnectAsync(CancellationToken.None);
            spotifyState.Text = "已断开:凭据管理器中的 refresh token 已删除。";
            disconnect.IsEnabled = false;
        };

        var panel = new StackPanel { Spacing = 14 };
        panel.Children.Add(new TextBlock
        {
            Text = "歌词来源(顺序即匹配质量相同时的优先级)",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        panel.Children.Add(listHost);
        panel.Children.Add(new TextBlock
        {
            Text = "至少启用一个来源;改动在下次启动 LyricsX 时生效。",
            Opacity = 0.7,
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(musixmatch);
        panel.Children.Add(clientId);
        panel.Children.Add(spotifyState);
        var spotifyButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        spotifyButtons.Children.Add(connect);
        spotifyButtons.Children.Add(disconnect);
        panel.Children.Add(spotifyButtons);
        return panel;
    }

    private static UIElement PlayerSection(PlayerPreferencesViewModel viewModel)
    {
        var automatic = viewModel.AutomaticLabel;
        var options = new System.Collections.ObjectModel.ObservableCollection<string> { automatic };
        foreach (var known in viewModel.KnownSources)
        {
            options.Add(known);
        }

        viewModel.KnownSources.CollectionChanged += (_, args) =>
        {
            if (args.NewItems is null)
            {
                return;
            }

            foreach (string added in args.NewItems)
            {
                if (!options.Contains(added, StringComparer.OrdinalIgnoreCase) &&
                    !string.Equals(added, automatic, StringComparison.OrdinalIgnoreCase))
                {
                    options.Add(added);
                }
            }
        };

        var selector = new ComboBox
        {
            Header = "跟踪的播放器",
            ItemsSource = options,
            MinWidth = 360
        };
        selector.SelectedItem = string.IsNullOrEmpty(viewModel.SelectedSourceId)
            ? (object)automatic
            : viewModel.SelectedSourceId;
        selector.SelectionChanged += (_, _) =>
        {
            viewModel.SelectedSourceId = selector.SelectedItem as string == automatic
                ? PlayerPreferencesViewModel.AutomaticSourceId
                : selector.SelectedItem as string ?? PlayerPreferencesViewModel.AutomaticSourceId;
        };

        var panel = new StackPanel { Spacing = 14 };
        panel.Children.Add(selector);
        panel.Children.Add(new TextBlock
        {
            Text = "播放器通过系统媒体传输(SMTC)暴露播放信息。自动模式按「正在播放 → 最近更新」选择;" +
                "指定播放器仅在该应用未运行时回退到自动。改动立即保存并生效。",
            Opacity = 0.7,
            TextWrapping = TextWrapping.Wrap
        });
        return panel;
    }

    private static UIElement FilterSection(FilterPreferencesViewModel viewModel)
    {
        var strict = new ToggleSwitch
        {
            Header = "严格匹配(标题与艺术家都需命中)",
            IsOn = viewModel.StrictSearch,
            OnContent = "开",
            OffContent = "关"
        };
        strict.Toggled += (_, _) => viewModel.StrictSearch = strict.IsOn;

        var phrases = new ListView
        {
            ItemsSource = viewModel.BlockedPhrases,
            MinHeight = 96,
            MaxHeight = 200,
            SelectionMode = ListViewSelectionMode.Single
        };
        var input = new TextBox { Header = "新增屏蔽词", PlaceholderText = "如: 纯音乐, 作词, 作曲", Width = 300 };
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(FilterPreferencesViewModel.NewPhrase) &&
                input.Text != viewModel.NewPhrase)
            {
                input.Text = viewModel.NewPhrase ?? string.Empty;
            }
        };
        var add = new Button { Content = "添加" };
        add.Click += (_, _) => viewModel.AddPhrase(input.Text);
        var remove = new Button { Content = "删除所选" };
        remove.Click += (_, _) =>
        {
            if (phrases.SelectedItem is string phrase)
            {
                viewModel.RemovePhrase(phrase);
            }
        };

        var ignored = new TextBlock();
        void RenderIgnored() => ignored.Text =
            $"已忽略歌曲:{viewModel.IgnoredTrackCount} 首,已忽略专辑:{viewModel.IgnoredAlbumCount} 张";
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(FilterPreferencesViewModel.IgnoredTrackCount) ||
                args.PropertyName == nameof(FilterPreferencesViewModel.IgnoredAlbumCount))
            {
                RenderIgnored();
            }
        };
        RenderIgnored();

        var clearIgnored = new Button { Content = "清除全部忽略" };
        clearIgnored.Click += async (_, _) => await viewModel.ClearIgnoredAsync();

        var panel = new StackPanel { Spacing = 14 };
        panel.Children.Add(strict);
        panel.Children.Add(new TextBlock
        {
            Text = "屏蔽词(从每首获取到的歌词中删除对应行)",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        panel.Children.Add(phrases);
        var phraseButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        phraseButtons.Children.Add(input);
        phraseButtons.Children.Add(add);
        phraseButtons.Children.Add(remove);
        panel.Children.Add(phraseButtons);
        panel.Children.Add(new TextBlock
        {
            Text = "忽略列表(来自菜单「忽略这首歌」)",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        panel.Children.Add(ignored);
        panel.Children.Add(clearIgnored);
        return panel;
    }

    private static UIElement ShortcutSection(
        ShortcutPreferencesViewModel viewModel,
        AppSettingsAccessor accessor)
    {
        var actions = new (string Id, string Label)[]
        {
            (HotKeySettings.ShowPreferencesAction, "打开偏好设置"),
            (HotKeySettings.ToggleVisibilityAction, "显示/隐藏任务栏歌词"),
            (HotKeySettings.EarlierOffsetAction, "歌词提前 0.5 秒"),
            (HotKeySettings.LaterOffsetAction, "歌词延后 0.5 秒")
        };

        var committed = HotKeySettings.Parse(
            accessor.Current.Extra is not null &&
            accessor.Current.Extra.TryGetValue(HotKeySettings.SettingsKey, out var serialized)
                ? serialized
                : null)
            .ToDictionary(binding => binding.ActionId);

        var boxes = new Dictionary<string, TextBox>();
        var state = new InfoBar { IsOpen = false };

        foreach (var (id, label) in actions)
        {
            var box = new TextBox
            {
                Header = label + "(点击输入框后按下组合键)",
                IsReadOnly = false,
                Width = 300,
                Text = committed.TryGetValue(id, out var binding)
                    ? HotKeyBindingText(binding.Modifiers, binding.VirtualKey)
                    : "未设置"
            };
            box.KeyDown += (_, args) =>
            {
                var modifiers = ToHotKeyModifiers(CurrentModifiers());
                if (modifiers == HotKeyModifiers.None || !IsBindableKey(args.Key))
                {
                    return;
                }

                var virtualKey = (uint)args.Key;
                if (!viewModel.ValidateBinding(modifiers, virtualKey))
                {
                    state.Message = viewModel.ValidationError;
                    state.Severity = InfoBarSeverity.Warning;
                    state.IsOpen = true;
                    args.Handled = true;
                    return;
                }

                viewModel.SetBinding(id, modifiers, virtualKey);
                box.Text = HotKeyBindingText(modifiers, virtualKey);
                state.IsOpen = false;
                args.Handled = true;
            };
            boxes[id] = box;
        }

        var apply = new Button { Content = "注册并保存快捷键" };
        apply.Click += async (_, _) =>
        {
            if (!await viewModel.ApplyAsync())
            {
                state.Message = viewModel.ValidationError;
                state.Severity = InfoBarSeverity.Error;
                state.IsOpen = true;
                return;
            }

            state.Message = "快捷键已注册并保存,重启 LyricsX 后由启动流程接管。";
            state.Severity = InfoBarSeverity.Success;
            state.IsOpen = true;
        };
        var cancel = new Button { Content = "取消修改" };
        cancel.Click += (_, _) =>
        {
            viewModel.Cancel();
            state.IsOpen = false;
            foreach (var (id, box) in boxes)
            {
                box.Text = committed.TryGetValue(id, out var binding)
                    ? HotKeyBindingText(binding.Modifiers, binding.VirtualKey)
                    : "未设置";
            }
        };

        var panel = new StackPanel { Spacing = 14 };
        foreach (var (_, box) in boxes)
        {
            panel.Children.Add(box);
        }
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        buttons.Children.Add(apply);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);
        panel.Children.Add(state);
        panel.Children.Add(new TextBlock
        {
            Text = "快捷键必须包含 Ctrl/Alt/Shift 修饰键;Win+D 等 Windows 保留组合会被拒绝。",
            Opacity = 0.7,
            TextWrapping = TextWrapping.Wrap
        });
        return panel;
    }

    private static async Task RefreshPlayerSourcesAsync(PlayerPreferencesViewModel viewModel)
    {
        try
        {
            var source = App.Services.GetRequiredService<IMediaSessionSource>();
            var sessions = await source.GetSessionsAsync(CancellationToken.None);
            viewModel.UpdateKnownSources(sessions.Select(session => session.SourceAppId));
        }
        catch (Exception)
        {
            // GSMTC enumeration can fail on some builds; the automatic option
            // always remains available
        }
    }

    private static VirtualKeyModifiers CurrentModifiers()
    {
        var result = VirtualKeyModifiers.None;
        if (IsModifierDown(VirtualKey.Control))
        {
            result |= VirtualKeyModifiers.Control;
        }

        if (IsModifierDown(VirtualKey.Menu))
        {
            result |= VirtualKeyModifiers.Menu;
        }

        if (IsModifierDown(VirtualKey.Shift))
        {
            result |= VirtualKeyModifiers.Shift;
        }

        if (IsModifierDown(VirtualKey.LeftWindows) || IsModifierDown(VirtualKey.RightWindows))
        {
            result |= VirtualKeyModifiers.Windows;
        }

        return result;
    }

    private static bool IsModifierDown(VirtualKey key) =>
        Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(key)
            .HasFlag(global::Windows.UI.Core.CoreVirtualKeyStates.Down);

    private static HotKeyModifiers ToHotKeyModifiers(VirtualKeyModifiers modifiers)
    {
        var result = HotKeyModifiers.None;
        if (modifiers.HasFlag(VirtualKeyModifiers.Control))
        {
            result |= HotKeyModifiers.Control;
        }

        if (modifiers.HasFlag(VirtualKeyModifiers.Menu))
        {
            result |= HotKeyModifiers.Alt;
        }

        if (modifiers.HasFlag(VirtualKeyModifiers.Shift))
        {
            result |= HotKeyModifiers.Shift;
        }

        if (modifiers.HasFlag(VirtualKeyModifiers.Windows))
        {
            result |= HotKeyModifiers.Windows;
        }

        return result;
    }

    private static bool IsBindableKey(VirtualKey key) =>
        key is >= VirtualKey.A and <= VirtualKey.Z ||
        key is >= VirtualKey.Number0 and <= VirtualKey.Number9 ||
        key is >= VirtualKey.F1 and <= VirtualKey.F12 ||
        key is >= VirtualKey.NumberPad0 and <= VirtualKey.NumberPad9 ||
        key == VirtualKey.Space;

    private static string HotKeyBindingText(HotKeyModifiers modifiers, uint virtualKey)
    {
        var text = new List<string>();
        if (modifiers.HasFlag(HotKeyModifiers.Control))
        {
            text.Add("Ctrl");
        }

        if (modifiers.HasFlag(HotKeyModifiers.Alt))
        {
            text.Add("Alt");
        }

        if (modifiers.HasFlag(HotKeyModifiers.Shift))
        {
            text.Add("Shift");
        }

        if (modifiers.HasFlag(HotKeyModifiers.Windows))
        {
            text.Add("Win");
        }

        text.Add(KeyName(virtualKey));
        return string.Join("+", text);
    }

    private static string KeyName(uint virtualKey) => virtualKey switch
    {
        >= 0x41 and <= 0x5A => ((char)virtualKey).ToString(),
        >= 0x30 and <= 0x39 => ((char)virtualKey).ToString(),
        >= 0x70 and <= 0x87 => "F" + (virtualKey - 0x6F),
        0x20 => "Space",
        >= 0x60 and <= 0x69 => "Num" + (virtualKey - 0x60),
        _ => "0x" + virtualKey.ToString("X")
    };

    private static void ShowError(InfoBar error, string? message)
    {
        error.Message = message ?? "保存失败";
        error.Severity = InfoBarSeverity.Error;
        error.IsOpen = true;
    }

    private static void ShowInfo(InfoBar error, string message)
    {
        error.Message = message;
        error.Severity = InfoBarSeverity.Success;
        error.IsOpen = true;
    }

    private static Microsoft.UI.Xaml.Style? ApplyButtonStyle() =>
        Application.Current.Resources.TryGetValue("AccentButtonStyle", out var style)
            ? style as Microsoft.UI.Xaml.Style
            : null;
}

file static class UiExtensions
{
    public static T Also<T>(this T element, Action<T> action)
    {
        action(element);
        return element;
    }
}
