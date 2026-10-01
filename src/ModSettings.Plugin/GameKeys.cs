using ModSettings.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ModSettings.Plugin;

/// <summary>The game's own keyboard controls, including the player's rebinds, for clash warnings.</summary>
internal static class GameKeys
{
    public static IReadOnlyList<KeyBinding> Read(Action<string, Exception> report)
    {
        var jsons = new List<string>();
        try
        {
            foreach (var asset in Assets()) jsons.Add(asset.ToJson());
        }
        catch (Exception e)
        {
            report("reading the game's controls", e);
        }

        string overrides = null;
        try
        {
            overrides = KeyRebinder.CurrentOverridesJson();
        }
        catch (Exception e)
        {
            report("reading the game's rebinds", e);
        }

        return GameBindings.FromJson(jsons, overrides);
    }

    private static List<InputActionAsset> Assets()
    {
        var assets = new List<InputActionAsset>();
        void Add(InputActionAsset asset)
        {
            if (asset != null && assets.All(a => a.Pointer != asset.Pointer)) assets.Add(asset);
        }

        Add(KeyRebinder.mainAsset);
        Add(KeyRebinder.uiAsset);
        var runtime = KeyRebinder.RuntimeAssets;
        if (runtime != null)
        {
            for (var i = 0; i < runtime.Count; i++) Add(runtime[i]);
        }

        // Before the rebinder has run, fall back to every asset the game has loaded.
        if (assets.Count == 0)
        {
            foreach (var asset in Resources.FindObjectsOfTypeAll<InputActionAsset>()) Add(asset);
        }
        return assets;
    }
}
