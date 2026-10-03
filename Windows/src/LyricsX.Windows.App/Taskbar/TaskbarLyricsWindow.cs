using LyricsX.Platform.Windows;
using LyricsX.Platform.Windows.Windowing;
using LyricsX.Windows.App.Menus;
using LyricsX.Windows.App.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace LyricsX.Windows.App.Taskbar;

/// <summary>
/// The lyric surface hosted over the taskbar: borderless, non-activating,
/// topmost, built in code. One real Canvas carries two text blocks for the
/// seamless marquee seam; the state machine advances on wall-clock time.
/// </summary>
public sealed class TaskbarLyricsWindow : Window
{
    private readonly TaskbarLyricsViewModel viewModel = new();
    private readonly WindowStyleService styles;
    private readonly DispatcherTimer marqueeTimer;
    private readonly Canvas marqueeCanvas = new();
    private readonly TextBlock marqueePrimary = CreateTextBlock();
    private readonly TextBlock marqueeSecondary = CreateTextBlock();
    private MarqueeStateMachine? marquee;
    private DateTimeOffset lastMarqueeTick = DateTimeOffset.UtcNow;

    public TaskbarLyricsViewModel ViewModel => viewModel;

    private LyricsMenuViewModel? menuViewModel;

    internal void AttachMenus(LyricsMenuViewModel menuViewModel)
    {
        this.menuViewModel = menuViewModel;

        var compact = BuildMenu(isFull: false);
        var full = BuildMenu(isFull: true);
        FlyoutBase.SetAttachedFlyout(marqueeCanvas, compact);
        fullFlyout = full;

        marqueeCanvas.PointerReleased += OnPointerReleased;
        marqueeCanvas.KeyDown += OnKeyDown;
    }

    private MenuFlyout? fullFlyout;

    private MenuFlyout BuildMenu(bool isFull)
    {
        var menu = new MenuFlyout();
        if (menuViewModel is null)
        {
            return menu;
        }

        var header = new MenuFlyoutItem
        {
            Text = menuViewModel.CurrentTrackTitle,
            IsEnabled = false
        };
        menu.Items.Add(header);

        menu.Items.Add(Sep());
        menu.Items.Add(Item("歌词提前 0.5 秒", menuViewModel.EarlierByHalfSecondCommand));
        menu.Items.Add(Item("歌词延后 0.5 秒", menuViewModel.LaterByHalfSecondCommand));

        if (isFull)
        {
            menu.Items.Add(Item("重置歌词偏移", menuViewModel.ResetOffsetCommand));
        }

        menu.Items.Add(Item("手动搜索歌词…", menuViewModel.ManualSearchCommand));
        menu.Items.Add(Item(menuViewModel.VisibilityToggleText,
            menuViewModel.ToggleVisibilityCommand));

        if (isFull)
        {
            menu.Items.Add(Sep());
            menu.Items.Add(Item("打开本地歌词…", menuViewModel.OpenLocalCommand));
            menu.Items.Add(Item("导出当前歌词…", menuViewModel.ExportCommand));
            menu.Items.Add(Item("忽略这首歌", menuViewModel.IgnoreTrackCommand));
            menu.Items.Add(Sep());
            menu.Items.Add(Item("打开日志文件夹", menuViewModel.OpenLogsCommand));
            menu.Items.Add(Item("复制诊断信息", menuViewModel.CopyDiagnosticsCommand));
            menu.Items.Add(Sep());
        }

        menu.Items.Add(Item("偏好设置…", menuViewModel.PreferencesCommand));

        if (isFull)
        {
            menu.Items.Add(Item("退出 LyricsX", menuViewModel.ExitCommand));
        }

        return menu;
    }

    private static MenuFlyoutItem Item(string text, CommunityToolkit.Mvvm.Input.IRelayCommand command)
    {
        var item = new MenuFlyoutItem { Text = text };
        item.Command = command;
        return item;
    }

    private static MenuFlyoutSeparator Sep() => new();

    public TaskbarLyricsWindow()
    {
        Title = "LyricsX Taskbar Lyrics";
        marqueeCanvas.Children.Add(marqueePrimary);
        marqueeCanvas.Children.Add(marqueeSecondary);
        Content = marqueeCanvas;

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        styles = new WindowStyleService(new Win32WindowApi());
        styles.ApplyNonActivatingStyle(hwnd);

        marqueeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        marqueeTimer.Tick += (_, _) => AdvanceMarquee();
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(marqueeCanvas);
        var kind = point.Properties.PointerUpdateKind;
        var isRight = kind == Microsoft.UI.Input.PointerUpdateKind.RightButtonReleased;
        var isLeft = kind == Microsoft.UI.Input.PointerUpdateKind.LeftButtonReleased;
        if (!isRight && !isLeft)
        {
            return;
        }

        var menu = isRight && fullFlyout is not null ? fullFlyout : BuildMenu(isFull: false);
        FlyoutBase.SetAttachedFlyout(marqueeCanvas, menu);
        FlyoutBase.ShowAttachedFlyout(marqueeCanvas);
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs args)
    {
        // Menu (Application) key → full menu (keyboard accessibility)
        if (args.Key == global::Windows.System.VirtualKey.Application)
        {
            var menu = BuildMenu(isFull: true);
            FlyoutBase.SetAttachedFlyout(marqueeCanvas, menu);
            FlyoutBase.ShowAttachedFlyout(marqueeCanvas);
        }
    }

    public void ApplyState(LyricsPresentationState state)
    {
        var textChanged = viewModel.Text != state.StatusText;
        viewModel.ApplyState(state);
        if (textChanged)
        {
            marqueePrimary.Text = viewModel.Text;
            AutomationProperties.SetName(marqueePrimary, viewModel.Text);
            marqueeSecondary.Text = viewModel.Text;
            marquee = null; // re-arm once the new text has been measured
        }

        var animate = viewModel.IsVisible && marqueeCanvas.ActualWidth >= 120;
        if (animate && marquee is null)
        {
            var textWidth = marqueePrimary.ActualWidth > 0
                ? marqueePrimary.ActualWidth
                : marqueeCanvas.ActualWidth;
            marquee = new MarqueeStateMachine(
                viewportWidth: marqueeCanvas.ActualWidth - 16,
                textWidth: textWidth,
                speedPixelsPerSecond: 30);
            lastMarqueeTick = DateTimeOffset.UtcNow;
            marqueeTimer.Start();
            marqueePrimary.TextTrimming = TextTrimming.None;
        }
        else if (!animate)
        {
            marqueeTimer.Stop();
            marquee = null;
            marqueePrimary.TextTrimming = TextTrimming.CharacterEllipsis;
            Canvas.SetLeft(marqueePrimary, 8.0);
        }
    }

    public void ApplyPlacement(TaskbarPlacement placement)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        styles.ApplyNonActivatingStyle(hwnd);
        styles.ShowAt(hwnd, placement);
        // visibility is placement-driven only: never Activate() here, the
        // lyrics surface must never steal focus
        if (!placement.IsVisible)
        {
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId).Hide();
        }
    }

    private void AdvanceMarquee()
    {
        if (marquee is null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var delta = now - lastMarqueeTick;
        lastMarqueeTick = now;

        var (offset, _) = marquee.Advance(delta);
        Canvas.SetLeft(marqueePrimary, 8.0 + offset);
        Canvas.SetLeft(marqueeSecondary, 8.0 + offset + marquee.CycleTravelSeconds * 30);
    }

    private static TextBlock CreateTextBlock()
    {
        var textBlock = new TextBlock
        {
            FontFamily = new FontFamily("Segoe UI Variable Text"),
            FontSize = 13,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Canvas.SetLeft(textBlock, 8.0);
        return textBlock;
    }
}
