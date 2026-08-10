# Changelog

## 1.0.0 - 2026-08-10

### Added

- Home launch pad with feature status cards, quick actions, and getting-started tips.
- Optional plugin home status API through `IShadowHomeStatusProvider`.
- Paradox Game Launcher home status for current game, playset, setup state, and direct launch.
- Settings About section with app version, local app data path, repository, and license info.
- Smooth mouse-wheel scrolling for `ScrollViewer` with frame-synced animation.

### Changed

- Product positioning for the first stable release: Paradox multi-game launch pad shell.
- Settings no longer exposes the unfinished Workspace placeholder section.
- Host and launcher localization refreshed for the home launch pad and About page.

### Fixed

- Workshop/local mod descriptor generation now preserves full metadata such as
  `replace_path`, `dependencies`, and `tags` instead of writing minimal stubs.
- `dlc_load.json` `enabled_mods` are written in playset order to match the official launcher.
- Path values are normalized to the official-style `C:/` forward-slash form.
- Broken `ugc_*.mod` files are repaired from Workshop `descriptor.mod` before launch when needed.
- Smooth scrolling no longer relies on `Task.Delay(16)`, which caused uneven frame pacing.

### Notes

- This is the first stable `1.0.0` release intended for general Windows use and Store packaging.
- Back up your Paradox game user directories before switching launchers for important playsets.
- Game paths, Workshop folders, and playsets remain managed inside the Paradox launcher plugin.

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
- Mod index export to `%APPDATA%\Posdaca\<Game Name>\\mods\\index.json`.
- Plugin command interface and `paradox.launch` command.
- Repository newline policy through `.gitattributes`.

### Notes

- This is an early beta intended for local Windows testing.
- Back up the HOI4 user directory before using Shadow with important playsets.
- The plugin API may still change before a stable release.
