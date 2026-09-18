using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
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
        label.fontSize = 22f;
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
