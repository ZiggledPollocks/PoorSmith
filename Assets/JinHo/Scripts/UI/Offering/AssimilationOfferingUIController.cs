// [코드 지도] AssimilationOfferingUIController: 인벤토리 아이템을 돌바구니에 담아 동화 수치를 낮추는 화면입니다. 바구니는 최대5종을 담고 같은 종류는 수량을 누적합니다. 담는 순간 인벤토리에서 차감하고 확정하면 소모, 취소하면 반환하는 예약 구조입니다. 화면은 런타임에 생성하며 실제 일시 정지와 모달 관리는 GameUIController에 맡깁니다.
// 주요 함수: BuildUI, CreateSlot, BuildStoneBasket
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/UI/Offering/AssimilationOfferingUIController.cs.md

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

[DisallowMultipleComponent]
/// <summary>Controls the offering UI and its player interaction state.</summary>
public sealed class AssimilationOfferingUIController : MonoBehaviour
{
    public event Action OfferingCommitted;
    private const int BasketCapacity = 5;

    [Header("Data")]
    [SerializeField] private InventorySystem inventory;
    [SerializeField] private PlayerAssimilate assimilation;

    [Header("UI Sprites")]
    [SerializeField] private Sprite backpackSprite;
    [SerializeField] private Sprite paperSprite;
    [SerializeField] private Sprite gridSprite;
    [SerializeField] private Sprite selectionSprite;
    [SerializeField] private Sprite stoneBasketSprite;
    [SerializeField] private Shader liquidShader;

    [Header("Inventory Slots")]
    [SerializeField, Min(1)] private int slotColumns = 5;
    [SerializeField, Min(1)] private int slotRows = 4;
    [SerializeField] private Vector2 slotSize = new(76f, 76f);
    [SerializeField, Min(0f)] private float slotSpacing = 6f;

    [Header("Window")]
    [SerializeField] private bool initialOpen;
    [SerializeField] private int canvasSortingOrder = 200;

    private readonly List<SlotView> inventorySlotViews = new();
    private readonly List<SlotView> basketSlotViews = new();
    private readonly List<Image> basketItemIcons = new();
    private readonly List<BasketEntry> basketEntries = new();

    private GameObject canvasObject;
    private GameObject inventoryRoot;
    private GameObject createdEventSystem;
    private TMP_FontAsset runtimeFontAsset;
    private TMP_Text currentAssimilationText;
    private TMP_Text blessingText;
    private TMP_Text expectedAssimilationText;
    private TMP_Text selectedItemText;
    private TMP_Text totalWeightText;
    private TMP_Text basketHintText;
    private Button offerButton;
    private ItemData selectedItemData;
    private bool isReturningItems;

    public bool IsOpen { get; private set; }
    public int BasketItemTypeCount => basketEntries.Count;
    public float TotalFairyBlessing => CalculateFairyBlessing();

    [Serializable]
    private sealed class BasketEntry
    {
        public ItemData ItemData;
        public int Quantity;

        public BasketEntry(ItemData itemData)
        {
            ItemData = itemData;
            Quantity = 1;
        }
    }

    private sealed class SlotView
    {
        public Image Icon;
        public TMP_Text QuantityText;
        public TMP_Text FallbackText;
        public GameObject Selection;
    }

    public void AttachToUIRoot(Transform root, int sortingOrder)
    {
        if (canvasObject == null)
            return;

        canvasObject.transform.SetParent(root, false);
        canvasObject.GetComponent<Canvas>().sortingOrder = sortingOrder;
    }

    private void Awake()
    {
        inventory ??= GetComponent<InventorySystem>();
        assimilation ??= GetComponent<PlayerAssimilate>();
        liquidShader ??= Shader.Find("UI/LiquidCircleGauge");

        EnsureEventSystem();
        BuildUI();
        SetOpen(initialOpen);
    }

    private void OnEnable()
    {
        if (inventory != null)
        {
            inventory.InventoryChanged -= HandleInventoryChanged;
            inventory.InventoryChanged += HandleInventoryChanged;
        }

        if (assimilation != null)
        {
            assimilation.AssimilationChanged -= HandleAssimilationChanged;
            assimilation.AssimilationChanged += HandleAssimilationChanged;
        }
    }

    private void Start() => RefreshAll();

    private void OnDisable()
    {
        ReturnAllBasketItems();
        if (inventory != null)
            inventory.InventoryChanged -= HandleInventoryChanged;

        if (assimilation != null)
            assimilation.AssimilationChanged -= HandleAssimilationChanged;
    }

    private void OnDestroy()
    {
        if (canvasObject != null)
            Destroy(canvasObject);

        if (createdEventSystem != null)
            Destroy(createdEventSystem);

        // TMP_Settings.defaultFontAsset is a shared project asset, not owned by this UI.
        runtimeFontAsset = null;

    }

    public void Toggle() => SetOpen(!IsOpen);

    public void SetOpen(bool open)
    {
        if (!open && IsOpen && basketEntries.Count > 0)
            ReturnAllBasketItems();

        IsOpen = open;
        if (open)
            RefreshAll();

        if (inventoryRoot != null)
            inventoryRoot.SetActive(open);
    }

    // 핵심 분기: itemData == null || inventory == null || AvailableCount(itemData) <= 0 판정.
    // 상태 변경: entry 갱신.
    // 다음 연결: AssimilationOfferingUIController.AvailableCount(ItemData) 호출.
    public bool TryAddToBasket(ItemData itemData)
    {
        if (itemData == null || inventory == null || AvailableCount(itemData) <= 0)
            return false;

        if (!itemData.CanOfferToStoneBasket)
        {
            SetHint($"{itemData.ItemName}은(는) StoneBasket에 담을 수 없습니다.");
            return false;
        }

        BasketEntry entry = basketEntries.Find(candidate => candidate.ItemData == itemData);
        if (entry == null && basketEntries.Count >= BasketCapacity)
        {
            SetHint("바구니 슬롯은 최대 5종까지 사용할 수 있습니다.");
            return false;
        }

        if (entry == null)
        {
            entry = new BasketEntry(itemData);
            basketEntries.Add(entry);
        }
        else
        {
            entry.Quantity++;
        }


        SetHint($"{itemData.ItemName} 1개를 바구니에 넣었습니다.");
        RefreshAll();
        return true;
    }

    public bool ReturnOneFromBasket(int basketIndex)
    {
        if (inventory == null || basketIndex < 0 || basketIndex >= basketEntries.Count)
            return false;

        BasketEntry entry = basketEntries[basketIndex];

        entry.Quantity--;
        if (entry.Quantity <= 0)
            basketEntries.RemoveAt(basketIndex);

        SetHint($"{entry.ItemData.ItemName} 1개를 인벤토리로 돌려보냈습니다.");
        RefreshAll();
        return true;
    }

    public int AvailableCount(ItemData item)
    {
        var reserved = basketEntries.Find(e => e.ItemData == item);
        return Mathf.Max(0, (inventory != null ? inventory.GetItemCount(item) : 0) - (reserved?.Quantity ?? 0));
    }

    public void ReturnAllBasketItems()
    {
        if (isReturningItems) return;
        isReturningItems = true;
        try { basketEntries.Clear(); SetHint("바구니 선택을 취소했습니다. 재료는 인벤토리에 보존됩니다."); }
        finally { isReturningItems = false; }
        if (inventoryRoot != null) RefreshAll();
    }

    // 핵심 분기: basketEntries.Count == 0 || inventory == null || assimilation == null || assimilation.IsDead || assimilation.… 판정.
    // 상태 변경: isReturningItems 갱신.
    // 다음 연결: InventorySystem.GetItemCount(ItemData) 호출.
    public void CommitOffering()
    {
        if (basketEntries.Count == 0 || inventory == null || assimilation == null || assimilation.IsDead ||
            assimilation.CurrentAssimilation <= 0) return;
        foreach (var entry in basketEntries)
            if (inventory.GetItemCount(entry.ItemData) < entry.Quantity) { SetHint("선택한 재료가 부족합니다."); return; }
        int recovery = Mathf.RoundToInt(CalculateFairyBlessing());
        if (recovery <= 0) return;
        var costs = new List<InventoryItem>();
        foreach (var entry in basketEntries) costs.Add(new InventoryItem(entry.ItemData, entry.Quantity));
        isReturningItems = true;
        try
        {
            if (!inventory.TryConsume(costs)) { SetHint("선택한 재료가 부족합니다."); return; }
            basketEntries.Clear();
            assimilation.Assimilate(-recovery);
            OfferingCommitted?.Invoke();
        }
        finally { isReturningItems = false; }
        selectedItemData = null;
        RefreshAll(); RequestClose(false);
    }

    private void HandleInventoryChanged()
    {
        if (!isReturningItems)
            RefreshAll();
    }

    private void HandleAssimilationChanged(float current, int maximum) => RefreshStats();

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        createdEventSystem = new GameObject(
            "EventSystem",
            typeof(EventSystem),
            typeof(InputSystemUIInputModule));
    }

    // 핵심 분기: resetLabel != null 판정.
    // 상태 변경: canvasObject 갱신.
    // 다음 연결: AssimilationOfferingUIController.CreateUIObject(string, UnityEngine.Transform) 호출.
    private void BuildUI()
    {
        canvasObject = new GameObject(
            "AssimilationOfferingCanvas",
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

        inventoryRoot = CreateUIObject("AssimilationOfferingWindow", canvasObject.transform);
        RectTransform rootRect = inventoryRoot.GetComponent<RectTransform>();
        StretchToParent(rootRect);

        Image dimBackground = inventoryRoot.AddComponent<Image>();
        dimBackground.color = new Color(0f, 0f, 0f, 0.72f);
        dimBackground.raycastTarget = true;

        BuildBackButton(rootRect);

        RectTransform contentRect = CreateRectTransform("Content", rootRect);
        SetCenteredRect(contentRect, new Vector2(1840f, 1000f), new Vector2(0f, -15f));

        RectTransform paperRect = CreateRectTransform("Paper", contentRect);
        SetCenteredRect(paperRect, new Vector2(520f, 610f), new Vector2(-620f, 125f));
        Image paper = paperRect.gameObject.AddComponent<Image>();
        paper.sprite = paperSprite;
        paper.preserveAspect = true;
        paper.raycastTarget = false;
        BuildStats(paperRect);

        RectTransform backpackRect = CreateRectTransform("Backpack", contentRect);
        SetCenteredRect(backpackRect, new Vector2(620f, 760f), new Vector2(575f, 20f));
        Image backpack = backpackRect.gameObject.AddComponent<Image>();
        backpack.sprite = backpackSprite;
        backpack.preserveAspect = true;
        backpack.raycastTarget = false;
        BuildWeight(backpackRect);
        BuildInventorySlots(backpackRect);

        RectTransform basketRect = CreateRectTransform("StoneBasketArea", contentRect);
        SetCenteredRect(basketRect, new Vector2(690f, 520f), new Vector2(-75f, -235f));
        BuildStoneBasket(basketRect);

        Button resetButton = CreateButton(
            "ReturnAllButton",
            contentRect,
            "↻",
            new Color(0.08f, 0.28f, 0.27f, 0.95f),
            new Vector2(72f, 72f),
            new Vector2(-465f, 265f));
        TMP_Text resetLabel = resetButton.GetComponentInChildren<TMP_Text>();
        if (resetLabel != null)
            RuntimeUIFactory.FitText(resetLabel, 40f);
        resetButton.onClick.AddListener(ReturnAllBasketItems);

        offerButton = CreateButton(
            "OfferButton",
            contentRect,
            "바치기",
            new Color(0.25f, 0.66f, 0.62f, 0.98f),
            new Vector2(360f, 92f),
            new Vector2(-620f, 50f));
        TMP_Text offerLabel = offerButton.GetComponentInChildren<TMP_Text>();
        if (offerLabel != null)
            RuntimeUIFactory.FitText(offerLabel, 36f);
        offerButton.onClick.AddListener(CommitOffering);
    }

    private void BuildBackButton(RectTransform rootRect)
    {
        Button backButton = CreateButton(
            "BackButton",
            rootRect,
            "←",
            new Color(0.5f, 0.5f, 0.5f, 0.95f),
            new Vector2(116f, 82f),
            Vector2.zero);
        RectTransform rect = backButton.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(42f, -42f);
        TMP_Text label = backButton.GetComponentInChildren<TMP_Text>();
        if (label != null)
            RuntimeUIFactory.FitText(label, 48f);
        backButton.onClick.AddListener(() => RequestClose(true));
    }

    // 상태 변경: currentAssimilationText 갱신.
    // 다음 연결: AssimilationOfferingUIController.CreateText(string, UnityEngine.Transform, float, TMPro.TextAlignmentOptions,… 호출.
    private void BuildStats(RectTransform paperRect)
    {
        currentAssimilationText = CreateText("CurrentAssimilation", paperRect, 31f,
            TextAlignmentOptions.Left, new Color(0.08f, 0.07f, 0.05f, 1f));
        SetAnchoredRect(currentAssimilationText.rectTransform, new Vector2(0.5f, 1f),
            new Vector2(390f, 58f), new Vector2(5f, -175f));
        currentAssimilationText.fontStyle = FontStyles.Bold;

        blessingText = CreateText("FairyBlessing", paperRect, 31f,
            TextAlignmentOptions.Left, new Color(0.08f, 0.07f, 0.05f, 1f));
        SetAnchoredRect(blessingText.rectTransform, new Vector2(0.5f, 1f),
            new Vector2(390f, 58f), new Vector2(5f, -255f));

        RectTransform separator = CreateRectTransform("Separator", paperRect);
        SetAnchoredRect(separator, new Vector2(0.5f, 1f),
            new Vector2(390f, 5f), new Vector2(5f, -315f));
        separator.gameObject.AddComponent<Image>().color = new Color(0.08f, 0.07f, 0.05f, 1f);

        expectedAssimilationText = CreateText("ExpectedAssimilation", paperRect, 31f,
            TextAlignmentOptions.Left, new Color(0.08f, 0.07f, 0.05f, 1f));
        SetAnchoredRect(expectedAssimilationText.rectTransform, new Vector2(0.5f, 1f),
            new Vector2(390f, 58f), new Vector2(5f, -375f));
        expectedAssimilationText.fontStyle = FontStyles.Bold;

        selectedItemText = CreateText("SelectedItem", paperRect, 22f,
            TextAlignmentOptions.TopLeft, new Color(0.2f, 0.13f, 0.08f, 1f));
        SetAnchoredRect(selectedItemText.rectTransform, new Vector2(0.5f, 1f),
            new Vector2(390f, 105f), new Vector2(5f, -465f));
    }

    private void BuildWeight(RectTransform backpackRect)
    {
        totalWeightText = CreateText("TotalWeight", backpackRect, 22f,
            TextAlignmentOptions.Center, Color.white);
        SetAnchoredRect(totalWeightText.rectTransform, new Vector2(0.5f, 1f),
            new Vector2(260f, 48f), new Vector2(0f, -250f));
        totalWeightText.fontStyle = FontStyles.Bold;
        totalWeightText.outlineWidth = 0.18f;
        totalWeightText.outlineColor = new Color(0.18f, 0.08f, 0.04f, 1f);
    }

    private void BuildInventorySlots(RectTransform backpackRect)
    {
        RectTransform gridRect = CreateRectTransform("InventoryGrid", backpackRect);
        float gridWidth = slotColumns * slotSize.x + (slotColumns - 1) * slotSpacing;
        float gridHeight = slotRows * slotSize.y + (slotRows - 1) * slotSpacing;
        SetCenteredRect(gridRect, new Vector2(gridWidth, gridHeight), new Vector2(0f, -125f));

        GridLayoutGroup layout = gridRect.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = slotSize;
        layout.spacing = new Vector2(slotSpacing, slotSpacing);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = slotColumns;
        layout.childAlignment = TextAnchor.MiddleCenter;

        int slotCount = slotColumns * slotRows;
        for (int i = 0; i < slotCount; i++)
            inventorySlotViews.Add(CreateSlot(gridRect, i, false));
    }

    // 핵심 분기: liquidShader != null 판정.
    // 상태 변경: basket.sprite 갱신.
    // 다음 연결: AssimilationOfferingUIController.CreateImage(string, UnityEngine.Transform) 호출.
    private void BuildStoneBasket(RectTransform basketRect)
    {
        Image basket = CreateImage("StoneBasket", basketRect);
        SetCenteredRect(basket.rectTransform, new Vector2(650f, 500f), Vector2.zero);
        basket.sprite = stoneBasketSprite;
        basket.preserveAspect = true;
        basket.raycastTarget = false;

        Image water = CreateImage("Liquid", basketRect);
        SetCenteredRect(water.rectTransform, new Vector2(455f, 145f), new Vector2(0f, 72f));
        water.color = Color.white;
        water.raycastTarget = false;

        if (liquidShader != null)
        {
            LiquidCircleGauge liquid = water.gameObject.AddComponent<LiquidCircleGauge>();
            liquid.Configure(water, null, liquidShader, null);
            liquid.SetNormalizedValue(0.72f);
        }

        Vector2[] itemIconPositions =
        {
            new(-150f, 74f), new(-75f, 105f), new(0f, 78f), new(75f, 106f), new(150f, 74f)
        };
        for (int i = 0; i < BasketCapacity; i++)
        {
            Image itemIcon = CreateImage($"OfferingItemIcon_{i + 1}", basketRect);
            SetCenteredRect(itemIcon.rectTransform, new Vector2(104f, 104f), itemIconPositions[i]);
            itemIcon.preserveAspect = true;
            itemIcon.raycastTarget = false;
            itemIcon.enabled = false;
            basketItemIcons.Add(itemIcon);
        }

        RectTransform basketSlotsRect = CreateRectTransform("BasketSlots", basketRect);
        SetCenteredRect(basketSlotsRect, new Vector2(430f, 82f), new Vector2(0f, -185f));
        GridLayoutGroup layout = basketSlotsRect.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(78f, 78f);
        layout.spacing = new Vector2(10f, 0f);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = BasketCapacity;
        layout.childAlignment = TextAnchor.MiddleCenter;

        for (int i = 0; i < BasketCapacity; i++)
            basketSlotViews.Add(CreateSlot(basketSlotsRect, i, true));

        basketHintText = CreateText("BasketHint", basketRect, 19f,
            TextAlignmentOptions.Center, new Color(0.78f, 0.95f, 0.92f, 1f));
        SetCenteredRect(basketHintText.rectTransform, new Vector2(610f, 45f), new Vector2(0f, -245f));
        basketHintText.outlineWidth = 0.15f;
        basketHintText.outlineColor = Color.black;
        SetHint("같은 슬롯을 다시 클릭하면 1개, 길게 누르면 계속 담깁니다.");
    }

    // 핵심 분기: basketSlot 판정.
    // 상태 변경: background.sprite 갱신.
    // 다음 연결: AssimilationOfferingUIController.CreateUIObject(string, UnityEngine.Transform) 호출.
    private SlotView CreateSlot(RectTransform parent, int slotIndex, bool basketSlot)
    {
        GameObject slotObject = CreateUIObject(
            basketSlot ? $"BasketSlot_{slotIndex + 1}" : $"InventorySlot_{slotIndex + 1:00}", parent);
        Image background = slotObject.AddComponent<Image>();
        background.sprite = gridSprite;
        background.type = Image.Type.Simple;

        Button button = slotObject.AddComponent<Button>();
        button.targetGraphic = background;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.9f, 0.7f, 1f);
        colors.pressedColor = new Color(0.78f, 0.62f, 0.45f, 1f);
        button.colors = colors;

        int capturedIndex = slotIndex;
        if (basketSlot)
            button.onClick.AddListener(() => ReturnOneFromBasket(capturedIndex));
        else
        {
            var hold=slotObject.AddComponent<OfferingSlotHold>();
            hold.Configure(()=>
            {
                var item=GetInventoryItem(capturedIndex);
                if(item?.itemData==null||item.quantity<=0)return false;
                selectedItemData=item.itemData;
                return TryAddToBasket(item.itemData);
            });
            button.onClick.AddListener(()=>
            {
                if(!hold.ConsumeHeldClick())HandleInventorySlotClick(capturedIndex);
            });
        }

        Image icon = CreateImage("Icon", slotObject.transform);
        StretchToParent(icon.rectTransform, 12f);
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        TMP_Text fallback = CreateText("MissingIconText", slotObject.transform, 28f,
            TextAlignmentOptions.Center, new Color(0.25f, 0.12f, 0.06f, 1f));
        StretchToParent(fallback.rectTransform, 14f);
        fallback.fontStyle = FontStyles.Bold;

        TMP_Text quantity = CreateText("Quantity", slotObject.transform, 18f,
            TextAlignmentOptions.BottomRight, Color.white);
        quantity.rectTransform.anchorMin = new Vector2(0.35f, 0f);
        quantity.rectTransform.anchorMax = Vector2.one;
        quantity.rectTransform.offsetMin = new Vector2(0f, 4f);
        quantity.rectTransform.offsetMax = new Vector2(-6f, -4f);
        quantity.fontStyle = FontStyles.Bold;
        quantity.outlineWidth = 0.2f;
        quantity.outlineColor = Color.black;

        Image selection = CreateImage("Selection", slotObject.transform);
        StretchToParent(selection.rectTransform);
        selection.sprite = selectionSprite;
        selection.raycastTarget = false;

        return new SlotView
        {
            Icon = icon,
            QuantityText = quantity,
            FallbackText = fallback,
            Selection = selection.gameObject
        };
    }

    // 핵심 분기: item?.itemData == null || item.quantity <= 0 판정.
    // 상태 변경: selectedItemData 갱신.
    // 다음 연결: AssimilationOfferingUIController.GetInventoryItem(int) 호출.
    private void HandleInventorySlotClick(int slotIndex)
    {
        InventoryItem item = GetInventoryItem(slotIndex);
        if (item?.itemData == null || item.quantity <= 0)
        {
            selectedItemData = null;
            RefreshAll();
            return;
        }

        if (selectedItemData == item.itemData)
        {
            TryAddToBasket(item.itemData);
            return;
        }

        selectedItemData = item.itemData;
        SetHint(selectedItemData.CanOfferToStoneBasket
            ? $"{selectedItemData.ItemName} 선택됨 - 다시 클릭하면 바구니에 담깁니다."
            : $"{selectedItemData.ItemName}은(는) StoneBasket에 담을 수 없습니다.");
        RefreshAll();
    }

    private InventoryItem GetInventoryItem(int slotIndex)
    {
        IReadOnlyList<InventoryItem> items = inventory != null ? inventory.Items : null;
        if (items == null || slotIndex < 0 || slotIndex >= items.Count)
            return null;
        return items[slotIndex];
    }

    private void RefreshAll()
    {
        RefreshInventorySlots();
        RefreshBasketSlots();
        RefreshStats();
    }

    // 핵심 분기: selectedItemData != null && (inventory == null || inventory.GetItemCount(selectedItemData) <= 0) 판정.
    // 상태 변경: selectedItemData 갱신.
    // 다음 연결: InventorySystem.GetItemCount(ItemData) 호출.
    private void RefreshInventorySlots()
    {
        IReadOnlyList<InventoryItem> items = inventory != null ? inventory.Items : null;
        int itemCount = items?.Count ?? 0;

        if (selectedItemData != null && (inventory == null || inventory.GetItemCount(selectedItemData) <= 0))
            selectedItemData = null;

        for (int i = 0; i < inventorySlotViews.Count; i++)
        {
            InventoryItem item = i < itemCount ? items[i] : null;
            bool hasItem = item?.itemData != null && item.quantity > 0;
            ApplyItemToSlot(inventorySlotViews[i], hasItem ? item.itemData : null, hasItem ? AvailableCount(item.itemData) : 0);
            inventorySlotViews[i].Selection.SetActive(hasItem && item.itemData == selectedItemData);
        }

        float currentWeight = inventory != null ? inventory.CurrentWeight : 0f;
        float maxWeight = inventory != null ? inventory.MaxWeight : 0f;
        if (totalWeightText != null)
            totalWeightText.text = $"가방 무게  {currentWeight:0.##} / {maxWeight:0.##}";

        if (selectedItemText != null)
        {
            selectedItemText.text = selectedItemData == null
                ? "아이템을 한 번 클릭해 선택하세요."
                : $"선택: {selectedItemData.ItemName}\n" +
                  (selectedItemData.CanOfferToStoneBasket
                      ? $"동화율 감소량: {selectedItemData.DiscountAssimilationRate:0.##}%"
                      : "<color=#A7352A>StoneBasket에 담을 수 없는 아이템</color>");
        }
    }

    private void RefreshBasketSlots()
    {
        for (int i = 0; i < basketSlotViews.Count; i++)
        {
            BasketEntry entry = i < basketEntries.Count ? basketEntries[i] : null;
            ApplyItemToSlot(basketSlotViews[i], entry?.ItemData, entry?.Quantity ?? 0);
            basketSlotViews[i].Selection.SetActive(false);
        }

        for (int i = 0; i < basketItemIcons.Count; i++)
        {
            BasketEntry entry = i < basketEntries.Count ? basketEntries[i] : null;
            Sprite icon = entry?.ItemData != null ? entry.ItemData.Icon : null;
            basketItemIcons[i].sprite = icon;
            basketItemIcons[i].enabled = icon != null;
        }

        if (offerButton != null)
            offerButton.interactable = basketEntries.Count > 0 && assimilation != null && !assimilation.IsDead && assimilation.CurrentAssimilation > 0;
    }

    private void RefreshStats()
    {
        float current = assimilation != null ? assimilation.CurrentAssimilation : 0;
        int maximum = assimilation != null ? assimilation.MaxAssimilation : 100;
        float blessing = CalculateFairyBlessing();
        float expected = Mathf.Clamp(current - Mathf.RoundToInt(blessing), 0, maximum);

        if (currentAssimilationText != null)
            currentAssimilationText.text = $"현재 동화율:  {current}%";
        if (blessingText != null)
            blessingText.text = $"−  요정의 가호:  <color=#4FB8AD>{blessing:0.##}%</color>";
        if (expectedAssimilationText != null)
            expectedAssimilationText.text = $"예상 동화율:  {expected}%";
    }

    private float CalculateFairyBlessing()
    {
        float total = 0f;
        for (int i = 0; i < basketEntries.Count; i++)
        {
            BasketEntry entry = basketEntries[i];
            if (entry.ItemData != null && entry.Quantity > 0)
                total += entry.ItemData.DiscountAssimilationRate * entry.Quantity;
        }
        return total;
    }

    private static void ApplyItemToSlot(SlotView slot, ItemData itemData, int quantity)
    {
        bool hasItem = itemData != null && quantity > 0;
        Sprite icon = hasItem ? itemData.Icon : null;
        slot.Icon.sprite = icon;
        slot.Icon.enabled = icon != null;
        slot.FallbackText.gameObject.SetActive(hasItem && icon == null);
        slot.FallbackText.text = hasItem && icon == null ? GetFirstCharacter(itemData.ItemName) : string.Empty;
        slot.QuantityText.text = hasItem ? $"x{quantity}" : string.Empty;
    }

    private void RequestClose(bool returnItems)
    {
        if (returnItems)
            ReturnAllBasketItems();

        if (GameUIController.Instance != null)
            GameUIController.Instance.CloseAssimilationOffering();
        else
            SetOpen(false);
    }

    private void SetHint(string message)
    {
        if (basketHintText != null)
            basketHintText.text = message;
    }

    private TMP_Text CreateText(string objectName, Transform parent, float fontSize,
        TextAlignmentOptions alignment, Color color)
    {
        return RuntimeUIFactory.CreateText(objectName, parent, fontSize, alignment, color, GetFontAsset());
    }

    // 상태 변경: background.color 갱신.
    // 다음 연결: AssimilationOfferingUIController.CreateRectTransform(string, UnityEngine.Transform) 호출.
    private Button CreateButton(string objectName, Transform parent, string label,
        Color backgroundColor, Vector2 size, Vector2 position)
    {
        RectTransform buttonRect = CreateRectTransform(objectName, parent);
        SetAnchoredRect(buttonRect, new Vector2(0.5f, 0f), size, position);
        Image background = buttonRect.gameObject.AddComponent<Image>();
        background.color = backgroundColor;

        Button button = buttonRect.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.86f, 0.74f, 1f);
        colors.pressedColor = new Color(0.72f, 0.62f, 0.55f, 1f);
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.55f);
        button.colors = colors;

        TMP_Text labelText = CreateText("Label", buttonRect, 24f, TextAlignmentOptions.Center, Color.white);
        StretchToParent(labelText.rectTransform, 4f);
        labelText.text = label;
        labelText.fontStyle = FontStyles.Bold;
        return button;
    }

    // 핵심 분기: runtimeFontAsset != null 판정.
    // 상태 변경: runtimeFontAsset 갱신.
    private TMP_FontAsset GetFontAsset()
    {
        if (runtimeFontAsset != null)
            return runtimeFontAsset;

        runtimeFontAsset = TMP_Settings.defaultFontAsset;
        return runtimeFontAsset;
    }

    private static string GetFirstCharacter(string text) => string.IsNullOrWhiteSpace(text)
        ? "?"
        : text.Substring(0, 1).ToUpperInvariant();

    private static GameObject CreateUIObject(string objectName, Transform parent)
    {
        return RuntimeUIFactory.CreateUIObject(objectName, parent);
    }

    private static RectTransform CreateRectTransform(string objectName, Transform parent) =>
        RuntimeUIFactory.CreateRectTransform(objectName, parent);

    private static Image CreateImage(string objectName, Transform parent)
    {
        return RuntimeUIFactory.CreateImage(objectName, parent);
    }

    private static void StretchToParent(RectTransform rectTransform, float inset = 0f)
    {
        RuntimeUIFactory.StretchToParent(rectTransform, inset);
    }

    private static void SetCenteredRect(RectTransform rectTransform, Vector2 size, Vector2 position)
    {
        RuntimeUIFactory.SetCenteredRect(rectTransform, size, position);
    }

    private static void SetAnchoredRect(RectTransform rectTransform, Vector2 anchor,
        Vector2 size, Vector2 position)
    {
        RuntimeUIFactory.SetAnchoredRect(rectTransform, anchor, size, position);
    }
}

/// <summary>Repeats a basket add while the pointer remains down on an offering slot.</summary>
public sealed class OfferingSlotHold : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    const float InitialDelay=.38f;
    const float RepeatInterval=.14f;
    Func<bool> addOne;
    Coroutine repeat;
    bool suppressClick;

    public void Configure(Func<bool> action)=>addOne=action;
    public bool ConsumeHeldClick(){bool held=suppressClick;suppressClick=false;return held;}
    public void OnPointerDown(PointerEventData eventData)
    {
        if(eventData.button!=PointerEventData.InputButton.Left)return;
        StopRepeating();suppressClick=false;
        repeat=StartCoroutine(Repeat());
    }
    public void OnPointerUp(PointerEventData eventData)=>StopRepeating();
    public void OnPointerExit(PointerEventData eventData)=>StopRepeating();
    void OnDisable()=>StopRepeating();
    void StopRepeating(){if(repeat!=null)StopCoroutine(repeat);repeat=null;}
    System.Collections.IEnumerator Repeat()
    {
        yield return new WaitForSecondsRealtime(InitialDelay);
        while(addOne!=null&&addOne())
        {
            suppressClick=true;
            yield return new WaitForSecondsRealtime(RepeatInterval);
        }
        repeat=null;
    }
}
