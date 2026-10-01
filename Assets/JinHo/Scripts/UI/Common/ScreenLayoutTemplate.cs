using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Copies authored RectTransform placement from a screen prefab onto a dynamically populated UI.
/// Prefab children are matched by hierarchy name, so game data and button callbacks stay in their owners.
/// </summary>
public static class ScreenLayoutTemplate
{
    private static readonly Dictionary<string, GameObject> Cache = new();

    public static void Apply(string screenName, Transform actualRoot, bool applyRoot = false)
    {
        if (actualRoot == null || string.IsNullOrEmpty(screenName)) return;
        if (!Cache.TryGetValue(screenName, out GameObject template))
        {
            template = Resources.Load<GameObject>("UI/Screens/Layouts/" + screenName);
            Cache[screenName] = template;
        }
        if (template == null) return;
        if (applyRoot && template.transform is RectTransform rootSource && actualRoot is RectTransform rootDestination)
            CopyPlacement(rootSource, rootDestination);
        ApplyChildren(template.transform, actualRoot);
    }

    private static void ApplyChildren(Transform templateParent, Transform actualParent)
    {
        foreach (Transform guide in templateParent)
        {
            Transform actual = actualParent.Find(guide.name);
            if (actual == null) continue;
            if (guide is RectTransform source && actual is RectTransform destination)
                CopyPlacement(source, destination);
            ApplyChildren(guide, actual);
        }
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
