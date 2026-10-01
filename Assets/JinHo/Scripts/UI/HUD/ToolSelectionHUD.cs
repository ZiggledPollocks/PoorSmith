// [코드 지도] ToolSelectionHUD: 검·도끼·곡괭이 세 아이콘을 화면 우하단에 배치하고 선택한 도구를 크게 표시한다. Controller의 슬롯 전체를 시각화하는 인벤토리 UI가 아니라 세 도구 종류에 특화된 HUD다. 선택 이벤트에서 목표 위치를 바꾸고 Update에서 부드럽게 보간한다.
// 주요 함수: BuildUI, CreateToolView, RefreshIcons
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/UI/HUD/ToolSelectionHUD.cs.md

using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-60)]
[DisallowMultipleComponent]
/// <summary>Displays the tool currently selected by the player.</summary>
public sealed class ToolSelectionHUD : MonoBehaviour
{
    private const int ToolCount = 3;

    [Header("Sprites")]
    [SerializeField] private Sprite backgroundSprite;
    [SerializeField] private Sprite swordSprite;

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
    private readonly Image[] toolIcons = new Image[ToolCount];
    private readonly Vector2[] targetPositions = new Vector2[ToolCount];
    private readonly float[] targetSizes = new float[ToolCount];
    private PlayerToolController toolController;
    private ToolData lastSelectedWeapon;
    private Canvas hudCanvas;
    private int selectedIndex;
    private bool requestedVisible = true;
    private static bool CombatEnabled => FindFirstObjectByType<CampaignCombat>() != null;

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
        {
            toolController.CurrentToolChanged -= HandleCurrentToolChanged;
            toolController.ToolSlotsChanged -= RefreshIcons;
        }

        toolController = found;
        toolController.CurrentToolChanged += HandleCurrentToolChanged;
        toolController.ToolSlotsChanged += RefreshIcons;
        if (toolController.CurrentTool != null && toolController.CurrentTool.IsWeapon)
            lastSelectedWeapon = toolController.CurrentTool;
        RefreshIcons();
        ApplySelection(GetSelectedIndex());
    }

    private void HandleCurrentToolChanged(ToolData tool, int slotIndex)
    {
        if (tool != null && tool.IsWeapon)
            lastSelectedWeapon = tool;
        RefreshIcons();
        ApplySelection(ToIndex(tool != null ? tool.ToolType : ToolType.Sword));
    }

    // 핵심 분기: toolController == null 판정.
    // 상태 변경: weapon 갱신.
    // 다음 연결: ToolSelectionHUD.SetIcon(int, UnityEngine.Sprite) 호출.
    private void RefreshIcons()
    {
        if (toolController == null) return;
        ToolData weapon = null, axe = null, pickaxe = null;
        bool lastWeaponStillEquipped = false;
        foreach (ToolData tool in toolController.ToolSlots)
        {
            if (tool == null) continue;
            if (tool.IsWeapon)
            {
                weapon ??= tool;
                if (tool == lastSelectedWeapon) lastWeaponStillEquipped = true;
            }
            else if (tool.ToolType == ToolType.Axe) axe ??= tool;
            else if (tool.ToolType == ToolType.Pickaxe) pickaxe ??= tool;
        }
        if (lastWeaponStillEquipped) weapon = lastSelectedWeapon;
        else lastSelectedWeapon = null;
        SetIcon(0, weapon == null ? null : weapon.Icon != null ? weapon.Icon : swordSprite);
        if (CombatEnabled)
        {
            SetIcon(1, pickaxe?.Icon);
            SetIcon(2, axe?.Icon);
        }
        else
        {
            SetIcon(1, axe?.Icon);
            SetIcon(2, pickaxe?.Icon);
        }
    }

    private void SetIcon(int index, Sprite equipped)
    {
        Image image = toolIcons[index];
        if (image == null) return;
        // 슬롯에 표시할 장착 도구가 없으면 아이콘 자체를 숨긴다.
        image.sprite = equipped;
        image.enabled = equipped != null;
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
            ToolType.Axe => CombatEnabled ? 2 : 1,
            ToolType.Pickaxe => CombatEnabled ? 1 : 2,
            _ => 0
        };
    }

    // 핵심 분기: hudCanvas != null 판정.
    // 상태 변경: hudCanvas 갱신.
    // 다음 연결: ToolSelectionHUD.CreateRect(string, UnityEngine.Transform) 호출.
    private void BuildUI()
    {
        if (hudCanvas != null)
            return;

        GameObject canvasObject = new("ToolSelectionCanvas", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
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

        string[] labels = { "1", "2", "3" };

        for (int i = 0; i < ToolCount; i++)
        {
            toolRects[i] = CreateToolView(panel, labels[i]);
            toolIcons[i] = toolRects[i].GetComponent<Image>();
            if (CombatEnabled) { int slot = i; var image = toolRects[i].GetComponent<Image>(); image.raycastTarget = true; var button = toolRects[i].gameObject.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(() => { if (!GameUIController.BlocksGameplayInput) toolController.SelectToolType(slot == 0 ? ToolType.Sword : slot == 1 ? ToolType.Pickaxe : ToolType.Axe); }); }
        }

        hudCanvas.enabled = requestedVisible;
    }

    // 상태 변경: root.anchorMin 갱신.
    // 다음 연결: ToolSelectionHUD.CreateRect(string, UnityEngine.Transform) 호출.
    private static RectTransform CreateToolView(RectTransform parent, string number)
    {
        RectTransform root = CreateRect($"Tool_{number}", parent);
        root.anchorMin = new Vector2(1f, 0f);
        root.anchorMax = new Vector2(1f, 0f);
        root.pivot = new Vector2(0.5f, 0.5f);

        Image icon = root.gameObject.AddComponent<Image>();
        icon.sprite = null;
        icon.enabled = false;
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
        RuntimeUIFactory.FitText(label, 24f);
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

    // 핵심 분기: i == selectedIndex 판정.
    // 상태 변경: selectedIndex 갱신.
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
        {
            toolController.CurrentToolChanged -= HandleCurrentToolChanged;
            toolController.ToolSlotsChanged -= RefreshIcons;
        }
    }
}
