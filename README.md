# LyricsX 菜单栏歌词版

这是一个基于 LyricsX 的个人精简版 fork。原项目是 macOS 歌词应用，包含桌面歌词、独立歌词窗口、Touch Bar 歌词和菜单栏歌词等功能；本版本只保留我实际需要的 **菜单栏歌词显示能力**，移除了桌面歌词和独立窗口入口，让应用更轻、更安静。

<img src="docs/img/icon.png" width="128px">

## 功能

- 在 macOS 菜单栏显示当前播放歌曲的同步歌词
- 自动搜索、下载并匹配歌词
- 支持常见音乐播放器，例如 Apple Music、Spotify、Vox、Audirvana、Swinsian
- 支持多歌词源，依赖 LyricsKit
- 可在菜单栏菜单中调整歌词延迟
- 支持手动搜索歌词、标记错误歌词、打开歌词文件位置
- 支持繁简中文转换
- 支持随播放器启动或退出

## 与原版的区别

本版本移除了或禁用了以下显示能力：

- 桌面 Karaoke 歌词窗口
- 独立歌词 HUD 窗口
- Touch Bar 歌词
- 与上述显示方式相关的偏好设置和快捷键入口

保留的核心能力是：播放器监听、歌词搜索、歌词同步、菜单栏显示和基础偏好设置。

## 系统要求

- macOS 11 或更高版本
- Xcode 26 或兼容的完整 Xcode 环境

## 构建

首次构建前，请在 Xcode 中登录自己的 Apple ID，并为 `LyricsX` 和 `LyricsXHelper` 两个 target 设置自己的 Team 与 Bundle Identifier。

本 fork 当前使用：

```text
Team ID: 85WF7CFPKM
主程序 Bundle ID: com.nabian.LyricsX
Helper Bundle ID: com.nabian.LyricsXHelper
```

Debug 构建：

```bash
xcodebuild -project LyricsX.xcodeproj \
  -scheme LyricsX \
  -configuration Debug \
  -derivedDataPath ./DerivedData \
  -skipMacroValidation \
  -allowProvisioningUpdates \
  build
```

Release 构建：

```bash
xcodebuild -project LyricsX.xcodeproj \
  -scheme LyricsX \
  -configuration Release \
  -derivedDataPath ./DerivedData \
  -skipMacroValidation \
  -allowProvisioningUpdates \
  build
```

Release 产物路径：

```text
DerivedData/Build/Products/Release/LyricsX.app
```

## 签名说明

如果使用 Personal Team，部分能力不可用，例如 iCloud capability。本版本已经移除 iCloud entitlement，以便个人账号也可以构建和运行。

如果构建时弹出钥匙串密码请求：

- 密码是当前 Mac 的登录密码
- 建议选择 `Always Allow`，避免每次构建都重复询问

## 使用

1. 构建 Release 版本。
2. 打开 `DerivedData/Build/Products/Release/LyricsX.app`。
3. 按 macOS 提示授予自动化、音乐控制等权限。
4. 播放音乐后，歌词会显示在菜单栏。

如果歌词没有出现，可以从菜单栏图标打开菜单，检查是否启用了菜单栏歌词，并尝试手动搜索歌词。

## Musixmatch 歌词源

如果需要使用 Musixmatch 作为歌词源，需要自行获取 usertoken，并在偏好设置中填写。

可参考：

- https://gist.github.com/TrueMyst/0461aea999e347182486934fd83a4cf9
- https://spicetify.app/docs/faq#sometimes-popup-lyrics-andor-lyrics-plus-seem-to-not-work

## 致谢

本项目基于 LyricsX 及其相关生态：

- LyricsX: https://github.com/ddddxxx/LyricsX
- LyricsKit: https://github.com/ddddxxx/LyricsKit
- MusicPlayer: https://github.com/ddddxxx/MusicPlayer

主要开源依赖包括：

- SwiftyOpenCC
- GenericID
- SwiftCF
- Regex
- Semver
- SnapKit
- MASShortcut
- Sparkle

## 免责声明

歌词版权归原作者和权利方所有。本项目仅用于个人学习和自用场景。
