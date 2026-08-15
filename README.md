# Weapon Tweaker

## 1.2.2

- Manually opened plugins now resolve to MO2's active Data version when a profile is configured. This prevents patches from using a shadowed pre-ESL copy whose compacted FormIDs differ from the version used in game.

A focused Skyrim Special Edition plugin editor for simple weapon stat changes without xEdit.

Weapon Tweaker provides a searchable weapon list and edits damage, speed, reach, weight, value, and critical damage. It protects the original plugin and writes only changed records into a new ESL-flagged ESP patch.

It can open one plugin for a fast focused edit, or optionally load every winning weapon from the active MO2 load order. A persistent output-folder setting controls where new patches are offered for saving.

## MO2 setup

1. Add `WeaponTweaker.exe` as an executable in Mod Organizer 2.
2. Run it through MO2 so the virtual Data folder is visible.
3. Open an ESP, ESM, or ESL and edit weapon values in the grid.
4. Save the new patch into an MO2 mod folder or Overwrite.
5. Enable the patch after the source plugin.

Weapon Tweaker never overwrites the source plugin. It writes only changed weapon overrides into a new ESL-flagged ESP.

To extend an existing Weapon Tweaker patch, choose that ESP in the Save dialog. The app preserves its existing records, appends or updates the changed weapons, and creates a timestamped backup before replacing it.

The locally deployed executable is located at:

`D:\Games\Modding\Tools\Weapon Tweaker\WeaponTweaker.exe`

## Building

Requires the .NET 8 SDK or newer.

```powershell
dotnet build -c Release
dotnet run -c Release --no-build -- --self-test
dotnet publish -c Release -o Distribution
```

The project uses Mutagen.Bethesda.Skyrim 0.53.1 for plugin reading and writing.

## Changelog

### 1.2.1

- Fixed append mode reordering an existing patch's masters, which changed the raw master indices encoded in its existing records.
- Existing master order is now immutable during append; newly required masters are added after it.

### 1.2.0

- Added safe appending to existing patch plugins while preserving their current records and header flags.
- Existing patches receive a timestamped backup and are replaced only after a complete temporary plugin is written successfully.

### 1.1.4

- Fixed clipped Save and Cancel buttons in the Settings dialog at scaled Windows display settings.

### 1.1.3

- Replaced the confusing Settings submenu with one dialog containing `Output folder` and `Profile folder` fields; blank fields use defaults.
- Fixed portable MO2 profiles loading against the wrong Skyrim Data folder by resolving the selected instance's `ModOrganizer.ini` game path.
- Added an explicit error when active profile plugins are not visible through MO2 instead of silently displaying vanilla-only weapons.

### 1.1.2

- Added a persistent MO2 profile-folder setting for reliable `plugins.txt` and `loadorder.txt` discovery with portable and multi-instance MO2 setups.
- Load-order mode now reports how many active plugins it actually loaded.

### 1.1.1

- Added ascending and descending sorting for every grid column.

### 1.1.0

- Added a Settings menu with a persistent default output folder.
- Added opt-in loading of all winning weapons from the active MO2 load order.
- Added a custom application icon.

### 1.0.1

- Fixed patch saving when a weapon or its source plugin references `Skyrim.esm` or another master.
