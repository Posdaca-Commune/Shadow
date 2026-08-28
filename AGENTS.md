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

### macOS 打包

- macOS 版本由 `scripts/build-macos.ps1` 生成 `.app` bundle，并可可选地生成 `.dmg`。
- `Info.plist` 模板位于 `packaging/macos/Info.plist`，`CFBundleExecutable` 指向主程序可执行文件 `Shadow`（无扩展名）。
- 生成 `.dmg` 需要运行在 macOS 上的 `hdiutil`；在非 macOS 主机上脚本只产出 `.app` 布局并跳过 `.dmg`。
- macOS bundle 的 `Contents/MacOS/Plugins` 子目录用于内置插件，结构与 Windows 一致。

### Linux 打包

- Linux 版本由 `scripts/build-linux.ps1` 生成 `tar.gz` 便携包，并可可选地生成 `.AppImage`。
- `.desktop` 文件位于 `packaging/linux/com.posdacacommune.shadow.desktop`，图标复用 `packaging/branding/` 下的资源。
- 生成 `.AppImage` 需要 PATH 上有 `appimagetool`；缺失时脚本只产出 `tar.gz` 并跳过 `.AppImage`。
- tar.gz 解压后可直接运行其中的 `Shadow.sh` 启动器，内置插件位于解压目录的 `Plugins` 子目录。

### CI

- `.github/workflows/build-msix.yml`、`build-macos.yml`、`build-linux.yml` 分别在对应平台 runner 上调用打包脚本。

## 协作约定

- 可以调用 MCP 工具辅助查看、分析和修改项目。
- 切换聊天窗口后，优先阅读本文件来恢复项目背景。
