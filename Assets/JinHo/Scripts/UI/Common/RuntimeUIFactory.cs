// [코드 지도] RuntimeUIFactory: UI GameObject, RectTransform, Image, TMP_Text 생성과 앵커 배치를 공통화한다. 화면의 게임 규칙이나 메뉴 전환은 각 UI 컨트롤러의 책임이다.
// 주요 함수: CreateText, SetAnchoredRect, SetCenteredRect
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/UI/Common/RuntimeUIFactory.cs.md

using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Builds common UI elements created at runtime.</summary>
public static class RuntimeUIFactory
{
    // Ordinary story/HUD content ends at5000; release decisions31000 and travel fades32000+ stay above modals.
    public static void RaiseModal(RectTransform root)
    {
        if (root == null) return;
        Canvas owner = null;
        foreach (var parent in root.parent.GetComponentsInParent<Canvas>(true))
            if (parent.isRootCanvas || parent.overrideSorting) { owner = parent; break; }
        if (owner != null)
        {
            var layer = root.GetComponent<Canvas>();
            if(layer == null) layer = root.gameObject.AddComponent<Canvas>();
            layer.overrideSorting = true;
            layer.sortingLayerID = owner.sortingLayerID;
            layer.sortingOrder = Mathf.Max(5001, owner.sortingOrder + 1);
            layer.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1 |
                AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
            if (root.GetComponent<GraphicRaycaster>() == null) root.gameObject.AddComponent<GraphicRaycaster>();
        }
        root.SetAsLastSibling();
        // A newly activated nested prefab must enter the Canvas rebuild queue, including unchanged TMP text.
        foreach (var graphic in root.GetComponentsInChildren<Graphic>(true)) graphic.SetAllDirty();
    }

    // Fixed readable typography; FullTextLayout expands only content that needs more room.
    public static void FitText(TMP_Text text, float authoredSize)
    {
        if (text == null) return;
        FullTextLayout.Bind(text);
        if (UIElementTemplate.Owns(text, "typography")) { ReleasePreferences.Fit(text, authoredSize); return; }
        float maximum = Mathf.Max(1f, authoredSize >= 20f ? authoredSize * .94f : authoredSize);
        text.fontSize = maximum;
        text.fontSizeMax = maximum;
        text.fontSizeMin = Mathf.Min(maximum, Mathf.Max(10f, maximum * .72f));
        ReleasePreferences.Fit(text,maximum);
        text.enableAutoSizing = false;
        text.autoSizeTextContainer = false;
        // Keep glyphs off the text rect edges without moving the surrounding UI.
        Vector4 margin = text.margin;
        text.margin = new Vector4(
            Mathf.Max(margin.x, 3f), Mathf.Max(margin.y, 2f),
            Mathf.Max(margin.z, 3f), Mathf.Max(margin.w, 2f));
    }

    public static void FitExistingText(Transform root)
    {
        if (root == null) return;
        GameArtSkin.Apply(root);
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            FitText(text, ReleasePreferences.AuthoredSize(text));
    }

    public static GameObject CreateUIObject(string objectName, Transform parent)
    {
        GameObject uiObject = new(objectName, typeof(RectTransform));
        uiObject.transform.SetParent(parent, false);
        return uiObject;
    }

    public static RectTransform CreateRectTransform(string objectName, Transform parent)
    {
        return CreateUIObject(objectName, parent).GetComponent<RectTransform>();
    }

    public static Image CreateImage(string objectName, Transform parent)
    {
        GameObject imageObject = CreateUIObject(objectName, parent);
        Image image = imageObject.AddComponent<Image>();
        image.type = Image.Type.Simple;
        GameArtSkin.Panel(image);
        return image;
    }

    public static void StretchToParent(RectTransform rectTransform, float inset = 0f)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.offsetMin = new Vector2(inset, inset);
        rectTransform.offsetMax = new Vector2(-inset, -inset);
    }

    public static void SetCenteredRect(
        RectTransform rectTransform,
        Vector2 size,
        Vector2 position)
    {
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = position;
    }

    public static void SetAnchoredRect(
        RectTransform rectTransform,
        Vector2 anchor,
        Vector2 size,
        Vector2 position)
    {
        rectTransform.anchorMin = anchor;
        rectTransform.anchorMax = anchor;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = position;
    }

    public static TMP_Text CreateText(string objectName, Transform parent, float fontSize,
        TextAlignmentOptions alignment, Color color, TMP_FontAsset font)
    {
        GameObject textObject = CreateUIObject(objectName, parent);
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.font = GameTextFonts.Primary ?? font;
        FitText(text, fontSize);
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        GameArtSkin.Button(parent != null ? parent.GetComponent<Button>() : null);
        return text;
    }
}
