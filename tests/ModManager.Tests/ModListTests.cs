namespace ModManager.Tests;

public class ModListTests
{
    private const string Cfg =
        "## Settings file was created by plugin Fancy Mod v1.2.3\n## Plugin GUID: fancy\n\n" +
        "[General]\n\n# Setting type: Boolean\n# Default value: true\nEnabled = true\n\n" +
        "# Setting type: Int32\n# Default value: 1\nCount = 1\n\n" +
        "[Hidden]\n\n# Setting type: Int32\n# Default value: 1\nSecret = 1\n";

    private static ConfigSource Source(string path, SourceKind kind, string? text = Cfg, string? name = null,
        IReadOnlySet<string>? bound = null, IReadOnlyDictionary<string, SettingTags>? tags = null) =>
        new(path, text, text == null ? "locked" : null, kind, name, name == null ? null : "9.9", bound, tags);

    [Fact]
    public void TitleComesFromLoadedPluginThenHeaderThenFileName()
    {
        var mods = ModList.Build(new[]
        {
            Source("a.cfg", SourceKind.Loaded, name: "Live Name"),
            Source("b.cfg", SourceKind.NotLoaded),
            Source("sub/c.cfg", SourceKind.NotLoaded, text: "[A]\nX = 1\n"),
        });
        Assert.Equal(new[] { "Live Name", "c", "Fancy Mod" }, mods.Select(m => m.Title));
        Assert.Equal("9.9", mods[0].Version);
        Assert.Equal("1.2.3", mods[2].Version);
        Assert.Equal("", mods[1].Version);
    }

    [Fact]
    public void LoadedFirstThenNotLoadedThenLoaderAlphabetically()
    {
        var mods = ModList.Build(new[]
        {
            Source("BepInEx.cfg", SourceKind.Loader, text: "[A]\nX = 1\n"),
            Source("z.cfg", SourceKind.NotLoaded, text: "[A]\nX = 1\n"),
            Source("m.cfg", SourceKind.Loaded, name: "beta"),
            Source("n.cfg", SourceKind.Loaded, name: "Alpha"),
        });
        Assert.Equal(new[] { "Alpha", "beta", "z", "BepInEx" }, mods.Select(m => m.Title));
        Assert.Equal(new[] { "loaded", "loaded", "not loaded", "mod loader" }, mods.Select(m => m.Status));
    }

    [Fact]
    public void UnreadableFileKeepsItsError()
    {
        var mod = ModList.Build(new[] { Source("x.cfg", SourceKind.NotLoaded, text: null) }).Single();
        Assert.Equal("locked", mod.Error);
        Assert.Empty(mod.Sections);
    }

    [Fact]
    public void OnlyBoundKeysOfLoadedModsApplyLive()
    {
        var bound = new HashSet<string> { ConfigSource.KeyOf("General", "Enabled") };
        var loaded = ModList.Build(new[] { Source("a.cfg", SourceKind.Loaded, name: "A", bound: bound) }).Single();
        var general = loaded.Sections.Single(s => s.Name == "General").Settings;
        Assert.True(general.Single(s => s.Setting.Key == "Enabled").AppliesLive);
        Assert.False(general.Single(s => s.Setting.Key == "Count").AppliesLive);

        var loader = ModList.Build(new[] { Source("BepInEx.cfg", SourceKind.Loader, bound: bound) }).Single();
        Assert.All(loader.Sections.SelectMany(s => s.Settings), s => Assert.False(s.AppliesLive));
    }

    [Fact]
    public void TagsHideRenameLockAndOrder()
    {
        var tags = new Dictionary<string, SettingTags>
        {
            [ConfigSource.KeyOf("Hidden", "Secret")] = new(Hidden: true),
            [ConfigSource.KeyOf("General", "Count")] = new(ReadOnly: true, Order: 5, DisplayName: "How many"),
        };
        var mod = ModList.Build(new[] { Source("a.cfg", SourceKind.Loaded, name: "A", tags: tags) }).Single();

        Assert.Equal(new[] { "General" }, mod.Sections.Select(s => s.Name));
        var general = mod.Sections[0].Settings;
        Assert.Equal(new[] { "How many", "Enabled" }, general.Select(s => s.Label));
        Assert.True(general[0].ReadOnly);
        Assert.Equal(ControlKind.Toggle, general[1].Control);
    }
}
