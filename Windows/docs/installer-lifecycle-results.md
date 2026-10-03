# 安装器生命周期验证结果

- 日期:2026-10-02
- 机器:Windows 11 ARM64 Insider build 26220(VMware Fusion)
- 账户:标准用户(非管理员),全程无 UAC
- 安装包:LyricsX-Windows-Setup-x64.exe 0.1.0(未签名)

| 场景 | 结果 |
|---|---|
| 静默安装(/VERYSILENT /NORESTART) | PASS |
| 可执行文件落位 %LOCALAPPDATA%\Programs\LyricsX | PASS |
| 升级场景(重装覆盖,同 AppId) | PASS |
| 默认卸载保留用户数据(marker 存活) | PASS |
| 静默卸载应用文件移除 | PASS |
| /DELETEUSERDATA=1 显式删除用户数据 | PASS |
| 重装后应用可用 | PASS |
| 运行中应用卸载(启动应用→静默卸载) | PASS — 应用经 shutdown 链路退出,进程消失,文件全移除 |

附注:
- 全部通过 SSH 驱动的交互会话计划任务执行,无 UAC 弹窗。
- 用户数据 marker 位于 %LOCALAPPDATA%\LyricsX\lifecycle-marker.txt,
  默认卸载后存活,/DELETEUSERDATA=1 后移除。
- ARM64 安装包同管线构建;生命周期脚本对两个 RID 行为一致
  (仅发布目录不同)。
