using global::LyricsX.Core;
using global::LyricsX.Core.Matching;
using global::LyricsX.Core.Search;
using global::LyricsX.Core.Storage;
using global::LyricsX.Windows.App.LyricFiles;
using global::LyricsX.Windows.App.Search;
using global::LyricsX.Windows.App.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LyricsX.Windows.App.Search;

/// <summary>
/// Single-instance manual search window: prefilled metadata, incremental
/// results, use-once / always-use / remove-association, and local LRC import
/// through FileOpenPicker (HWND-initialized for the unpackaged desktop app).
/// </summary>
public static class ManualSearchWindowService
{
    private static Window? window;

    private static readonly LocalLyricFileService FileService = new();

    public static void Show(Microsoft.UI.WindowId ownerWindowId)
    {
        if (window is not null)
        {
            try
            {
                window.Activate();
                return;
            }
            catch
            {
                window = null; // closed-window Activate throws; rebuild below
            }
        }

        window = BuildWindow(ownerWindowId);
        window.Activate();
    }

    private static Window BuildWindow(Microsoft.UI.WindowId ownerWindowId)
    {
        var window = new Window { Title = "手动搜索歌词" };
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);

        var searchCoordinator = App.Services.GetRequiredService<ILyricsSearchCoordinator>();
        var associations = App.Services.GetRequiredService<ManualAssociationStore>();
        var cache = App.Services.GetRequiredService<LyricCache>();
        var coordinator = App.Services.GetRequiredService<ILyricsApplicationCoordinator>();

        var viewModel = new ManualSearchViewModel(searchCoordinator, associations, cache, coordinator)
        {
            Title = coordinator.Current.Playback.Track?.Title ?? string.Empty,
            Artist = coordinator.Current.Playback.Track?.Artist ?? string.Empty,
            CurrentTrackFingerprint = coordinator.CurrentFingerprint
        };

        var titleBox = new TextBox { Header = "歌曲名", Text = viewModel.Title, Width = 360 };
        var artistBox = new TextBox { Header = "艺术家", Text = viewModel.Artist, Width = 360 };
        var searchButton = new Button { Content = "搜索" };
        var resultsList = new ListView
        {
            Width = 700,
            Height = 320,
            SelectionMode = ListViewSelectionMode.Single
        };

        searchButton.Click += async (_, _) =>
        {
            viewModel.Title = titleBox.Text;
            viewModel.Artist = artistBox.Text;
            await viewModel.SearchAsync(CancellationToken.None);

            resultsList.Items.Clear();
            foreach (var result in viewModel.Results)
            {
                resultsList.Items.Add(
                    $"{result.ProviderId}  |  {result.Title}  |  {result.Artist}  |  匹配 {result.MatchSummary}");
            }
        };

        var useOnce = new Button { Content = "仅本次使用" };
        var always = new Button { Content = "始终用于这首歌" };
        var removeAssociation = new Button { Content = "取消关联" };
        var importButton = new Button { Content = "打开本地歌词…" };

        useOnce.Click += async (_, _) => await ApplySelectedAsync(
            viewModel, resultsList,
            candidate => viewModel.UseOnceAsync(candidate));
        always.Click += async (_, _) => await ApplySelectedAsync(
            viewModel, resultsList,
            candidate => viewModel.AlwaysUseAsync(candidate));
        removeAssociation.Click += async (_, _) => await ApplySelectedAsync(
            viewModel, resultsList,
            candidate => viewModel.RemoveAssociationAsync(candidate));

        importButton.Click += async (_, _) =>
        {
            var picker = new global::Windows.Storage.Pickers.FileOpenPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            picker.FileTypeFilter.Add(".lrc");
            picker.FileTypeFilter.Add(".lrcx");
            var file = await picker.PickSingleFileAsync();
            if (file is null)
            {
                return;
            }

            var data = await ReadAllBytesAsync(file);
            try
            {
                var candidate = FileService.Import(file.Name, data);
                await viewModel.UseOnceAsync(candidate);
            }
            catch (Exception exception)
            {
                var dialog = new ContentDialog
                {
                    Title = "导入失败",
                    Content = exception.Message,
                    CloseButtonText = "确定",
                    XamlRoot = window.Content.XamlRoot
                };
                await dialog.ShowAsync();
            }
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        buttons.Children.Add(importButton);
        buttons.Children.Add(useOnce);
        buttons.Children.Add(always);
        buttons.Children.Add(removeAssociation);
        var closeButton = new Button { Content = "关闭" };
        closeButton.Click += (_, _) => window.Close();
        buttons.Children.Add(closeButton);

        var panel = new StackPanel { Spacing = 10, Padding = new Thickness(20) };
        panel.Children.Add(titleBox);
        panel.Children.Add(artistBox);
        panel.Children.Add(searchButton);
        panel.Children.Add(resultsList);
        panel.Children.Add(buttons);

        window.Content = new ScrollViewer { Content = panel };
        return window;
    }

    private static async Task ApplySelectedAsync(
        ManualSearchViewModel viewModel,
        ListView resultsList,
        Func<LyricCandidate, Task> action)
    {
        if (resultsList.SelectedIndex is >= 0 &&
            viewModel.Results.Count > resultsList.SelectedIndex)
        {
            await action(viewModel.Results[resultsList.SelectedIndex].Candidate);
        }
    }

    private static async Task<byte[]> ReadAllBytesAsync(global::Windows.Storage.StorageFile file)
    {
        var buffer = await global::Windows.Storage.FileIO.ReadBufferAsync(file);
        var data = new byte[buffer.Length];
        using var reader = global::Windows.Storage.Streams.DataReader.FromBuffer(buffer);
        reader.ReadBytes(data);
        return data;
    }
}
