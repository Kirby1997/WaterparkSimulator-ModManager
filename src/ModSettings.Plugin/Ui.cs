using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ModSettings.Plugin;

/// <summary>Small builders for uGUI objects made from code.</summary>
internal static class Ui
{
    public static readonly Color PanelColor = new(0.04f, 0.17f, 0.32f, 1f);
    public static readonly Color ButtonColor = new(0.02f, 0.55f, 0.72f, 1f);
    public static readonly Color SelectedColor = new(0.96f, 0.78f, 0.08f, 1f);
    public static readonly Color FieldColor = new(0.01f, 0.09f, 0.18f, 1f);
    public static readonly Color OffColor = new(0.2f, 0.27f, 0.35f, 1f);
    public static readonly Color ErrorColor = new(0.62f, 0.12f, 0.12f, 1f);

    private const int UiLayer = 5;

    private static TMP_FontAsset _font;
    private static Material _fontMaterial;

    /// <summary>Takes the font from one of the game's labels so the panel matches its menus.</summary>
    public static void UseFont(TMP_Text template)
    {
        if (template?.font == null) return;
        _font = template.font;
        _fontMaterial = template.fontSharedMaterial;
    }

    public static RectTransform Rect(string name, Transform parent)
    {
        var gameObject = new GameObject(name) { layer = UiLayer };
        var rect = gameObject.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    /// <summary>Anchors a rect to a fraction of its parent, inset by the given pixels.</summary>
    public static void Place(RectTransform rect, float xMin, float yMin, float xMax, float yMax,
        float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
    {
        rect.anchorMin = new Vector2(xMin, yMin);
        rect.anchorMax = new Vector2(xMax, yMax);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    public static void Stretch(RectTransform rect, float inset = 0f) => Place(rect, 0f, 0f, 1f, 1f, inset, inset, inset, inset);

    public static Image NewImage(Component owner, Color color)
    {
        var image = owner.gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    public static TextMeshProUGUI NewText(Transform parent, string text, float size,
        TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft, bool rich = false)
    {
        var label = Rect("Text", parent).gameObject.AddComponent<TextMeshProUGUI>();
        if (_font == null) _font = FallbackFont();
        if (_font != null) label.font = _font;
        if (_fontMaterial != null) label.fontSharedMaterial = _fontMaterial;
        label.color = Color.white;
        label.fontSize = size;
        label.alignment = alignment;
        label.richText = rich;
        label.enableWordWrapping = true;
        label.raycastTarget = false;
        label.text = text;
        return label;
    }

    public static Button NewButton(Transform parent, string label, float size, Color color, Action onClick)
    {
        var rect = Rect("Button", parent);
        var image = NewImage(rect, color);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        OnClick(button, onClick);
        var text = NewText(rect, label, size, TextAlignmentOptions.Center, rich: true);
        Stretch(text.rectTransform, 6f);
        return button;
    }

    public static void OnClick(Button button, Action onClick) =>
        button.onClick.AddListener(DelegateSupport.ConvertDelegate<UnityAction>(onClick));

    public static Slider NewSlider(Transform parent, float min, float max, bool whole, float value, Action<float> changed)
    {
        var rect = Rect("Slider", parent);

        var background = Rect("Background", rect);
        Place(background, 0f, 0.35f, 1f, 0.65f);
        NewImage(background, FieldColor);

        var fillArea = Rect("Fill Area", rect);
        Place(fillArea, 0f, 0.35f, 1f, 0.65f, left: 12f, right: 12f);
        var fill = Rect("Fill", fillArea);
        fill.sizeDelta = Vector2.zero;
        NewImage(fill, ButtonColor);

        var handleArea = Rect("Handle Slide Area", rect);
        Place(handleArea, 0f, 0f, 1f, 1f, left: 12f, right: 12f);
        var handle = Rect("Handle", handleArea);
        handle.sizeDelta = new Vector2(24f, 0f);
        var handleImage = NewImage(handle, SelectedColor);

        var slider = rect.gameObject.AddComponent<Slider>();
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = whole;
        slider.SetValueWithoutNotify(value);
        slider.onValueChanged.AddListener(DelegateSupport.ConvertDelegate<UnityAction<float>>(changed));
        return slider;
    }

    public static TMP_InputField NewInput(Transform parent, string value, float size, Action<string> endEdit)
    {
        var rect = Rect("Input", parent);
        // Configured while inactive: the field complains in OnEnable if its parts are missing.
        rect.gameObject.SetActive(false);
        var background = NewImage(rect, FieldColor);

        var area = Rect("Text Area", rect);
        Place(area, 0f, 0f, 1f, 1f, 10f, 4f, 10f, 4f);
        area.gameObject.AddComponent<RectMask2D>();
        var text = NewText(area, "", size);
        Stretch(text.rectTransform);
        text.enableWordWrapping = false;

        var input = rect.gameObject.AddComponent<TMP_InputField>();
        input.textViewport = area;
        input.textComponent = text;
        input.targetGraphic = background;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.richText = false;
        input.customCaretColor = true;
        input.caretColor = Color.white;
        input.SetTextWithoutNotify(value);
        input.onEndEdit.AddListener(DelegateSupport.ConvertDelegate<UnityAction<string>>(endEdit));
        rect.gameObject.SetActive(true);
        return input;
    }

    /// <summary>A vertical scroll area; children added to <paramref name="content"/> stack top to bottom.</summary>
    public static ScrollRect NewScroll(Transform parent, float spacing, out RectTransform content)
    {
        var rect = Rect("Scroll", parent);
        var viewport = Rect("Viewport", rect);
        Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        // Transparent, but catches the mouse wheel between rows.
        NewImage(viewport, new Color(0f, 0f, 0f, 0f));

        content = Rect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.padding = new RectOffset { left = 4, right = 16, top = 4, bottom = 4 };
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = rect.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;
        return scroll;
    }

    public static void Height(Component row, float height)
    {
        var element = row.gameObject.AddComponent<LayoutElement>();
        element.minHeight = height;
        element.preferredHeight = height;
    }

    /// <summary>Removes every child now, so layout groups stop counting them this frame.</summary>
    public static void Clear(Transform parent)
    {
        for (var i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i).gameObject;
            child.SetActive(false);
            UnityEngine.Object.Destroy(child);
        }
    }

    private static TMP_FontAsset FallbackFont()
    {
        var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        return fonts.Length > 0 ? fonts[0] : null;
    }
}
