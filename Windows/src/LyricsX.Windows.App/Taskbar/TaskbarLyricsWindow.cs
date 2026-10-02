using LyricsX.Platform.Windows;
using LyricsX.Platform.Windows.Windowing;
using LyricsX.Windows.App.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
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
