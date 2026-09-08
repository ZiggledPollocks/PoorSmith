using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class InventoryUIController : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private InventorySystem inventory;
    [SerializeField] private PlayerInputHandler inputHandler;

    [Header("UI Sprites")]
    [SerializeField] private Sprite backpackSprite;
    [SerializeField] private Sprite paperSprite;
    [SerializeField] private Sprite gridSprite;
    [SerializeField] private Sprite selectionSprite;

    [Header("Slots")]
    [SerializeField, Min(1)] private int slotColumns = 5;
    [SerializeField, Min(1)] private int slotRows = 4;
    [SerializeField] private Vector2 slotSize = new(76f, 76f);
    [SerializeField, Min(0f)] private float slotSpacing = 6f;

    [Header("Window")]
    [SerializeField] private bool initialOpen;
    [SerializeField] private int canvasSortingOrder = 200;

    private readonly List<SlotView> slotViews = new();

    private GameObject canvasObject;
    private GameObject inventoryRoot;
    private GameObject itemDetailsRoot;
    private GameObject discardPopup;
    private GameObject createdEventSystem;
    private TMP_FontAsset runtimeFontAsset;
    private TMP_Text itemNameText;
    private TMP_Text itemDetailsText;
    private TMP_Text totalWeightText;
    private TMP_Text discardItemNameText;
    private TMP_Text discardAmountText;
    private Slider discardSlider;
    private ItemData pendingDiscardItem;
    private int selectedSlotIndex;

    public bool IsOpen { get; private set; }

    private sealed class SlotView
    {
        public Image Icon;
        public TMP_Text QuantityText;
        public TMP_Text FallbackText;
        public GameObject Selection;
    }

    private void Awake()
    {
        inventory ??= GetComponent<InventorySystem>();
        inputHandler ??= GetComponent<PlayerInputHandler>();

        EnsureEventSystem();
        BuildUI();
        SetOpen(initialOpen);
    }

    private void OnEnable()
    {
        if (inventory == null)
            return;

        inventory.InventoryChanged -= HandleInventoryChanged;
        inventory.InventoryChanged += HandleInventoryChanged;
    }

    private void Start()
    {
        RefreshSlots();
    }

    private void Update()
    {
        bool toggleRequested =
            inputHandler != null &&
            inputHandler.ConsumeInventoryToggleInput();

        if (inputHandler == null && Keyboard.current != null)
            toggleRequested = Keyboard.current.eKey.wasPressedThisFrame;

        if (!toggleRequested)
            return;

        if (IsOpen)
            SetOpen(false);
        else
            SetOpen(true);
    }

    private void OnDisable()
    {
        if (inventory != null)
            inventory.InventoryChanged -= HandleInventoryChanged;
    }

    private void OnDestroy()
    {
        if (canvasObject != null)
            Destroy(canvasObject);

        if (createdEventSystem != null)
            Destroy(createdEventSystem);

        if (runtimeFontAsset != null)
            Destroy(runtimeFontAsset);
    }

    public void Toggle()
    {
        SetOpen(!IsOpen);
    }

    public void SetOpen(bool open)
    {
        IsOpen = open;

        if (open)
            RefreshSlots();
        else
            CloseDiscardPopup();

        if (inventoryRoot != null)
            inventoryRoot.SetActive(open);
    }

    private void HandleInventoryChanged()
    {
        if (pendingDiscardItem != null)
        {
            int availableAmount = inventory.GetItemCount(pendingDiscardItem);

            if (availableAmount <= 0)
            {
                CloseDiscardPopup();
            }
            else if (discardSlider != null)
            {
                discardSlider.maxValue = availableAmount;
                discardSlider.value = Mathf.Min(discardSlider.value, availableAmount);
                UpdateDiscardAmountText();
            }
        }

        RefreshSlots();
    }

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        createdEventSystem = new GameObject(
            "EventSystem",
            typeof(EventSystem),
            typeof(InputSystemUIInputModule));
    }

    private void BuildUI()
    {
        canvasObject = new GameObject(
            "InventoryCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = canvasSortingOrder;
        canvas.pixelPerfect = true;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        inventoryRoot = CreateUIObject("InventoryWindow", canvasObject.transform);
        RectTransform rootRect = inventoryRoot.GetComponent<RectTransform>();
        StretchToParent(rootRect);

        Image dimBackground = inventoryRoot.AddComponent<Image>();
        dimBackground.color = new Color(0f, 0f, 0f, 0.45f);
        dimBackground.raycastTarget = true;

        RectTransform contentRect = CreateRectTransform("Content", rootRect);
        SetCenteredRect(contentRect, new Vector2(1120f, 720f), Vector2.zero);

        RectTransform backpackRect = CreateRectTransform("Backpack", contentRect);
        SetCenteredRect(backpackRect, new Vector2(620f, 700f), new Vector2(-255f, 0f));
        Image backpack = backpackRect.gameObject.AddComponent<Image>();
        backpack.sprite = backpackSprite;
        backpack.preserveAspect = true;
        backpack.raycastTarget = false;

        BuildWeight(backpackRect);
        BuildSlots(backpackRect);

        RectTransform paperRect = CreateRectTransform("Paper", contentRect);
        SetCenteredRect(paperRect, new Vector2(470f, 610f), new Vector2(325f, -10f));
        Image paper = paperRect.gameObject.AddComponent<Image>();
        paper.sprite = paperSprite;
        paper.preserveAspect = true;
        paper.raycastTarget = false;

        BuildDetails(paperRect);
        BuildDiscardPopup(rootRect);
    }

    private void BuildWeight(RectTransform backpackRect)
    {
        totalWeightText = CreateText(
            "TotalWeight",
            backpackRect,
            22f,
            TextAlignmentOptions.Center,
            Color.white);
        SetAnchoredRect(
            totalWeightText.rectTransform,
            new Vector2(0.5f, 1f),
            new Vector2(230f, 48f),
            new Vector2(0f, -238f));
        totalWeightText.fontStyle = FontStyles.Bold;
        totalWeightText.outlineWidth = 0.18f;
        totalWeightText.outlineColor = new Color(0.18f, 0.08f, 0.04f, 1f);
    }

    private void BuildSlots(RectTransform backpackRect)
    {
        RectTransform gridRect = CreateRectTransform("ItemGrid", backpackRect);

        float gridWidth = slotColumns * slotSize.x + (slotColumns - 1) * slotSpacing;
        float gridHeight = slotRows * slotSize.y + (slotRows - 1) * slotSpacing;
        SetCenteredRect(gridRect, new Vector2(gridWidth, gridHeight), new Vector2(0f, -110f));

        GridLayoutGroup gridLayout = gridRect.gameObject.AddComponent<GridLayoutGroup>();
        gridLayout.cellSize = slotSize;
        gridLayout.spacing = new Vector2(slotSpacing, slotSpacing);
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = slotColumns;
        gridLayout.childAlignment = TextAnchor.MiddleCenter;

        int slotCount = slotColumns * slotRows;
        for (int i = 0; i < slotCount; i++)
            slotViews.Add(CreateSlot(gridRect, i));
    }

    private SlotView CreateSlot(RectTransform parent, int slotIndex)
    {
        GameObject slotObject = CreateUIObject($"Slot_{slotIndex + 1:00}", parent);
        Image slotBackground = slotObject.AddComponent<Image>();
        slotBackground.sprite = gridSprite;
        slotBackground.type = Image.Type.Simple;
        slotBackground.preserveAspect = false;

        Button button = slotObject.AddComponent<Button>();
        button.targetGraphic = slotBackground;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.9f, 0.7f, 1f);
        colors.pressedColor = new Color(0.78f, 0.62f, 0.45f, 1f);
        colors.selectedColor = Color.white;
        button.colors = colors;

        int capturedIndex = slotIndex;
        button.onClick.AddListener(() => SelectSlot(capturedIndex));

        Image icon = CreateImage("Icon", slotObject.transform);
        StretchToParent(icon.rectTransform, 13f);
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        TMP_Text fallbackText = CreateText(
            "MissingIconText",
            slotObject.transform,
            30f,
            TextAlignmentOptions.Center,
            new Color(0.25f, 0.12f, 0.06f, 1f));
        StretchToParent(fallbackText.rectTransform, 15f);
        fallbackText.fontStyle = FontStyles.Bold;

        TMP_Text quantityText = CreateText(
            "Quantity",
            slotObject.transform,
            18f,
            TextAlignmentOptions.BottomRight,
            Color.white);
        RectTransform quantityRect = quantityText.rectTransform;
        quantityRect.anchorMin = new Vector2(0.35f, 0f);
        quantityRect.anchorMax = Vector2.one;
        quantityRect.offsetMin = new Vector2(0f, 5f);
        quantityRect.offsetMax = new Vector2(-7f, -5f);
        quantityText.fontStyle = FontStyles.Bold;
        quantityText.outlineWidth = 0.2f;
        quantityText.outlineColor = Color.black;

        Image selection = CreateImage("Selection", slotObject.transform);
        // grid와 checkgrid의 전체 PNG 영역이 슬롯의 동일한 픽셀 영역을 사용한다.
        StretchToParent(selection.rectTransform);
        selection.sprite = selectionSprite;
        selection.type = Image.Type.Simple;
        selection.preserveAspect = false;
        selection.raycastTarget = false;

        return new SlotView
        {
            Icon = icon,
            QuantityText = quantityText,
            FallbackText = fallbackText,
            Selection = selection.gameObject
        };
    }

    private void BuildDetails(RectTransform paperRect)
    {
        itemDetailsRoot = CreateUIObject("ItemDetailsContent", paperRect);
        StretchToParent(itemDetailsRoot.GetComponent<RectTransform>());

        TMP_Text header = CreateText(
            "Header",
            itemDetailsRoot.transform,
            24f,
            TextAlignmentOptions.Center,
            new Color(0.32f, 0.16f, 0.08f, 1f));
        SetAnchoredRect(
            header.rectTransform,
            new Vector2(0.5f, 1f),
            new Vector2(350f, 42f),
            new Vector2(0f, -88f));
        header.text = "아이템 정보";
        header.fontStyle = FontStyles.Bold;

        itemNameText = CreateText(
            "ItemName",
            itemDetailsRoot.transform,
            34f,
            TextAlignmentOptions.Center,
            new Color(0.24f, 0.11f, 0.05f, 1f));
        SetAnchoredRect(
            itemNameText.rectTransform,
            new Vector2(0.5f, 1f),
            new Vector2(350f, 82f),
            new Vector2(0f, -148f));
        itemNameText.fontStyle = FontStyles.Bold;

        itemDetailsText = CreateText(
            "ItemDetails",
            itemDetailsRoot.transform,
            25f,
            TextAlignmentOptions.TopLeft,
            new Color(0.3f, 0.16f, 0.08f, 1f));
        SetAnchoredRect(
            itemDetailsText.rectTransform,
            new Vector2(0.5f, 1f),
            new Vector2(330f, 220f),
            new Vector2(0f, -310f));

        Button discardButton = CreateButton(
            "OpenDiscardButton",
            itemDetailsRoot.transform,
            "버리기",
            new Color(0.7f, 0.12f, 0.08f, 0.92f),
            new Vector2(170f, 52f),
            new Vector2(0f, 92f));
        discardButton.onClick.AddListener(OpenDiscardPopup);
    }

    private void BuildDiscardPopup(RectTransform rootRect)
    {
        discardPopup = CreateUIObject("DiscardPopup", rootRect);
        RectTransform popupRootRect = discardPopup.GetComponent<RectTransform>();
        StretchToParent(popupRootRect);

        Image blocker = discardPopup.AddComponent<Image>();
        blocker.color = new Color(0f, 0f, 0f, 0.72f);
        blocker.raycastTarget = true;

        RectTransform panelRect = CreateRectTransform("Panel", popupRootRect);
        SetCenteredRect(panelRect, new Vector2(500f, 430f), Vector2.zero);
        Image panel = panelRect.gameObject.AddComponent<Image>();
        panel.sprite = paperSprite;
        panel.preserveAspect = true;
        panel.raycastTarget = true;

        TMP_Text title = CreateText(
            "Title",
            panelRect,
            30f,
            TextAlignmentOptions.Center,
            new Color(0.28f, 0.1f, 0.05f, 1f));
        SetAnchoredRect(
            title.rectTransform,
            new Vector2(0.5f, 1f),
            new Vector2(350f, 48f),
            new Vector2(0f, -92f));
        title.text = "아이템 버리기";
        title.fontStyle = FontStyles.Bold;

        discardItemNameText = CreateText(
            "DiscardItemName",
            panelRect,
            26f,
            TextAlignmentOptions.Center,
            new Color(0.3f, 0.14f, 0.07f, 1f));
        SetAnchoredRect(
            discardItemNameText.rectTransform,
            new Vector2(0.5f, 1f),
            new Vector2(360f, 44f),
            new Vector2(0f, -148f));
        discardItemNameText.fontStyle = FontStyles.Bold;

        discardAmountText = CreateText(
            "DiscardAmount",
            panelRect,
            23f,
            TextAlignmentOptions.Center,
            new Color(0.3f, 0.14f, 0.07f, 1f));
        SetAnchoredRect(
            discardAmountText.rectTransform,
            new Vector2(0.5f, 0.5f),
            new Vector2(340f, 42f),
            new Vector2(0f, 45f));

        discardSlider = CreateSlider(panelRect);
        discardSlider.onValueChanged.AddListener(_ => UpdateDiscardAmountText());

        Button confirmButton = CreateButton(
            "ConfirmDiscardButton",
            panelRect,
            "버리기",
            new Color(0.7f, 0.12f, 0.08f, 0.95f),
            new Vector2(155f, 52f),
            new Vector2(-88f, 62f));
        confirmButton.onClick.AddListener(ConfirmDiscard);

        Button cancelButton = CreateButton(
            "CancelDiscardButton",
            panelRect,
            "취소",
            new Color(0.34f, 0.24f, 0.18f, 0.95f),
            new Vector2(155f, 52f),
            new Vector2(88f, 62f));
        cancelButton.onClick.AddListener(CloseDiscardPopup);

        discardPopup.SetActive(false);
    }

    private Slider CreateSlider(RectTransform parent)
    {
        RectTransform sliderRect = CreateRectTransform("DiscardAmountSlider", parent);
        SetCenteredRect(sliderRect, new Vector2(260f, 34f), new Vector2(0f, -12f));

        Slider slider = sliderRect.gameObject.AddComponent<Slider>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.wholeNumbers = true;
        slider.minValue = 1f;
        slider.maxValue = 1f;
        slider.value = 1f;

        Image background = CreateImage("Background", sliderRect);
        RectTransform backgroundRect = background.rectTransform;
        backgroundRect.anchorMin = new Vector2(0f, 0.5f);
        backgroundRect.anchorMax = new Vector2(1f, 0.5f);
        backgroundRect.sizeDelta = new Vector2(0f, 12f);
        backgroundRect.anchoredPosition = Vector2.zero;
        background.color = new Color(0.25f, 0.15f, 0.1f, 0.8f);

        RectTransform fillArea = CreateRectTransform("Fill Area", sliderRect);
        StretchToParent(fillArea, 10f);
        Image fill = CreateImage("Fill", fillArea);
        StretchToParent(fill.rectTransform);
        fill.color = new Color(0.76f, 0.32f, 0.15f, 1f);

        RectTransform handleArea = CreateRectTransform("Handle Slide Area", sliderRect);
        StretchToParent(handleArea, 10f);
        Image handle = CreateImage("Handle", handleArea);
        RectTransform handleRect = handle.rectTransform;
        handleRect.anchorMin = new Vector2(0f, 0.5f);
        handleRect.anchorMax = new Vector2(0f, 0.5f);
        handleRect.sizeDelta = new Vector2(28f, 34f);
        handleRect.anchoredPosition = Vector2.zero;
        handle.color = new Color(0.96f, 0.82f, 0.55f, 1f);

        slider.fillRect = fill.rectTransform;
        slider.handleRect = handleRect;
        slider.targetGraphic = handle;
        return slider;
    }

    private void SelectSlot(int slotIndex)
    {
        selectedSlotIndex = slotIndex;
        UpdateSelection();
        UpdateDetails();
    }

    private void OpenDiscardPopup()
    {
        InventoryItem selectedItem = GetSelectedItem();

        if (selectedItem?.itemData == null || selectedItem.quantity <= 0)
            return;

        pendingDiscardItem = selectedItem.itemData;
        discardItemNameText.text = pendingDiscardItem.ItemName;
        discardSlider.minValue = 1f;
        discardSlider.maxValue = selectedItem.quantity;
        discardSlider.value = 1f;
        UpdateDiscardAmountText();
        discardPopup.SetActive(true);
    }

    private void CloseDiscardPopup()
    {
        pendingDiscardItem = null;

        if (discardPopup != null)
            discardPopup.SetActive(false);
    }

    private void ConfirmDiscard()
    {
        if (inventory == null || pendingDiscardItem == null)
        {
            CloseDiscardPopup();
            return;
        }

        int availableAmount = inventory.GetItemCount(pendingDiscardItem);
        int discardAmount = Mathf.Clamp(
            Mathf.RoundToInt(discardSlider.value),
            1,
            availableAmount);

        if (availableAmount > 0)
            inventory.RemoveItem(pendingDiscardItem, discardAmount);

        CloseDiscardPopup();
    }

    private void UpdateDiscardAmountText()
    {
        if (discardAmountText == null || discardSlider == null)
            return;

        int amount = Mathf.RoundToInt(discardSlider.value);
        int maximum = Mathf.RoundToInt(discardSlider.maxValue);
        discardAmountText.text = $"버릴 수량: {amount} / {maximum}";
    }

    private void RefreshSlots()
    {
        if (slotViews.Count == 0)
            return;

        IReadOnlyList<InventoryItem> items = inventory != null ? inventory.Items : null;
        int itemCount = items?.Count ?? 0;

        for (int i = 0; i < slotViews.Count; i++)
        {
            SlotView slot = slotViews[i];
            InventoryItem item = i < itemCount ? items[i] : null;
            bool hasItem = item?.itemData != null && item.quantity > 0;
            Sprite iconSprite = hasItem ? item.itemData.Icon : null;

            slot.Icon.sprite = iconSprite;
            slot.Icon.enabled = iconSprite != null;

            bool needsFallback = hasItem && iconSprite == null;
            slot.FallbackText.gameObject.SetActive(needsFallback);
            slot.FallbackText.text = needsFallback
                ? GetFirstCharacter(item.itemData.ItemName)
                : string.Empty;

            slot.QuantityText.text = hasItem ? $"x{item.quantity}" : string.Empty;
        }

        UpdateSelection();
        UpdateDetails();
    }

    private void UpdateSelection()
    {
        for (int i = 0; i < slotViews.Count; i++)
            slotViews[i].Selection.SetActive(i == selectedSlotIndex);
    }

    private void UpdateDetails()
    {
        if (itemDetailsRoot == null || itemNameText == null ||
            itemDetailsText == null || totalWeightText == null)
            return;

        InventoryItem selectedItem = GetSelectedItem();
        bool hasSelectedItem = selectedItem?.itemData != null && selectedItem.quantity > 0;
        itemDetailsRoot.SetActive(hasSelectedItem);

        if (hasSelectedItem)
        {
            ItemData itemData = selectedItem.itemData;
            itemNameText.text = itemData.ItemName;
            itemDetailsText.text =
                $"수량              {selectedItem.quantity}\n\n" +
                $"개당 무게         {itemData.Weight:0.##}\n\n" +
                $"아이템 총 무게    {selectedItem.TotalWeight:0.##}";
        }

        float currentWeight = inventory != null ? inventory.CurrentWeight : 0f;
        float maxWeight = inventory != null ? inventory.MaxWeight : 0f;
        totalWeightText.text = $"가방 무게  {currentWeight:0.##} / {maxWeight:0.##}";
    }

    private InventoryItem GetSelectedItem()
    {
        IReadOnlyList<InventoryItem> items = inventory != null ? inventory.Items : null;

        if (items == null || selectedSlotIndex < 0 || selectedSlotIndex >= items.Count)
            return null;

        return items[selectedSlotIndex];
    }

    private TMP_Text CreateText(
        string objectName,
        Transform parent,
        float fontSize,
        TextAlignmentOptions alignment,
        Color color)
    {
        GameObject textObject = CreateUIObject(objectName, parent);
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.font = GetFontAsset();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private Button CreateButton(
        string objectName,
        Transform parent,
        string label,
        Color backgroundColor,
        Vector2 size,
        Vector2 position)
    {
        RectTransform buttonRect = CreateRectTransform(objectName, parent);
        SetAnchoredRect(
            buttonRect,
            new Vector2(0.5f, 0f),
            size,
            position);

        Image background = buttonRect.gameObject.AddComponent<Image>();
        background.color = backgroundColor;

        Button button = buttonRect.gameObject.AddComponent<Button>();
        button.targetGraphic = background;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.85f, 0.72f, 1f);
        colors.pressedColor = new Color(0.72f, 0.62f, 0.55f, 1f);
        button.colors = colors;

        TMP_Text buttonText = CreateText(
            "Label",
            buttonRect,
            23f,
            TextAlignmentOptions.Center,
            Color.white);
        StretchToParent(buttonText.rectTransform, 4f);
        buttonText.text = label;
        buttonText.fontStyle = FontStyles.Bold;
        return button;
    }

    private TMP_FontAsset GetFontAsset()
    {
        if (runtimeFontAsset != null)
            return runtimeFontAsset;

        // ItemData 이름과 UI 문구에 한글이 포함될 수 있으므로 한글 동적 폰트를 우선 사용한다.
        runtimeFontAsset = TMP_FontAsset.CreateFontAsset("Malgun Gothic", "Regular", 90);

        if (runtimeFontAsset == null)
        {
            Font builtInFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (builtInFont != null)
                runtimeFontAsset = TMP_FontAsset.CreateFontAsset(builtInFont);
        }

        if (runtimeFontAsset != null)
        {
            runtimeFontAsset.name = "Inventory Runtime Font";
            runtimeFontAsset.hideFlags = HideFlags.HideAndDontSave;
        }

        return runtimeFontAsset != null
            ? runtimeFontAsset
            : TMP_Settings.defaultFontAsset;
    }

    private static string GetFirstCharacter(string text)
    {
        return string.IsNullOrWhiteSpace(text)
            ? "?"
            : text.Substring(0, 1).ToUpperInvariant();
    }

    private static GameObject CreateUIObject(string objectName, Transform parent)
    {
        GameObject uiObject = new(objectName, typeof(RectTransform));
        uiObject.transform.SetParent(parent, false);
        return uiObject;
    }

    private static RectTransform CreateRectTransform(string objectName, Transform parent)
    {
        return CreateUIObject(objectName, parent).GetComponent<RectTransform>();
    }

    private static Image CreateImage(string objectName, Transform parent)
    {
        GameObject imageObject = CreateUIObject(objectName, parent);
        Image image = imageObject.AddComponent<Image>();
        image.type = Image.Type.Simple;
        return image;
    }

    private static void StretchToParent(RectTransform rectTransform, float inset = 0f)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.offsetMin = new Vector2(inset, inset);
        rectTransform.offsetMax = new Vector2(-inset, -inset);
    }

    private static void SetCenteredRect(
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

    private static void SetAnchoredRect(
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
}
