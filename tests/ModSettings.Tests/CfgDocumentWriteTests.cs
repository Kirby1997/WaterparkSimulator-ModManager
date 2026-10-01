namespace ModSettings.Tests;

public class CfgDocumentWriteTests
{
    private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    [Fact]
    public void ChangesOnlyTheValueLine()
    {
        var text = Fixture("parkstats.cfg");
        var doc = CfgDocument.Parse(text);
        var days = doc.Settings.Single(s => s.Key == "HistoryDays");

        var written = doc.WithValue(days, "90");

        var before = text.Split('\n');
        var after = written.Split('\n');
        Assert.Equal(before.Length, after.Length);
        for (var i = 0; i < before.Length; i++)
        {
            if (i == days.Line) Assert.Equal("HistoryDays = 90" + (before[i].EndsWith('\r') ? "\r" : ""), after[i]);
            else Assert.Equal(before[i], after[i]);
        }
        Assert.Equal("90", CfgDocument.Parse(written).Settings.Single(s => s.Key == "HistoryDays").RawValue);
    }

    [Fact]
    public void KeepsLfEndings()
    {
        var doc = CfgDocument.Parse("[A]\nKey = 1\n");
        Assert.Equal("[A]\nKey = 2\n", doc.WithValue(doc.Settings[0], "2"));
    }

    [Fact]
    public void WritesEmptyValueLikeBepInEx()
    {
        var doc = CfgDocument.Parse("[A]\nName = x\n");
        Assert.Equal("[A]\nName = \n", doc.WithValue(doc.Settings[0], ""));
    }

    [Fact]
    public void RejectsLineBreaks()
    {
        var doc = CfgDocument.Parse("[A]\nName = x\n");
        Assert.Throws<ArgumentException>(() => doc.WithValue(doc.Settings[0], "a\nb"));
    }

    [Fact]
    public void RejectsSettingFromAnotherFile()
    {
        var doc = CfgDocument.Parse("[A]\nName = x\n");
        var other = CfgDocument.Parse("[A]\nOther = y\n").Settings[0];
        Assert.Throws<InvalidOperationException>(() => doc.WithValue(other, "z"));
    }
}
