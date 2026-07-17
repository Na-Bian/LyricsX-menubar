# Windows Taskbar Lyrics 02: Windows Platform Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the Windows 11 media-session, taskbar-geometry, overlay-host, tray, startup, shortcut, and credential services used by the WinUI app.

**Architecture:** `LyricsX.Platform.Windows` adapts public Windows APIs behind narrow interfaces. Pure ranking and interval calculations are separated from Win32/UI Automation calls. An unpackaged full-trust process reads GSMTC directly; the lyric surface is an independent no-activation tool window positioned over verified free taskbar space, never injected into Explorer.

**Tech Stack:** .NET 10, C# 14, Windows App SDK 1.8.6 (`1.8.260317003`), WinRT GSMTC, Win32 P/Invoke, UI Automation, Windows Credential Manager, MSTest.

## Global Constraints

- Complete plan 01 first and consume its stable `LyricsX.Core` contracts.
- Support Windows 11 x64 only; reject older OS versions with a clear startup message.
- Use public APIs and read-only taskbar inspection. Do not inject DLLs, subclass Explorer, patch the taskbar, or write Explorer-owned state.
- Keep taskbar placement on the primary display in the first release.
- Treat taskbar and media-session data as transient; never persist window handles or WinRT session objects.
- Hide the overlay when fewer than 80 DIPs are available.
- Commit only files named by the current task.

## Stable Platform Contracts

```csharp
namespace LyricsX.Platform.Windows;

public sealed record MediaSessionSnapshot(
    string SessionId,
    string SourceAppId,
    LyricsX.Core.PlaybackStatus Status,
    LyricsX.Core.MediaTrack? Track,
    TimeSpan Position,
    double Rate,
    DateTimeOffset LastUpdatedAt,
    object NativeSession);

public interface IMediaSessionSource : IAsyncDisposable
{
    event EventHandler? SessionsChanged;
    Task<IReadOnlyList<MediaSessionSnapshot>> GetSessionsAsync(
        CancellationToken cancellationToken);
}

public interface ISelectedMediaSession : IAsyncDisposable
{
    event EventHandler<LyricsX.Core.PlaybackSnapshot>? PlaybackChanged;
    LyricsX.Core.PlaybackSnapshot Current { get; }
    Task StartAsync(CancellationToken cancellationToken);
}

public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
}

public sealed record TaskbarPlacement(
    PixelRect Bounds,
    double RasterizationScale,
    bool IsVisible);

public interface ITaskbarPlacementService : IAsyncDisposable
{
    event EventHandler? PlacementChanged;
    Task<TaskbarPlacement> GetPlacementAsync(
        int preferredWidthDip,
        CancellationToken cancellationToken);
}

public interface ITaskbarLyricsWindowHost
{
    bool IsVisible { get; }
    void Show(TaskbarPlacement placement);
    void Hide();
}
```

---

## Task 1: Add the Windows platform project and interop boundary

**Files:**

- Create: `Windows/src/LyricsX.Platform.Windows/LyricsX.Platform.Windows.csproj`
- Create: `Windows/src/LyricsX.Platform.Windows/Interop/NativeMethods.cs`
- Create: `Windows/src/LyricsX.Platform.Windows/Interop/SafeHandles.cs`
- Create: `Windows/tests/LyricsX.Platform.Windows.Tests/LyricsX.Platform.Windows.Tests.csproj`
- Create: `Windows/tests/LyricsX.Platform.Windows.Tests/Architecture/PlatformBoundaryTests.cs`
- Modify: `Windows/LyricsX.Windows.slnx`
- Modify: `Windows/Directory.Packages.props`

**Interfaces produced:** project boundary and centralized Windows App SDK dependency.

- [ ] Add package pin:

```xml
<PackageVersion Include="Microsoft.WindowsAppSDK" Version="1.8.260317003" />
```

- [ ] Create `LyricsX.Platform.Windows.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <UseWinUI>true</UseWinUI>
    <EnableMsixTooling>false</EnableMsixTooling>
    <WindowsPackageType>None</WindowsPackageType>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.WindowsAppSDK" />
    <ProjectReference Include="..\LyricsX.Core\LyricsX.Core.csproj" />
    <ProjectReference Include="..\LyricsX.Providers\LyricsX.Providers.csproj" />
    <Reference Include="UIAutomationClient" />
    <Reference Include="UIAutomationTypes" />
  </ItemGroup>
</Project>
```

- [ ] Create the MSTest project using the package structure from plan 01, reference `LyricsX.Platform.Windows`, and add both projects to `LyricsX.Windows.slnx`.

- [ ] Write `PlatformBoundaryTests`:

```csharp
namespace LyricsX.Platform.Windows.Tests.Architecture;

[TestClass]
public sealed class PlatformBoundaryTests
{
    [TestMethod]
    public void Core_DoesNotReferenceWindowsPlatform()
    {
        var references = typeof(LyricsX.Core.MediaTrack)
            .Assembly.GetReferencedAssemblies()
            .Select(name => name.Name)
            .ToArray();

        CollectionAssert.DoesNotContain(references, "LyricsX.Platform.Windows");
        CollectionAssert.DoesNotContain(references, "Microsoft.WindowsAppSDK");
    }
}
```

- [ ] Put all `[LibraryImport]` declarations in `NativeMethods.cs`; include `user32`, `shell32`, `dwmapi`, `kernel32`, `advapi32`, and `credui` entry points. Use `SetLastError = true`, blittable structs, and `SafeHandle` wrappers for registry/event/credential allocations.

- [ ] Run:

```powershell
dotnet build LyricsX.Windows.slnx
dotnet test tests/LyricsX.Platform.Windows.Tests/LyricsX.Platform.Windows.Tests.csproj
```

Expected: build and boundary test pass on Windows 11 x64.

- [ ] Commit:

```powershell
git add Windows/Directory.Packages.props Windows/LyricsX.Windows.slnx Windows/src/LyricsX.Platform.Windows Windows/tests/LyricsX.Platform.Windows.Tests
git commit -m "build(windows): add Windows platform boundary"
```

## Task 2: Read Global System Media Transport Control sessions

**Files:**

- Create: `Windows/src/LyricsX.Platform.Windows/Media/GlobalMediaSessionSource.cs`
- Create: `Windows/src/LyricsX.Platform.Windows/Media/GsmtcMapper.cs`
- Create: `Windows/src/LyricsX.Platform.Windows/Media/MediaSessionSnapshot.cs`
- Create: `Windows/tests/LyricsX.Platform.Windows.Tests/Media/GsmtcMapperTests.cs`
- Create: `Windows/tools/MediaSessionProbe/MediaSessionProbe.csproj`
- Create: `Windows/tools/MediaSessionProbe/Program.cs`
- Modify: `Windows/LyricsX.Windows.slnx`

**Interfaces produced:** `IMediaSessionSource`.

- [ ] Write failing pure mapping tests for Playing/Paused/Stopped, missing album/artist/duration, duplicate property-change events, and app IDs for Apple Music, NetEase Cloud Music, QQ Music, browser, and Windows Media Player.

- [ ] Run:

```powershell
dotnet test tests/LyricsX.Platform.Windows.Tests/LyricsX.Platform.Windows.Tests.csproj --filter "FullyQualifiedName~GsmtcMapper"
```

Expected: compilation fails because the mapper does not exist.

- [ ] Implement `GsmtcMapper.Map` from:

```csharp
GlobalSystemMediaTransportControlsSession
GlobalSystemMediaTransportControlsSessionPlaybackInfo
GlobalSystemMediaTransportControlsSessionTimelineProperties
GlobalSystemMediaTransportControlsSessionMediaProperties
```

Map `SourceAppUserModelId`, title, artist, album title, playback status, position, end-start duration, playback rate defaulting to `1.0`, and `DateTimeOffset.UtcNow`. Generate `TrackId` as SHA-256 of source app ID, normalized title, normalized artist, album, and rounded duration.

- [ ] Implement `GlobalMediaSessionSource`:

```csharp
public sealed class GlobalMediaSessionSource : IMediaSessionSource
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;

    public event EventHandler? SessionsChanged;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _manager = await GlobalSystemMediaTransportControlsSessionManager
            .RequestAsync()
            .AsTask(cancellationToken);
        _manager.SessionsChanged += OnSessionsChanged;
        _manager.CurrentSessionChanged += OnCurrentSessionChanged;
    }

    private void OnSessionsChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        SessionsChangedEventArgs args) =>
        SessionsChanged?.Invoke(this, EventArgs.Empty);

    private void OnCurrentSessionChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        CurrentSessionChangedEventArgs args) =>
        SessionsChanged?.Invoke(this, EventArgs.Empty);
}
```

Complete `GetSessionsAsync` by reading all media properties concurrently, skipping sessions that throw `UnauthorizedAccessException` or disappear during enumeration, and retaining the native session only inside `MediaSessionSnapshot`.

- [ ] Create `MediaSessionProbe` as an unpackaged console executable that calls `RequestAsync`, prints redacted source app ID/status/title/artist once, and exits. Do not print album art URIs or account identifiers.

- [ ] Run on Windows 11 while each supported player is playing:

```powershell
dotnet run --project tools/MediaSessionProbe/MediaSessionProbe.csproj
```

Expected: Apple Music, NetEase Cloud Music, QQ Music, a browser tab, and Windows Media Player each appear with usable title, artist, status, and position. Record player-version-specific gaps in `Windows/docs/manual-media-observations.md` during plan 04.

- [ ] Run tests:

```powershell
dotnet test tests/LyricsX.Platform.Windows.Tests/LyricsX.Platform.Windows.Tests.csproj
```

Expected: mapper tests pass.

- [ ] Commit:

```powershell
git add Windows/src/LyricsX.Platform.Windows/Media Windows/tests/LyricsX.Platform.Windows.Tests/Media Windows/tools/MediaSessionProbe Windows/LyricsX.Windows.slnx
git commit -m "feat(windows): read system media sessions"
```

## Task 3: Select the active session and coalesce playback changes

**Files:**

- Create: `Windows/src/LyricsX.Platform.Windows/Media/MediaSessionRanker.cs`
- Create: `Windows/src/LyricsX.Platform.Windows/Media/SelectedMediaSession.cs`
- Create: `Windows/tests/LyricsX.Platform.Windows.Tests/Media/MediaSessionRankerTests.cs`
- Create: `Windows/tests/LyricsX.Platform.Windows.Tests/Media/SelectedMediaSessionTests.cs`

**Interfaces produced:** `ISelectedMediaSession`.

- [ ] Write the failing ranking test:

```csharp
namespace LyricsX.Platform.Windows.Tests.Media;

[TestClass]
public sealed class MediaSessionRankerTests
{
    [TestMethod]
    public void Select_PrefersPlayingThenMostRecentlyUpdated()
    {
        var now = DateTimeOffset.UtcNow;
        var paused = Session("paused", PlaybackStatus.Paused, now);
        var oldPlaying = Session("old", PlaybackStatus.Playing, now.AddSeconds(-5));
        var newPlaying = Session("new", PlaybackStatus.Playing, now);

        var selected = new MediaSessionRanker().Select(
            [paused, oldPlaying, newPlaying], previousSessionId: "old");

        Assert.AreEqual("new", selected?.SessionId);
    }
}
```

- [ ] Run:

```powershell
dotnet test tests/LyricsX.Platform.Windows.Tests/LyricsX.Platform.Windows.Tests.csproj --filter "FullyQualifiedName~MediaSessionRanker"
```

Expected: compilation fails because `MediaSessionRanker` does not exist.

- [ ] Implement deterministic ranking in this order:

  1. sessions whose status is Playing;
  2. most recently updated active session;
  3. the GSMTC current session when supplied;
  4. the previously selected session if still present;
  5. Paused, then Stopped, then Closed;
  6. ordinal `SessionId` tie-breaker.

- [ ] Implement `SelectedMediaSession` with one serialized refresh loop:

```csharp
private readonly SemaphoreSlim _refreshGate = new(1, 1);

private async Task RefreshAsync(CancellationToken cancellationToken)
{
    if (!await _refreshGate.WaitAsync(0, cancellationToken))
    {
        Interlocked.Exchange(ref _refreshRequested, 1);
        return;
    }

    try
    {
        do
        {
            Interlocked.Exchange(ref _refreshRequested, 0);
            await RefreshCoreAsync(cancellationToken);
        }
        while (Interlocked.Exchange(ref _refreshRequested, 0) == 1);
    }
    finally
    {
        _refreshGate.Release();
    }
}
```

Coalesce bursts for 100 ms, publish only semantically changed snapshots, and synthesize position between events from `ObservedAt` only while Playing.

- [ ] Test disappearing sessions, rapid events, previous-session stability, track changes, pause/resume, and disposal unsubscribing all event handlers.

- [ ] Run:

```powershell
dotnet test tests/LyricsX.Platform.Windows.Tests/LyricsX.Platform.Windows.Tests.csproj
```

Expected: all media tests pass.

- [ ] Commit:

```powershell
git add Windows/src/LyricsX.Platform.Windows/Media/MediaSessionRanker.cs Windows/src/LyricsX.Platform.Windows/Media/SelectedMediaSession.cs Windows/tests/LyricsX.Platform.Windows.Tests/Media
git commit -m "feat(windows): select and track active media session"
```

## Task 4: Calculate the leftmost usable taskbar interval

**Files:**

- Create: `Windows/src/LyricsX.Platform.Windows/Taskbar/TaskbarModels.cs`
- Create: `Windows/src/LyricsX.Platform.Windows/Taskbar/TaskbarIntervalCalculator.cs`
- Create: `Windows/tests/LyricsX.Platform.Windows.Tests/Taskbar/TaskbarIntervalCalculatorTests.cs`

**Interfaces produced:**

```csharp
public enum TaskbarEdge { Bottom, Top, Left, Right }

public sealed record TaskbarSnapshot(
    PixelRect TaskbarBounds,
    TaskbarEdge Edge,
    IReadOnlyList<PixelRect> OccupiedBounds,
    double RasterizationScale,
    bool AutoHide,
    bool IsForegroundFullscreen);
```

- [ ] Write failing tests for a centered Windows 11 taskbar, Widgets at the left, no Widgets, vertical taskbar fallback, overlapping occupied rectangles, taskbar auto-hide, full-screen foreground window, and less than 80 available DIPs.

- [ ] Use this canonical centered-taskbar assertion:

```csharp
[TestMethod]
public void Calculate_ReturnsFirstGapAfterLeftSystemButtons()
{
    var snapshot = new TaskbarSnapshot(
        new PixelRect(0, 1040, 1920, 40),
        TaskbarEdge.Bottom,
        [
            new PixelRect(0, 1040, 48, 40),
            new PixelRect(760, 1040, 400, 40),
            new PixelRect(1650, 1040, 270, 40)
        ],
        1.0,
        AutoHide: false,
        IsForegroundFullscreen: false);

    var result = new TaskbarIntervalCalculator().Calculate(snapshot, 260, 80, 360);

    Assert.AreEqual(new PixelRect(52, 1040, 260, 40), result.Bounds);
    Assert.IsTrue(result.IsVisible);
}
```

- [ ] Run:

```powershell
dotnet test tests/LyricsX.Platform.Windows.Tests/LyricsX.Platform.Windows.Tests.csproj --filter "FullyQualifiedName~TaskbarIntervalCalculator"
```

Expected: compilation fails because the interval calculator does not exist.

- [ ] Implement the pure algorithm:

  - Return hidden for auto-hide, foreground full-screen, non-horizontal edge, zero taskbar size, or scale below `0.5`.
  - Inflate occupied rectangles by 4 physical pixels on each horizontal side.
  - Clip occupied rectangles to the taskbar and merge overlapping/touching horizontal intervals.
  - Enumerate gaps left-to-right.
  - Convert requested/min/max DIPs to physical pixels using scale.
  - Select the first gap at least the minimum width; use `min(preferred, maximum, gap width)`.
  - Keep the full taskbar height and the gap's left edge.

- [ ] Run:

```powershell
dotnet test tests/LyricsX.Platform.Windows.Tests/LyricsX.Platform.Windows.Tests.csproj --filter "FullyQualifiedName~TaskbarIntervalCalculator"
```

Expected: all interval tests pass.

- [ ] Commit:

```powershell
git add Windows/src/LyricsX.Platform.Windows/Taskbar/TaskbarModels.cs Windows/src/LyricsX.Platform.Windows/Taskbar/TaskbarIntervalCalculator.cs Windows/tests/LyricsX.Platform.Windows.Tests/Taskbar/TaskbarIntervalCalculatorTests.cs
git commit -m "feat(windows): calculate free taskbar lyrics space"
```

## Task 5: Observe real taskbar geometry and occupied controls

**Files:**

- Create: `Windows/src/LyricsX.Platform.Windows/Taskbar/TaskbarSnapshotReader.cs`
- Create: `Windows/src/LyricsX.Platform.Windows/Taskbar/TaskbarAutomationReader.cs`
- Create: `Windows/src/LyricsX.Platform.Windows/Taskbar/FullscreenDetector.cs`
- Create: `Windows/src/LyricsX.Platform.Windows/Taskbar/TaskbarPlacementService.cs`
- Create: `Windows/tests/LyricsX.Platform.Windows.Tests/Taskbar/TaskbarSnapshotReaderTests.cs`

**Interfaces produced:** `ITaskbarPlacementService`.

- [ ] Write failing tests around injected native/automation adapters:

```csharp
public interface ITaskbarNativeApi
{
    nint FindPrimaryTaskbar();
    PixelRect GetWindowRect(nint window);
    TaskbarEdge GetEdge(nint window);
    bool IsAutoHideEnabled();
    double GetRasterizationScale(nint window);
    bool IsForegroundFullscreen(PixelRect monitorBounds);
}

public interface ITaskbarAutomationApi
{
    IReadOnlyList<PixelRect> GetVisibleInteractiveBounds(nint taskbarWindow);
}
```

Test absent Explorer, restarting Explorer with a new HWND, automation access denied, stale/off-screen controls, DPI change, and display change.

- [ ] Run:

```powershell
dotnet test tests/LyricsX.Platform.Windows.Tests/LyricsX.Platform.Windows.Tests.csproj --filter "FullyQualifiedName~TaskbarSnapshotReader"
```

Expected: compilation fails because readers do not exist.

- [ ] Implement native geometry with `FindWindow("Shell_TrayWnd", null)`, `SHAppBarMessage(ABM_GETTASKBARPOS)`, `GetWindowRect`, `SHAppBarMessage(ABM_GETSTATE)`, `GetDpiForWindow`, `MonitorFromWindow`, and `GetMonitorInfo`.

- [ ] Implement UI Automation in a dedicated MTA thread:

  - start from `AutomationElement.FromHandle(taskbarHwnd)`;
  - walk visible descendants with control types Button, List, ToolBar, and Custom;
  - retain leaf interactive elements whose bounding rectangle intersects the taskbar;
  - discard rectangles under 4×4 px and rectangles outside the primary taskbar;
  - merge duplicates within 2 px;
  - on `ElementNotAvailableException`, retry once with a fresh root;
  - on access denied or an unexpected Explorer tree, return an empty list and log a diagnostic.

- [ ] Implement `TaskbarPlacementService` with polling every 500 ms only while the lyric window is visible, plus reactions to `WM_DISPLAYCHANGE`, `WM_DPICHANGED`, `WM_SETTINGCHANGE`, `TaskbarCreated`, shell-hook window events, foreground changes, and Explorer process changes. Debounce repositioning to 100 ms.

- [ ] Add a safe fallback when automation returns no occupied rectangles: reserve the leftmost 64 DIPs and the centered 520 DIPs, then use the interval calculator. Hide instead of overlapping if the fallback does not yield 80 DIPs.

- [ ] Run the tests and a manual geometry probe at 100%, 125%, 150%, and 200% scaling:

```powershell
dotnet test tests/LyricsX.Platform.Windows.Tests/LyricsX.Platform.Windows.Tests.csproj
```

Expected: all tests pass; physical bounds remain within the primary taskbar.

- [ ] Commit:

```powershell
git add Windows/src/LyricsX.Platform.Windows/Taskbar Windows/tests/LyricsX.Platform.Windows.Tests/Taskbar
git commit -m "feat(windows): observe Windows 11 taskbar layout"
```

## Task 6: Host a no-activation WinUI taskbar window

**Files:**

- Create: `Windows/src/LyricsX.Platform.Windows/Windowing/TaskbarLyricsWindowHost.cs`
- Create: `Windows/src/LyricsX.Platform.Windows/Windowing/WindowStyleService.cs`
- Create: `Windows/tests/LyricsX.Platform.Windows.Tests/Windowing/WindowStyleServiceTests.cs`

**Interfaces consumed:** `ITaskbarLyricsWindowHost`, `TaskbarPlacement`.

- [ ] Write failing tests over an injected style adapter proving the host adds `WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE`, removes `WS_EX_APPWINDOW`, uses `SWP_NOACTIVATE`, and hides for `IsVisible = false`.

- [ ] Run:

```powershell
dotnet test tests/LyricsX.Platform.Windows.Tests/LyricsX.Platform.Windows.Tests.csproj --filter "FullyQualifiedName~WindowStyleService"
```

Expected: compilation fails because the window-style types do not exist.

- [ ] Implement WinUI/AppWindow setup:

```csharp
var hwnd = WindowNative.GetWindowHandle(window);
var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
var appWindow = AppWindow.GetFromWindowId(windowId);
var presenter = OverlappedPresenter.Create();
presenter.SetBorderAndTitleBar(false, false);
presenter.IsAlwaysOnTop = true;
presenter.IsResizable = false;
presenter.IsMinimizable = false;
presenter.IsMaximizable = false;
appWindow.SetPresenter(presenter);
```

- [ ] Apply extended styles with `GetWindowLongPtr`/`SetWindowLongPtr`, call `SetWindowPos(hwnd, HWND_TOPMOST, bounds.X, bounds.Y, bounds.Width, bounds.Height, SWP_NOACTIVATE | SWP_SHOWWINDOW)`, suppress activation from `WM_MOUSEACTIVATE` with `MA_NOACTIVATE`, and never call `SetForegroundWindow`.

- [ ] `Show` must move and resize to `TaskbarPlacement.Bounds`; `Hide` must call `ShowWindow(hwnd, SW_HIDE)`. Marshal both to the WinUI dispatcher queue.

- [ ] Manually verify:

  - Alt+Tab does not show the lyric window.
  - Clicking lyrics does not steal focus from the current player.
  - The window remains topmost over taskbar free space but hides for full-screen apps and auto-hide taskbar.
  - Explorer restart causes a reposition, not a crash.

- [ ] Run:

```powershell
dotnet test tests/LyricsX.Platform.Windows.Tests/LyricsX.Platform.Windows.Tests.csproj
```

Expected: all platform tests pass.

- [ ] Commit:

```powershell
git add Windows/src/LyricsX.Platform.Windows/Windowing Windows/tests/LyricsX.Platform.Windows.Tests/Windowing
git commit -m "feat(windows): host nonactivating taskbar lyrics"
```

## Task 7: Add tray icon, global shortcuts, startup, and Credential Manager

**Files:**

- Create: `Windows/src/LyricsX.Platform.Windows/Shell/TrayIconService.cs`
- Create: `Windows/src/LyricsX.Platform.Windows/Shell/GlobalHotKeyService.cs`
- Create: `Windows/src/LyricsX.Platform.Windows/Shell/StartupRegistrationService.cs`
- Create: `Windows/src/LyricsX.Platform.Windows/Security/WindowsCredentialStore.cs`
- Create: `Windows/tests/LyricsX.Platform.Windows.Tests/Shell/GlobalHotKeyServiceTests.cs`
- Create: `Windows/tests/LyricsX.Platform.Windows.Tests/Shell/StartupRegistrationServiceTests.cs`
- Create: `Windows/tests/LyricsX.Platform.Windows.Tests/Security/WindowsCredentialStoreTests.cs`

**Interfaces produced:**

```csharp
public interface ITrayIconService : IDisposable
{
    event EventHandler? OpenPreferencesRequested;
    event EventHandler? ExitRequested;
    void Show();
}

public interface IGlobalHotKeyService : IDisposable
{
    bool TryRegister(int id, HotKeyModifiers modifiers, uint virtualKey);
    void Unregister(int id);
    event EventHandler<int>? Pressed;
}

public interface IStartupRegistrationService
{
    bool IsEnabled { get; }
    void SetEnabled(bool enabled);
}
```

- [ ] Write failing adapter tests for hotkey conflicts, unregister/dispose, startup enable/disable/idempotency, credential write/read/delete, and token buffers zeroed after use.

- [ ] Implement the tray with `Shell_NotifyIconW(NIM_ADD/NIM_MODIFY/NIM_DELETE)`, `NIF_GUID`, a stable GUID, `NOTIFYICON_VERSION_4`, and a hidden message window. Re-add it after the registered `TaskbarCreated` message.

- [ ] Implement global shortcuts with `RegisterHotKey`, emit the numeric registration ID on `WM_HOTKEY`, and surface `ERROR_HOTKEY_ALREADY_REGISTERED` as a non-fatal settings validation error.

- [ ] Implement per-user startup under:

```text
HKCU\Software\Microsoft\Windows\CurrentVersion\Run
Name: LyricsX
Value: "<absolute install path>\LyricsX.Windows.App.exe" --startup
```

Quote the path, never request elevation, and remove only the `LyricsX` value.

- [ ] Implement `WindowsCredentialStore` with `CredWriteW`, `CredReadW`, `CredDeleteW`, and target names prefixed `LyricsX/`. Expose it as the `ISpotifyTokenStore` from plan 01 using target `LyricsX/SpotifyRefreshToken`. Use `SecureZeroMemory` for native credential buffers before freeing them.

- [ ] Run:

```powershell
dotnet test tests/LyricsX.Platform.Windows.Tests/LyricsX.Platform.Windows.Tests.csproj
dotnet build LyricsX.Windows.slnx --configuration Release
```

Expected: all tests pass and Release builds with zero warnings.

- [ ] Commit:

```powershell
git add Windows/src/LyricsX.Platform.Windows/Shell Windows/src/LyricsX.Platform.Windows/Security Windows/tests/LyricsX.Platform.Windows.Tests/Shell Windows/tests/LyricsX.Platform.Windows.Tests/Security
git commit -m "feat(windows): add shell integration and credential storage"
```

## Task 8: Build a deterministic Windows media test application

**Files:**

- Create: `Windows/tests/LyricsX.Windows.TestMediaApp/LyricsX.Windows.TestMediaApp.csproj`
- Create: `Windows/tests/LyricsX.Windows.TestMediaApp/App.xaml`
- Create: `Windows/tests/LyricsX.Windows.TestMediaApp/App.xaml.cs`
- Create: `Windows/tests/LyricsX.Windows.TestMediaApp/MainWindow.xaml`
- Create: `Windows/tests/LyricsX.Windows.TestMediaApp/MainWindow.xaml.cs`
- Create: `Windows/tests/LyricsX.Windows.TestMediaApp/MediaScenarioController.cs`
- Create: `Windows/tests/LyricsX.Platform.Windows.Tests/Integration/MediaSessionIntegrationTests.cs`
- Modify: `Windows/LyricsX.Windows.slnx`

**Interfaces consumed:** GSMTC; no production provider or lyric service.

- [ ] Create an unpackaged WinUI test app that registers `SystemMediaTransportControls`, exposes Play/Pause/Next buttons, and publishes three deterministic tracks:

```csharp
private static readonly (string Title, string Artist, TimeSpan Duration)[] Tracks =
[
    ("Windows Test One", "LyricsX Fixture", TimeSpan.FromSeconds(120)),
    ("Windows Test Two", "LyricsX Fixture", TimeSpan.FromSeconds(180)),
    ("Windows Test Three", "Second Fixture", TimeSpan.FromSeconds(240))
];
```

- [ ] Add a `--automation-pipe <name>` argument. Accept newline-delimited commands `play`, `pause`, `next`, `position:<seconds>`, `exit`; reply `ok` after the GSMTC state is updated.

- [ ] Write integration tests that launch the app, connect to the pipe, wait up to five seconds for `GlobalMediaSessionSource`, assert title/status/position changes, and always kill the child process in cleanup.

- [ ] Mark the tests:

```csharp
[TestCategory("WindowsIntegration")]
[TestMethod]
public async Task GlobalSource_ObservesDeterministicPlaybackChanges()
```

- [ ] Run:

```powershell
dotnet test tests/LyricsX.Platform.Windows.Tests/LyricsX.Platform.Windows.Tests.csproj --filter "TestCategory=WindowsIntegration"
```

Expected: the test app drives a real GSMTC session and all integration assertions pass.

- [ ] Commit:

```powershell
git add Windows/tests/LyricsX.Windows.TestMediaApp Windows/tests/LyricsX.Platform.Windows.Tests/Integration Windows/LyricsX.Windows.slnx
git commit -m "test(windows): add deterministic media session app"
```

## Plan 02 Exit Criteria

- [ ] The media probe reads all five target player categories through GSMTC without package identity.
- [ ] Ranking remains stable when several players are open and switches promptly to a newly Playing session.
- [ ] Taskbar calculations pass at 100%, 125%, 150%, and 200% scale.
- [ ] The overlay never overlaps a discovered occupied rectangle and hides below 80 available DIPs.
- [ ] Auto-hide, foreground full-screen, DPI/display changes, and Explorer restart are handled without stealing focus.
- [ ] Tray, shortcut, startup, and Spotify credential services operate per-user without elevation.
- [ ] `dotnet test Windows/LyricsX.Windows.slnx --filter "TestCategory!=LiveProvider"` passes on Windows 11 x64.
