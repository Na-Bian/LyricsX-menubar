# 手动媒体会话观察记录

## 环境

- VM:VMware Fusion,Windows 11 ARM64 Insider build 26220(水印 26120)
- 日期:2026-10-02
- 探针:Windows/tools/MediaSessionProbe

## 关键观察

1. **GSMTC 枚举系统性不可见**(环境级):以下三路在生产者确认存活时
   `GetSessions()` 均返回空——
   - LyricsX 测试应用经 MediaPlayer 发布(静音 WAV,DisplayProperties 完整);
   - SystemMediaTransportControlsInterop.GetForWindow 路线;
   - 系统"媒体播放器"播放 wav 的金丝雀。
   同一构建在计划 01 T2 时 NetEase 实网歌词链路正常,与本问题无冲突。

2. **LyricsX 自身管线不受影响**:测试应用 SMTC 状态更新
   (play/pause/next/position)全部成功;coordinator 状态机
   (无媒体→搜索→首候选→更优→缓存/忽略/关联)由单测覆盖。

3. 结论:在此 Insider VM 上无法对五类播放器做 GSMTC 端到端验收;
   需在 RTM(build 26100 及以下或正式发布)真机/镜像复跑 T6 矩阵。
   该复验结果决定是否按计划条款接受为版本限制。

## 播放器逐台记录(模板,RTM 真机填写)

| 播放器 | 版本 | SourceAppUserModelId | GSMTC 提供字段 | 位置更新 | 备注 |
|---|---|---|---|---|---|
| Apple Music | | | | | |
| 网易云音乐 | | | | | |
| QQ 音乐 | | | | | |
| 浏览器(Edge) | | | | | |
| 媒体播放器 | | | | | |
