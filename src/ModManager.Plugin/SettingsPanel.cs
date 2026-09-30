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
    private readonly Func<IReadOnlyList<KeyBinding>> _gameKeys;

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
    private int _escapeHandledFrame = -1;
    private IReadOnlyList<KeyBinding> _gameBindings = Array.Empty<KeyBinding>();
    private IReadOnlyDictionary<string, IReadOnlyList<string>> _clashes = new Dictionary<string, IReadOnlyList<string>>();

    // The key setting waiting for the player to press a key.
    private (ModEntry Mod, SettingView View, TextMeshProUGUI Label)? _capture;

    // A slider saves once it has been still for a moment, not on every step of a drag.
    private (ModEntry Mod, SettingView View, string Text)? _pendingSlider;
    private float _pendingSince;

    public SettingsPanel(ManualLogSource log, ConfigStore store, Func<IReadOnlyList<KeyBinding>> gameKeys)
    {
        _log = log;
        _store = store;
        _gameKeys = gameKeys;
    }

    public bool IsOpen => _root != null && _root.activeSelf;

    public bool IsCapturing => IsOpen && _capture != null;

    /// <summary>True while capturing for a shortcut, which waits for a key that is not a modifier.</summary>
    public bool CapturingShortcut => IsCapturing && _capture.Value.View.Setting.TypeName == "KeyboardShortcut";

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
            _gameBindings = _gameKeys();
            _capture = null;
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
        _capture = null;
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

        // Esc normally reaches the game's window code, whose hooks call HandleEscapeFromGame.
        // Where no game window handles it, act here: at once to cancel a key capture, a frame
        // later to close, so the game's handler cannot close the menu underneath as well.
        var frame = Time.frameCount;
        if (escapePressed && _escapeHandledFrame != frame)
        {
            if (IsCapturing)
            {
                _escapeHandledFrame = frame;
                CancelCapture();
            }
            else
            {
                _escapeFrame = frame;
            }
        }
        else if (_escapeFrame == frame - 1 && _escapeHandledFrame != _escapeFrame)
        {
            Close();
        }
    }

    /// <summary>Esc or Back reached the game's window code. True when the panel used it.</summary>
    public bool HandleEscapeFromGame()
    {
        if (!IsOpen) return false;
        var frame = Time.frameCount;
        if (_escapeHandledFrame == frame) return true;
        _escapeHandledFrame = frame;
        if (IsCapturing) CancelCapture();
        else Close();
        return true;
    }

    /// <summary>The player pressed a key while a key setting was waiting for one.</summary>
    public void FinishCapture(string key, IReadOnlyList<string> heldModifiers)
    {
        if (_capture is not { } capture) return;
        _capture = null;

        var check = KeyNames.Capture(capture.View.Setting, key, heldModifiers);
        if (!check.Ok)
        {
            capture.Label.text = KeyText(capture.View);
            ShowStatus($"{capture.View.Label} not saved: {check.Error}", error: true);
            return;
        }
        Apply(capture.Mod, capture.View, check.Raw, rebuild: true);
    }

    private void StartCapture(ModEntry mod, SettingView view, TextMeshProUGUI label)
    {
        CancelCapture();
        _capture = (mod, view, label);
        label.text = "Press a key...";
        var combo = view.Setting.TypeName == "KeyboardShortcut" ? " Hold Ctrl, Shift or Alt with it for a combination." : "";
        ShowStatus($"Press the key to use for {view.Label}.{combo} Esc cancels.", error: false);
    }

    private void CancelCapture()
    {
        if (_capture is not { } capture) return;
        _capture = null;
        capture.Label.text = KeyText(capture.View);
        ShowStatus(Note, error: false);
    }

    private static string KeyText(SettingView view) =>
        KeyNames.Parse(view.Setting.TypeName, view.Setting.RawValue) == null ? "(none)" : ValueRules.Display(view.Setting);

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
        _clashes = KeyClashes.Find(KeyClashes.FromMods(mods).Concat(_gameBindings));
        var selected = mods.FirstOrDefault(m => m.Source.RelativePath == _selectedPath) ?? mods.FirstOrDefault();
        _selectedPath = selected?.Source.RelativePath;

        Ui.Clear(_modList);
        foreach (var mod in mods)
        {
            var version = mod.Version.Length > 0 ? "v" + mod.Version + " · " : "";
            var isSelected = mod == selected;
            var clash = _clashes.Keys.Any(id => id.StartsWith(mod.Source.RelativePath + "\n", StringComparison.Ordinal)) ? " · key clash" : "";
            var button = Ui.NewButton(_modList, $"{mod.Title}\n<size=70%>{version}{mod.Status}{clash}</size>", 28f,
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

    /// <summary>White text on a red strip; tinted text is hard to read on this background.</summary>
    private void AddWarning(string text)
    {
        var holder = Ui.Rect("Warning", _settings);
        Ui.NewImage(holder, Ui.ErrorColor);
        var layout = holder.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.padding = new RectOffset { left = 10, right = 10, top = 4, bottom = 4 };
        Ui.NewText(holder, text, 22f);
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

        if (_clashes.TryGetValue(KeyClashes.IdOf(mod, view), out var others)) AddWarning("Also bound to: " + string.Join(", ", others));

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
            case ControlKind.Key:
            {
                TextMeshProUGUI keyLabel = null;
                var key = Ui.NewButton(area, KeyText(view), 28f, Ui.ButtonColor, () => StartCapture(mod, view, keyLabel));
                keyLabel = key.GetComponentInChildren<TextMeshProUGUI>();
                Ui.Place(key.GetComponent<RectTransform>(), 0f, 0f, 0.7f, 1f);
                var clear = Ui.NewButton(area, "Clear", 26f, Ui.OffColor, () => Apply(mod, view, KeyNames.Unbound, rebuild: true));
                Ui.Place(clear.GetComponent<RectTransform>(), 0.72f, 0f, 1f, 1f);
                break;
            }
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
