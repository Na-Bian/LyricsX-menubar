using LyricsX.Platform.Windows;
using LyricsX.Platform.Windows.Windowing;

namespace LyricsX.Platform.Windows.Tests.Windowing;

[TestClass]
public sealed class WindowStyleServiceTests
{
    private const nint Hwnd = 0x1234;

    [TestMethod]
    public void ApplyToolWindowStyle_AddsToolWindowAndNoActivate_RemovesAppWindow()
    {
        var api = new FakeWindowApi { ExStyle = 0 };
        var service = new WindowStyleService(api);

        service.ApplyNonActivatingStyle(Hwnd);

        Assert.AreEqual(
            NativeWindowStyles.WS_EX_TOOLWINDOW |
            NativeWindowStyles.WS_EX_NOACTIVATE,
            api.ExStyle & (
                NativeWindowStyles.WS_EX_TOOLWINDOW |
                NativeWindowStyles.WS_EX_NOACTIVATE |
                NativeWindowStyles.WS_EX_APPWINDOW));
        Assert.AreEqual(0, api.ExStyle & NativeWindowStyles.WS_EX_APPWINDOW);
    }

    [TestMethod]
    public void ApplyToolWindowStyle_PreservesUnrelatedBits()
    {
        var api = new FakeWindowApi { ExStyle = NativeWindowStyles.WS_EX_TOPMOST | 0x10 };
        var service = new WindowStyleService(api);

        service.ApplyNonActivatingStyle(Hwnd);

        Assert.AreNotEqual(0, api.ExStyle & NativeWindowStyles.WS_EX_TOPMOST);
        Assert.AreNotEqual(0, api.ExStyle & 0x10);
    }

    [TestMethod]
    public void ShowAt_MovesResizesAndShowsWithoutActivation()
    {
        var api = new FakeWindowApi { ExStyle = 0 };
        var service = new WindowStyleService(api);
        var placement = new TaskbarPlacement(
            new PixelRect(52, 1744, 442, 96), 2.25, true);

        service.ShowAt(Hwnd, placement);

        var call = api.SetWindowPosCalls.Single();
        Assert.AreEqual(52, call.X);
        Assert.AreEqual(1744, call.Y);
        Assert.AreEqual(442, call.Width);
        Assert.AreEqual(96, call.Height);
        Assert.AreEqual(NativeWindowStyles.SWP_NOACTIVATE | NativeWindowStyles.SWP_SHOWWINDOW,
            call.Flags & (NativeWindowStyles.SWP_NOACTIVATE | NativeWindowStyles.SWP_SHOWWINDOW));
        Assert.AreEqual(NativeWindowStyles.HWND_TOPMOST, call.InsertAfter);
    }

    [TestMethod]
    public void ShowAt_InvisiblePlacement_Hides()
    {
        var api = new FakeWindowApi { ExStyle = 0 };
        var service = new WindowStyleService(api);
        var placement = new TaskbarPlacement(default, 1.0, false);

        service.ShowAt(Hwnd, placement);

        Assert.AreEqual(1, api.ShowWindowCalls.Count);
        Assert.AreEqual(NativeWindowStyles.SW_HIDE, api.ShowWindowCalls.Single());
        Assert.AreEqual(0, api.SetWindowPosCalls.Count);
    }

    [TestMethod]
    public void Hide_CallsShowWindowWithSwHide()
    {
        var api = new FakeWindowApi { ExStyle = 0 };
        var service = new WindowStyleService(api);

        service.Hide(Hwnd);

        Assert.AreEqual(NativeWindowStyles.SW_HIDE, api.ShowWindowCalls.Single());
    }

    [TestMethod]
    public void HandleMouseActivate_ReturnsMaNoActivate()
    {
        Assert.AreEqual(
            NativeWindowStyles.MA_NOACTIVATE,
            WindowStyleService.HandleMouseActivate());
    }
}

internal sealed class FakeWindowApi : IWindowApi
{
    public long ExStyle { get; set; }

    public List<(nint Hwnd, nint InsertAfter, int X, int Y, int Width, int Height, uint Flags)> SetWindowPosCalls { get; } = [];

    public List<int> ShowWindowCalls { get; } = [];

    public long GetExStyle(nint hwnd) => ExStyle;

    public void SetExStyle(nint hwnd, long style) => ExStyle = style;

    public void SetWindowPos(
        nint hwnd,
        nint insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags) =>
        SetWindowPosCalls.Add((hwnd, insertAfter, x, y, width, height, flags));

    public void ShowWindow(nint hwnd, int command) => ShowWindowCalls.Add(command);
}
