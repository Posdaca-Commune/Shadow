# Changelog

## 1.0.0-beta.1 - 2026-06-17

### Added

- Initial Avalonia/Fluent workstation shell.
- Runtime plugin loading through `Shadow.Abstractions`.
- Built-in `Shadow.ParadoxGameLauncher` multi-game plugin.
- HOI4 mod discovery for local descriptors and Steam Workshop descriptors.
- Editable Shadow playsets stored in `%APPDATA%\Posdaca\<Game Name>`.
- Read-only import of Paradox Launcher playsets from `launcher-v2.sqlite`.
- DLC selection support through HOI4 `dlc_load.json`.
- Game settings editor for selected `settings.txt` fields.
- Mod index export to `%APPDATA%\Posdaca\<Game Name>\mods\index.json`.
- Plugin command interface and `paradox.launch` command.
- Repository newline policy through `.gitattributes`.

### Notes

- This is an early beta intended for local Windows testing.
- Back up the HOI4 user directory before using Shadow with important playsets.
- The plugin API may still change before a stable release.
