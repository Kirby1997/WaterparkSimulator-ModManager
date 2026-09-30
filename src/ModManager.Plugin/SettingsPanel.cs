using BepInEx.Logging;
using ModManager.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ModManager.Plugin;

/// <summary>
/// The full-screen mod settings window: installed mods on the left, the chosen mod's settings on
/// the right. It sits on its own canvas above every game menu.
/// </summary>
internal sealed class SettingsPanel
{
    private const float SliderDelay = 0.4f;
    private const string Note = "Changes save straight away. Some mods only read their settings when the game starts.";

    private readonly ManualLogSource _log;
    private readonly ConfigStore _store;

    private GameObject _root;
    private RectTransform _modList;
    private RectTransform _settings;
    private ScrollRect _settingsScroll;
    private TextMeshProUGUI _status;
    private Image _statusBar;
    private string _selectedPath;
    private CursorLockMode _cursorLock;
    private bool _cursorVisible;
    private int _escapeFrame = -1;

    // A slider saves once it has been still for a moment, not on every step of a drag.
    private (ModEntry Mod, SettingView View, string Text)? _pendingSlider;
    private float _pendingSince;

    public SettingsPanel(ManualLogSource log, ConfigStore store)
    {
        _log = log;
        _store = store;
    }

    public bool IsOpen => _root != null && _root.activeSelf;

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    public void Open()
    {
        try
        {
            if (_root == null) Build();
            _store.Refresh();
            _cursorLock = Cursor.lockState;
            _cursorVisible = Cursor.visible;
            _root.SetActive(true);
            ShowStatus(Note, error: false);
            ShowMods();
        }
        catch (Exception e)
        {
            _log.LogError($"Could not open the mod settings: {e}");
        }
    }

    public void Close()
    {
        if (!IsOpen) return;
        FlushSlider();
        _root.SetActive(false);
        Cursor.lockState = _cursorLock;
        Cursor.visible = _cursorVisible;
    }

    /// <summary>Called once per frame.</summary>
    public void Tick(bool escapePressed)
    {
        if (!IsOpen) return;

        // Opened with the hotkey during play, the game keeps trying to take the cursor back.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (_pendingSlider != null && Time.unscaledTime - _pendingSince >= SliderDelay) FlushSlider();

        // Esc normally reaches the game's window code, which the hooks turn into closing this
        // panel. Where no game window handles it, close a frame later.
        if (escapePressed) _escapeFrame = Time.frameCount;
        else if (_escapeFrame == Time.frameCount - 1) Close();
    }

    private void Build()
    {
        _root = new GameObject("ModManager Settings") { layer = 5 };
        UnityEngine.Object.DontDestroyOnLoad(_root);
        var canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        var scaler = _root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        _root.AddComponent<GraphicRaycaster>();
        var rootRect = _root.GetComponent<RectTransform>();

        // Dims the game and stops clicks reaching the menu underneath.
        var blocker = Ui.Rect("Blocker", rootRect);
        Ui.Stretch(blocker);
        Ui.NewImage(blocker, new Color(0f, 0f, 0f, 0.65f));

        var window = Ui.Rect("Window", rootRect);
        window.anchorMin = window.anchorMax = new Vector2(0.5f, 0.5f);
        window.sizeDelta = new Vector2(1500f, 900f);
        Ui.NewImage(window, Ui.PanelColor);

        var title = Ui.NewText(window, "Mod settings", 44f);
        Ui.Place(title.rectTransform, 0f, 1f, 0.6f, 1f, left: 32f, bottom: -84f, top: 20f);

        var close = Ui.NewButton(window, "Close", 30f, Ui.ButtonColor, Close);
        var closeRect = close.GetComponent<RectTransform>();
        Ui.Place(closeRect, 1f, 1f, 1f, 1f, left: -196f, bottom: -80f, right: 28f, top: 24f);

        var modScroll = Ui.NewScroll(window, 6f, out _modList);
        Ui.Place(modScroll.GetComponent<RectTransform>(), 0f, 0f, 0f, 1f, left: 24f, bottom: 104f, right: -444f, top: 100f);

        _settingsScroll = Ui.NewScroll(window, 4f, out _settings);
        Ui.Place(_settingsScroll.GetComponent<RectTransform>(), 0f, 0f, 1f, 1f, left: 468f, bottom: 104f, right: 24f, top: 100f);

        var statusRect = Ui.Rect("Status", window);
        Ui.Place(statusRect, 0f, 0f, 1f, 0f, left: 24f, bottom: 24f, right: 24f, top: -84f);
        _statusBar = Ui.NewImage(statusRect, Ui.FieldColor);
        _status = Ui.NewText(statusRect, "", 24f);
        Ui.Stretch(_status.rectTransform, 12f);

        _root.SetActive(false);
    }

    private void ShowMods()
    {
        var mods = _store.Mods;
        var selected = mods.FirstOrDefault(m => m.Source.RelativePath == _selectedPath) ?? mods.FirstOrDefault();
        _selectedPath = selected?.Source.RelativePath;

        Ui.Clear(_modList);
        foreach (var mod in mods)
        {
            var version = mod.Version.Length > 0 ? "v" + mod.Version + " · " : "";
            var isSelected = mod == selected;
            var button = Ui.NewButton(_modList, $"{mod.Title}\n<size=70%>{version}{mod.Status}</size>", 28f,
                isSelected ? Ui.SelectedColor : Ui.ButtonColor, () => Select(mod));
            Ui.Height(button, 76f);
        }

        ShowSettings(selected);
    }

    private void Select(ModEntry mod)
    {
        try
        {
            FlushSlider();
            _selectedPath = mod.Source.RelativePath;
            ShowMods();
            _settingsScroll.verticalNormalizedPosition = 1f;
        }
        catch (Exception e)
        {
            _log.LogError($"Could not show {mod.Title}: {e}");
        }
    }

    private void ShowSettings(ModEntry mod)
    {
        Ui.Clear(_settings);
        if (mod == null)
        {
            AddLine("No config files found in BepInEx\\config.", 28f);
            return;
        }

        AddLine(mod.Source.RelativePath, 22f);
        if (mod.Error != null)
        {
            AddLine("Could not read this file: " + mod.Error, 28f);
            return;
        }
        if (mod.Sections.Count == 0) AddLine("No settings.", 28f);

        foreach (var section in mod.Sections)
        {
            var header = AddLine(section.Name.Length > 0 ? section.Name : "(no section)", 34f);
            header.fontStyle = FontStyles.Bold;
            foreach (var view in section.Settings) AddSetting(mod, view);
        }
    }

    private TextMeshProUGUI AddLine(string text, float size)
    {
        var holder = Ui.Rect("Line", _settings);
        var layout = holder.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.padding = new RectOffset { top = 6 };
        return Ui.NewText(holder, text, size);
    }

    private void AddSetting(ModEntry mod, SettingView view)
    {
        var nextLaunch = !view.AppliesLive;
        var row = Ui.Rect(view.Label, _settings);
        Ui.Height(row, nextLaunch ? 76f : 60f);

        var label = Ui.NewText(row, view.Label + (nextLaunch ? "\n<size=70%>applies next launch</size>" : ""), 28f, rich: true);
        Ui.Place(label.rectTransform, 0f, 0f, 0.42f, 1f);

        var control = Ui.Rect("Control", row);
        Ui.Place(control, 0.44f, 0f, 1f, 1f, bottom: 4f, top: 4f);
        try
        {
            AddControl(mod, view, control);
        }
        catch (Exception e)
        {
            _log.LogError($"Could not draw {view.Setting.Key}: {e}");
            Ui.Stretch(Ui.NewText(control, ValueRules.Display(view.Setting), 28f).rectTransform);
        }

        var details = ValueRules.Details(view.Setting);
        if (details.Length > 0) AddLine(details, 22f);
    }

    private void AddControl(ModEntry mod, SettingView view, RectTransform area)
    {
        var setting = view.Setting;
        var shown = ValueRules.Display(setting);
        if (view.ReadOnly)
        {
            Ui.Stretch(Ui.NewText(area, shown + "  (read-only)", 28f).rectTransform);
            return;
        }

        switch (view.Control)
        {
            case ControlKind.Toggle:
            {
                var on = string.Equals(shown, "true", StringComparison.OrdinalIgnoreCase);
                var toggle = Ui.NewButton(area, on ? "On" : "Off", 28f, on ? Ui.ButtonColor : Ui.OffColor,
                    () => Apply(mod, view, on ? "false" : "true", rebuild: true));
                Ui.Place(toggle.GetComponent<RectTransform>(), 0f, 0f, 0.3f, 1f);
                break;
            }
            case ControlKind.Cycle:
            {
                var previous = Ui.NewButton(area, "<", 30f, Ui.ButtonColor, () => Apply(mod, view, ValueRules.Cycle(setting, -1), rebuild: true));
                Ui.Place(previous.GetComponent<RectTransform>(), 0f, 0f, 0.12f, 1f);
                var value = Ui.NewText(area, shown, 28f, TextAlignmentOptions.Center);
                Ui.Place(value.rectTransform, 0.13f, 0f, 0.87f, 1f);
                var next = Ui.NewButton(area, ">", 30f, Ui.ButtonColor, () => Apply(mod, view, ValueRules.Cycle(setting, 1), rebuild: true));
                Ui.Place(next.GetComponent<RectTransform>(), 0.88f, 0f, 1f, 1f);
                break;
            }
            case ControlKind.Slider:
            {
                var range = setting.Range!.Value;
                var number = ValueRules.ParseNumber(setting) ?? range.Min;
                var valueText = Ui.NewText(area, shown, 28f, TextAlignmentOptions.MidlineRight);
                Ui.Place(valueText.rectTransform, 0.76f, 0f, 1f, 1f);
                var slider = Ui.NewSlider(area, (float)range.Min, (float)range.Max, ValueRules.IsInteger(setting.TypeName), (float)number, position =>
                {
                    var text = ValueRules.SliderText(setting, position);
                    valueText.text = text;
                    _pendingSlider = (mod, view, text);
                    _pendingSince = Time.unscaledTime;
                });
                Ui.Place(slider.GetComponent<RectTransform>(), 0f, 0f, 0.74f, 1f);
                break;
            }
            default:
            {
                var input = Ui.NewInput(area, shown, 26f, text =>
                {
                    if (text != shown) Apply(mod, view, text, rebuild: true);
                });
                Ui.Stretch(input.GetComponent<RectTransform>());
                break;
            }
        }
    }

    private void FlushSlider()
    {
        if (_pendingSlider is not { } pending) return;
        _pendingSlider = null;
        // The slider stays as it is; rebuilding would cut a drag short.
        Apply(pending.Mod, pending.View, pending.Text, rebuild: false);
    }

    private void Apply(ModEntry mod, SettingView view, string text, bool rebuild)
    {
        try
        {
            var error = _store.Apply(mod, view, text);
            if (error == null) ShowStatus($"Saved {view.Label} = {text}.", error: false);
            else ShowStatus($"{view.Label} not saved: {error}", error: true);

            if (!rebuild && error == null) return;
            var position = _settingsScroll.verticalNormalizedPosition;
            ShowMods();
            Canvas.ForceUpdateCanvases();
            _settingsScroll.verticalNormalizedPosition = position;
        }
        catch (Exception e)
        {
            _log.LogError($"Could not apply {view.Setting.Key}: {e}");
        }
    }

    private void ShowStatus(string text, bool error)
    {
        _status.text = text;
        _statusBar.color = error ? Ui.ErrorColor : Ui.FieldColor;
    }
}
