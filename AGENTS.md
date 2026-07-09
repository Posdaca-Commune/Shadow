# Shadow 项目上下文

这个仓库是一个预备做成 HOI4 工作站的项目。

## 项目定位

- 主程序负责加载插件，并提供基础功能。
- 插件负责提供更丰富、可扩展的功能。
- 每一个插件都是和主程序位于同一目录层级下的独立项目。
- 主程序和插件之间应保持清晰边界，避免把插件专属功能写死在主程序中。

## 技术与界面约定

- 项目采用 Avalonia。
- UI 采用 Fluent 设计风格。
- 已安装 `FluentAvaloniaUI` 软件包，开发界面时优先使用现有 FluentAvaloniaUI 能力和项目已有样式。

## 发布与打包约定

- 后续 Windows 版本优先发布 MSIX 包，不再要求用户下载并解压 zip。
- MSIX 包由 `scripts/build-msix.ps1` 生成，包清单模板位于 `packaging/msix/AppxManifest.xml`。
- 打包脚本会发布主程序，并将内置插件输出放入包目录的 `Plugins` 子目录。
- 生成 MSIX 需要 Windows 10/11 SDK 中的 `makeappx.exe`；可安装前的正式包还需要用受信任证书通过 `signtool.exe` 签名。
- MSIX 包清单注册 `shadow.exe` 应用执行别名，安装后可在终端通过 `shadow` 调用命令行功能；不要依赖安装时写入 `SHADOW_PATH` 这类全局环境变量。
- MSIX 安装目录基本只读。内置插件可以随包发布；后续若支持用户安装插件，应把可写插件目录放到 `%LOCALAPPDATA%\Shadow\Plugins` 等用户数据路径。

## 协作约定

- 可以调用 MCP 工具辅助查看、分析和修改项目。
- 切换聊天窗口后，优先阅读本文件来恢复项目背景。
