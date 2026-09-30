namespace ModManager.Tests;

public class KeyClashesTests
{
    private static KeyBinding Binding(string id, string label, bool game, params string[] keys) =>
        new(id, label, new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase), game);

    [Fact]
    public void SameKeyInTwoPlacesIsAClash()
    {
        var clashes = KeyClashes.Find(new[]
        {
            Binding("a", "Mod A: Open", false, "F10"),
            Binding("b", "Mod B: Toggle", false, "f10"),
            Binding("c", "Mod C: Other", false, "F11"),
        });
        Assert.Equal(new[] { "Mod B: Toggle" }, clashes["a"]);
        Assert.Equal(new[] { "Mod A: Open" }, clashes["b"]);
        Assert.False(clashes.ContainsKey("c"));
    }

    [Fact]
    public void GameControlsCountButGameAgainstGameIsIgnored()
    {
        var clashes = KeyClashes.Find(new[]
        {
            Binding("g1", "Game: Player/Jump", true, "Space"),
            Binding("g2", "Game: UI/Submit", true, "Space"),
            Binding("m", "Mod: Boost", false, "Space"),
        });
        Assert.Equal(new[] { "Game: Player/Jump", "Game: UI/Submit" }, clashes["m"]);
        Assert.Single(clashes);
    }

    [Fact]
    public void ShortcutsClashOnlyWithTheSameCombination()
    {
        var clashes = KeyClashes.Find(new[]
        {
            Binding("a", "A", false, "F1", "LeftCtrl"),
            Binding("b", "B", false, "LeftCtrl", "F1"),
            Binding("c", "C", false, "F1"),
        });
        Assert.Equal(new[] { "B" }, clashes["a"]);
        Assert.False(clashes.ContainsKey("c"));
    }

    [Fact]
    public void ModBindingsComeFromKeySettings()
    {
        var cfg = "## Settings file was created by plugin Fancy v1\n\n[Keys]\n\n# Setting type: Key\n# Default value: F10\n# Acceptable values: None, F10\nOpen = F10\n\n" +
                  "# Setting type: Key\n# Default value: None\n# Acceptable values: None, F10\nSpare = None\n\n# Setting type: Int32\n# Default value: 1\nCount = 1\n";
        var mods = ModList.Build(new[] { new ConfigSource("fancy.cfg", cfg, null, SourceKind.NotLoaded) });

        var binding = Assert.Single(KeyClashes.FromMods(mods));
        Assert.Equal("Fancy: Open", binding.Label);
        Assert.Equal(KeyClashes.IdOf(mods[0], mods[0].Sections[0].Settings[0]), binding.Id);
        Assert.Equal(new[] { "F10" }, binding.Keys);
        Assert.False(binding.FromGame);
    }
}
