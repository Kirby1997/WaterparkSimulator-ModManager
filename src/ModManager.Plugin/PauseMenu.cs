namespace ModManager.Plugin;

/// <summary>
/// The game's pause menu. The hotkey opens it under the panel during play, so the game pauses and
/// the camera stops following the mouse, just as when the panel is opened from the pause menu.
/// </summary>
internal static class PauseMenu
{
    private static EscapeMenuUI _menu;

    /// <summary>Called with each park's pause menu as it starts; there is none on the main menu.</summary>
    public static void Remember(EscapeMenuUI menu) => _menu = menu;

    /// <summary>
    /// Opens the pause menu the way Esc does. Returns true when this call opened it; false on the
    /// main menu, when it is already open, or when another game window (the tablet, say) has the screen.
    /// </summary>
    public static bool Open()
    {
        var menu = _menu;
        if (menu == null || menu.IsOpen) return false;
        var ui = UIManager.Instance;
        if (ui != null && ui.IsAnyWindowOpen()) return false;
        menu.TryOpen();
        return true;
    }

    public static void Close()
    {
        var menu = _menu;
        if (menu != null && menu.IsOpen) menu.TryClose();
    }
}
