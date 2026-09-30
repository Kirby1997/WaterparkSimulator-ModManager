# ModManager: in-game mod settings for Waterpark Simulator

Date: 2026-09-30

## Goal

Let players change the settings of their installed mods from inside the game, without
opening `.cfg` files or r2modman's config editor.

## Scope

In scope:

- A BepInEx 6 IL2CPP plugin (`BasePlugin`, net6.0), the same kind of mod as ParkStats.
- A "Mods" button on the main menu and on the Esc (pause) menu that opens a settings panel.
- Listing every `.cfg` file in the BepInEx config folder and editing its settings.
- Applying changes live when the owning mod is loaded; saving to the file otherwise.

Out of scope (user chose "configs only"):

- Reset-to-default buttons.
- Enabling or disabling mods.
- Press-a-key capture for key bindings (they are edited as text or as an enum list).

## Where configs come from

The config folder is BepInEx's `Paths.ConfigPath`. It is never hard-coded. With the r2modman
Default profile it resolves to
`C:\Users\Jacob\AppData\Roaming\r2modmanPlus-local\WaterparkSimulator\profiles\Default\BepInEx\config`.

Discovery is hybrid:

1. Scan the config folder, subfolders included, for `*.cfg`.
2. Parse each file into sections and settings. BepInEx writes metadata as comments above each
   key; the parser reads them:
   - `## <text>` lines: description.
   - `# Setting type: <Type>`
   - `# Default value: <value>`
   - `# Acceptable values: a, b, c`
   - `# Acceptable value range: From <min> to <max>`
   - The header `## Settings file was created by plugin <Name> v<Version>` and
     `## Plugin GUID: <guid>` give the mod's display name and version.
3. Match each file to a live `ConfigFile` whose `ConfigFilePath` is the same file. Live
   `ConfigFile`s are the `Config` of every plugin in `IL2CPPChainloader.Instance.Plugins`, plus
   `ConfigFile.CoreConfig` (BepInEx.cfg).
   - Matched ("live"): each setting that has a bound `ConfigEntry` is edited through
     `entry.BoxedValue`. BepInEx saves the file and raises `SettingChanged`, so mods that listen
     update at once. Type, default, range and allowed values come from the entry, not the
     comments. Keys in the file with no bound entry are edited as file settings (below).
   - Not matched ("file"): the new value is written into the file in place. Comments, key order
     and blank lines are kept. These settings are tagged "applies next launch".
4. Files with no settings are listed with "No settings".

`BepInEx.cfg` is included. It matches `ConfigFile.CoreConfig`, so it is edited live, but the
loader reads it only at start-up; its settings are tagged "applies next launch".

Known limit: a mod that creates extra `ConfigFile`s of its own (not its `Config`) cannot be
found. Those files are edited as file settings, and the mod may overwrite them when it saves.

ConfigurationManager tags are honoured when a live entry carries them (read by name through
reflection, so no dependency): `Browsable = false` hides the setting, `ReadOnly = true` shows it
without a control, `Order` sorts within a section, `DispName` replaces the key as the label.

## Controls

Chosen in Core from the setting's type and constraints:

| Setting | Control |
|---|---|
| `Boolean` | Toggle |
| Enum type, or a list of acceptable values | ◀ value ▶ cycling through the allowed values (a `[Flags]` enum is edited as text) |
| Number with an acceptable range | Slider plus a value label; integers step by 1 |
| Number without a range, `String`, anything else | Text field |

Text input is parsed with the same rules BepInEx uses (`TomlTypeConverter` for live entries;
invariant-culture parsing for file settings of known types; strings are stored as typed).
Invalid input is not applied: the status bar under the panel turns red and gives the reason, and the old value stays.
Out-of-range numbers are rejected, not clamped.

## Menu and panel

- The "Mods" button is a clone of an existing button in `MainMenuUI` and in `EscapeMenuUI`, so
  it matches the game's look. It is added once per menu instance.
- F10 also opens the panel from anywhere (the key is a ModManager setting). It is the fallback
  when no button can be found to clone; that case logs a warning.
- The panel is uGUI + TextMeshPro, using the game's font and panel sprites where they can be
  found, white text on a solid background (the user rejected tinted text in ParkStats).
- Layout: mod list on the left (name, version, a "not loaded" mark for file-only configs);
  the selected mod's settings on the right, grouped by section, scrollable. Each setting shows
  its description, default value and range in smaller text under its control. A footer notes that some mods
  apply changes only after a restart. Close with a button or Esc.
- While the panel is open the game's own menu input is blocked (Esc closes only the panel).
- On open the panel re-reads every file, and calls `Reload()` on live `ConfigFile`s whose file
  changed on disk since the last read, so edits made outside the game show up.

## Structure

New repository `ModManager` next to ParkStats, same layout and build:

- `src/ModManager.Core` (no game or BepInEx references, test-first):
  - `CfgDocument`: parse a `.cfg` into sections/settings with their line positions; write a
    changed value back without touching other lines.
  - `SettingInfo`: key, section, description, type name, default, range, allowed values,
    source (live/file), flags (read-only, hidden, next launch).
  - `ControlKind` choice from a `SettingInfo`.
  - `ValueRules`: parse and validate text for file settings; cycle to the next/previous allowed
    value; format numbers for display.
  - `ModListBuilder`: merge parsed files with live-entry descriptions into the ordered list the
    panel shows.
- `src/ModManager.Plugin`: plugin entry, discovery of live `ConfigFile`s and entries, menu
  hooks (Harmony postfix on the menus' start/open methods, parameterless only), uGUI panel.
- `tests/ModManager.Tests`: xUnit tests for Core.
- `thunderstore/` manifest and icon; `-t:PackThunderstore` target copied from ParkStats.
- `GamePaths.props` shared pattern for the r2modman profile.

## Errors

- Every game-facing call is null-guarded; a failure in one mod's settings shows an error row for
  that mod and does not break the list.
- File writes go to a temp file then replace the original, so a crash never leaves a half file.
- A file that fails to parse is listed as "Could not read" with the reason, not edited.

## Testing

- Core unit tests: parsing real BepInEx output (ParkStats's and BepInEx.cfg as fixtures),
  round-trip writes that change only the value line, control choice per type, parse/validate
  for bool/int/float/double/string/enum, range rejection, list cycling, tag handling, sort order.
- In game: open the panel from the main menu and from the Esc menu; change ParkStats `FontScale`
  and see the tablet text resize after reopening the page, without a restart; change `HistoryDays` and see the `.cfg`
  updated; edit a `BepInEx.cfg` value and see it tagged "applies next launch" and written.

## Publishing

Public GitHub repo and a Thunderstore package in the `waterpark-simulator` community, like
ParkStats. Nothing is pushed or uploaded without an explicit, action-specific confirmation.
