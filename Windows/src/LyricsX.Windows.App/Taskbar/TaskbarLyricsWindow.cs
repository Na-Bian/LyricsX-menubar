using LyricsX.Platform.Windows;
using LyricsX.Platform.Windows.Windowing;
using LyricsX.Windows.App.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace LyricsX.Windows.App.Taskbar;

/// <summary>
/// The lyric surface hosted over the taskbar: borderless, non-activating,
/// topmost. Built in code (no XAML compilation): one clipped canvas with two
/// text blocks for the seamless marquee seam.
/// </summary>
public sealed class TaskbarLyricsWindow : Window
{
    private readonly TaskbarLyricsViewModel viewModel = new();
    private readonly WindowStyleService styles;
    private readonly DispatcherTimer marqueeTimer;
    private readonly Grid marqueeCanvas = new();
    private readonly TextBlock marqueePrimary = CreateTextBlock("当前歌词");
    private readonly TextBlock marqueeSecondary = CreateTextBlock("当前歌词");
    private MarqueeStateMachine? marquee;

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
        viewModel.ApplyState(state);
        marqueePrimary.Text = viewModel.Text;
        AutomationProperties.SetName(marqueePrimary, viewModel.Text);
        marqueeSecondary.Text = viewModel.Text;

        // narrow taskbars get ellipsis instead of scrolling text
        var animate = viewModel.IsVisible && marqueeCanvas.ActualWidth >= 120;
        if (animate)
        {
            marquee = new MarqueeStateMachine(
                viewportWidth: marqueeCanvas.ActualWidth - 16,
                textWidth: marqueePrimary.ActualWidth > 0
                    ? marqueePrimary.ActualWidth
                    : marqueeCanvas.ActualWidth,
                speedPixelsPerSecond: 30,
                reducedMotion: false);
            marqueeTimer.Start();
            marqueePrimary.TextTrimming = TextTrimming.None;
        }
        else
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
        if (placement.IsVisible)
        {
            Activate();
        }
        else
        {
            var appWindow = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var window = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(appWindow);
            window.Hide();
        }
    }

    private void AdvanceMarquee()
    {
        if (marquee is null)
        {
            return;
        }

        var (offset, _) = marquee.Advance(TimeSpan.FromMilliseconds(33));
        Canvas.SetLeft(marqueePrimary, 8.0 + offset);
        Canvas.SetLeft(marqueeSecondary, 8.0 + offset + marquee.CycleTravelSeconds * 30);
    }

    private static TextBlock CreateTextBlock(string automationName)
    {
        var textBlock = new TextBlock
        {
            FontFamily = new FontFamily("Segoe UI Variable Text"),
            FontSize = 13,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Canvas.SetLeft(textBlock, 8.0);
        AutomationProperties.SetName(textBlock, automationName);
        return textBlock;
    }
}
