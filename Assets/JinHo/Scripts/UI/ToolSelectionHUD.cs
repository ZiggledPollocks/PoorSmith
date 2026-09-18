using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-60)]
[DisallowMultipleComponent]
public sealed class ToolSelectionHUD : MonoBehaviour
{
    private const int ToolCount = 3;

    [Header("Sprites")]
    [SerializeField] private Sprite backgroundSprite;
    [SerializeField] private Sprite swordSprite;
    [SerializeField] private Sprite axeSprite;
    [SerializeField] private Sprite pickaxeSprite;

    [Header("Layout")]
    [SerializeField] private Vector2 panelSize = new(430f, 430f);
    [SerializeField, Range(0.5f, 1f)] private float uiScale = 0.75f;
    [SerializeField] private Vector2 screenMargin = Vector2.zero;
    [SerializeField, Min(0f)] private float edgeOverscan = 8f;
    [SerializeField] private Vector2 selectedPosition = new(-155f, 150f);
    [SerializeField] private Vector2 upperPosition = new(-100f, 284f);
    [SerializeField] private Vector2 lowerPosition = new(-315f, 78f);
    [SerializeField, Min(1f)] private float selectedSize = 178f;
    [SerializeField, Min(1f)] private float unselectedSize = 104f;
    [SerializeField, Min(0.1f)] private float transitionSpeed = 12f;
    [SerializeField] private int canvasSortingOrder = 110;

    private readonly RectTransform[] toolRects = new RectTransform[ToolCount];
    private readonly Vector2[] targetPositions = new Vector2[ToolCount];
    private readonly float[] targetSizes = new float[ToolCount];
    private PlayerToolController toolController;
    private Canvas hudCanvas;
    private int selectedIndex;
    private bool requestedVisible = true;

    private void Start()
    {
        BuildUI();
        TryBindToolController();
        ApplySelection(GetSelectedIndex());
    }

    private void Update()
    {
        if (toolController == null)
            TryBindToolController();

        AnimateLayout();
    }

    public void SetVisible(bool visible)
    {
        requestedVisible = visible;
        if (hudCanvas != null)
            hudCanvas.enabled = requestedVisible;
    }

    private void TryBindToolController()
    {
        PlayerToolController found = FindFirstObjectByType<PlayerToolController>();
        if (found == null || found == toolController)
            return;

        if (toolController != null)
            toolController.CurrentToolChanged -= HandleCurrentToolChanged;

        toolController = found;
        toolController.CurrentToolChanged += HandleCurrentToolChanged;
        ApplySelection(GetSelectedIndex());
    }

    private void HandleCurrentToolChanged(ToolData tool, int slotIndex)
    {
        ApplySelection(ToIndex(tool != null ? tool.ToolType : ToolType.Sword));
    }

    private int GetSelectedIndex()
    {
        return toolController != null && toolController.CurrentTool != null
            ? ToIndex(toolController.CurrentTool.ToolType)
            : 0;
    }

    private static int ToIndex(ToolType type)
    {
        return type switch
        {
            ToolType.Axe => 1,
            ToolType.Pickaxe => 2,
            _ => 0
        };
    }

    private void BuildUI()
    {
        if (hudCanvas != null)
            return;

        GameObject canvasObject = new("ToolSelectionCanvas", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        hudCanvas = canvasObject.GetComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        hudCanvas.sortingOrder = canvasSortingOrder;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform panel = CreateRect("ToolSelectionPanel", canvasObject.transform);
        panel.anchorMin = new Vector2(1f, 0f);
        panel.anchorMax = new Vector2(1f, 0f);
        panel.pivot = new Vector2(1f, 0f);
        panel.anchoredPosition = new Vector2(
            edgeOverscan - screenMargin.x,
            screenMargin.y - edgeOverscan);
        panel.sizeDelta = panelSize;
        panel.localScale = Vector3.one * uiScale;

        Image background = panel.gameObject.AddComponent<Image>();
        background.sprite = backgroundSprite;
        background.preserveAspect = true;
        background.raycastTarget = false;

        Mask backgroundMask = panel.gameObject.AddComponent<Mask>();
        backgroundMask.showMaskGraphic = true;

        Sprite[] sprites = { swordSprite, axeSprite, pickaxeSprite };
        string[] labels = { "1", "2", "3" };

        for (int i = 0; i < ToolCount; i++)
            toolRects[i] = CreateToolView(panel, sprites[i], labels[i]);

        hudCanvas.enabled = requestedVisible;
    }

    private static RectTransform CreateToolView(RectTransform parent, Sprite sprite,
        string number)
    {
        RectTransform root = CreateRect($"Tool_{number}", parent);
        root.anchorMin = new Vector2(1f, 0f);
        root.anchorMax = new Vector2(1f, 0f);
        root.pivot = new Vector2(0.5f, 0.5f);

        Image icon = root.gameObject.AddComponent<Image>();
        icon.sprite = sprite;
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        RectTransform badge = CreateRect("NumberBadge", root);
        badge.anchorMin = new Vector2(1f, 0f);
        badge.anchorMax = new Vector2(1f, 0f);
        badge.pivot = new Vector2(0.5f, 0.5f);
        badge.anchoredPosition = new Vector2(-4f, 4f);
        badge.sizeDelta = new Vector2(38f, 44f);

        Image badgeImage = badge.gameObject.AddComponent<Image>();
        badgeImage.color = new Color(0.23f, 0.23f, 0.25f, 0.96f);
        badgeImage.raycastTarget = false;

        RectTransform textRect = CreateRect("Number", badge);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = textRect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = number;
        label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = 24f;
        label.fontStyle = FontStyles.Bold;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;

        return root;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject child = new(name, typeof(RectTransform));
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private void ApplySelection(int index)
    {
        selectedIndex = Mathf.Clamp(index, 0, ToolCount - 1);
        int nextIndex = (selectedIndex + 1) % ToolCount;
        int previousIndex = (selectedIndex + ToolCount - 1) % ToolCount;

        for (int i = 0; i < ToolCount; i++)
        {
            if (i == selectedIndex)
            {
                targetPositions[i] = selectedPosition;
                targetSizes[i] = selectedSize;
            }
            else if (i == nextIndex)
            {
                targetPositions[i] = upperPosition;
                targetSizes[i] = unselectedSize;
            }
            else
            {
                targetPositions[i] = lowerPosition;
                targetSizes[i] = unselectedSize;
            }
        }

        if (toolRects[selectedIndex] != null)
            toolRects[selectedIndex].SetAsLastSibling();
    }

    private void AnimateLayout()
    {
        float blend = 1f - Mathf.Exp(-transitionSpeed * Time.unscaledDeltaTime);
        for (int i = 0; i < ToolCount; i++)
        {
            RectTransform rect = toolRects[i];
            if (rect == null) continue;
            rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, targetPositions[i], blend);
            rect.sizeDelta = Vector2.Lerp(rect.sizeDelta, Vector2.one * targetSizes[i], blend);
        }
    }

    private void OnDestroy()
    {
        if (toolController != null)
            toolController.CurrentToolChanged -= HandleCurrentToolChanged;
    }
}
