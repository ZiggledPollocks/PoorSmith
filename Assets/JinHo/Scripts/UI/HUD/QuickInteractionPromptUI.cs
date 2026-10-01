// [코드 지도] QuickInteractionPromptUI: 상호작용 대상 위에 월드 공간 F 안내를 표시한다. PlayerInteraction이 런타임 생성·표시·숨김·파괴를 관리한다. 입력을 받는 버튼이 아니라 안내용 UI다.
// 주요 함수: Build, TryGetTargetBounds, Show
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/UI/HUD/QuickInteractionPromptUI.cs.md

using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
/// <summary>Shows the nearby quick interaction prompt.</summary>
public sealed class QuickInteractionPromptUI : MonoBehaviour
{
    private const float WorldScale = 0.012f;
    private const float HeightOffset = 0.45f;

    private Component target;
    private RectTransform promptRect;

    public bool IsVisible => gameObject.activeSelf;
    public Component Target => target;

    public static QuickInteractionPromptUI Create(Camera worldCamera)
    {
        GameObject root = new(
            "QuickInteractionPrompt",
            typeof(RectTransform),
            typeof(Canvas));

        QuickInteractionPromptUI prompt = root.AddComponent<QuickInteractionPromptUI>();
        prompt.Build(worldCamera);
        root.SetActive(false);
        return prompt;
    }

    public void Show(Component newTarget)
    {
        target = newTarget;
        if (target == null)
        {
            Hide();
            return;
        }

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        UpdatePosition();
    }

    public void Hide()
    {
        target = null;
        if (gameObject.activeSelf)
            gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            Hide();
            return;
        }

        UpdatePosition();
    }

    // 핵심 분기: TMP_Settings.defaultFontAsset != null 판정.
    // 상태 변경: promptRect 갱신.
    // 다음 연결: QuickInteractionPromptUI.StretchToParent(UnityEngine.RectTransform, UnityEngine.Vector2) 호출.
    private void Build(Camera worldCamera)
    {
        promptRect = GetComponent<RectTransform>();
        promptRect.sizeDelta = new Vector2(48f, 32f);
        promptRect.localScale = Vector3.one * WorldScale;

        Canvas canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = worldCamera;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 500;

        GameObject backgroundObject = new("Background", typeof(RectTransform), typeof(Image));
        backgroundObject.transform.SetParent(transform, false);
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        StretchToParent(backgroundRect, Vector2.zero);

        Image background = backgroundObject.GetComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.9f);
        background.raycastTarget = false;

        GameObject labelObject = new("KeyLabel", typeof(RectTransform));
        labelObject.transform.SetParent(backgroundObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        StretchToParent(labelRect, new Vector2(3f, 2f));

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = "F";
        label.color = Color.white;
        RuntimeUIFactory.FitText(label, 22f);
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;

        if (TMP_Settings.defaultFontAsset != null)
            label.font = TMP_Settings.defaultFontAsset;
    }

    private void UpdatePosition()
    {
        if (!TryGetTargetBounds(target, out Bounds bounds))
        {
            Hide();
            return;
        }

        transform.position = new Vector3(
            bounds.center.x,
            bounds.max.y + HeightOffset,
            bounds.center.z);
        transform.rotation = Quaternion.identity;
    }

    // 핵심 분기: renderers.Length > 0 판정.
    // 상태 변경: bounds 갱신.
    private static bool TryGetTargetBounds(Component targetComponent, out Bounds bounds)
    {
        Renderer[] renderers = targetComponent.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return true;
        }

        Collider2D[] colliders = targetComponent.GetComponentsInChildren<Collider2D>(true);
        if (colliders.Length > 0)
        {
            bounds = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++)
                bounds.Encapsulate(colliders[i].bounds);
            return true;
        }

        bounds = new Bounds(targetComponent.transform.position, Vector3.zero);
        return true;
    }

    private static void StretchToParent(RectTransform rectTransform, Vector2 inset)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = inset;
        rectTransform.offsetMax = -inset;
    }
}
