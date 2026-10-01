using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using ModManager.Core;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem;

namespace ModManager.Plugin;

[BepInPlugin(Id, Name, Version)]
public sealed class Plugin : BasePlugin
{
    public const string Id = "com.github.kirby1997.modmanager";
    public const string Name = "ModManager";
    public const string Version = "0.1.0";

    private readonly HashSet<string> _reported = new();
    private ConfigEntry<Key> _hotkey;
    private List<(Key Key, KeyControl Control)> _keyControls;
    private IntPtr _keyboardPointer;
    private bool _pausedForPanel;

    internal static Plugin Instance { get; private set; }

    internal SettingsPanel Panel { get; private set; }

    internal string HotkeyName => _hotkey.Value.ToString();

    public override void Load()
    {
        Instance = this;
        _hotkey = Config.Bind("General", "Hotkey", Key.F10,
            "Key that opens and closes the mod settings anywhere in the game. None turns it off.");

        Panel = new SettingsPanel(Log, new ConfigStore(Log), () => GameKeys.Read(ReportOnce));
        AddComponent<FrameWatcher>();
        new Harmony(Id).PatchAll(typeof(Hooks));
        Log.LogInfo($"{Name} {Version} loaded");
    }

    internal void OnFrame()
    {
        try
        {
            var keyboard = Keyboard.current;
            var escape = keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
            if (!Panel.IsOpen) _pausedForPanel = false;
            if (keyboard != null && Panel.IsCapturing) Capture(keyboard);
            else if (keyboard != null && _hotkey.Value != Key.None && keyboard[_hotkey.Value].wasPressedThisFrame) ToggleFromHotkey();
            Panel.Tick(escape);
        }
        catch (Exception e)
        {
            ReportOnce("frame update", e);
        }
    }

    /// <summary>
    /// During play the hotkey pauses first: it opens the game's pause menu, then the panel on top.
    /// Closing with the hotkey closes both; Esc or the panel's own button leave the pause menu open.
    /// </summary>
    private void ToggleFromHotkey()
    {
        if (Panel.IsOpen)
        {
            Panel.Close();
            if (_pausedForPanel) PauseMenu.Close();
            _pausedForPanel = false;
            return;
        }

        _pausedForPanel = PauseMenu.Open();
        Panel.Open();
    }

    /// <summary>Hands the first key pressed this frame to the key setting waiting for one.</summary>
    private void Capture(Keyboard keyboard)
    {
        foreach (var (key, control) in KeyControls(keyboard))
        {
            if (!control.wasPressedThisFrame) continue;
            var name = key.ToString();
            // A combination is finished by its last key, not by the modifiers held down for it.
            if (Panel.CapturingShortcut && KeyNames.IsModifier(name)) continue;

            var held = KeyControls(keyboard)
                .Where(k => KeyNames.IsModifier(k.Key.ToString()) && k.Control.isPressed)
                .Select(k => k.Key.ToString())
                .ToList();
            Panel.FinishCapture(name, held);
            return;
        }
    }

    /// <summary>Every key the keyboard has, found once; Esc is left out because it cancels.</summary>
    private List<(Key Key, KeyControl Control)> KeyControls(Keyboard keyboard)
    {
        if (_keyControls != null && _keyboardPointer == keyboard.Pointer) return _keyControls;

        _keyboardPointer = keyboard.Pointer;
        _keyControls = new List<(Key, KeyControl)>();
        foreach (var key in Enum.GetValues<Key>().Distinct())
        {
            if (key == Key.None || key == Key.Escape) continue;
            try
            {
                var control = keyboard[key];
                if (control != null) _keyControls.Add((key, control));
            }
            catch (ArgumentException)
            {
                // Not a key this keyboard has.
            }
        }
        return _keyControls;
    }

    internal void ReportOnce(string what, Exception e)
    {
        if (_reported.Add(what)) Log.LogError($"{what} failed: {e}");
    }
}
