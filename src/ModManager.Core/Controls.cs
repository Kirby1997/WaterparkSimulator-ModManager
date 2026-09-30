namespace ModManager.Core;

public enum ControlKind
{
    Toggle,
    Cycle,
    Slider,
    Text,
}

public static class Controls
{
    public static ControlKind For(CfgSetting setting)
    {
        if (setting.TypeName == "Boolean") return ControlKind.Toggle;
        if (setting.AcceptableValues.Count > 0 && !setting.IsFlags) return ControlKind.Cycle;
        if (setting.Range != null && ValueRules.IsNumeric(setting.TypeName)) return ControlKind.Slider;
        return ControlKind.Text;
    }
}
