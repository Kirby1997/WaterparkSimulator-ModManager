using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
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

    internal static Plugin Instance { get; private set; }

    internal SettingsPanel Panel { get; private set; }

    internal string HotkeyName => _hotkey.Value.ToString();

    public override void Load()
    {
        Instance = this;
        _hotkey = Config.Bind("General", "Hotkey", Key.F10,
            "Key that opens and closes the mod settings anywhere in the game. None turns it off.");

        Panel = new SettingsPanel(Log, new ConfigStore(Log));
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
            if (keyboard != null && _hotkey.Value != Key.None && keyboard[_hotkey.Value].wasPressedThisFrame) Panel.Toggle();
            Panel.Tick(escape);
        }
        catch (Exception e)
        {
            ReportOnce("frame update", e);
        }
    }

    internal void ReportOnce(string what, Exception e)
    {
        if (_reported.Add(what)) Log.LogError($"{what} failed: {e}");
    }
}
