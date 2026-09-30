namespace ModManager.Tests;

public class ControlsTests
{
    [Fact]
    public void BooleanIsToggle() => Assert.Equal(ControlKind.Toggle, Controls.For(Settings.Make("Boolean", "true")));

    [Fact]
    public void ListIsCycle() =>
        Assert.Equal(ControlKind.Cycle, Controls.For(Settings.Make("DetourProvider", "Default", new[] { "Default", "Dobby" })));

    [Fact]
    public void FlagsAreText() =>
        Assert.Equal(ControlKind.Text, Controls.For(Settings.Make("LogLevel", "Info", new[] { "Info", "Debug" }, flags: true)));

    [Fact]
    public void RangedNumberIsSlider() =>
        Assert.Equal(ControlKind.Slider, Controls.For(Settings.Make("Single", "1", range: new NumberRange(0.5, 3))));

    [Fact]
    public void UnrangedNumberStringAndUnknownAreText()
    {
        Assert.Equal(ControlKind.Text, Controls.For(Settings.Make("Int32", "60")));
        Assert.Equal(ControlKind.Text, Controls.For(Settings.Make("String", "x")));
        Assert.Equal(ControlKind.Text, Controls.For(Settings.Make(null, "x")));
    }
}
