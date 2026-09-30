using BepInEx.Logging;
using I2.Loc;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ModManager.Plugin;

/// <summary>Adds a "Mods" button to a game menu, copied from one of the menu's own buttons.</summary>
internal static class MenuButtons
{
    private const string CloneName = "ModManager Mods Button";

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
    }

    private static string PathOf(Transform transform)
    {
        var names = new List<string>();
        for (var t = transform; t != null; t = t.parent) names.Add(t.name);
        names.Reverse();
        return string.Join("/", names);
    }
}
