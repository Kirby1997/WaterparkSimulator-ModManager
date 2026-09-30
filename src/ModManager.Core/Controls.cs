namespace ModManager.Core;

public enum ControlKind
{
    Toggle,
    Cycle,
    Slider,
    Text,
    Key,
}

public static class Controls
{
    public static ControlKind For(CfgSetting setting)
    {
        if (setting.TypeName == "Boolean") return ControlKind.Toggle;
        if (KeyNames.IsKeyType(setting.TypeName)) return ControlKind.Key;
        if (setting.AcceptableValues.Count > 0 && !setting.IsFlags) return ControlKind.Cycle;
        if (setting.Range != null && ValueRules.IsNumeric(setting.TypeName)) return ControlKind.Slider;
        return ControlKind.Text;
    }
}
