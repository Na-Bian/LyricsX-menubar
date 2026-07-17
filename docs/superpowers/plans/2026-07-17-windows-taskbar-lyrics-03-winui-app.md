# Windows Taskbar Lyrics 03: WinUI App Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the unpackaged WinUI 3 application, taskbar lyric presentation, compact/full menus, preferences, manual search, localization, diagnostics, and application orchestration.

**Architecture:** `LyricsX.Windows.App` is a thin composition and presentation layer. View models consume the core and platform interfaces from plans 01–02. A single coordinator reacts to selected media changes, cache/manual associations, asynchronous provider search, synchronization ticks, and taskbar placement. Preferences use Mac-compatible wording but expose only Windows-applicable, non-experimental features.

**Tech Stack:** .NET 10, C# 14, WinUI 3 / Windows App SDK 1.8.6, CommunityToolkit.Mvvm 8.4.2, Microsoft.Extensions.DependencyInjection 10.0.10, XAML, MSTest.

## Global Constraints

- Complete plans 01 and 02 first.
- The app is unpackaged, full-trust, self-contained, x64, and per-user.
- Do not add a Lab/Experimental page or copy macOS-only Touch Bar, AppleScript, Sparkle, or metadata-writing controls.
- Use the approved wording `偏好设置…`.
- Left click opens the compact menu. Right click opens the full menu. The tray opens the same full menu.
- Every command must be reachable by keyboard and expose a meaningful AutomationProperties name.
- The taskbar lyric window must not activate; preferences and manual search are normal activatable windows.
- Commit only files named by the current task.

## Stable App Services

```csharp
namespace LyricsX.Windows.App.Services;

public interface ILyricsApplicationCoordinator : IAsyncDisposable
{
    event EventHandler<LyricsPresentationState>? StateChanged;
    LyricsPresentationState Current { get; }
    Task StartAsync(CancellationToken cancellationToken);
    Task SearchManuallyAsync(string title, string artist, CancellationToken cancellationToken);
    Task UseCandidateAsync(LyricCandidate candidate, bool remember, CancellationToken cancellationToken);
    Task AdjustOffsetAsync(TimeSpan delta, CancellationToken cancellationToken);
}

public sealed record LyricsPresentationState(
    PlaybackSnapshot Playback,
    LyricDocument? Document,
    LyricFrame Frame,
    string StatusText,
    bool IsSearching,
    string? ProviderId);
```

---

## Task 1: Scaffold the unpackaged self-contained WinUI app

**Files:**

- Create: `Windows/src/LyricsX.Windows.App/LyricsX.Windows.App.csproj`
- Create: `Windows/src/LyricsX.Windows.App/App.xaml`
- Create: `Windows/src/LyricsX.Windows.App/App.xaml.cs`
- Create: `Windows/src/LyricsX.Windows.App/Program.cs`
- Create: `Windows/src/LyricsX.Windows.App/Composition/ServiceRegistration.cs`
- Create: `Windows/src/LyricsX.Windows.App/Services/SingleInstanceService.cs`
- Create: `Windows/tests/LyricsX.Windows.App.Tests/LyricsX.Windows.App.Tests.csproj`
- Create: `Windows/tests/LyricsX.Windows.App.Tests/Architecture/AppCompositionTests.cs`
- Modify: `Windows/Directory.Packages.props`
- Modify: `Windows/LyricsX.Windows.slnx`

**Interfaces produced:** application entry point and DI container.

- [ ] Add package pins:

```xml
<PackageVersion Include="CommunityToolkit.Mvvm" Version="8.4.2" />
<PackageVersion Include="Microsoft.Extensions.DependencyInjection" Version="10.0.10" />
```

- [ ] Create `LyricsX.Windows.App.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <UseWinUI>true</UseWinUI>
    <EnableMsixTooling>false</EnableMsixTooling>
    <WindowsPackageType>None</WindowsPackageType>
    <WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>
    <SelfContained>true</SelfContained>
    <PublishSingleFile>false</PublishSingleFile>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <ApplicationIcon>Assets\LyricsX.ico</ApplicationIcon>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="CommunityToolkit.Mvvm" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" />
    <PackageReference Include="Microsoft.WindowsAppSDK" />
    <ProjectReference Include="..\LyricsX.Core\LyricsX.Core.csproj" />
    <ProjectReference Include="..\LyricsX.Providers\LyricsX.Providers.csproj" />
    <ProjectReference Include="..\LyricsX.Platform.Windows\LyricsX.Platform.Windows.csproj" />
  </ItemGroup>
</Project>
```

- [ ] Write failing composition tests:

```csharp
using LyricsX.Windows.App.Composition;
using LyricsX.Windows.App.Preferences;
using LyricsX.Windows.App.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LyricsX.Windows.App.Tests.Architecture;

[TestClass]
public sealed class AppCompositionTests
{
    [TestMethod]
    public void BuildServices_ResolvesCoordinatorAndPreferencesViewModel()
    {
        using var provider = ServiceRegistration.BuildServices(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        Assert.IsNotNull(provider.GetRequiredService<ILyricsApplicationCoordinator>());
        Assert.IsNotNull(provider.GetRequiredService<PreferencesViewModel>());
    }
}
```

- [ ] Run:

```powershell
dotnet test tests/LyricsX.Windows.App.Tests/LyricsX.Windows.App.Tests.csproj
```

Expected: compilation fails because app composition does not exist.

- [ ] Implement `ServiceRegistration.BuildServices` to register:

  - one `HttpClient` per provider;
  - all six provider objects, with Spotify disabled by settings;
  - core parser, scorer, coordinator, synchronizer, transformer, stores;
  - Windows GSMTC, taskbar placement, window host, tray, hotkey, startup, credential services;
  - application coordinator and view models.

Use `%LOCALAPPDATA%\LyricsX` in production and the injected root in tests.

- [ ] Implement one process per user with a named mutex `Local\LyricsX.Windows.App`. A second process writes `show-preferences` or `shutdown` to named pipe `LyricsX.Windows.App.Command`, waits for `ok`, and exits with code 0. The primary process dispatches `show-preferences` onto the UI thread and performs graceful shutdown for `shutdown`. If the first process starts with `--shutdown`, it exits immediately without creating WinUI.

- [ ] In `Program.Main`, check `OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)` before WinUI initialization. Show a native message box and exit code 10 on unsupported Windows.

- [ ] Run:

```powershell
dotnet test tests/LyricsX.Windows.App.Tests/LyricsX.Windows.App.Tests.csproj
dotnet publish src/LyricsX.Windows.App/LyricsX.Windows.App.csproj -c Debug -r win-x64 --self-contained true
```

Expected: tests pass and the unpackaged publish folder starts on a clean Windows 11 x64 machine without a separately installed .NET runtime.

- [ ] Commit:

```powershell
git add Windows/Directory.Packages.props Windows/LyricsX.Windows.slnx Windows/src/LyricsX.Windows.App Windows/tests/LyricsX.Windows.App.Tests
git commit -m "feat(windows): scaffold unpackaged WinUI application"
```

## Task 2: Orchestrate playback, cache, search, and synchronized frames

**Files:**

- Create: `Windows/src/LyricsX.Windows.App/Services/LyricsApplicationCoordinator.cs`
- Create: `Windows/src/LyricsX.Windows.App/Services/LyricsPresentationState.cs`
- Create: `Windows/src/LyricsX.Windows.App/Services/ILyricsApplicationCoordinator.cs`
- Create: `Windows/src/LyricsX.Windows.App/Services/IDispatcherTimer.cs`
- Create: `Windows/tests/LyricsX.Windows.App.Tests/Services/LyricsApplicationCoordinatorTests.cs`

**Interfaces produced:** `ILyricsApplicationCoordinator`.

- [ ] Write a failing test with fake session, cache, search coordinator, clock, and timer. Assert this sequence:

```text
No media → Searching → first candidate → synchronized line → better final candidate
```

Use this state assertion:

```csharp
CollectionAssert.AreEqual(
    new[] { "无正在播放的媒体", "正在搜索歌词…", "first line", "better line" },
    observed.Select(state => state.StatusText).Distinct().ToArray());
```

- [ ] Run:

```powershell
dotnet test tests/LyricsX.Windows.App.Tests/LyricsX.Windows.App.Tests.csproj --filter "FullyQualifiedName~LyricsApplicationCoordinator"
```

Expected: compilation fails because the coordinator does not exist.

- [ ] Implement a coordinator that:

  - listens to `ISelectedMediaSession.PlaybackChanged`;
  - cancels the prior track pipeline when fingerprint changes;
  - resolves manual association first, cache second, providers third;
  - checks ignored track and album fingerprints before cache/search and reports `已忽略此歌曲` without provider requests;
  - applies filtering and Chinese conversion before presentation;
  - accepts the first candidate immediately and better candidate during the five-second window;
  - stores the final candidate in cache;
  - ticks at 50 ms while Playing and 250 ms while Paused;
  - extrapolates position only while Playing;
  - keeps the last lyric when paused if `ShowWhenPaused` is true;
  - reports `无正在播放的媒体`, `正在搜索歌词…`, `未找到歌词`, and provider-specific non-secret errors;
  - restarts only the affected layer after a settings change.

- [ ] Implement `AdjustOffsetAsync` by clamping the user offset to ±30 seconds, saving settings atomically, and immediately recalculating the frame.

- [ ] Test cache hits bypass network, manual association wins over cache, track cancellation prevents stale results, pause freezes progress, stopped state hides lyrics, and disposal cancels every worker.

- [ ] Run:

```powershell
dotnet test tests/LyricsX.Windows.App.Tests/LyricsX.Windows.App.Tests.csproj
```

Expected: all application service tests pass.

- [ ] Commit:

```powershell
git add Windows/src/LyricsX.Windows.App/Services Windows/tests/LyricsX.Windows.App.Tests/Services
git commit -m "feat(windows): coordinate playback and lyric search"
```

## Task 3: Build the taskbar lyric window and marquee

**Files:**

- Create: `Windows/src/LyricsX.Windows.App/Taskbar/TaskbarLyricsWindow.xaml`
- Create: `Windows/src/LyricsX.Windows.App/Taskbar/TaskbarLyricsWindow.xaml.cs`
- Create: `Windows/src/LyricsX.Windows.App/Taskbar/TaskbarLyricsViewModel.cs`
- Create: `Windows/src/LyricsX.Windows.App/Taskbar/MarqueeStateMachine.cs`
- Create: `Windows/src/LyricsX.Windows.App/Taskbar/TaskbarLyricsStyles.xaml`
- Create: `Windows/tests/LyricsX.Windows.App.Tests/Taskbar/MarqueeStateMachineTests.cs`
- Create: `Windows/tests/LyricsX.Windows.App.Tests/Taskbar/TaskbarLyricsViewModelTests.cs`

**Interfaces consumed:** coordinator state, taskbar placement service, window host.

- [ ] Write failing marquee tests for fitting text, overflow, 800 ms initial pause, 30 px/s travel, 500 ms end pause, repeat, lyric change reset, and reduced-motion mode.

- [ ] Use this deterministic transition assertion:

```csharp
[TestMethod]
public void Advance_OverflowTextPausesMovesAndRepeats()
{
    var marquee = new MarqueeStateMachine(
        viewportWidth: 100, textWidth: 160, speedPixelsPerSecond: 30);

    Assert.AreEqual(0, marquee.Advance(TimeSpan.FromMilliseconds(799)).Offset);
    Assert.IsTrue(marquee.Advance(TimeSpan.FromSeconds(1)).Offset < 0);
    Assert.AreEqual(0, marquee.Advance(TimeSpan.FromSeconds(4)).Offset, 0.01);
}
```

- [ ] Run:

```powershell
dotnet test tests/LyricsX.Windows.App.Tests/LyricsX.Windows.App.Tests.csproj --filter "FullyQualifiedName~Taskbar"
```

Expected: compilation fails because the taskbar presentation types do not exist.

- [ ] Build the lyric surface with one clipped `Canvas`, two `TextBlock` instances for seamless marquee repetition, transparent background, one-line ellipsis fallback, and vertical centering within taskbar height. Defaults:

```text
Font: Segoe UI Variable Text
Size: 13 DIPs
Weight: SemiBold
Foreground: system text color
Shadow: subtle black/white contrast chosen from system theme
Horizontal padding: 8 DIPs
```

- [ ] Bind AutomationProperties.Name to the full current lyric. Use `TextTrimming=CharacterEllipsis` when animation is disabled or the taskbar is narrower than 120 DIPs.

- [ ] `TaskbarLyricsViewModel` must expose:

```csharp
[ObservableProperty] private string _text = "无正在播放的媒体";
[ObservableProperty] private string? _nextText;
[ObservableProperty] private double _progress;
[ObservableProperty] private bool _isSearching;
[ObservableProperty] private bool _isVisible;
```

- [ ] Subscribe to coordinator and placement changes, marshal to the UI dispatcher, call `ITaskbarLyricsWindowHost.Show` only when both state and placement are visible, and reset marquee when text or width changes.

- [ ] Run:

```powershell
dotnet test tests/LyricsX.Windows.App.Tests/LyricsX.Windows.App.Tests.csproj
```

Expected: marquee and view-model tests pass.

- [ ] Commit:

```powershell
git add Windows/src/LyricsX.Windows.App/Taskbar Windows/tests/LyricsX.Windows.App.Tests/Taskbar
git commit -m "feat(windows): render synchronized taskbar lyrics"
```

## Task 4: Implement compact left-click and full right-click menus

**Files:**

- Create: `Windows/src/LyricsX.Windows.App/Menus/LyricsMenuService.cs`
- Create: `Windows/src/LyricsX.Windows.App/Menus/CompactLyricsMenu.xaml`
- Create: `Windows/src/LyricsX.Windows.App/Menus/FullLyricsMenu.xaml`
- Create: `Windows/src/LyricsX.Windows.App/Menus/LyricsMenuViewModel.cs`
- Create: `Windows/src/LyricsX.Windows.App/LyricFiles/LocalLyricFileService.cs`
- Create: `Windows/src/LyricsX.Windows.App/LyricFiles/LyricExportService.cs`
- Create: `Windows/tests/LyricsX.Windows.App.Tests/Menus/LyricsMenuViewModelTests.cs`
- Create: `Windows/tests/LyricsX.Windows.App.Tests/LyricFiles/LocalLyricFileServiceTests.cs`

**Interfaces consumed:** coordinator, settings store, tray service, preference window service.

- [ ] Write failing command tests proving left-menu actions change offset, open manual search, toggle lyric visibility, and open `偏好设置…`; full menu additionally exposes local import, export, ignore track/album, provider, player, startup, diagnostics, and exit actions. Add file-service tests for local LRC/LRCX import, LRCX word-timing preservation, size/encoding validation, and export round-trip.

- [ ] Run:

```powershell
dotnet test tests/LyricsX.Windows.App.Tests/LyricsX.Windows.App.Tests.csproj --filter "FullyQualifiedName~LyricsMenu"
```

Expected: compilation fails because menu types do not exist.

- [ ] Implement the compact menu in this exact order:

```text
当前歌曲 — <title> / <artist>        disabled
歌词提前 0.5 秒
歌词延后 0.5 秒
手动搜索歌词…
隐藏任务栏歌词 / 显示任务栏歌词
偏好设置…
```

- [ ] Implement the full menu:

```text
当前歌曲 — <title> / <artist>        disabled
播放状态 — <player>                  disabled
歌词提前 0.5 秒
歌词延后 0.5 秒
重置歌词偏移
手动搜索歌词…
重新搜索歌词
打开本地歌词…
导出当前歌词…
忽略这首歌
忽略此专辑
隐藏任务栏歌词 / 显示任务栏歌词
歌词来源 >
播放器 >
登录时启动
打开日志文件夹
复制诊断信息
偏好设置…
退出 LyricsX
```

- [ ] In `TaskbarLyricsWindow`, distinguish `PointerPoint.Properties.PointerUpdateKind`:

  - left button: show compact `MenuFlyout`;
  - right button: show full `MenuFlyout`;
  - keyboard Menu key or Shift+F10: show full menu;
  - tray left/right activation: show full menu at the cursor.

Do not activate the lyric window; use the hidden shell message window as the flyout owner if WinUI requires an activatable owner.

- [ ] Implement `LocalLyricFileService` with WinUI `FileOpenPicker`, initialize it with the menu/manual-search owner HWND, allow `.lrc` and `.lrcx`, reject files over 4 MiB, decode UTF-8/UTF-8 BOM/UTF-16 LE/UTF-16 BE, parse through `ILyricParser`, and create provider ID `local-file`. Do not retain the original absolute path in settings, cache sidecars, logs, or diagnostics.

- [ ] Implement `LyricExportService` with `FileSavePicker`, initialize it with the owner HWND, offer `.lrc` and `.lrcx`, serialize standard lines as LRC and enhanced-word lines as LRCX, write UTF-8 without BOM through a temporary file followed by atomic replacement, and show a non-modal error without changing the active lyric when export fails.

- [ ] Run:

```powershell
dotnet test tests/LyricsX.Windows.App.Tests/LyricsX.Windows.App.Tests.csproj
```

Expected: all menu tests pass.

- [ ] Commit:

```powershell
git add Windows/src/LyricsX.Windows.App/Menus Windows/src/LyricsX.Windows.App/LyricFiles Windows/tests/LyricsX.Windows.App.Tests/Menus Windows/tests/LyricsX.Windows.App.Tests/LyricFiles Windows/src/LyricsX.Windows.App/Taskbar/TaskbarLyricsWindow.xaml.cs
git commit -m "feat(windows): add lyric and tray menus"
```

## Task 5: Build the Preferences shell and General/Display/Filter pages

**Files:**

- Create: `Windows/src/LyricsX.Windows.App/Preferences/PreferencesWindow.xaml`
- Create: `Windows/src/LyricsX.Windows.App/Preferences/PreferencesWindow.xaml.cs`
- Create: `Windows/src/LyricsX.Windows.App/Preferences/PreferencesViewModel.cs`
- Create: `Windows/src/LyricsX.Windows.App/Preferences/Pages/GeneralPage.xaml`
- Create: `Windows/src/LyricsX.Windows.App/Preferences/Pages/DisplayPage.xaml`
- Create: `Windows/src/LyricsX.Windows.App/Preferences/Pages/FilterPage.xaml`
- Create: `Windows/src/LyricsX.Windows.App/Preferences/ViewModels/GeneralPreferencesViewModel.cs`
- Create: `Windows/src/LyricsX.Windows.App/Preferences/ViewModels/DisplayPreferencesViewModel.cs`
- Create: `Windows/src/LyricsX.Windows.App/Preferences/ViewModels/FilterPreferencesViewModel.cs`
- Create: `Windows/tests/LyricsX.Windows.App.Tests/Preferences/PreferencesViewModelTests.cs`

**Interfaces consumed:** settings, startup, coordinator.

- [ ] Write failing tests proving validation, atomic save, cancel rollback, defaults, width clamping 80–360, offset clamping ±30, ignored track/album add-remove-clear behavior, and startup-service errors shown without losing other settings.

- [ ] Run:

```powershell
dotnet test tests/LyricsX.Windows.App.Tests/LyricsX.Windows.App.Tests.csproj --filter "FullyQualifiedName~Preferences"
```

Expected: compilation fails because preference view models do not exist.

- [ ] Build a 720×560 minimum-size normal WinUI window with left `NavigationView`. Use the title `LyricsX 偏好设置`, remember last page, support Ctrl+W, and save on Apply/OK.

- [ ] General page wording:

```text
通用
登录时启动 LyricsX
没有播放时隐藏歌词
暂停时继续显示当前歌词
自动匹配歌词
歌词时间偏移
恢复默认设置…
```

- [ ] Display page wording:

```text
显示
任务栏歌词宽度
字体
字号
字重
文字颜色
自动滚动过长歌词
跟随 Windows 动画设置
显示下一句歌词
```

- [ ] Filter page wording:

```text
过滤
忽略纯音乐
移除空行
过滤包含以下文字的歌词
简繁转换：不转换 / 转为简体 / 转为繁体
严格匹配歌曲标题和艺术家
已忽略的歌曲
已忽略的专辑
```

- [ ] Show ignored songs/albums as fingerprint hash prefixes plus the locally available current-session label, never as persisted plain song metadata. Provide Remove and Clear All actions with confirmation. `忽略这首歌` and `忽略此专辑` in the full menu update these lists; disable album ignore when album metadata is absent.

- [ ] Show inline validation below the affected control. Disable Apply only for invalid width, offset, font size, or duplicate filter phrase. Persist with `ISettingsStore.SaveAsync`; if save fails, retain edits and show a non-modal `InfoBar`.

- [ ] Run:

```powershell
dotnet test tests/LyricsX.Windows.App.Tests/LyricsX.Windows.App.Tests.csproj
dotnet build src/LyricsX.Windows.App/LyricsX.Windows.App.csproj
```

Expected: tests and XAML compilation pass.

- [ ] Commit:

```powershell
git add Windows/src/LyricsX.Windows.App/Preferences Windows/tests/LyricsX.Windows.App.Tests/Preferences
git commit -m "feat(windows): add general display and filter preferences"
```

## Task 6: Add Source, Player, and Shortcut preference pages

**Files:**

- Create: `Windows/src/LyricsX.Windows.App/Preferences/Pages/SourcePage.xaml`
- Create: `Windows/src/LyricsX.Windows.App/Preferences/Pages/PlayerPage.xaml`
- Create: `Windows/src/LyricsX.Windows.App/Preferences/Pages/ShortcutPage.xaml`
- Create: `Windows/src/LyricsX.Windows.App/Preferences/ViewModels/SourcePreferencesViewModel.cs`
- Create: `Windows/src/LyricsX.Windows.App/Preferences/ViewModels/PlayerPreferencesViewModel.cs`
- Create: `Windows/src/LyricsX.Windows.App/Preferences/ViewModels/ShortcutPreferencesViewModel.cs`
- Create: `Windows/tests/LyricsX.Windows.App.Tests/Preferences/SourcePreferencesViewModelTests.cs`
- Create: `Windows/tests/LyricsX.Windows.App.Tests/Preferences/PlayerPreferencesViewModelTests.cs`
- Create: `Windows/tests/LyricsX.Windows.App.Tests/Preferences/ShortcutPreferencesViewModelTests.cs`

**Interfaces consumed:** provider registry, media source, global hotkey, Spotify authorization.

- [ ] Write failing tests for source enable/reorder, at least one public source enabled, Spotify connect/disconnect state, player priority, ignored session restoration, shortcut conflict, and shortcut unregister on cancel.

- [ ] Run:

```powershell
dotnet test tests/LyricsX.Windows.App.Tests/LyricsX.Windows.App.Tests.csproj --filter "FullyQualifiedName~SourcePreferences|FullyQualifiedName~PlayerPreferences|FullyQualifiedName~ShortcutPreferences"
```

Expected: compilation fails because the view models do not exist.

- [ ] Source page:

  - Reorderable list with enable checkbox for QQ 音乐, 网易云音乐, 酷狗音乐, Musixmatch, LRCLIB, Spotify.
  - Explain that source order is used as a tie-breaker after match quality.
  - Spotify section accepts client ID, contains `连接 Spotify…` and `断开连接`, and stores refresh token only in Credential Manager.
  - Never show, copy, or export the refresh token.

- [ ] Player page:

  - Live list of GSMTC sessions with app name, source app ID, status, and last update.
  - `自动选择正在播放的播放器` enabled by default.
  - Optional priority ordering for Apple Music, 网易云音乐, QQ 音乐, 浏览器, Windows 媒体播放器, 其他.
  - Per-session `忽略` action stored by source app ID.

- [ ] Shortcut page:

```text
显示/隐藏任务栏歌词
手动搜索歌词
歌词提前 0.5 秒
歌词延后 0.5 秒
打开偏好设置
```

Capture modifier + virtual key, reject bare letters/digits and Windows-reserved combinations, test registration before saving, and restore prior registrations on Cancel.

- [ ] Run:

```powershell
dotnet test tests/LyricsX.Windows.App.Tests/LyricsX.Windows.App.Tests.csproj
dotnet build src/LyricsX.Windows.App/LyricsX.Windows.App.csproj
```

Expected: tests and XAML compilation pass.

- [ ] Commit:

```powershell
git add Windows/src/LyricsX.Windows.App/Preferences/Pages/SourcePage.xaml Windows/src/LyricsX.Windows.App/Preferences/Pages/PlayerPage.xaml Windows/src/LyricsX.Windows.App/Preferences/Pages/ShortcutPage.xaml Windows/src/LyricsX.Windows.App/Preferences/ViewModels Windows/tests/LyricsX.Windows.App.Tests/Preferences
git commit -m "feat(windows): add source player and shortcut preferences"
```

## Task 7: Implement manual lyric search and manual associations

**Files:**

- Create: `Windows/src/LyricsX.Windows.App/Search/ManualSearchWindow.xaml`
- Create: `Windows/src/LyricsX.Windows.App/Search/ManualSearchWindow.xaml.cs`
- Create: `Windows/src/LyricsX.Windows.App/Search/ManualSearchViewModel.cs`
- Create: `Windows/src/LyricsX.Windows.App/Search/LyricCandidateViewModel.cs`
- Create: `Windows/tests/LyricsX.Windows.App.Tests/Search/ManualSearchViewModelTests.cs`

**Interfaces consumed:** providers, scorer, coordinator, manual association store.

- [ ] Write failing tests for prefilled metadata, cancellation of prior query, incremental provider results, selection preview, `仅本次使用`, `始终用于这首歌`, deletion of an existing association, and delegation of `打开本地歌词…` to the tested file service from Task 4.

- [ ] Run:

```powershell
dotnet test tests/LyricsX.Windows.App.Tests/LyricsX.Windows.App.Tests.csproj --filter "FullyQualifiedName~ManualSearch"
```

Expected: compilation fails because manual search types do not exist.

- [ ] Build a 760×600 normal window:

```text
手动搜索歌词
歌曲名 [prefilled]
艺术家 [prefilled]
[搜索]

结果 columns: 来源 / 歌曲 / 艺术家 / 专辑 / 时长 / 匹配度
Preview: first five non-empty lyric lines

[打开本地歌词…] [仅本次使用] [始终用于这首歌] [取消关联] [取消]
```

- [ ] Debounce text changes 300 ms, cancel the prior search before starting a new one, append results on the UI dispatcher, and preserve keyboard selection.

- [ ] `仅本次使用` updates the active coordinator state without persisting a mapping. `始终用于这首歌` saves candidate into cache and fingerprint-to-cache association. `取消关联` removes only the current fingerprint mapping and leaves cached lyrics intact.

- [ ] Run:

```powershell
dotnet test tests/LyricsX.Windows.App.Tests/LyricsX.Windows.App.Tests.csproj
dotnet build src/LyricsX.Windows.App/LyricsX.Windows.App.csproj
```

Expected: tests and XAML compilation pass.

- [ ] Commit:

```powershell
git add Windows/src/LyricsX.Windows.App/Search Windows/tests/LyricsX.Windows.App.Tests/Search
git commit -m "feat(windows): add manual lyric matching"
```

## Task 8: Add localization, diagnostics, and safe log export

**Files:**

- Create: `Windows/src/LyricsX.Windows.App/Strings/zh-CN/Resources.resw`
- Create: `Windows/src/LyricsX.Windows.App/Strings/en-US/Resources.resw`
- Create: `Windows/src/LyricsX.Windows.App/Diagnostics/AppLogger.cs`
- Create: `Windows/src/LyricsX.Windows.App/Diagnostics/DiagnosticReportService.cs`
- Create: `Windows/src/LyricsX.Windows.App/Diagnostics/LogRetentionService.cs`
- Create: `Windows/tests/LyricsX.Windows.App.Tests/Diagnostics/DiagnosticReportServiceTests.cs`

**Interfaces produced:** localized resources and redacted diagnostic report.

- [ ] Write failing tests that feed access tokens, refresh tokens, query strings, Windows user paths, and authorization headers into diagnostics; assert none appear in output.

- [ ] Run:

```powershell
dotnet test tests/LyricsX.Windows.App.Tests/LyricsX.Windows.App.Tests.csproj --filter "FullyQualifiedName~DiagnosticReport"
```

Expected: compilation fails because diagnostics types do not exist.

- [ ] Move all visible strings from XAML/C# into RESW resources. Chinese is the default resource, English is the fallback. Include every menu and preference string from Tasks 4–7.

- [ ] Log JSON Lines to `%LOCALAPPDATA%\LyricsX\logs\LyricsX-YYYYMMDD.jsonl` with UTC timestamp, level, component, event ID, redacted message, and exception type. Rotate at 5 MiB, retain seven days, and cap the directory at 50 MiB.

- [ ] `DiagnosticReportService.CreateText` must include:

```text
LyricsX version
Windows version/build
Windows App SDK version
Architecture
Process uptime
Taskbar bounds/scale/auto-hide/fullscreen flags
Detected source app IDs and playback statuses
Enabled provider IDs and priority
Current lyric fingerprint hash prefix
Recent redacted error event IDs
```

Exclude titles, artists, lyrics, tokens, credential state, absolute user-profile paths, browser URLs, and raw provider responses.

- [ ] Implement `复制诊断信息` through the WinUI clipboard and `打开日志文件夹` through `ProcessStartInfo { FileName = logDirectory, UseShellExecute = true }`.

- [ ] Run:

```powershell
dotnet test tests/LyricsX.Windows.App.Tests/LyricsX.Windows.App.Tests.csproj
dotnet build LyricsX.Windows.slnx --configuration Release
```

Expected: all tests pass, both resource languages compile, and Release has zero warnings.

- [ ] Commit:

```powershell
git add Windows/src/LyricsX.Windows.App/Strings Windows/src/LyricsX.Windows.App/Diagnostics Windows/tests/LyricsX.Windows.App.Tests/Diagnostics
git commit -m "feat(windows): localize UI and add safe diagnostics"
```

## Task 9: Wire application lifetime and perform UI/accessibility smoke tests

**Files:**

- Modify: `Windows/src/LyricsX.Windows.App/App.xaml.cs`
- Modify: `Windows/src/LyricsX.Windows.App/Composition/ServiceRegistration.cs`
- Create: `Windows/tests/LyricsX.Windows.App.Tests/Lifetime/ApplicationLifetimeTests.cs`
- Create: `Windows/docs/ui-smoke-checklist.md`

**Interfaces consumed:** every service from plans 01–03.

- [ ] Write failing lifetime tests for startup order, second-instance command, tray exit, clean cancellation, preference window reuse, and unhandled-exception logging.

- [ ] Run:

```powershell
dotnet test tests/LyricsX.Windows.App.Tests/LyricsX.Windows.App.Tests.csproj --filter "FullyQualifiedName~ApplicationLifetime"
```

Expected: at least startup-order and clean-shutdown assertions fail.

- [ ] Use this startup order:

```text
load settings
start logging
create hidden shell message window
start tray
start media session source
start selected-session coordinator
start lyrics application coordinator
create taskbar lyric window
start taskbar placement observation
register enabled hotkeys
register startup state
```

- [ ] Use reverse order on shutdown, cancel one root `CancellationTokenSource`, await disposal with a five-second timeout, remove tray icon, and flush logs. Exit code 0 for user exit, 10 unsupported OS, 20 fatal initialization failure.

- [ ] Create `ui-smoke-checklist.md` with checkbox rows for:

  - keyboard-only access;
  - 100%, 125%, 150%, 200% scale;
  - light/dark/high-contrast themes;
  - reduced animation;
  - Chinese and English resources;
  - screen-reader names;
  - compact/full menu behavior;
  - preference validation;
  - manual search;
  - taskbar overlay focus behavior.

- [ ] Run:

```powershell
dotnet test LyricsX.Windows.slnx --filter "TestCategory!=LiveProvider"
dotnet publish src/LyricsX.Windows.App/LyricsX.Windows.App.csproj -c Release -r win-x64 --self-contained true
```

Expected: all automated tests pass and the published app completes the checklist on Windows 11 x64.

- [ ] Commit:

```powershell
git add Windows/src/LyricsX.Windows.App/App.xaml.cs Windows/src/LyricsX.Windows.App/Composition/ServiceRegistration.cs Windows/tests/LyricsX.Windows.App.Tests/Lifetime Windows/docs/ui-smoke-checklist.md
git commit -m "feat(windows): complete application lifetime"
```

## Plan 03 Exit Criteria

- [ ] The app runs unpackaged and self-contained on a clean Windows 11 x64 user account.
- [ ] Taskbar lyrics update smoothly without stealing focus or appearing in Alt+Tab.
- [ ] Left click opens the approved compact menu; right click and tray open the approved full menu.
- [ ] Preferences use Mac-compatible wording and contain no experimental page.
- [ ] General, Display, Filter, Source, Player, and Shortcut pages persist valid settings.
- [ ] Manual search supports one-time use and remembered association.
- [ ] Diagnostics contain no lyric content, tokens, user paths, or browser URLs.
- [ ] Chinese/English, keyboard, screen reader, theme, DPI, and reduced-motion checks pass.
