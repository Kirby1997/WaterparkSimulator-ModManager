using BepInEx.Logging;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using I2.Loc;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ModSettings.Plugin;

/// <summary>Adds a "Mods" button to a game menu, copied from one of the menu's own buttons.</summary>
internal static class MenuButtons
{
    private const string CloneName = "ModSettings Mods Button";

    public static void Add(Component menu, string where, ManualLogSource log, string hotkeyName, Action onClick)
    {
        if (menu == null) return;

        var buttons = menu.GetComponentsInChildren<Button>(true).ToArray();
        if (buttons.Any(b => b.name == CloneName)) return;

        // The menu's button column: the parent holding the most buttons.
        var column = buttons
            .Where(b => b.transform.parent != null && b.gameObject.activeSelf)
            .GroupBy(b => b.transform.parent.Pointer)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault()
            ?.OrderBy(b => b.transform.GetSiblingIndex())
            .ToList();
        if (column == null || column.Count == 0)
        {
            log.LogWarning($"No buttons found on the {where}; press {hotkeyName} to open the mod settings.");
            return;
        }

        // Goes just above the last button, which is usually Quit or Back.
        var last = column[^1];
        var template = column.Count >= 2 ? column[^2] : last;
        var parent = last.transform.parent;
        var heightBefore = ColumnHeight(parent);
        var clone = UnityEngine.Object.Instantiate(template.gameObject, parent);
        clone.name = CloneName;
        clone.transform.SetSiblingIndex(last.transform.GetSiblingIndex());

        // Left enabled, the localisation would put the copied button's own text back.
        foreach (var localize in clone.GetComponentsInChildren<Localize>(true)) localize.enabled = false;
        var label = clone.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.text = "Mods";
            Ui.UseFont(label);
        }

        var button = clone.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent();
        Ui.OnClick(button, onClick);

        var laidOut = parent.GetComponent<LayoutGroup>() != null;
        log.LogInfo($"Added a Mods button to the {where}, copied from '{template.name}' under '{PathOf(parent)}'" +
                    (laidOut ? "." : "; that column has no layout group, so the button may overlap another."));
        KeepHeight(parent, heightBefore, where, log);
    }

    /// <summary>
    /// The extra button makes a laid-out column taller, which pushes its last button under
    /// whatever the menu draws below it (the main menu's Discord button covered Exit). Tighten
    /// the gaps, and if that is not enough shrink the buttons, until the column is as tall as before.
    /// </summary>
    private static void KeepHeight(Transform parent, float heightBefore, string where, ManualLogSource log)
    {
        var layout = parent.GetComponent<VerticalLayoutGroup>();
        var rect = parent.TryCast<RectTransform>();
        if (layout == null || rect == null || heightBefore <= 0f) return;

        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        var growth = ColumnHeight(parent) - heightBefore;
        if (growth <= 0.5f) return;

        var children = ActiveChildren(parent);
        var gaps = Math.Max(1, children.Count - 1);
        var spacingBefore = layout.spacing;
        var spacing = Math.Max(0f, spacingBefore - growth / gaps);
        layout.spacing = spacing;

        var remaining = growth - (spacingBefore - spacing) * gaps;
        var shrink = remaining > 0.5f ? remaining / children.Count : 0f;
        if (shrink > 0f)
        {
            foreach (var child in children)
            {
                var height = child.rect.height - shrink;
                if (layout.childControlHeight)
                {
                    var element = child.GetComponent<LayoutElement>() ?? child.gameObject.AddComponent<LayoutElement>();
                    element.minHeight = height;
                    element.preferredHeight = height;
                }
                else
                {
                    child.sizeDelta = new Vector2(child.sizeDelta.x, child.sizeDelta.y - shrink);
                }
            }
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        log.LogInfo($"Kept the {where} column {heightBefore:0} tall: spacing {spacingBefore:0.#} -> {spacing:0.#}, " +
                    $"buttons {shrink:0.#} shorter (it had grown by {growth:0.#}).");
    }

    /// <summary>Distance from the top of the column's highest child to the bottom of its lowest, in the column's space.</summary>
    private static float ColumnHeight(Transform parent)
    {
        var rect = parent.TryCast<RectTransform>();
        if (rect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(rect);

        float top = float.MinValue, bottom = float.MaxValue;
        var corners = new Il2CppStructArray<Vector3>(4);
        foreach (var child in ActiveChildren(parent))
        {
            child.GetWorldCorners(corners);
            for (var i = 0; i < 4; i++)
            {
                var y = parent.InverseTransformPoint(corners[i]).y;
                top = Math.Max(top, y);
                bottom = Math.Min(bottom, y);
            }
        }
        return top > bottom ? top - bottom : 0f;
    }

    private static List<RectTransform> ActiveChildren(Transform parent)
    {
        var children = new List<RectTransform>();
        for (var i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i).TryCast<RectTransform>();
            if (child != null && child.gameObject.activeSelf) children.Add(child);
        }
        return children;
    }

    private static string PathOf(Transform transform)
    {
        var names = new List<string>();
        for (var t = transform; t != null; t = t.parent) names.Add(t.name);
        names.Reverse();
        return string.Join("/", names);
    }
}
