namespace ModSettings.Tests;

public class CfgDocumentTests
{
    private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static CfgSetting Find(CfgDocument doc, string section, string key) =>
        doc.Settings.Single(s => s.Section == section && s.Key == key);

    [Fact]
    public void ReadsPluginHeader()
    {
        var doc = CfgDocument.Parse(Fixture("parkstats.cfg"));
        Assert.Equal("ParkStats", doc.PluginName);
        Assert.Equal("0.2.0", doc.PluginVersion);
        Assert.Equal("com.github.kirby1997.parkstats", doc.PluginGuid);
    }

    [Fact]
    public void ReadsSettingsWithMetadata()
    {
        var doc = CfgDocument.Parse(Fixture("parkstats.cfg"));
        var scale = Find(doc, "General", "FontScale");
        Assert.Equal("1", scale.RawValue);
        Assert.Equal("Single", scale.TypeName);
        Assert.Equal("1", scale.DefaultValue);
        Assert.Equal("Multiplier for the text size in the stats tabs.", scale.Description);
        Assert.Empty(scale.AcceptableValues);
        Assert.Null(scale.Range);
        Assert.Equal(4, doc.Settings.Count);
    }

    [Fact]
    public void HeaderIsNotADescription()
    {
        var doc = CfgDocument.Parse(Fixture("parkstats.cfg"));
        Assert.DoesNotContain("Settings file", Find(doc, "Debug", "WriteDiagnostics").Description);
    }

    [Fact]
    public void ReadsEnumsFlagsAndMultiLineDescriptions()
    {
        var doc = CfgDocument.Parse(Fixture("BepInEx.cfg"));
        Assert.Null(doc.PluginName);

        var detours = Find(doc, "Detours", "DetourProviderType");
        Assert.Equal(new[] { "Default", "Dobby", "Funchook" }, detours.AcceptableValues);
        Assert.False(detours.IsFlags);

        var channels = Find(doc, "Harmony.Logger", "LogChannels");
        Assert.True(channels.IsFlags);
        Assert.Equal("Warn, Error", channels.RawValue);
        Assert.Equal("Specifies which Harmony log channels to listen to.\nNOTE: IL channel dumps the whole patch methods, use only when needed!",
            channels.Description);
    }

    [Fact]
    public void KeyWithoutMetadataHasNoType()
    {
        var doc = CfgDocument.Parse(Fixture("BepInEx.cfg"));
        var hide = Find(doc, "Chainloader", "HideManagerGameObject");
        Assert.Null(hide.TypeName);
        Assert.Equal("", hide.Description);
        Assert.Equal("true", hide.RawValue);
    }

    [Fact]
    public void ReadsRangeAndEmptyDefault()
    {
        var doc = CfgDocument.Parse(
            "[A]\n\n## Size\n# Setting type: Single\n# Default value: 1.5\n# Acceptable value range: From 0.5 to 3\nSize = 2\n\n" +
            "# Setting type: String\n# Default value:\nName = \n");
        Assert.Equal(new NumberRange(0.5, 3), Find(doc, "A", "Size").Range);
        var name = Find(doc, "A", "Name");
        Assert.Equal("", name.DefaultValue);
        Assert.Equal("", name.RawValue);
    }

    [Fact]
    public void SplitsAtFirstEqualsAndRemembersLine()
    {
        var doc = CfgDocument.Parse("[A]\r\nUrl = http://x/?a=b\r\n");
        var url = Find(doc, "A", "Url");
        Assert.Equal("http://x/?a=b", url.RawValue);
        Assert.Equal(1, url.Line);
    }

    [Fact]
    public void MetadataDoesNotLeakIntoNextSection()
    {
        var doc = CfgDocument.Parse("[A]\n# Setting type: Int32\n[B]\nKey = 1\n");
        Assert.Null(Find(doc, "B", "Key").TypeName);
    }
}
