# Shadow

[English](../README.md)

Shadow 是一个基于 Avalonia 的 Paradox Interactive 工作站壳层。
主程序从 `Plugins` 目录加载功能插件，游戏相关流程放在独立插件项目中。
当前内置插件是 `Shadow.ParadoxGameLauncher`，用于管理多款 P 社游戏的启动、
播放集、Mod 与 DLC，例如 Hearts of Iron IV、Crusader Kings III、
Europa Universalis IV、Stellaris、Victoria 3 和 Imperator: Rome。

> 状态：`1.0.0-beta.1` 是早期 beta 版。它已经可以用于本地启动流程，
> 但在把它作为唯一启动器之前，建议先备份游戏用户配置目录。

## 功能

- Fluent Avalonia 桌面壳层，支持插件式导航。
- 通过 `Shadow.Abstractions` 在运行时加载插件。
- 多游戏 Paradox 启动器，可发现本地 Mod 和 Steam Workshop Mod。
- 播放集管理：可编辑的 Shadow 播放集，以及从 Paradox Launcher 数据库只读导入。
- DLC 启用/禁用状态写入游戏的 `dlc_load.json`。
- 游戏设置编辑器，支持 `settings.txt` 中的语言、显示模式、分辨率、刷新率、
  垂直同步和音量等常见选项。
- 按游戏分类的共享工作区文件位于 `%APPDATA%\Posdaca\<游戏名称>`。
- Mod 索引导出到 `%APPDATA%\Posdaca\<游戏名称>\mods\index.json`。
- 支持命令行插件命令分发，当前包含 `paradox.launch`。

## 运行要求

- Windows 10 或 Windows 11。
- 本地已安装一款或多款受支持的 Paradox 游戏。
- 源码构建需要：.NET SDK 10 以及 Avalonia 所需工作负载。

Windows 发布物优先使用 MSIX 包。包内包含主程序和内置插件，用户无需再解压 zip
或单独安装 .NET 运行时。

## 快速开始

1. 从 GitHub Release 下载 `Shadow-1.0.0-beta.1.msix`。
2. 使用 Windows 应用安装程序安装。
3. 从开始菜单或桌面快捷方式启动 `Shadow`。
4. 在左侧导航打开 `Paradox 游戏启动器`。
5. 点击左上角游戏名称切换目标游戏。
6. 在 `游戏设置` 中配置：
   - 所选游戏可执行文件路径；
   - 游戏用户目录，通常是
     `%USERPROFILE%\Documents\Paradox Interactive\<游戏名称>`；
   - 如需发现创意工坊 Mod，再配置 Steam Workshop 目录。
7. 刷新、创建或导入播放集，然后保存并启动。

## 命令行启动

Shadow 可在启动桌面 UI 前分发插件命令。内置 Paradox 启动器插件当前暴露这些命令。
MSIX 安装会注册 `shadow.exe` 应用执行别名，因此可以在终端里直接使用 `shadow`：

```powershell
shadow PDXGameLauncher hoi4
shadow PDXGameLauncher hoi4 -playset default
shadow PDXGameLauncher "Hearts of Iron IV" -p oiia:my-project
shadow PDXGameLauncher hoi4 -playset default -debug
```

旧的长参数写法仍然兼容：

```powershell
shadow --shadow-command paradox.launch --game hoi4 --playset-id default
```

语法：

```text
shadow PDXGameLauncher <游戏> [-playset <播放集id>] [-debug] [-allow-missing-mods]
```

参数：

- `PDXGameLauncher`：调用 Paradox 启动器插件命令。
- `<游戏>`：目标游戏 id 或显示名（`hoi4`、`ck3`、`eu4`、`stellaris`、
  `vic3`、`imperator`，或 `Hearts of Iron IV` 这类名称）。
- `-playset`、`-p`、`--playset-id`：要启动的播放集 id。
- `-debug`：以游戏 debug 模式启动，会把 `-debug` 传给游戏进程。
- `-allow-missing-mods`：即使启用的 mod id 当前扫描不到，也允许启动。

如果未指定播放集，Shadow 会使用已保存启动器状态中的选中播放集，再回退到第一个可用播放集。

## 数据位置

- 主程序/插件状态：`%LOCALAPPDATA%\Shadow`
- 游戏工作区播放集：`%APPDATA%\Posdaca\<游戏名称>\playsets`
- 游戏工作区 Mod 索引：`%APPDATA%\Posdaca\<游戏名称>\mods\index.json`
- 默认游戏用户目录：
  `%USERPROFILE%\Documents\Paradox Interactive\<游戏名称>`

Shadow 会写入启动流程使用的标准用户文件，包括 `dlc_load.json` 和
`settings.txt` 中的部分字段。

## 仓库结构

```text
Shadow/
  Shadow/                       桌面主程序
  Shadow.Abstractions/           主程序/插件契约
  Shadow.ParadoxGameLauncher/   内置多游戏启动器插件
```

主程序保持通用。插件专属 UI 与游戏行为应放在插件项目中。

## 源码构建

```powershell
dotnet restore
dotnet build
dotnet run --project Shadow/Shadow.csproj
```

若要生成包含内置插件的 Windows x64 MSIX 包，请安装 Windows 10/11 SDK，然后运行：

```powershell
.\scripts\build-msix.ps1 -Version 1.0.0-beta.1
```

生成的包会写入 `artifacts/msix/Shadow-1.0.0-beta.1.msix`。
MSIX 包在安装前需要签名。可使用 `-CertificatePath` 或 `-CertificateThumbprint`
在打包时签名。

使用 PFX 证书的示例：

```powershell
.\scripts\build-msix.ps1 -Version 1.0.0-beta.1 -CertificatePath .\certs\Shadow.pfx -CertificatePassword (Read-Host -AsSecureString)
```


