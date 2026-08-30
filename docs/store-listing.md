# Microsoft Store listing copy

Product name / 产品名称: Shadow Studio

## English

### Short description
A Windows desktop utility for managing Paradox playsets, mods, DLC, and launch settings.

### Description
Shadow Studio is a Windows desktop utility for organizing launch configurations of Paradox games that are already installed on your PC. It is not a game and does not include any playable game content.

Use it to keep playsets, mods, DLC selections, and common settings files in one Fluent desktop app. Supported workflows include locally installed titles such as Hearts of Iron IV, Stellaris, and Victoria 3. Those games are separate products; Shadow Studio only manages files on your device and can start an executable you already installed.

What you can do:
- Discover local and Steam Workshop mods already present on your PC
- Create editable Shadow playsets and add several mods at once
- Import playsets from the official Paradox Launcher as read-only copies
- Browse, search, and delete game saves with in-game date and country metadata
- Enable or disable DLC and write the result to standard game load files
- Edit common settings.txt options such as language, display mode, resolution, refresh rate, VSync, and volume
- Open a configured game executable from the home page or the launcher page
- Start a configured executable from a terminal with the shadow command

Shadow Studio works locally. Configure the game executable, the Paradox user folder, and the Steam Workshop folder, then refresh, save a playset, and start the installed program.

Before you rely on it as your only launch tool, back up your Paradox user folders.

### Features
- Desktop utility for local Paradox playset, mod, and save files
- Playset management with official-launcher import and multi-select mod adding
- Save management with browse, search, and per-game save detection
- DLC and settings written to standard local game files
- Home status, quick actions, and command-line start
- No playable game content in the app package

### What's new in 1.1.3
This release hardens the launcher: a failed refresh no longer locks the UI, generated mod descriptor names resist path traversal from third-party mods, and launcher state is written atomically with automatic backups of corrupt files. Steam and Paradox folder discovery now also works on Linux, with a steam:// launch fallback. Packaging scripts fail fast and the release ships with a cross-platform unit test suite.

### What's new in 1.1.2
Mod import now accepts a folder instead of a zip archive: select a mod folder and Shadow copies it into the Paradox mod directory, auto-generating the .mod descriptor file. Refresh and save discovery now run off-thread with a loading overlay to keep the UI responsive.

## 中文

### 简短说明
Windows 桌面工具，用于管理 Paradox 播放集、Mod、DLC 和启动设置。

### 说明
Shadow Studio 是一款 Windows 桌面工具，用于整理本机已安装 Paradox 游戏的启动配置。它不是游戏，也不包含任何可玩的游戏内容。

你可以在同一个 Fluent 界面里管理播放集、Mod、DLC 选择和常用设置文件。适用于本机已安装的 Hearts of Iron IV、群星、维多利亚 3 等作品。这些游戏是独立产品；Shadow Studio 只处理你电脑上的本地文件，并可启动你已经安装的程序。

你可以：
- 发现本机已有的本地 Mod 和 Steam Workshop Mod
- 创建可编辑的 Shadow 播放集，支持一次添加多个 Mod
- 从官方 Paradox Launcher 导入只读播放集
- 浏览、搜索和删除游戏存档，显示游戏内日期和国家元数据
- 启用或禁用 DLC，并写入标准游戏加载文件
- 修改 settings.txt 中的常见选项，例如语言、显示模式、分辨率、刷新率、垂直同步和音量
- 从主页或启动器页面打开已配置的游戏可执行文件
- 在终端使用 shadow 命令启动已配置的程序

Shadow Studio 在本地运行。请先配置游戏可执行文件、Paradox 用户目录和 Steam Workshop 目录，然后刷新、保存播放集并启动已安装的程序。

如果打算把它当作唯一启动工具，请先备份游戏用户配置目录。

### 功能要点
- 用于本地 Paradox 播放集、Mod 和存档文件的桌面工具
- 播放集管理，支持导入官方启动器播放集和多选添加 Mod
- 存档管理：浏览、搜索和删除，自动识别各游戏存档目录
- DLC 和设置写入标准本地游戏文件
- 主页状态、快捷操作和命令行启动
- 应用包内不含可玩的游戏内容

### 1.1.3 更新内容
本版本重点加固启动器：刷新失败不再卡死界面，生成的 Mod 描述符文件名可抵御第三方 Mod 的路径穿越，启动器状态改为原子写入并自动备份损坏文件。Steam 与 Paradox 目录发现现已支持 Linux，并提供 steam:// 启动回退。打包脚本失败即报错，本版本起附带跨平台单元测试。

### 1.1.2 更新内容
Mod 导入改为选择文件夹而非压缩包：选择 Mod 文件夹后自动复制到 Paradox mod 目录并生成 .mod 描述符文件。刷新和存档发现改为后台线程执行，显示加载遮罩以保持界面响应。
