# LyricsX DMG 安装界面设计

## 目标

为 LyricsX 的 macOS DMG 增加类似 JetBrains IDE 安装盘的引导式 Finder 布局，
让用户打开 DMG 后能立即理解“将 LyricsX 拖到 Applications”这一安装动作。

## 视觉方向

采用“JetBrains 式布局 + LyricsX 自有品牌”，借鉴安装画布的空间关系，
不复制 JetBrains 的 Logo、品牌名称或宣传文案。

- 白色 Finder 窗口，使用浅灰边框和标准 macOS 窗口标题栏。
- 左上角放 LyricsX 应用图标和简短的 LyricsX 品牌提示。
- 左侧放 `LyricsX.app`，右侧放 `Applications` 文件夹别名。
- 两个图标之间放置清晰的 `drag to` 提示，避免依赖隐含的拖动手势。
- 背景使用低对比度的 LyricsX 品牌色块或渐变，不能降低图标和文字可读性。
- 不在 DMG 中增加额外的安装器、脚本或联网步骤。

## DMG 内容和布局

DMG 根目录继续只包含两个安装相关项目：

- `LyricsX.app`：应用本体。
- `Applications`：指向 `/Applications` 的符号链接。

Finder 窗口布局由 DMG 暂存目录中的背景资源和窗口配置控制。默认窗口采用
横向画布，应用位于左侧、Applications 位于右侧，中央提示位于两者之间。
图标位置与窗口尺寸作为打包脚本中的稳定常量，确保每次 CI 生成的 DMG 视觉一致。

如果当前 CI runner 缺少用于设置 Finder 窗口的可选工具，打包必须继续成功，
并退回到包含正确内容和符号链接的标准 DMG；视觉配置失败不能破坏安装包。

## 资源策略

- 优先复用 `LyricsX/Assets.xcassets/AppIcon.appiconset` 中的应用图标。
- 新增的 DMG 背景资源放在 release 脚本附近的专用资源目录，避免混入应用运行时资源。
- 背景资源应为静态、可审查的 PNG，不依赖网络或运行时生成。
- 文案保持中性，以英文为主；Release notes 继续承担双语安装说明。

## 实现边界

- 修改现有 `Scripts/release/package.sh` 的 DMG staging 和视觉配置步骤。
- 保持 ZIP、dSYMs ZIP、SHA-256、签名验证和 GitHub Release 上传流程不变。
- 不修改应用代码、Bundle ID、版本号或现有正式 Release workflow。
- 不修改用户已有的 Xcode project、storyboard 和 Info.plist 未提交改动。

## 验收标准

自动化检查必须证明：

1. DMG 仍包含 `LyricsX.app`。
2. `Applications` 仍是指向 `/Applications` 的符号链接。
3. DMG staging 包含背景资源和窗口布局配置的生成/应用步骤。
4. 可选 Finder 配置工具缺失时，打包脚本仍以成功状态结束。
5. 现有 ZIP、dSYMs ZIP、校验和和发布上传测试继续通过。
6. 实际生成的 DMG 可通过 `hdiutil verify`，并能只读挂载检查安装布局。
