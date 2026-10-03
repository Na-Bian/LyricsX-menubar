using LyricsX.Windows.App.Preferences;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LyricsX.Windows.App.Services;

/// <summary>
/// Single-instance preferences window over the shared view model; created on
/// demand and activated when already open.
/// </summary>
public static class PreferencesWindowService
{
    private static Window? window;

    public static void Show()
    {
        if (window is not null)
        {
            window.Activate();
            return;
        }

        window = new Window { Title = "LyricsX 偏好设置" };
        var viewModel = App.Services.GetRequiredService<PreferencesViewModel>();

        var widthBox = new NumberBox
        {
            Header = "任务栏歌词宽度 (80–360)",
            Value = viewModel.WidthDip,
            Minimum = 80,
            Maximum = 360,
            Width = 240
        };
        var offsetBox = new NumberBox
        {
            Header = "歌词时间偏移(毫秒,±30000)",
            Value = viewModel.UserOffsetMilliseconds,
            Width = 240
        };
        var error = new InfoBar { IsOpen = false, Severity = InfoBarSeverity.Error };
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(PreferencesViewModel.ValidationError))
            {
                error.Message = viewModel.ValidationError;
                error.IsOpen = viewModel.ValidationError is not null;
            }
        };

        var apply = new Button { Content = "应用" };
        apply.Click += async (_, _) =>
        {
            viewModel.WidthDip = (int)widthBox.Value;
            viewModel.UserOffsetMilliseconds = (int)offsetBox.Value;
            if (!await viewModel.ApplyAsync())
            {
                error.Message = viewModel.ValidationError;
                error.IsOpen = true;
            }
        };

        var panel = new StackPanel { Spacing = 12, Padding = new Thickness(24) };
        panel.Children.Add(widthBox);
        panel.Children.Add(offsetBox);
        panel.Children.Add(error);
        panel.Children.Add(apply);
        window.Content = new ScrollViewer { Content = panel };
        window.Activate();
    }
}
