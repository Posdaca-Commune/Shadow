# 变更日志

## 1.0.0-beta.1 - 2026-06-17

### 新增

- 初始 Avalonia/Fluent 工作站外壳。
- 通过 `Shadow.Abstractions` 在运行时加载插件。
- 内置 `Shadow.Hoi4Launcher` 插件。
- 支持发现本地 Mod 描述文件和 Steam Workshop 描述文件。
- Shadow 本地可编辑播放集，存储在 `%APPDATA%\Posdaca\Hoi4Workspace`。
- 从 `launcher-v2.sqlite` 导入 Paradox Launcher 只读播放集。
- 通过 HOI4 `dlc_load.json` 管理 DLC 启用状态。
- 游戏设置编辑器，可修改部分 `settings.txt` 字段。
- Mod 索引导出到 `%APPDATA%\Posdaca\Hoi4Workspace\mods\index.json`。
- 插件命令接口和 `hoi4.launch` 命令。
- 通过 `.gitattributes` 固定仓库换行策略。

### 说明

- 这是面向 Windows 本地测试的早期 beta 版。
- 对重要播放集使用 Shadow 前，请先备份 HOI4 用户目录。
- 插件 API 在稳定版前仍可能调整。
