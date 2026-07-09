# Shadow

[简体中文](docs/README.zh-CN.md)

Shadow is an Avalonia-based workstation shell for Hearts of Iron IV tooling.
The host application loads feature plugins from a `Plugins` directory, while
game-specific workflows live in separate plugin projects. The first bundled
plugin is `Shadow.Hoi4Launcher`, a HOI4 launcher and playset manager.

> Status: `1.0.0-beta.1` is an early beta. It is usable for local HOI4 launch
> workflows, but you should back up your HOI4 user configuration before relying
> on it as your only launcher.

## Features

- Fluent Avalonia desktop shell with plugin-based navigation.
- Runtime plugin loading through `Shadow.Abstractions`.
- HOI4 launcher plugin for discovering local and Steam Workshop mods.
- Playset management with editable Shadow playsets and read-only imports from
  the Paradox Launcher database.
- DLC enable/disable state written to HOI4 `dlc_load.json`.
- Game settings editor for common `settings.txt` options such as language,
  display mode, resolution, refresh rate, VSync, and volume.
- Shared HOI4 workspace files under `%APPDATA%\Posdaca\Hoi4Workspace`.
- Mod index export to `%APPDATA%\Posdaca\Hoi4Workspace\mods\index.json`.
- Command-line plugin command dispatch, including `hoi4.launch`.

## Requirements

- Windows 10 or Windows 11.
- Hearts of Iron IV installed locally.
- For source builds: .NET SDK 10 and the workloads required by Avalonia.

The Windows release asset is published as an MSIX package. The package contains
the host and bundled plugins, so users do not need to unpack a zip or install a
separate .NET runtime.

## Getting Started

1. Download `Shadow-1.0.0-beta.1.msix` from the GitHub release.
2. Install the package with Windows App Installer.
3. Start `Shadow` from the Start menu or desktop shortcut.
4. Open `HOI4 Launcher` from the left navigation.
5. In `Game Settings`, set:
   - the path to `hoi4.exe` or `dowser.exe`;
   - the HOI4 user folder, usually
     `%USERPROFILE%\Documents\Paradox Interactive\Hearts of Iron IV`;
   - the Steam Workshop folder, if you want Workshop mods discovered.
6. Refresh, create or import a playset, then save and launch.

## Command-Line Launch

Shadow can dispatch plugin commands before starting the desktop UI. The bundled
HOI4 plugin currently exposes these commands. MSIX installs register the
`shadow.exe` app execution alias, so `shadow` can be used from a terminal:

```powershell
shadow --shadow-command hoi4.launch
shadow --shadow-command hoi4.launch --playset-id default
shadow --shadow-command hoi4.launch --playset-id default --allow-missing-mods
```

Options:

- `--shadow-command` or `--command`: command name.
- `--playset-id`, `--playsetId`, or `--playset`: playset id to launch.
- `--allow-missing-mods`: allows a playset to launch even when enabled mod ids
  are not found in the current scan.

If no playset is specified, Shadow uses the selected playset from the saved
launcher state, then falls back to the first available playset.

## Data Locations

- Host/plugin state: `%LOCALAPPDATA%\Shadow`
- HOI4 workspace playsets: `%APPDATA%\Posdaca\Hoi4Workspace\playsets`
- HOI4 workspace mod index: `%APPDATA%\Posdaca\Hoi4Workspace\mods\index.json`
- Default HOI4 user directory:
  `%USERPROFILE%\Documents\Paradox Interactive\Hearts of Iron IV`

Shadow writes HOI4 launch state to the standard HOI4 user files used by the
launcher flow, including `dlc_load.json` and selected fields in `settings.txt`.

## Repository Layout

```text
Shadow/
  Shadow/                  Desktop host application
  Shadow.Abstractions/     Host/plugin contracts
  Shadow.Hoi4Launcher/     Bundled HOI4 launcher plugin
```

The host is intentionally kept generic. Plugin-specific UI and HOI4 behavior
belong in plugin projects.

## Build From Source

```powershell
dotnet restore
dotnet build
dotnet run --project Shadow/Shadow.csproj
```

To create a Windows x64 MSIX package with the bundled plugin, install the
Windows 10/11 SDK, then run:

```powershell
.\scripts\build-msix.ps1 -Version 1.0.0-beta.1
```

The generated package is written to `artifacts/msix/Shadow-1.0.0-beta.1.msix`.
MSIX packages must be signed before installation. Use `-CertificatePath` or
`-CertificateThumbprint` to sign during packaging.

Example with a PFX certificate:

```powershell
.\scripts\build-msix.ps1 -Version 1.0.0-beta.1 -CertificatePath .\certs\Shadow.pfx -CertificatePassword (Read-Host -AsSecureString)
```

## Plugin Contract

Plugins implement `IShadowPlugin` from `Shadow.Abstractions`:

- `CreateNavigationItems` contributes pages to the main navigation.
- `CreateSettingsSections` contributes settings pages.
- `IShadowCommandPlugin` optionally handles command-line commands.

Build plugin projects beside the host and copy their output into the host
`Plugins` folder. The host loads `Shadow.*.dll` plugin assemblies at runtime and
shares `Shadow.Abstractions` from the host context.

## Release Notes

See [CHANGELOG.md](CHANGELOG.md) for the version history.
