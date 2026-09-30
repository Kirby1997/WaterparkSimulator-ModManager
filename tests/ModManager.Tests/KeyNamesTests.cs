namespace ModManager.Tests;

public class KeyNamesTests
{
    [Theory]
    [InlineData("Key")]
    [InlineData("KeyCode")]
    [InlineData("KeyboardShortcut")]
    public void KeyTypesGetKeyControl(string type) =>
        Assert.Equal(ControlKind.Key, Controls.For(Settings.Make(type, "F10", new[] { "None", "F10" })));

    [Fact]
    public void KeyCodeAndInputSystemNamesMeanTheSameKey()
    {
        Assert.Equal(new[] { "Digit1" }, KeyNames.Parse("KeyCode", "Alpha1"));
        Assert.Equal(new[] { "Enter" }, KeyNames.Parse("KeyCode", "Return"));
        Assert.Equal(new[] { "LeftCtrl" }, KeyNames.Parse("KeyCode", "LeftControl"));
        Assert.Equal(new[] { "F10" }, KeyNames.Parse("Key", "F10"));
    }

    [Theory]
    [InlineData("Key", "None")]
    [InlineData("KeyCode", "none")]
    [InlineData("KeyboardShortcut", "")]
    [InlineData("KeyboardShortcut", "None")]
    public void UnboundIsNull(string type, string raw) => Assert.Null(KeyNames.Parse(type, raw));

    [Fact]
    public void ShortcutIsAllItsKeys() =>
        Assert.Equal(new[] { "F1", "LeftCtrl" }, KeyNames.Parse("KeyboardShortcut", "F1 + LeftControl"));

    [Fact]
    public void NotAKeyTypeIsNull() => Assert.Null(KeyNames.Parse("Int32", "5"));

    [Theory]
    [InlineData("<Keyboard>/f10", "F10")]
    [InlineData("<Keyboard>/1", "Digit1")]
    [InlineData("<Keyboard>/leftCtrl", "LeftCtrl")]
    [InlineData("<Keyboard>/ctrl", "LeftCtrl")]
    [InlineData("<keyboard>/numpadEnter", "NumpadEnter")]
    [InlineData("<Gamepad>/buttonSouth", null)]
    [InlineData("<Mouse>/leftButton", null)]
    [InlineData("<Keyboard>/anyKey", null)]
    [InlineData("<Keyboard>/#(a)", null)]
    [InlineData("", null)]
    public void ReadsKeyboardControlPaths(string path, string? key) => Assert.Equal(key, KeyNames.FromControlPath(path));

    [Fact]
    public void CaptureForKeyTypeIsTheKeyName() =>
        Assert.Equal("F5", KeyNames.Capture(Settings.Make("Key", "F10", new[] { "None", "F5", "F10" }), "F5", new[] { "LeftCtrl" }).Raw);

    [Fact]
    public void CaptureForKeyCodeUsesKeyCodeNames() =>
        Assert.Equal("Alpha1", KeyNames.Capture(Settings.Make("KeyCode", "None", new[] { "None", "Alpha1" }), "Digit1", Array.Empty<string>()).Raw);

    [Fact]
    public void CaptureOfKeyTheSettingCannotHoldIsRefused() =>
        Assert.Equal("OEM1 cannot be used for this setting.",
            KeyNames.Capture(Settings.Make("KeyCode", "None", new[] { "None", "A" }), "OEM1", Array.Empty<string>()).Error);

    [Fact]
    public void CaptureForShortcutAddsHeldModifiers() =>
        Assert.Equal("F1 + LeftControl + LeftShift",
            KeyNames.Capture(Settings.Make("KeyboardShortcut", ""), "F1", new[] { "LeftCtrl", "LeftShift" }).Raw);

    [Fact]
    public void ModifiersAreKnown()
    {
        Assert.True(KeyNames.IsModifier("LeftShift"));
        Assert.True(KeyNames.IsModifier("RightCtrl"));
        Assert.False(KeyNames.IsModifier("F1"));
    }

    [Fact]
    public void ClearValueIsNone() => Assert.Equal("None", KeyNames.Unbound);
}
