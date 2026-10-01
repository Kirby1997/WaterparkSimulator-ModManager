namespace ModSettings.Tests;

public class ValueRulesTests
{
    private static readonly string[] Levels = { "None", "Info", "Warn", "Error" };

    [Theory]
    [InlineData("true", "true")]
    [InlineData(" False ", "false")]
    public void BooleansAreNormalised(string input, string raw) =>
        Assert.Equal(raw, ValueRules.Check(Settings.Make("Boolean"), input).Raw);

    [Fact]
    public void BadBooleanIsRejected() =>
        Assert.Equal("Must be true or false.", ValueRules.Check(Settings.Make("Boolean"), "yes").Error);

    [Fact]
    public void IntegersAreCheckedAgainstTheirType()
    {
        Assert.Equal("42", ValueRules.Check(Settings.Make("Int32"), " 42 ").Raw);
        Assert.Equal("Must be a whole number.", ValueRules.Check(Settings.Make("Int32"), "4.5").Error);
        Assert.Equal("Must be a whole number from 0 to 255.", ValueRules.Check(Settings.Make("Byte"), "300").Error);
    }

    [Fact]
    public void DecimalsAcceptPointOrComma()
    {
        Assert.Equal("1.5", ValueRules.Check(Settings.Make("Single"), "1.5").Raw);
        Assert.Equal("1.5", ValueRules.Check(Settings.Make("Double"), "1,5").Raw);
        Assert.Equal("Must be a number.", ValueRules.Check(Settings.Make("Single"), "abc").Error);
        Assert.Equal("Must be a number.", ValueRules.Check(Settings.Make("Single"), "1e40").Error);
    }

    [Fact]
    public void OutOfRangeIsRejectedNotClamped()
    {
        var setting = Settings.Make("Single", "1", range: new NumberRange(0.5, 3));
        Assert.Equal("Must be from 0.5 to 3.", ValueRules.Check(setting, "4").Error);
        Assert.Equal("3", ValueRules.Check(setting, "3").Raw);
    }

    [Fact]
    public void ListedValuesAreMatchedWithoutCase()
    {
        var setting = Settings.Make("LogChannel", "Warn", Levels);
        Assert.Equal("Error", ValueRules.Check(setting, "error").Raw);
        Assert.Equal("Must be one of: None, Info, Warn, Error.", ValueRules.Check(setting, "Loud").Error);
    }

    [Fact]
    public void FlagsAreSplitCheckedAndDeduplicated()
    {
        var setting = Settings.Make("LogChannel", "Warn", Levels, flags: true);
        Assert.Equal("Warn, Error", ValueRules.Check(setting, "warn,Error , warn").Raw);
        Assert.Equal("'Loud' is not one of: None, Info, Warn, Error.", ValueRules.Check(setting, "Warn, Loud").Error);
        Assert.Equal("Pick at least one of: None, Info, Warn, Error.", ValueRules.Check(setting, " , ").Error);
    }

    [Fact]
    public void StringsAreEscapedAndKeptUntrimmed()
    {
        Assert.Equal(" a\\nb ", ValueRules.Check(Settings.Make("String"), " a\nb ").Raw);
        Assert.Equal("a\nb", ValueRules.Display(Settings.Make("String", "a\\nb")));
    }

    [Fact]
    public void ListedStringsAreEscaped() =>
        Assert.Equal("it\\'s", ValueRules.Check(Settings.Make("String", "", new[] { "it's", "no" }), "IT'S").Raw);

    [Fact]
    public void UnknownTypesAreTakenAsTypedOnOneLine()
    {
        Assert.Equal("Ctrl + F1", ValueRules.Check(Settings.Make("KeyboardShortcut"), " Ctrl + F1 ").Raw);
        Assert.Equal("Must fit on one line.", ValueRules.Check(Settings.Make(null), "a\nb").Error);
    }

    [Fact]
    public void CycleWrapsBothWays()
    {
        var setting = Settings.Make("LogChannel", "Error", Levels);
        Assert.Equal("None", ValueRules.Cycle(setting, 1));
        Assert.Equal("Warn", ValueRules.Cycle(setting, -1));
        Assert.Equal("None", ValueRules.Cycle(Settings.Make("LogChannel", "Gone", Levels), 1));
    }

    [Theory]
    [InlineData("Single", 0.5, 3, 1.2345, "1.23")]
    [InlineData("Single", 0, 100, 42.4, "42")]
    [InlineData("Int32", 0, 365, 59.6, "60")]
    [InlineData("Single", 0.5, 3, 5, "3")]
    public void SliderTextRoundsToTheRange(string type, double min, double max, double position, string text) =>
        Assert.Equal(text, ValueRules.SliderText(Settings.Make(type, range: new NumberRange(min, max)), position));

    [Fact]
    public void ParseNumberReadsRawValue()
    {
        Assert.Equal(1.5, ValueRules.ParseNumber(Settings.Make("Single", "1.5")));
        Assert.Null(ValueRules.ParseNumber(Settings.Make("Single", "x")));
    }

    [Fact]
    public void DetailsListDescriptionDefaultAndRange()
    {
        var setting = Settings.Make("Single", "1", range: new NumberRange(0.5, 3), defaultValue: "1", description: "Text size.");
        Assert.Equal("Text size.\nDefault: 1. Range: 0.5 to 3.", ValueRules.Details(setting));
        Assert.Equal("Default: (empty).", ValueRules.Details(Settings.Make("String", defaultValue: "")));
        Assert.Equal("", ValueRules.Details(Settings.Make(null)));
    }

    [Fact]
    public void DetailsExplainFlags() =>
        Assert.Equal("Default: Warn. Several allowed, separated by commas: None, Info, Warn, Error.",
            ValueRules.Details(Settings.Make("LogChannel", "Warn", Levels, flags: true, defaultValue: "Warn")));
}
