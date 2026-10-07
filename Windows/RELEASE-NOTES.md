# LyricsX for Windows 0.1.0

发布日期:2026-10-02 · Windows 11(x64 / ARM64)· 当前用户安装,无需管理员

## 首个公开测试版特性

- 任务栏歌词:贴任务栏空闲区(默认左端),跑马灯滚动,逐字进度数据保留
- 六个歌词来源:QQ 音乐 / 网易云音乐 / 酷狗 / Musixmatch / LRCLIB /
  Spotify(可选连接,PKCE 授权,refresh token 存凭据管理器)
- 歌词偏移 ±30s、简繁转换、屏蔽词过滤、忽略歌曲/专辑
- 手动搜索窗口(预填元数据/仅本次使用/始终使用)与本地 LRC/LRCX
  导入导出(手动搜索窗口导入,歌词菜单导出)
- 任务栏歌词左键/右键菜单:偏移调整、显示切换、忽略曲目、日志与诊断
- "偏好设置"窗口六页:通用(自启/自动匹配)、显示(宽度/偏移/字号/
  下一句/跑马灯)、来源(启用排序/Musixmatch/Spotify)、播放器(自动或
  固定 GSMTC 来源)、过滤(严格匹配/屏蔽词/忽略列表)、快捷键(捕获式
  绑定四个动作)
- 全局快捷键(默认 Ctrl+Alt+L 打开偏好设置,可自定义)、开机自启
  (注册表当前用户)
- 诊断脱敏日志(%LOCALAPPDATA%\LyricsX\logs,JSON Lines,自动轮转)

## 已知限制

- 播放器须通过 GSMTC 公开元数据;浏览器取决于页面媒体会话
- 仅主显示器;宽度 <80 DIP / 自动隐藏 / 前台全屏时隐藏
- 不支持 Windows 10;不写 Apple Music/iTunes 文件元数据
- 未签名/自签名构建可能触发 SmartScreen(详见 README)

## 安装与升级

运行 Setup 即覆盖升级,用户数据全保留。卸载默认保留数据,
确认删除才清除 %LOCALAPPDATA%\LyricsX。

## 校验

发布目录 SHA256SUMS.txt 与 Get-FileHash 核对;私有签名流程见
docs/private-signing.md。
