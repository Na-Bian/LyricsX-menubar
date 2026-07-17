# LyricsX Windows 11 Taskbar Lyrics — Design

- **Date:** 2026-07-17
- **Status:** Approved by user
- **Target:** Windows 11 x64
- **UI stack:** C# / .NET 10 / WinUI 3 / Windows App SDK
- **Distribution:** Unpackaged, self-contained application installed by a traditional EXE installer

## 1. Goal

Create a Windows 11 edition of this LyricsX fork that preserves the Mac
edition's complete non-experimental lyric workflow while replacing macOS-only
integration with Windows-native components.

The Windows edition must:

1. Follow the active media session from Apple Music, NetEase Cloud Music,
   QQ Music, supported browser media, and Windows Media Player.
2. Search, select, cache, parse, and synchronize lyrics with behavior equivalent
   to the current Mac fork.
3. Display the current lyric line in the leftmost free area of the primary
   Windows 11 taskbar.
4. Provide taskbar menus, a tray fallback, manual search, preferences, filters,
   shortcuts, source priority, player settings, and lyric offset controls.
5. Install through a conventional `LyricsX-Windows-Setup-x64.exe`.

The Windows edition is a functional rewrite, not a source-compatible Swift port.

## 2. Confirmed Product Decisions

| Decision | Selected behavior |
|---|---|
| Taskbar location | Leftmost free area on the primary display taskbar |
| Taskbar implementation | Independent top-level tool window; never inject into `Explorer.exe` |
| Player selection | Automatically follow the playing or most recently active session |
| Player scope | Apple Music, NetEase Cloud Music, QQ Music, browser media, Windows Media Player |
| Feature scope | Full non-experimental, Windows-applicable Mac feature set; explicit exceptions are in §3.2 |
| Delivery cadence | Complete the agreed scope before the first user-facing delivery |
| UI technology | WinUI 3 with Windows App SDK |
| Runtime | .NET 10 LTS |
| Multi-monitor behavior | Show taskbar lyrics on the primary display only |
| Left click | Open the compact shortcut menu |
| Right click | Open the complete application menu |
| Preferences wording | Use `偏好设置…`, consistent with the Mac edition |
| Experimental features | Do not port the Mac edition's Lab/experimental page or behavior |
| Installer | Conventional x64 EXE installer |
| Signing | Unsigned by default; include optional private self-signing scripts |
| Final verification | User-provided Windows 11 x64 PC or virtual machine |

## 3. Scope

### 3.1 In scope

- A new Windows solution under `Windows/`.
- WinUI 3 preferences and manual lyric-search windows.
- A taskbar-adjacent lyric overlay and system tray icon.
- Windows Global System Media Transport Controls session discovery and tracking.
- Player metadata normalization for the five confirmed player categories.
- LRC and LRCX parsing, filtering, language recognition, simplified/traditional
  Chinese conversion, offset adjustment, and synchronized line selection.
- Default lyric providers: QQMusic, NetEase, Kugou, Musixmatch, and LRCLIB.
- Optional authenticated Spotify lyric provider.
- Local lyric files, application cache, manual lyric association, source
  ordering, strict matching, ignored tracks/albums, and retry behavior.
- Global shortcuts.
- Optional start at user sign-in.
- Per-user EXE installation, upgrade, uninstall, checksums, and optional
  self-signing utilities.
- Automated core/provider tests and Windows 11 integration verification.

### 3.2 Out of scope

- Porting the Mac Lab/experimental page or its features.
- Modifying or removing the existing Mac implementation.
- Injecting a DLL into Explorer or modifying private taskbar structures.
- Replacing the Windows shell or reserving permanent taskbar space through
  unsupported interfaces.
- Windows 10, ARM64, x86, Microsoft Store, or MSIX delivery in the first release.
- A publicly trusted commercial code-signing certificate.
- Writing lyrics back into Apple Music/iTunes track metadata. The Windows
  media-session API does not expose the Mac edition's AppleScript/iTunes
  write-back path; Windows instead supports saving or exporting LRC/LRCX files.
- Sparkle updates. The first Windows release upgrades by running a newer EXE
  installer over the existing per-user installation.
- Scraping media-player process memory, browser DOMs, or private IPC protocols.
- Desktop karaoke, Touch Bar, and other display modes already removed or disabled
  in this Mac fork.

## 4. Repository Layout

```text
Windows/
  LyricsX.Windows.sln
  Directory.Build.props

  src/
    LyricsX.Core/                  # domain models, parsing, matching, sync
    LyricsX.Providers/             # lyric provider adapters and HTTP behavior
    LyricsX.Platform.Windows/      # media sessions, taskbar, tray, startup, Win32
    LyricsX.Windows.App/           # WinUI 3 windows, pages, menus, ViewModels

  tests/
    LyricsX.Core.Tests/
    LyricsX.Providers.Tests/
    LyricsX.Platform.Windows.Tests/
    LyricsX.Windows.TestMediaApp/  # controlled media-session producer

  installer/
    LyricsX.iss                    # Inno Setup per-user EXE installer

  tools/
    Create-PrivateSigningCertificate.ps1
    Sign-Release.ps1
    Verify-Release.ps1

  README.md
```

The Mac Xcode project remains the primary entry point for macOS. The Windows
solution is isolated so neither platform's build graph depends on the other.

## 5. Runtime and Deployment Model

### 5.1 Application model

- Target `net10.0-windows10.0.22000.0`.
- Use the stable Windows App SDK 1.8 release line; pin the exact patch in the
  implementation plan and project lock data.
- Publish as `win-x64`, self-contained, and unpackaged.
- Use a full-trust desktop process without package identity.
- Access `Windows.Media.Control` directly from the desktop process.

Microsoft documents `GlobalSystemMediaTransportControlsSessionManager` use from
ordinary C++ and C# desktop console programs. Package identity is therefore not
part of the first-release architecture. If a future MSIX/Store build is added,
that package will separately declare `globalMediaControl` and use a trusted
package signature.

### 5.2 Installation

Inno Setup produces one per-user installer:

```text
LyricsX-Windows-Setup-x64.exe
```

The installer:

- installs under `%LocalAppData%\Programs\LyricsX`;
- does not require administrator rights for the normal path;
- creates Start menu and uninstall entries;
- offers an explicit start-at-sign-in checkbox;
- registers start-at-sign-in through the current user's `Run` entry;
- shuts down a running instance before upgrade;
- preserves preferences and lyric cache during normal uninstall;
- offers a separate `Delete user data` option;
- writes no Windows App SDK or .NET prerequisites because both are bundled.

### 5.3 Signing

The default private build is unsigned. Windows SmartScreen may require
`More info` → `Run anyway` on first launch.

The repository includes optional scripts that:

1. create a SHA-256 self-signed code-signing certificate on the user's Windows
   machine;
2. export the public `.cer` and password-protected private `.pfx`;
3. sign the application binaries and installer with SignTool;
4. verify the Authenticode signature.

The `.pfx` and its password are never committed or shared. A friend may receive
the public `.cer` only after separately verifying its SHA-256 fingerprint.
Self-signing does not create public SmartScreen reputation.

## 6. Component Architecture

### 6.1 `LyricsX.Core`

Pure C# with no WinUI or Win32 dependency. It owns:

- `MediaTrack`, `PlaybackSnapshot`, `LyricDocument`, `LyricLine`, and metadata
  models;
- LRC/LRCX parsing and serialization;
- title, artist, album, duration, and fingerprint normalization;
- candidate scoring and strict-match rules;
- line filtering and language recognition;
- simplified/traditional Chinese conversion through an isolated converter
  interface;
- offset application and synchronized current-line calculation;
- manual lyric association and cache naming rules.

All time-dependent code accepts an injected clock. All file-dependent code
accepts an injected storage interface.

### 6.2 `LyricsX.Providers`

Each provider implements one contract:

```csharp
public interface ILyricsProvider
{
    string Id { get; }
    IAsyncEnumerable<LyricCandidate> SearchAsync(
        LyricSearchRequest request,
        CancellationToken cancellationToken);
}
```

Adapters are independent for QQMusic, NetEase, Kugou, Musixmatch, LRCLIB, and
optional authenticated Spotify. Provider failures are captured independently;
one failed source never terminates the aggregate search.

Provider credentials are stored through Windows Credential Manager. They are
never written to settings files, crash reports, or logs.

### 6.3 `LyricsX.Platform.Windows`

This project owns operating-system integration:

- `WindowsMediaSessionSource`
- `MediaSessionCoordinator`
- `TaskbarLocator`
- `TaskbarOccupancyReader`
- `TaskbarLyricsWindowHost`
- `FullscreenDetector`
- `TrayIconHost`
- `StartupRegistration`
- `GlobalShortcutService`

Win32 and COM interop definitions stay internal to this project. Other projects
communicate through interfaces and immutable snapshots.

### 6.4 `LyricsX.Windows.App`

The WinUI 3 app owns:

- taskbar lyric content and animations;
- compact left-click menu;
- full right-click/tray menu;
- preferences pages;
- manual search and candidate selection;
- first-run and diagnostics UI;
- application lifetime and single-instance coordination.

ViewModels depend on core/platform interfaces through constructor injection.
They do not call P/Invoke or provider HTTP clients directly.

## 7. Media Session Flow

### 7.1 Session discovery

At startup, request
`GlobalSystemMediaTransportControlsSessionManager` and subscribe to:

- `SessionsChanged`;
- `CurrentSessionChanged`;
- each session's `MediaPropertiesChanged`;
- `PlaybackInfoChanged`;
- `TimelinePropertiesChanged`.

Event handlers enqueue refreshes through one serialized coordinator. Repeated
notifications are coalesced so UI work cannot overlap.

### 7.2 Active-session selection

Rank available sessions in this order:

1. a session whose playback status is `Playing`;
2. the most recently active session observed by this app;
3. the session returned by Windows as `GetCurrentSession()`;
4. the previously selected valid session;
5. a stable source-app ID tie-breaker.

The selected session remains sticky while paused. A different session replaces
it only when that session starts playing or becomes Windows' current session
after direct user interaction.

### 7.3 Player support contract

Apple Music, NetEase Cloud Music, QQ Music, browser media, and Windows Media
Player use the same public session interface. Per-player adapters may normalize
known metadata differences, source IDs, artist delimiters, or missing album
strings.

The application cannot invent metadata that a player does not publish. It must
surface `播放器未提供完整歌曲信息` in diagnostics rather than scrape private
process state. The user's Windows device is the acceptance environment for the
current versions of all five player categories.

## 8. Lyric Search and Selection

### 8.1 Search order

For each new track fingerprint:

1. cancel all work for the previous track;
2. load a manually associated lyric;
3. load a cached LRC/LRCX file;
4. if a player exposes a local media path, check adjacent `.lrcx` then `.lrc`;
5. query all enabled online providers with bounded concurrency.

The application shows the first valid online result immediately, then keeps a
five-second quality window open. A later candidate replaces the current lyric
only when its score is strictly higher.

### 8.2 Candidate scoring

Apply the following ordered comparison:

1. exact normalized title + artist + album;
2. exact normalized title + artist;
3. smaller duration difference;
4. user-configured provider priority;
5. provider result order.

Strict matching rejects candidates that fail title/artist validation. Empty,
disabled, or non-displayable lyric documents are always rejected.

### 8.3 Persistence

Use:

```text
%LocalAppData%\LyricsX\
  settings.json
  Lyrics\
  Logs\
```

Settings and association indexes use atomic replace-on-success writes. Manual
associations use both the player-provided track ID and a normalized
title/artist/album fingerprint, matching the current Mac fork's resilience to
unstable track IDs.

## 9. Synchronization

`LyricsSyncEngine` combines:

- the selected session's timeline position;
- playback status and playback rate;
- a monotonic local clock between session updates;
- the lyric document's built-in delay;
- the user's offset.

The engine reconciles against the system timeline on timeline events and at a
low-frequency safety interval. It schedules the next lyric boundary rather than
redrawing continuously. UI updates occur only when the current lyric line or
display state changes.

Track changes cancel scheduled boundaries. Paused playback freezes local
extrapolation. Seeking triggers immediate recomputation.

## 10. Taskbar Lyrics Window

### 10.1 Placement

The overlay is an independent, borderless, non-resizable WinUI 3 tool window.
It does not become an Explorer child and does not inject code into Explorer.

Placement logic:

1. locate the primary taskbar and its rectangle;
2. read visible taskbar elements through public window geometry and UI
   Automation;
3. compute occupied horizontal intervals, including Widgets/system buttons and
   the centered app group;
4. select the leftmost free interval;
5. inset by 12 physical pixels after DPI conversion;
6. clamp the user width to the free interval.

Default width is 260 device-independent pixels (DIPs); preferences display this
as `260 px` and allow 80–360 DIPs. Geometry is converted to physical pixels for
the active taskbar DPI. If content exceeds the available width, use a marquee
animation whose duration is based on text width and the current line's display
duration.

If no free interval of at least 80 DIPs exists, hide the overlay and keep the
tray icon available. Diagnostics explain that the taskbar has insufficient free
space.

### 10.2 Window behavior

- Keep the window visually above the taskbar without using unsupported taskbar
  internals.
- Hide when the primary taskbar auto-hides.
- Hide while a true full-screen app occupies the primary display.
- Restore after leaving full screen.
- Recompute placement after display, DPI, resolution, taskbar, Explorer, or
  primary-monitor changes.
- Reacquire the taskbar after Explorer restarts.
- Never take keyboard focus when a menu is not open.

### 10.3 Interaction

Left click opens a compact menu containing:

- current player and track;
- lyric offset decrement/increment;
- manual lyric search;
- show/hide lyrics;
- `偏好设置…`.

Right click opens the full menu containing the compact actions plus source,
player, startup, diagnostics, and exit actions. The tray icon exposes the same
full menu as a permanent recovery path.

## 11. Preferences

The Windows preference window contains:

1. `通用`
2. `显示`
3. `过滤`
4. `快捷键`
5. `歌词源`
6. `播放器`

There is no `实验` page.

The pages preserve the applicable Mac behavior, including:

- taskbar lyric enablement and width;
- font, color, marquee, paused-state, and language conversion settings;
- ignored track and album filters;
- global shortcuts;
- source enablement and drag-to-reorder priority;
- Musixmatch token and optional Spotify authentication;
- preferred/ignored media sessions;
- start at sign-in.

## 12. Display States

The taskbar window has five explicit states:

| State | Text/behavior |
|---|---|
| No session | `未检测到播放器` |
| Searching | `正在搜索歌词…` |
| Synchronized | Current lyric line |
| No result | `无可用歌词` |
| Offline cache | Cached lyric plus an offline diagnostic indicator |

State changes are deterministic and covered by ViewModel tests.

## 13. Error Handling and Diagnostics

- Provider errors are isolated and tagged by provider ID.
- No-result searches retry twice after a two-second delay.
- HTTP operations have cancellation and bounded timeouts.
- Track changes invalidate all prior result tokens.
- Corrupt cache files are quarantined and ignored.
- Settings writes are atomic and preserve the last known-good file.
- Explorer/taskbar lookup failure enters a retry loop with backoff while the tray
  remains usable.
- Global media-session access failure produces an actionable diagnostics entry.
- Logs rotate locally and redact credentials, query tokens, and sensitive paths.
- `导出诊断信息` creates a user-reviewable archive with settings secrets removed.

## 14. Testing Strategy

### 14.1 Automated tests

`LyricsX.Core.Tests` covers:

- LRC/LRCX parsing and serialization;
- malformed input;
- lyric filtering;
- language recognition/conversion;
- normalization and fingerprints;
- candidate scoring and strict matching;
- offset and line-boundary calculations;
- pause, seek, rate, and track-change behavior using a fake clock.

`LyricsX.Providers.Tests` uses checked-in response fixtures for every provider.
Live network tests are opt-in and never run as the only CI assertion.

`LyricsX.Platform.Windows.Tests` covers session ranking, event coalescing,
taskbar interval selection, DPI conversion, fullscreen decisions, startup
registration, and Explorer-restart recovery through fakes around native calls.

`LyricsX.Windows.TestMediaApp` publishes a controlled media session for
end-to-end tests of title, artist, timeline, pause, seek, and track changes.

### 14.2 Windows 11 manual matrix

The user's Windows 11 x64 machine or VM validates:

- Apple Music;
- NetEase Cloud Music;
- QQ Music;
- Edge/Chrome browser media;
- Windows Media Player;
- display scale at 100%, 125%, 150%, and 200%;
- taskbar auto-hide;
- centered taskbar icons and optional Widgets;
- fullscreen video/application behavior;
- Explorer restart;
- start at sign-in;
- install, upgrade, uninstall, and optional data removal.

For each player, acceptance requires correct session selection, track metadata,
search state, lyric result, playback position updates, and current-line changes.
If a player omits required metadata, diagnostics must identify the missing
fields and the exact player version.

### 14.3 CI

A Windows CI workflow builds the solution, runs non-live tests, publishes the
self-contained x64 payload, builds the installer, and emits SHA-256 checksums.
The first release is not considered verified until the same installer passes the
manual Windows 11 matrix.

## 15. Release Artifacts

```text
LyricsX-Windows-Setup-x64.exe
LyricsX-Windows-Setup-x64.exe.sha256
LyricsX-Windows-portable-x64.zip
LyricsX-Windows-portable-x64.zip.sha256
RELEASE-NOTES-zh.md
```

The portable ZIP is a diagnostic convenience, not the primary installation
path. It does not register start-at-sign-in or create shortcuts.

## 16. Licensing and Third-Party Services

Ported LyricsX/LyricsKit behavior remains under the repository's MPL-2.0
license. Source files derived from existing covered files retain the required
notices. New third-party packages must be license-compatible and recorded in a
Windows third-party notices file.

QQMusic, NetEase, Kugou, Musixmatch, LRCLIB, and Spotify integrations are
independent adapters because third-party endpoints and terms can change. A
provider can be disabled without disabling local lyrics or other providers.

## 17. Risks and Mitigations

| Risk | Mitigation |
|---|---|
| A player does not publish complete media-session data | Normalize known variants; show exact missing fields; do not scrape private state |
| Windows changes taskbar internals | Use a separate window, public geometry, and UI Automation; no Explorer injection |
| Widgets or centered icons consume the left region | Compute free intervals and clamp/hide safely |
| Lyric endpoint changes | Isolated providers, fixtures, timeouts, health diagnostics, user-controlled enablement |
| Unsigned installer triggers SmartScreen | Document the prompt; provide optional private self-signing scripts |
| macOS cannot build or run WinUI 3 | Build in Windows CI and verify on the user's Windows 11 device |
| Scope is large for a first delivery | Keep modules isolated and tests runnable throughout, while withholding the user-facing release until the complete agreed scope passes |

## 18. Completion Criteria

The migration is complete only when:

1. all projects build in a clean Windows environment;
2. all non-live automated tests pass;
3. the EXE installer installs, upgrades, launches, and uninstalls correctly;
4. all five confirmed player categories pass the Windows 11 manual matrix or
   have a user-approved, documented player-side limitation;
5. taskbar placement passes the DPI, Widgets, auto-hide, fullscreen, and
   Explorer-restart cases;
6. online search, cache, manual association, matching, offset, conversion,
   preferences, filters, shortcuts, source priority, and startup behavior pass;
7. the Windows preferences contain no experimental page or feature;
8. release artifacts and SHA-256 checksums are generated;
9. the user accepts the tested Windows installer.

## 19. Authoritative References

- Windows media session manager:
  <https://learn.microsoft.com/en-us/uwp/api/windows.media.control.globalsystemmediatransportcontrolssessionmanager>
- Microsoft desktop media-session sample:
  <https://devblogs.microsoft.com/oldnewthing/20231108-00/?p=108980>
- Windows App SDK deployment overview:
  <https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/deploy-overview>
- Unpackaged WinUI 3 distribution:
  <https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/unpackage-winui-app>
- WinUI 3 window management:
  <https://learn.microsoft.com/en-us/windows/apps/develop/ui/manage-app-windows>
- SignTool:
  <https://learn.microsoft.com/en-us/windows/win32/seccrypto/signtool>
- PowerShell self-signed certificates:
  <https://learn.microsoft.com/en-us/powershell/module/pki/new-selfsignedcertificate>
