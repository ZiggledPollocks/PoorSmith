using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Explicit presentation ownership. Copies no content, events, input or game state.</summary>
[DisallowMultipleComponent]
public sealed class UIElementTemplate : MonoBehaviour
{
    public enum Appearance { WoodDefaults, PrefabOverrides, PreserveRuntime }
    public Appearance appearance = Appearance.WoodDefaults;
    public bool placement = true;
    public bool image = true;
    [Tooltip("Disable for runtime-selected/tinted slots; sprite editing remains available.")]
    public bool imageColor = true;
    public bool selectable = true;
    public bool typography = true;
    public bool textColor = true;
    public bool layout = true;
    [Tooltip("Apply this guide to each immediate list child matching the prefix. Never copy list-item placement.")]
    public bool listItemStyle;
    public string itemNamePrefix = "";
    [System.NonSerialized] internal UIElementTemplate appliedFrom;
    bool capturedText; float baseSize, baseMin, baseMax; bool baseAuto;

    public static bool Owns(Component target, string property)
    {
        if (target == null) return false;
        var rule = target.GetComponent<UIElementTemplate>();
        if (rule == null || rule.appearance == Appearance.WoodDefaults) return false;
        return property switch
        {
            "image" => rule.image,
            "selectable" => rule.selectable,
            "textColor" => rule.textColor,
            "typography" => rule.typography,
            _ => false
        };
    }

    // Preserve authored typography while retaining the existing large-text accessibility preference.
    public static bool FitAuthored(TMP_Text target)
    {
        if (!Owns(target, "typography")) return false;
        var rule = target.GetComponent<UIElementTemplate>();
        var source = rule.appliedFrom != null ? rule.appliedFrom.GetComponent<TMP_Text>() : target;
        if (source == null) return false;
        if (!rule.capturedText || source != target)
        {
            rule.baseSize=source.fontSize; rule.baseMin=source.fontSizeMin;
            rule.baseMax=source.fontSizeMax; rule.baseAuto=source.enableAutoSizing; rule.capturedText=true;
        }
        float scale=ReleasePreferences.LargeText?1.2f:1f;
        target.fontSizeMax=rule.baseMax*scale; target.fontSizeMin=rule.baseMin;
        target.enableAutoSizing=rule.baseAuto;
        if (!rule.baseAuto || source != target) target.fontSize=rule.baseSize*scale;
        return true;
    }

    // Reassert only opted-in presentation after owner refreshes; live content and input remain untouched.
    internal void ApplyTo(Transform destination)
    {
        var marker = destination.GetComponent<UIElementTemplate>();
        bool first = marker == null || marker.appliedFrom != this;
        if (marker == null) marker = destination.gameObject.AddComponent<UIElementTemplate>();
        marker.appearance = appearance; marker.image = image; marker.imageColor = imageColor; marker.selectable = selectable;
        marker.typography = typography; marker.textColor = textColor;
        marker.layout = layout; marker.placement = placement; marker.appliedFrom = this;
        if (appearance == Appearance.PrefabOverrides)
        {
            if (image && TryGetComponent<Image>(out var fromImage) && destination.TryGetComponent<Image>(out var toImage))
            {
                toImage.sprite = fromImage.sprite; toImage.type = fromImage.type;
                if (imageColor) toImage.color = fromImage.color; toImage.preserveAspect = fromImage.preserveAspect;
                toImage.pixelsPerUnitMultiplier = fromImage.pixelsPerUnitMultiplier;
                // fillAmount, masks, material and raycastTarget belong to the live screen.
            }
            if (selectable && TryGetComponent<Selectable>(out var fromSelect) && destination.TryGetComponent<Selectable>(out var toSelect))
            {
                toSelect.transition = fromSelect.transition; toSelect.colors = fromSelect.colors;
                toSelect.spriteState = fromSelect.spriteState;
            }
            if (TryGetComponent<TMP_Text>(out var fromText) && destination.TryGetComponent<TMP_Text>(out var toText))
            {
                if (typography)
                {
                    if (first || !fromText.enableAutoSizing) toText.fontSize = fromText.fontSize;
                    toText.fontSizeMin = fromText.fontSizeMin;
                    toText.fontSizeMax = fromText.fontSizeMax * (ReleasePreferences.LargeText ? 1.2f : 1f); toText.enableAutoSizing = fromText.enableAutoSizing;
                    toText.margin = fromText.margin; toText.alignment = fromText.alignment;
                    toText.textWrappingMode = fromText.textWrappingMode; toText.overflowMode = fromText.overflowMode;
                }
                if (textColor) toText.color = fromText.color;
            }
        }
        if (layout && first) CopyLayout(destination);
    }

    void CopyLayout(Transform destination)
    {
        var from = GetComponent<LayoutGroup>(); var to = destination.GetComponent<LayoutGroup>();
        if (from != null && to != null && from.GetType() == to.GetType())
        {
            to.padding = new RectOffset(from.padding.left, from.padding.right, from.padding.top, from.padding.bottom);
            to.childAlignment = from.childAlignment;
            if (from is GridLayoutGroup grid && to is GridLayoutGroup target)
            {
                target.cellSize = grid.cellSize; target.spacing = grid.spacing;
                target.constraint = grid.constraint; target.constraintCount = grid.constraintCount;
                target.startAxis = grid.startAxis; target.startCorner = grid.startCorner;
            }
            if (from is HorizontalOrVerticalLayoutGroup list && to is HorizontalOrVerticalLayoutGroup targetList)
            {
                targetList.spacing = list.spacing; targetList.childControlWidth = list.childControlWidth;
                targetList.childControlHeight = list.childControlHeight;
                targetList.childForceExpandWidth = list.childForceExpandWidth;
                targetList.childForceExpandHeight = list.childForceExpandHeight;
            }
            LayoutRebuilder.MarkLayoutForRebuild((RectTransform)destination);
        }
        if (TryGetComponent<LayoutElement>(out var element) && destination.TryGetComponent<LayoutElement>(out var targetElement))
        {
            targetElement.minWidth = element.minWidth; targetElement.minHeight = element.minHeight;
            targetElement.preferredWidth = element.preferredWidth; targetElement.preferredHeight = element.preferredHeight;
            targetElement.flexibleWidth = element.flexibleWidth; targetElement.flexibleHeight = element.flexibleHeight;
            targetElement.layoutPriority = element.layoutPriority;
        }
    }
}
