# HOI4 工作区播放集集成

本文档说明外部项目如何通过 Shadow 的共享 HOI4 工作区读写播放集，并通过 Shadow 启动当前播放集。

## 共享目录

Shadow 使用以下目录作为 HOI4 工作区：

```text
%AppData%\Posdaca\Hoi4Workspace\
```

在 Windows 上通常展开为：

```text
C:\Users\<user>\AppData\Roaming\Posdaca\Hoi4Workspace\
```

当前约定的子目录：

```text
Hoi4Workspace\
  mods\
    index.json
  playsets\
    <playset-id>.json
```

## Mod 索引

Shadow 刷新 Mod 列表时会写入：

```text
%AppData%\Posdaca\Hoi4Workspace\mods\index.json
```

外部项目应使用这个索引把自己的 Mod 依赖映射成 Shadow 播放集需要的 `modIds`。

示例：

```json
{
  "schemaVersion": "1.0",
  "updatedAt": "2026-06-17T00:00:00+00:00",
  "mods": [
    {
      "id": "123456789",
      "name": "Example Workshop Mod",
      "source": "steam",
      "remoteFileId": "123456789",
      "descriptorPath": "C:\\Users\\user\\Documents\\Paradox Interactive\\Hearts of Iron IV\\mod\\ugc_123456789.mod",
      "launcherPath": "mod/ugc_123456789.mod",
      "contentPath": "C:\\Program Files (x86)\\Steam\\steamapps\\workshop\\content\\394360\\123456789",
      "version": "1.0"
    }
  ]
}
```

字段说明：

- `id`: Shadow 播放集必须写入的 Mod 标识。
- `remoteFileId`: Steam 创意工坊 Mod 的远程文件 ID。创意工坊 Mod 应优先用它匹配。
- `contentPath`: Mod 实际内容目录或压缩包路径。本地 Mod 应优先用它匹配。
- `descriptorPath`: Shadow 扫描到的 `.mod` 描述文件路径。
- `launcherPath`: HOI4 `dlc_load.json` 使用的启动器路径。
- `source`: 当前为 `steam` 或 `local`。

匹配规则建议：

1. 如果外部项目知道 Steam remote id，优先匹配 `mods[].remoteFileId`。
2. 否则把外部项目的 Mod 内容目录规范化后匹配 `mods[].contentPath`。
3. 匹配成功后，取 `mods[].id` 写入播放集的 `modIds` 和 `enabledModIds`。
4. 不要把外部项目自己的内容目录直接写入 `modIds`，Shadow 不按内容目录匹配播放集。

## 播放集文件

播放集写入：

```text
%AppData%\Posdaca\Hoi4Workspace\playsets\<playset-id>.json
```

示例：

```json
{
  "id": "oiia:my-project",
  "name": "Oiia - My Project",
  "enabledModIds": [
    "123456789",
    "C:\\USERS\\USER\\DOCUMENTS\\PARADOX INTERACTIVE\\HEARTS OF IRON IV\\MOD\\MY_LOCAL_MOD.MOD"
  ],
  "modIds": [
    "123456789",
    "C:\\USERS\\USER\\DOCUMENTS\\PARADOX INTERACTIVE\\HEARTS OF IRON IV\\MOD\\MY_LOCAL_MOD.MOD"
  ],
  "disabledDlcIds": [],
  "source": "Oiia",
  "isExternal": true,
  "can_edit": false
}
```

字段说明：

- `id`: 播放集稳定 ID。建议外部项目使用带命名空间的 ID，例如 `oiia:<project-id>`。
- `name`: Shadow UI 中显示的播放集名称。
- `modIds`: 播放集内所有 Mod，顺序即加载顺序。
- `enabledModIds`: 当前启用的 Mod。通常是 `modIds` 的子集。
- `disabledDlcIds`: 禁用 DLC 的 launcher id。没有需要禁用的 DLC 时写空数组。
- `source`: 播放集来源，例如 `Shadow`、`Oiia`、`Paradox Launcher`。
- `isExternal`: 外部工具托管的播放集建议写 `true`。
- `can_edit`: 是否允许 Shadow 编辑该播放集。外部工具托管的播放集建议写 `false`。

`can_edit=false` 时，Shadow 会把播放集视为只读：可以应用并启动游戏，但不会允许用户在 Shadow 中直接修改该播放集。

写文件建议使用临时文件 + 原子替换，避免 Shadow 刷新时读到半截 JSON。

## 通过 Shadow 启动游戏

Shadow 提供命令行接口，外部项目可以通过它应用播放集并启动 HOI4。

```powershell
Shadow.exe --shadow-command hoi4.launch --playset-id "oiia:my-project"
```

如果不传 `--playset-id`，Shadow 会使用当前选中的播放集：

```powershell
Shadow.exe --shadow-command hoi4.launch
```

命令执行流程：

1. 加载 HOI4 启动器插件配置。
2. 重新发现本机 Mod。
3. 更新 `%AppData%\Posdaca\Hoi4Workspace\mods\index.json`。
4. 读取共享播放集目录。
5. 查找指定播放集。
6. 校验 `enabledModIds` 是否都能匹配到当前本机 Mod。
7. 写入 HOI4 用户目录下的 `dlc_load.json`。
8. 启动游戏程序。

如果播放集里有 Shadow 当前未发现的启用 Mod，命令默认失败并返回非 0 exit code。调试时可以临时允许缺失 Mod：

```powershell
Shadow.exe --shadow-command hoi4.launch --playset-id "oiia:my-project" --allow-missing-mods true
```

## 外部项目接入流程

推荐流程：

1. 读取 `%AppData%\Posdaca\Hoi4Workspace\mods\index.json`。
2. 根据外部项目当前 Mod 和依赖列表匹配 Shadow Mod 索引。
3. 用匹配到的 `mods[].id` 生成播放集。
4. 写入 `%AppData%\Posdaca\Hoi4Workspace\playsets\<playset-id>.json`。
5. 调用 Shadow 命令行启动：

```powershell
Shadow.exe --shadow-command hoi4.launch --playset-id "<playset-id>"
```

对于 IDEA / Rider / IntelliJ Platform 插件，可以创建一个运行配置：

- Executable: `Shadow.exe`
- Arguments: `--shadow-command hoi4.launch --playset-id "oiia:<project-id>"`
- Working directory: Shadow 输出目录，或任意可访问目录

## 注意事项

- `mods/index.json` 由 Shadow 生成。若外部项目刚创建了 launcher `.mod` 文件，需要先让 Shadow 刷新一次，或直接调用启动命令让 Shadow 在启动前刷新索引。
- 创意工坊 Mod 用 `remoteFileId` 匹配最稳定。
- 本地 Mod 用 `contentPath` 匹配最稳定。
- 播放集里的 `modIds` 和 `enabledModIds` 必须使用 Shadow 索引里的 `id`。
- 外部托管播放集建议设置 `isExternal=true` 和 `can_edit=false`，避免 Shadow 用户编辑后被外部项目下次同步覆盖。
