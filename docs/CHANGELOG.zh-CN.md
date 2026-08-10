# 变更日志

## 1.0.0 - 2026-08-10

### 新增

- 主页启动台：功能状态卡片、快捷操作和快速开始提示。
- 插件可选主页状态接口 `IShadowHomeStatusProvider`。
- Paradox 游戏启动器可在主页展示当前游戏、播放集、配置状态，并支持直接启动。
- 设置页「关于」分区：应用版本、本地数据目录、仓库地址和许可证信息。
- 为 `ScrollViewer` 提供与渲染帧同步的平滑滚轮滚动。

### 变更

- 正式版定位调整为 Paradox 多游戏启动台壳层。
- 设置页移除尚未完成的「工作区」占位分类。
- 更新主页启动台与关于页相关中英文文案。

### 修复

- 对齐官方启动器的 mod 描述符生成逻辑：生成 `ugc_*.mod` 时保留完整
  `replace_path` / `dependencies` / `tags` 等元数据，不再写成仅含
  name/path/remote_file_id 的精简 stub。
- `dlc_load.json` 的 `enabled_mods` 按 playset 顺序写入，与官方启动器一致。
- 路径写入改为官方常用的 `C:/` 正斜杠格式。
- 对已损坏的 `ugc_*.mod` 会在启动前自动从 Workshop 的 `descriptor.mod` 修复。
- 平滑滚动不再使用 `Task.Delay(16)`，避免帧间隔不均导致卡顿。

### 说明

- 这是面向 Windows 常规使用与商店打包的首个稳定版 `1.0.0`。
- 在把 Shadow 作为重要播放集的主启动器前，请先备份游戏用户配置目录。
- 游戏路径、Workshop 目录和播放集仍由 Paradox 启动器插件管理。

## 1.0.0-beta.1 - 2026-06-17

### 新增

- 初始 Avalonia/Fluent 工作站外壳。
- 通过 `Shadow.Abstractions` 在运行时加载插件。
- 内置 `Shadow.ParadoxGameLauncher` 多游戏启动器插件。
- 支持发现本地 Mod 描述文件和 Steam Workshop 描述文件。
- Shadow 本地可编辑播放集，存储在 `%APPDATA%\Posdaca\<游戏名称>`。
- 从 `launcher-v2.sqlite` 导入 Paradox Launcher 只读播放集。
- 通过 HOI4 `dlc_load.json` 管理 DLC 启用状态。
- 游戏设置编辑器，可修改部分 `settings.txt` 字段。
- Mod 索引导出到 `%APPDATA%\Posdaca\<游戏名称>\mods\index.json`。
- 插件命令接口和 `paradox.launch` 命令。
- 通过 `.gitattributes` 固定仓库换行策略。

### 说明

- 这是面向 Windows 本地测试的早期 beta 版。
- 对重要播放集使用 Shadow 前，请先备份 HOI4 用户目录。
- 插件 API 在稳定版前仍可能调整。
