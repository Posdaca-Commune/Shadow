# Privacy Policy / 隐私政策

**Product:** Shadow Studio  
**Publisher:** NS9927  
**Effective date:** 2026-08-13

This policy applies to the Windows desktop app **Shadow Studio** distributed through the Microsoft Store and as an MSIX package.

本政策适用于通过 Microsoft Store 及 MSIX 包分发的 Windows 桌面应用 **Shadow Studio**。

## Summary / 摘要

Shadow Studio is a local utility for managing and launching Paradox games installed on your computer. It does **not** require an account, and the current version does **not** send your personal information to our servers.

Shadow Studio 是用于管理和启动本机已安装 Paradox 游戏的本地工具。当前版本**不需要账号**，也**不会把个人信息发送到我们的服务器**。

## Data we access locally / 本机访问的数据

The app may read and write files on your device in order to provide its features:

应用会在本机读写下列数据，以便提供功能：

- App settings and plugin state under `%LOCALAPPDATA%\Shadow`  
  应用设置与插件状态：`%LOCALAPPDATA%\Shadow`
- Playsets and mod indexes under `%APPDATA%\Posdaca\<Game Name>`  
  播放集与 Mod 索引：`%APPDATA%\Posdaca\<游戏名称>`
- Game folders that you configure, including the game executable, the Paradox user directory (usually under Documents), and the Steam Workshop folder  
  你配置的游戏目录，包括游戏可执行文件、Paradox 用户目录（通常在“文档”下）以及 Steam Workshop 目录
- Standard Paradox launch files such as `dlc_load.json` and selected fields in `settings.txt`  
  启动所需的标准游戏文件，例如 `dlc_load.json` 以及 `settings.txt` 中的部分字段
- Plugin load error logs written locally if a plugin fails to load  
  插件加载失败时写在本地的错误日志

These files may include folder paths that contain your Windows user name. That data stays on your device.

这些文件路径中可能包含你的 Windows 用户名。相关数据保留在本机。

## Data we do not collect / 我们不收集的数据

The current version does **not**:

当前版本**不会**：

- Create an account or ask for your name, email, or payment details  
  创建账号，或要求你提供姓名、电子邮件、支付信息
- Upload playsets, mods, crash reports, or usage analytics to us  
  向我们上传播放集、Mod、崩溃报告或使用统计
- Share your precise location  
  共享你的精确位置
- Show ads or sell your data  
  展示广告或出售你的数据

## Third-party software / 第三方软件

Shadow Studio can start games and related tools that you already installed, such as Paradox titles and Steam. Those products have their own privacy policies. Opening a Steam Workshop page uses your default browser.

Shadow Studio 可以启动你已经安装的游戏及相关软件（例如 Paradox 游戏和 Steam）。这些产品有各自的隐私政策。打开 Steam Workshop 页面时会使用你的默认浏览器。

## Children / 儿童

The app is a general-purpose desktop utility and is not directed at children. We do not knowingly collect personal information from children.

本应用是通用桌面工具，不以儿童为主要对象。我们不会故意收集儿童的个人信息。

## How to delete local data / 如何删除本地数据

Uninstalling the app from Windows may not remove all local files. You can delete:

从 Windows 卸载应用后，部分本地文件可能仍会保留。你可以自行删除：

- `%LOCALAPPDATA%\Shadow`
- `%APPDATA%\Posdaca`

Game files in your Documents folder or Steam libraries are owned by those games and are not removed by uninstalling Shadow Studio.

“文档”或 Steam 库中的游戏文件属于对应游戏，卸载 Shadow Studio 不会删除它们。

## Changes / 变更

If this policy changes, we will update this page and the effective date.

若本政策有变更，我们会更新本页面及生效日期。

## Contact / 联系

Questions about this policy: open an issue at  
隐私相关问题请在此提交 Issue：  
https://github.com/Posdaca-Commune/Shadow/issues
