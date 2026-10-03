# LyricsX for Windows 0.1.0

发布日期:2026-10-02 · Windows 11(x64 / ARM64)· 当前用户安装,无需管理员

## 首个公开测试版特性

- 任务栏歌词:贴任务栏空闲区(默认左端),跑马灯滚动,逐字进度数据保留
- 六个歌词来源:QQ 音乐 / 网易云音乐 / 酷狗 / Musixmatch / LRCLIB /
  Spotify(可选连接,refresh token 存凭据管理器)
- 歌词偏移 ±30s、简繁转换、屏蔽词过滤、忽略歌曲/专辑(偏好设置窗口
  提供;首版仅宽度与偏移两项设置,其余逻辑与测试就位)
- 手动关联与本地 LRC/LRCX 解析/导出(引擎级,UI 入口在后续版本)
- 全局快捷键、开机自启(注册表当前用户)
- 首版"偏好设置"窗口提供:任务栏歌词宽度、歌词时间偏移
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
