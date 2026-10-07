# LyricsX for Windows

任务栏歌词:在 Windows 11 的任务栏空闲区域实时显示正在播放歌曲的同步歌词。

## 环境要求 (Requirements)

- Windows 11(x64 或 ARM64),build 22000 或更高
- 播放器需通过系统媒体传输(GSMTC)公开播放信息(见"支持的播放器")

## 安装 (Install)

1. 校验 `SHA256SUMS.txt` 中的哈希后运行 `LyricsX-Windows-Setup-<架构>.exe`
2. 按向导完成安装(当前用户级,无需管理员权限)
3. 可选勾选"登录时启动 LyricsX"

## 首次启动与 SmartScreen (First launch and SmartScreen)

未签名的安装包首次运行时,Windows SmartScreen 可能提示"已保护你的电脑"。
点击"更多信息"→"仍要运行"。发布前请核对 SHA-256 校验和(见"私有分享与校验")。

## 支持的播放器 (Supported players)

以下五类播放器通过 GSMTC 提供元数据与进度:

- Apple Music(Windows 版)
- 网易云音乐
- QQ 音乐
- 浏览器媒体(Edge/Chrome 等,取决于网页是否暴露媒体会话)
- Windows 媒体播放器(媒体播放器应用)

播放器需在播放时将元数据/时间轴发布到系统媒体控件。

## 任务栏行为 (Taskbar behavior)

歌词条显示在任务栏最左侧的空闲区域(居中图标布局)。可用宽度低于 80 DIP、
任务栏自动隐藏、或前台应用全屏时自动隐藏。仅支持主显示器。

## 菜单与偏好设置 (Menus and preferences)

托盘/歌词菜单提供偏好设置、手动搜索、偏移调整、忽略曲目、日志与诊断入口。
偏好设置窗口(单实例)分六页:

- **通用**:登录时启动、自动匹配歌词、无媒体时隐藏;
- **显示**:任务栏歌词宽度(80–360 DIP)、时间偏移(±30 秒)、字号、
  暂停显示、下一句、跑马灯、跟随系统动画;
- **来源**:六源启用与优先级排序(至少启用一个)、Musixmatch token、
  Spotify 连接/断开;
- **播放器**:自动选择或固定跟踪某个 GSMTC 来源;
- **过滤**:严格匹配、屏蔽词、忽略列表清除;
- **快捷键**:捕获式绑定四个动作(偏好设置/显示切换/提前/延后),
  校验修饰键与 Windows 保留组合。

来源与播放器改动在下次启动时完全生效;显示/过滤立即生效。

## Spotify 可选连接 (Spotify optional connection)

偏好设置 → 来源 → Spotify:填入 Client ID 后点击"连接 Spotify…",
浏览器完成授权(PKCE + 本地回环),refresh token 仅存于 Windows 凭据管理器。

## 开机自启 (Startup)

安装时勾选,或偏好设置 → 通用 → 登录时启动。写入
`HKCU\...\CurrentVersion\Run`,卸载时移除。

## 升级 (Update by installing a newer version)

直接运行新版安装包覆盖安装。偏好设置、缓存、关联、日志全部保留。

## 卸载 (Uninstall)

系统"设置 → 应用"或开始菜单"卸载 LyricsX"。默认保留用户数据;
卸载时选择"是"或静默卸载传 `/DELETEUSERDATA=1` 才删除
`%LOCALAPPDATA%\LyricsX`。

## 用户数据位置 (User data location)

`%LOCALAPPDATA%\LyricsX`:settings.json / associations.json / cache\ / logs\。
Spotify refresh token 在 Windows 凭据管理器(LyricsX/SpotifyRefreshToken)。

## 私有分享与校验 (Private sharing and checksum verification)

```powershell
Get-FileHash .\LyricsX-Windows-Setup-x64.exe -Algorithm SHA256
Get-Content .\SHA256SUMS.txt
```

哈希一致再安装。自签名信任流程见 `docs/private-signing.md`。

## 已知限制 (Known limitations)

- 播放器必须通过 GSMTC 公开元数据与进度;
- 浏览器支持取决于具体标签页/站点是否暴露媒体会话;
- 仅支持主显示器任务栏;
- 空闲宽度不足 80 DIP、自动隐藏、前台全屏时歌词隐藏;
- 不支持 Windows 10;
- 不写入 Apple Music/iTunes 文件元数据;
- 未签名/自签名构建可能触发 SmartScreen。

## 从源码构建 (Build from source)

需要 .NET 10 SDK(x64/ARM64):

```powershell
cd Windows
dotnet restore LyricsX.Windows.slnx --locked-mode
dotnet test LyricsX.Windows.slnx -c Release
pwsh -File tools/Publish-Release.ps1 -Version 0.1.0
```
