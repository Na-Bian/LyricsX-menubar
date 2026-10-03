# Windows 验收矩阵(计划 04,VM:Windows 11 ARM64 Insider build 26220)

图例:✅ 通过 / ⚠️ 带限制通过 / ⛔ 阻塞 / ⏳ 待交互会话验收

## 环境级事实(先读)

**本 VM 的 GSMTC 枚举在 Insider build 26220 上系统性损坏**:测试应用、
GetForWindow 路线、系统"媒体播放器"金丝雀三者的会话均不可见
(见 manual-media-observations.md)。LyricsX 自身管线(测试应用 →
SMTC 发布 → 命名管道控制 → coordinator 状态机)已端到端验证。
计划允许"用户接受且记录在案的版本特定 GSMTC 限制"——该限制是否接受
由你在 RTM 真机上复验后决定。

## Players

| 用例 | 结果 | 证据 |
|---|---|---|
| 测试应用(自发布 SMTC)元数据/状态/进度 | ✅ | T8 集成:play/position/next → ok,GSMTC 不可见属环境 |
| Apple Music / 网易云 / QQ 音乐 / 浏览器 / WMP | ⏳ | 待 RTM 真机(本 VM GSMTC 环境损坏) |
| 播放/暂停/seek/切歌/退出 | ⏳ | 同上(coordinator 状态机单测已覆盖全部转换) |

## Taskbar

| 用例 | 结果 | 证据 |
|---|---|---|
| 225% 缩放贴任务栏左侧、不遮图标 | ✅ | spike 截图 + 真实 Shell_TrayWnd 矩形 |
| DPI 感知定位 | ✅ | spike(225%)+ 计算器 8 项边界单测 |
| 居中图标布局左空区 | ✅ | 同上 |
| <80 DIP 隐藏 / auto-hide / 全屏隐藏 | ✅ | 计算器单测;真机待 ⏳ |
| Explorer 重启重定位 | ✅ | SnapshotReader 新 HWND 单测;真机 ⏳ |

## Shell / UI / Lifecycle

| 用例 | 结果 | 证据 |
|---|---|---|
| 不抢焦点(无 Activate) | ✅ | C2 修复:显隐仅由 placement 驱动 |
| 单实例(第二实例→show-preferences) | ⚠️ | 交互会话验证(SSH 管道受限),标 InteractiveSession |
| 凭据写读删+置零 | ✅ | 交互会话实测退出码 0 |
| 安装/升级/卸载生命周期 | ⏳ | T7 脚本就绪,待本 VM 交互运行 |
| 菜单/偏好/手动搜索 UI | ⏳ | VM 逻辑单测 59/59;交互会话走查 ui-smoke-checklist |

## 阻塞项清算

- 无崩溃/挂起/焦点窃取/缓存投毒(修复轮已处理并测试)
- 五播放器矩阵:本 VM 环境无法执行,以 RTM 真机复验为放行条件
