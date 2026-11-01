using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Applies explicitly authored layout and visual properties to a dynamically populated UI.
/// Prefab children are matched by hierarchy name, so game data and button callbacks stay in their owners.
/// </summary>
public static class ScreenLayoutTemplate
{
    private static readonly Dictionary<string, GameObject> Cache = new();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetCache() => Cache.Clear();

    public static void Apply(string screenName, Transform actualRoot, bool applyRoot = false)
    {
        if (actualRoot == null || string.IsNullOrEmpty(screenName)) return;
        if (!Cache.TryGetValue(screenName, out GameObject template))
        {
            template = Resources.Load<GameObject>("UI/Screens/Layouts/" + screenName);
            Cache[screenName] = template;
        }
        if (template == null) return;
        if (applyRoot) ApplyElement(template.transform, actualRoot);
        ApplyChildren(template.transform, actualRoot);
        if(TownFieldUiStyle.IsGameplayScreen(screenName))TownFieldUiStyle.PresentGameplay(actualRoot);
    }

    private static void ApplyChildren(Transform templateParent, Transform actualParent)
    {
        foreach (Transform guide in templateParent)
        {
            var rule = guide.GetComponent<UIElementTemplate>();
            if (rule != null && rule.listItemStyle)
            {
                foreach (Transform item in actualParent)
                {
                    if (!item.name.StartsWith(rule.itemNamePrefix, System.StringComparison.Ordinal)) continue;
                    rule.ApplyTo(item);
                    ApplyChildren(guide, item);
                }
                continue;
            }
            Transform actual = actualParent.Find(guide.name);
            if (actual == null) continue;
            ApplyElement(guide, actual);
            ApplyChildren(guide, actual);
        }
    }

    static void ApplyElement(Transform guide, Transform actual)
    {
        var rule = guide.GetComponent<UIElementTemplate>();
        if ((rule == null || rule.placement) && guide is RectTransform source && actual is RectTransform destination)
            CopyPlacement(source, destination);
        if (rule != null) rule.ApplyTo(actual);
    }

    private static void CopyPlacement(RectTransform source, RectTransform destination)
    {
        destination.anchorMin = source.anchorMin;
        destination.anchorMax = source.anchorMax;
        destination.pivot = source.pivot;
        destination.sizeDelta = source.sizeDelta;
        destination.anchoredPosition = source.anchoredPosition;
    }
}
