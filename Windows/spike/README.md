# Windows 任务栏歌词覆盖窗口 Spike(2026-10-02)

在用户的 VMware Fusion VM(Windows 11 ARM64,Insider build 26220,beta 通道水印 26120)上,
用零依赖的 PowerShell + WinForms + Win32 P/Invoke 验证了任务栏覆盖窗口方案的可行性。

## 结论:方案可行,全部验证项通过

| 验证项 | 结果 |
|---|---|
| 贴任务栏最左侧(Win11 居中图标布局空区) | ✅ 不遮挡任何图标 |
| `WS_EX_NOACTIVATE \| WS_EX_TOOLWINDOW` + TOPMOST | ✅ exstyle=0x8010188 生效 |
| 中文(CJK)+ ♪ 符号渲染 | ✅ 正常(需 UTF-8 BOM) |
| 跑马灯滚动(120ms 定时器) | ✅ 持续渲染 |
| DPI 感知定位(225% 缩放,2940×1846 物理) | ✅ 完全落在任务栏 108px 高度内 |
| MinimizeAll("显示桌面"旧机制)存活性 | ✅ 未被最小化,无需恢复 |
| 真实 Win+D 按键(keybd_event)存活性 | ✅ 心跳不断、bounds 不变 |

任务栏物理矩形:`Shell_TrayWnd` (0,1738)-(2940,1846),高 108px;窗口占用 (16,1744) 756×96。

## 通过 SSH 在交互会话做 GUI 验收的模式(后续正式开发沿用)

1. **GUI 只能在控制台会话(session 1)创建**:SSH 会话里 `FindWindow` 找不到 `Shell_TrayWnd`,
   WinForms 窗口也不会显示在屏幕上。一律走计划任务:
   `schtasks /create /f /tn X /tr "powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File C:\...\x.ps1" /sc once /st 23:59`
   然后 `schtasks /run /tn X`(默认"仅在用户登录时运行",落在 session 1)。
2. **截图必须 DPI 感知**:`SetProcessDPIAware()` + `GetSystemMetrics(0/1)` 取物理尺寸再
   `CopyFromScreen`,否则只截到左上角逻辑区域(约 1/2.25)。
3. **stdin/观察**:进程状态用 `tasklist`、窗口状态用脚本自带心跳日志(写 `%TEMP%` 的 log)。
4. **时序余量**:截图任务后等 ≥10s 再 scp(该 VM 的 PowerShell 冷启动 4–6s)。
5. 真实按键模拟:在 session 1 的任务里用 `keybd_event`(SSH 会话里 SendKeys 只影响 SSH 会话)。

## 实现层教训(给计划 02/03)

- **PowerShell 5.1 读脚本**:含中文的 `.ps1` 必须带 UTF-8 BOM,否则按 ANSI 解码变乱码。
  (正式 app 用 .NET/C#,不受影响。)
- **`Form.AutoSizeMode` 没有 `'None'`**(只有 `GrowAndShrink`/`GrowOnly`),配合
  `$ErrorActionPreference='Stop'` 会让脚本静默退出码 1。排障手段:任务包一层
  `cmd /c powershell ... > err.txt 2>&1` 捕获 stderr,并在脚本里放 `trap` 落盘。
- **窗口样式组合结论**:`WS_EX_TOOLWINDOW + WS_EX_NOACTIVATE + TOPMOST` 的无边框窗口
  天然不被"显示桌面/Win+D"最小化,计划 02 里的"最小化自动恢复"可降级为防御性逻辑。
- VM 显示缩放 225%(2940×1846 物理),是 DPI 回归测试的极端档位,好事。

## 文件

- `spike.ps1` — 覆盖窗口本体(DPI 感知、心跳、防御性恢复、trap)
- `spike-shot.ps1` — DPI 感知全屏截图
- `spike-minimize.ps1` / `spike-winkey.ps1` — MinimizeAll / 真实 Win+D 触发器
- `spike-run.cmd` — stderr 捕获包装(排障用)
- `spike-shot-*.png` — 各阶段截图证据(2=v1乱码/溢出,4=v2中文正常,8=Win+D 后存活)

VM 上的进程、计划任务(LyricsXSpike/Shot/Min/Win/Dbg 共 5 个)与临时文件均已清理。
