# Shadow

[简体中文](docs/README.zh-CN.md)

Shadow is an Avalonia-based workstation shell for Paradox Interactive tooling.
The host application loads feature plugins from a `Plugins` directory, while
game-specific workflows live in separate plugin projects. The bundled launcher
plugin is `Shadow.ParadoxGameLauncher`, a multi-game launcher and playset manager
for titles such as Hearts of Iron IV, Crusader Kings III, Europa Universalis IV,
Stellaris, Victoria 3, and Imperator: Rome.

> Status: `1.0.0-beta.1` is an early beta. It is usable for local launch
> workflows, but you should back up your game user configuration before relying
> on it as your only launcher.

## Features

- Fluent Avalonia desktop shell with plugin-based navigation.
- Runtime plugin loading through `Shadow.Abstractions`.
- Multi-game Paradox launcher for discovering local and Steam Workshop mods.
- Playset management with editable Shadow playsets and read-only imports from
  the Paradox Launcher database.
- DLC enable/disable state written to game `dlc_load.json`.
- Game settings editor for common `settings.txt` options such as language,
  display mode, resolution, refresh rate, VSync, and volume.
- Shared per-game workspace files under `%APPDATA%\Posdaca\<Game Name>`.
- Mod index export to `%APPDATA%\Posdaca\<Game Name>\mods\index.json`.
- Command-line plugin command dispatch, including `paradox.launch`.

## Requirements

- Windows 10 or Windows 11.
- One or more supported Paradox games installed locally.
- For source builds: .NET SDK 10 and the workloads required by Avalonia.

The Windows release asset is published as an MSIX package. The package contains
the host and bundled plugins, so users do not need to unpack a zip or install a
separate .NET runtime.

## Getting Started

1. Download `Shadow-1.0.0-beta.1.msix` from the GitHub release.
2. Install the package with Windows App Installer.
3. Start `Shadow` from the Start menu or desktop shortcut.
4. Open `Paradox Game Launcher` from the left navigation.
5. Click the game name in the top-left corner to switch games.
6. In `Game Settings`, set:
   - the path to the selected game executable;
   - the game user folder under
     `%USERPROFILE%\Documents\Paradox Interactive\<Game Name>`;
   - the Steam Workshop folder, if you want Workshop mods discovered.
7. Refresh, create or import a playset, then save and launch.

## Command-Line Launch

Shadow can dispatch plugin commands before starting the desktop UI. The bundled
Paradox launcher plugin currently exposes these commands. MSIX installs register
the `shadow.exe` app execution alias, so `shadow` can be used from a terminal:

```powershell
shadow --shadow-command paradox.launch
shadow --shadow-command paradox.launch --game hoi4
shadow --shadow-command paradox.launch --game hoi4 --playset-id default
shadow --shadow-command paradox.launch --game hoi4 --playset-id default --allow-missing-mods
```

Options:

- `--shadow-command` or `--command`: command name.
- `--game`, `--game-id`, or `--gameId`: target game id (`hoi4`, `ck3`, `eu4`,
  `stellaris`, `vic3`, `imperator`).
- `--playset-id`, `--playsetId`, or `--playset`: playset id to launch.
- `--allow-missing-mods`: allows a playset to launch even when enabled mod ids
  are not found in the current scan.

If no playset is specified, Shadow uses the selected playset from the saved
launcher state, then falls back to the first available playset.

## Data Locations

- Host/plugin state: `%LOCALAPPDATA%\Shadow`
- Game workspace playsets: `%APPDATA%\Posdaca\<Game Name>\playsets`
- Game workspace mod index: `%APPDATA%\Posdaca\<Game Name>\mods\index.json`
- Default game user directory:
  `%USERPROFILE%\Documents\Paradox Interactive\<Game Name>`

Shadow writes launch state to the standard Paradox user files used by the
launcher flow, including `dlc_load.json` and selected fields in `settings.txt`.

## Repository Layout

```text
Shadow/
  Shadow/                       Desktop host application
  Shadow.Abstractions/           Host/plugin contracts
  Shadow.ParadoxGameLauncher/   Bundled multi-game launcher plugin
```

The host is intentionally kept generic. Plugin-specific UI and game behavior
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
