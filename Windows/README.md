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

## 左键紧凑菜单 (Left-click compact menu)

当前歌曲 / 歌词提前 0.5 秒 / 歌词延后 0.5 秒 / 手动搜索歌词… /
隐藏(显示)任务栏歌词 / 偏好设置…

## 右键与托盘完整菜单 (Right-click and tray full menu)

完整菜单额外提供:重置歌词偏移、重新搜索、打开本地歌词、导出当前歌词、
忽略这首歌/专辑、歌词来源、播放器、登录时启动、打开日志文件夹、
复制诊断信息、退出。

## 偏好设置 (Preferences)

标题"LyricsX 偏好设置"。通用(启动/暂停显示/自动匹配/时间偏移)、显示(宽度
80–360 DIP、字体字号字重、动画)、过滤(屏蔽词、简繁转换、忽略列表)、
来源(启用与优先级排序)、播放器(会话列表与忽略)、快捷键。

## 手动搜索歌词 (Manual lyric search)

完整菜单 → 手动搜索歌词…。预填当前歌曲元数据,可改写后搜索,
结果按匹配度排序;支持"仅本次使用"与"始终用于这首歌"。

## Spotify 可选连接 (Spotify optional connection)

偏好设置 → 来源 → Spotify:填入 Client ID 后点击"连接 Spotify…",
浏览器完成授权,refresh token 仅存于 Windows 凭据管理器。

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
