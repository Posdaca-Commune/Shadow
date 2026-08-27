# Changelog

## 1.1.1 - 2026-08-25

### Added

- Save metadata parsing: the save section now shows an in-game date badge and,
  where available, a country tag (HOI4) or empire name (Stellaris) on each card.
- "Reveal in folder" button on each save card to locate the save file (or folder
  for Stellaris-style saves) in Windows Explorer.
- Folder-based save support for Stellaris: each save directory (containing multiple
  timestamped `.sav` files) is now shown as a single card with a file-count badge.

### Changed

- Removed the backup/restore feature from the save section. The toolbar now has a
  single "open save folder" button instead of a dropdown with separate save and
  backup folder options. Save cards no longer show backup or restore buttons.
- Save directory resolution is now case-insensitive and normalises underscore/space
  variants, so `save_games` and `save games` are matched interchangeably.
- Save summary no longer includes a backup count.
- Delete confirmation dialog updated to reflect that deletion is irreversible
  (backups are no longer kept).

### Fixed

- Stellaris saves were not discovered because the catalog defined the save folder
  as `save_games` (underscore) while the actual directory is `save games` (space).
  The resolver now falls back to variant matching when an exact path is not found.

### Notes

- Save metadata is parsed from the ZIP `meta` entry (Stellaris/CK3/EU4/Vic3) or from
  the binary file header and filename (HOI4). Only lightweight fields are read; the
  full gamestate is not parsed.

## 1.1.0 - 2026-08-24

### Added

- Save management section for the selected game: browse saves with timestamps,
  sizes, and backup counts; back up a save to the Shadow workspace; restore a
  backup; and delete saves (existing backups are kept).
- Per-game save folder and format support for Hearts of Iron IV, Crusader Kings
  III, Europa Universalis IV, Stellaris, Victoria 3, and Imperator: Rome.
- Save search plus summary counters for total saves and backups.
- Open the game's save folder directly from the section header.

### Notes

- Save backups are stored under `%APPDATA%\Posdaca\<Game>\save-backups` and are
  not removed when a save is deleted.
- Restoring a backup overwrites the current save file.

## 1.0.1 - 2026-08-23

### Added

- Automatic discovery of installed Paradox game directories, with an option to
  pick the directory manually in game settings.
- The "Add mod" dialog now supports selecting multiple mods: click a card to
  select it, click again to deselect, then add them all at once.

### Changed

- Refined card margins across the launcher sections for a more consistent layout.
- Microsoft Store listing assets, privacy policy, and MSIX packaging updates.

### Fixed

- Creating a playset no longer copies the current playset's mods and disabled
  DLC state; new playsets now start empty.
- Selecting a mod card in the "Add mod" dialog no longer shifts the card
  content or adds stray borders to covers and badges.
- The drag-reorder insert indicator now sits consistently just below the
  preceding card, including the end-of-list position where it previously
  floated far below the last card.
- Reordering by drag now flips the insertion point after the same travel
  distance in both directions, regardless of where inside the card it is
  grabbed.

### Notes

- Maintenance release focused on first-run setup and playset drag interactions.

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

## 1.1.2 - 2026-08-27

### Changed

- Mod import now accepts a folder instead of a zip archive. Select a mod folder,
  and Shadow copies it into the Paradox `mod/` directory and auto-generates the
  corresponding `.mod` descriptor file. This removes the `.zip`-only restriction.
- Refresh and save discovery now run off the UI thread with a loading overlay,
  keeping the interface responsive during heavy mod/DLC discovery.

### Fixed

- Local mods with mixed-case `.mod` file names (e.g. `ATA.mod`, `AED.mod`) were
  written to `dlc_load.json` in lower case, but HOI4 matches entries
  case-sensitively. The launcher path now resolves the actual on-disk file name
  so the casing in `dlc_load.json` matches the real file.

- Removed duplicate localization keys (`Paradox.Status.ImportedMod` and
  `Paradox.Status.ImportedParadoxPlaysets`) that were accidentally introduced in
  the previous release.
