namespace ModManager.Tests;

public class TomlStringTests
{
    [Fact]
    public void EscapesControlCharactersAndQuotes() =>
        Assert.Equal("a\\nb\\t\\\"c\\\"\\'", TomlString.Escape("a\nb\t\"c\"'"));

    [Fact]
    public void EscapeLeavesBackslashAsIs_LikeBepInEx() =>
        Assert.Equal(@"C:\Games", TomlString.Escape(@"C:\Games"));

    [Fact]
    public void EscapeOfEmptyIsEmpty() => Assert.Equal("", TomlString.Escape(""));

    [Fact]
    public void UnescapeReversesEscape() =>
        Assert.Equal("a\nb\t\"c\"", TomlString.Unescape("a\\nb\\t\\\"c\\\""));

    [Fact]
    public void UnescapeKeepsUnknownSequencesAndTrailingBackslash()
    {
        Assert.Equal(@"a\qb", TomlString.Unescape(@"a\qb"));
        Assert.Equal(@"a\", TomlString.Unescape(@"a\"));
    }

    [Fact]
    public void ReadLeavesWindowsPathsAlone() =>
        Assert.Equal(@"C:\new folder", TomlString.Read(@"C:\new folder"));

    [Fact]
    public void ReadUnescapesOtherText() => Assert.Equal("a\nb", TomlString.Read(@"a\nb"));
}
