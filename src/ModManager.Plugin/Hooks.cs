using HarmonyLib;

namespace ModManager.Plugin;

/// <summary>
/// Hooks into the game's menus. None may throw: an exception escaping into native code would
/// take the game down with it.
/// </summary>
[HarmonyPatch]
internal static class Hooks
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(MainMenuUI), nameof(MainMenuUI.Start))]
    private static void AfterMainMenuStart(MainMenuUI __instance) => AddButton(__instance, "main menu");

    [HarmonyPostfix]
    [HarmonyPatch(typeof(EscapeMenuUI), nameof(EscapeMenuUI.Start))]
    private static void AfterEscapeMenuStart(EscapeMenuUI __instance) => AddButton(__instance, "pause menu");

    // In case the menu was built after Start ran; adding is skipped when the button is there.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(EscapeMenuUI), nameof(EscapeMenuUI.Show))]
    private static void AfterEscapeMenuShow(EscapeMenuUI __instance) => AddButton(__instance, "pause menu");

    // While the panel is open, Esc/Back closes it instead of the game window underneath.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIManager), nameof(UIManager.CloseTopWindow))]
    private static bool BeforeCloseTopWindow() => !ClosePanel();

    [HarmonyPrefix]
    [HarmonyPatch(typeof(UIWindow), nameof(UIWindow.TryClose), typeof(bool))]
    private static bool BeforeTryClose(bool fromEscapeEvent) => !(fromEscapeEvent && ClosePanel());

    private static void AddButton(UnityEngine.Component menu, string where)
    {
        try
        {
            var plugin = Plugin.Instance;
            MenuButtons.Add(menu, where, plugin.Log, plugin.HotkeyName, plugin.Panel.Open);
        }
        catch (Exception e)
        {
            Plugin.Instance.ReportOnce("Mods button on the " + where, e);
        }
    }

    private static bool ClosePanel()
    {
        try
        {
            return Plugin.Instance.Panel.HandleEscapeFromGame();
        }
        catch (Exception e)
        {
            Plugin.Instance.ReportOnce("closing the mod settings", e);
            return false;
        }
    }
}
