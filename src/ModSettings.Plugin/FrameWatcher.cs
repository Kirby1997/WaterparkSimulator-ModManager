using UnityEngine;

namespace ModSettings.Plugin;

/// <summary>Gives the plugin a per-frame update for the hotkey and the panel.</summary>
internal sealed class FrameWatcher : MonoBehaviour
{
    public FrameWatcher(IntPtr pointer) : base(pointer)
    {
    }

    private void Update() => Plugin.Instance?.OnFrame();
}
