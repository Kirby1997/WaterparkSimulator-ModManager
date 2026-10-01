# Changelog

## 0.1.1

- Fixed: with ModManager installed, saves never left the loading screen. One of its menu hooks broke a part of the game that loading relies on; that hook is gone.
- The hotkey (F10) now pauses the game during play: it opens the pause menu, then the mod settings on top, so the camera no longer follows the mouse. Pressing it again returns to play; Esc leaves you on the pause menu.

## 0.1.0

- First release. A Mods button on the main menu and the pause menu, and F10 anywhere, open a list of every config file in `BepInEx/config` with its settings.
- Toggles for on/off settings, arrows for lists, sliders for numbers with a range, text fields for the rest, and a press-a-key picker for key settings.
- A warning under any key setting that shares its key with another mod or one of the game's controls.
- Changes to loaded mods apply at once; other files, and BepInEx's own settings, are saved for the next launch.
