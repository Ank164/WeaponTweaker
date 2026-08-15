# Weapon Tweaker

A focused Skyrim Special Edition plugin editor for simple weapon stat changes without xEdit.

Weapon Tweaker provides a searchable weapon list and edits damage, speed, reach, weight, value, and critical damage. It protects the original plugin and writes only changed records into a new ESL-flagged ESP patch.

## MO2 setup

1. Add `WeaponTweaker.exe` as an executable in Mod Organizer 2.
2. Run it through MO2 so the virtual Data folder is visible.
3. Open an ESP, ESM, or ESL and edit weapon values in the grid.
4. Save the new patch into an MO2 mod folder or Overwrite.
5. Enable the patch after the source plugin.

Weapon Tweaker never overwrites the source plugin. It writes only changed weapon overrides into a new ESL-flagged ESP.

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

### 1.0.1

- Fixed patch saving when a weapon or its source plugin references `Skyrim.esm` or another master.
