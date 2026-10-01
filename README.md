# ModSettings

A Waterpark Simulator mod for changing your other mods' settings without leaving the game.

Formerly published as ModManager; versions before 0.2.0 went by that name.

BepInEx mods keep their settings in `.cfg` files under `BepInEx/config`. ModSettings adds a **Mods** button to the main menu and the pause menu (F10 also opens it anywhere) that lists every one of those files and lets you edit them.

- Every mod with a config file is listed, loaded or not, plus BepInEx's own `BepInEx.cfg`.
- Each setting gets a control that fits it: a toggle for on/off, arrows for a fixed list, a slider for a number with a range, a text field for anything else, and a press-a-key picker for keys.
- The description, default value and range BepInEx wrote for each setting are shown under it.
- A setting that shares its key with another mod, or with one of the game's own controls (including your rebinds), gets a warning naming what else uses that key.
- Input that the setting cannot hold is refused with the reason, and nothing is saved.

## How changes apply

- Settings of mods that are running are changed through the mod itself, so a mod that watches its settings picks the change up at once. Many mods only read their settings when the game starts; those need a restart.
- Settings in files no running mod owns, and everything in `BepInEx.cfg`, are marked "applies next launch". The value is written into the file, and comments, order and other values are left as they were.

## Status

Early. The file handling, input checks and key clash rules are covered by tests. The panel, the menu buttons and the key picker have been used in the game with mouse and keyboard; the key clash warnings have had less use. Controllers are untested.

Things to know:

- Only files BepInEx writes can be read properly. A mod that keeps its settings some other way is not listed.
- A mod that creates extra config files of its own may write its old values back to those files when it saves.
- Hidden, read-only, ordered and renamed settings follow the ConfigurationManager tags a mod sets.

## Install

With [r2modman](https://thunderstore.io/package/ebkr/r2modman/): select Waterpark Simulator, install `BepInExPack_IL2CPP`, then install ModSettings.

By hand: install BepInEx 6 (IL2CPP) for the game, then copy `ModSettings.Plugin.dll` and `ModSettings.Core.dll` into `BepInEx/plugins/ModSettings/`.

## Settings

`BepInEx/config/com.github.kirby1997.modsettings.cfg`, also editable in the panel itself:

| Setting | Default | Meaning |
|---|---|---|
| `Hotkey` | `F10` | Opens and closes the panel anywhere in the game. `None` turns it off |

## Building

Requirements: the .NET SDK 8 or newer, the game, and BepInExPack_IL2CPP installed through r2modman. Start the game once with BepInEx installed so it generates `BepInEx/interop`.

```
dotnet test
dotnet build -c Release
```

The build copies the plugin into the r2modman `Default` profile. To use another location, create `GamePaths.user.props` next to `GamePaths.props`:

```xml
<Project>
  <PropertyGroup>
    <ProfileDir>D:\path\to\profile</ProfileDir>
  </PropertyGroup>
</Project>
```

To build the Thunderstore package into `dist/`:

```
dotnet build src/ModSettings.Plugin -c Release -t:PackThunderstore
```

## Layout

- `src/ModSettings.Core` has no game references: reading and writing `.cfg` files, checking input, choosing controls, key names and key clashes. This is where the tests point.
- `src/ModSettings.Plugin` is the BepInEx plugin: it finds config files and running mods, adds the menu buttons and draws the panel.
- `tests/ModSettings.Tests` are the xUnit tests for Core.

## License

MIT. Not affiliated with the developers of Waterpark Simulator.
