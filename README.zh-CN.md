# Shadow

[English](README.md)

Shadow 是一个基于 Avalonia 的 Hearts of Iron IV 工作站外壳。主程序负责
加载插件、提供导航和基础设置；具体游戏工作流放在独立插件项目中。当前内置
的第一个插件是 `Shadow.Hoi4Launcher`，用于管理 HOI4 的启动、播放集、Mod
和 DLC。

> 状态：`1.0.0-beta.1` 是早期 beta 版。它已经可以用于本地 HOI4 启动流程，
> 但在把它作为唯一启动器之前，建议先备份 HOI4 用户配置目录。

## 功能

- 使用 Fluent Avalonia 的桌面工作站外壳。
- 通过 `Shadow.Abstractions` 在运行时加载插件。
- HOI4 启动器插件，可发现本地 Mod 和 Steam Workshop Mod。
- 播放集管理：支持 Shadow 本地可编辑播放集，也支持从 Paradox Launcher
  数据库导入只读播放集。
- DLC 启用/禁用状态写入 HOI4 的 `dlc_load.json`。
- 游戏设置编辑器，可修改 `settings.txt` 中常见选项，例如语言、显示模式、
  分辨率、刷新率、VSync 和音量。
- 共享 HOI4 工作区文件位于 `%APPDATA%\Posdaca\Hoi4Workspace`。
- Mod 索引导出到
  `%APPDATA%\Posdaca\Hoi4Workspace\mods\index.json`。
- 支持命令行插件命令分发，当前包含 `hoi4.launch`。

## 运行要求

- Windows 10 或 Windows 11。
- 本机已安装 Hearts of Iron IV。
- 从源码构建需要 .NET SDK 10，以及 Avalonia 所需工作负载。

Windows 发布资产以自包含 `win-x64` zip 形式发布，不需要用户额外安装 .NET
运行时。

## 快速开始

1. 从 GitHub Release 下载 `Shadow-1.0.0-beta.1-win-x64.zip`。
2. 解压到一个可写目录。
3. 运行 `Shadow.exe`。
4. 在左侧导航打开 `HOI4 启动器`。
5. 在 `游戏设置` 中设置：
   - `hoi4.exe` 或 `dowser.exe` 路径；
   - HOI4 用户目录，通常是
     `%USERPROFILE%\Documents\Paradox Interactive\Hearts of Iron IV`；
   - Steam Workshop 目录，用于发现创意工坊 Mod。
6. 刷新，创建或导入播放集，然后保存并启动。

## 命令行启动

Shadow 可以在启动桌面 UI 之前分发插件命令。内置 HOI4 插件当前提供：

```powershell
.\Shadow.exe --shadow-command hoi4.launch
.\Shadow.exe --shadow-command hoi4.launch --playset-id default
.\Shadow.exe --shadow-command hoi4.launch --playset-id default --allow-missing-mods
```

参数：

- `--shadow-command` 或 `--command`：命令名称。
- `--playset-id`、`--playsetId` 或 `--playset`：要启动的播放集 id。
- `--allow-missing-mods`：当播放集中启用的 Mod id 未被当前扫描发现时，仍
  允许启动。

如果没有指定播放集，Shadow 会使用已保存的当前播放集；如果仍找不到，则使用
第一个可用播放集。

## 数据位置

- 主程序和插件状态：`%LOCALAPPDATA%\Shadow`
- HOI4 工作区播放集：`%APPDATA%\Posdaca\Hoi4Workspace\playsets`
- HOI4 工作区 Mod 索引：`%APPDATA%\Posdaca\Hoi4Workspace\mods\index.json`
- 默认 HOI4 用户目录：
  `%USERPROFILE%\Documents\Paradox Interactive\Hearts of Iron IV`

Shadow 会写入 HOI4 启动流程使用的标准用户文件，包括 `dlc_load.json` 和
`settings.txt` 中被界面管理的字段。

## 仓库结构

```text
Shadow/
  Shadow/                  桌面主程序
  Shadow.Abstractions/     主程序与插件之间的契约
  Shadow.Hoi4Launcher/     内置 HOI4 启动器插件
```

主程序应保持通用能力。插件专属 UI 和 HOI4 行为应放在插件项目中。

## 从源码构建

```powershell
dotnet restore
dotnet build
dotnet run --project Shadow/Shadow.csproj
```

创建包含内置插件的 Windows x64 发布目录：

```powershell
dotnet publish Shadow/Shadow.csproj -c Release -r win-x64 --self-contained true -o artifacts/publish/Shadow-1.0.0-beta.1-win-x64
dotnet publish Shadow.Hoi4Launcher/Shadow.Hoi4Launcher.csproj -c Release -r win-x64 --self-contained false -o artifacts/publish/Shadow-1.0.0-beta.1-win-x64/Plugins/Hoi4Launcher
```

## 插件契约

插件实现 `Shadow.Abstractions` 中的 `IShadowPlugin`：

- `CreateNavigationItems` 向主导航添加页面。
- `CreateSettingsSections` 向设置页添加配置区块。
- `IShadowCommandPlugin` 可选实现命令行命令处理。

插件项目与主程序位于同一目录层级。构建后将插件输出复制到主程序的
`Plugins` 目录，主程序会在运行时加载 `Shadow.*.dll` 插件程序集，并复用主程
序上下文中的 `Shadow.Abstractions`。

## 发布说明

版本历史见 [CHANGELOG.zh-CN.md](CHANGELOG.zh-CN.md)。
