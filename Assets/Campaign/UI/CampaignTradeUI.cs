// [코드 지도] CampaignTradeUI: 상점 판매·구매와 전당포 담보·상환 화면을 구성하고 거래 요청을 전달한다.
// 주요 함수: DrawPurchase, DrawStage, DrawOwned
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/Campaign/UI/CampaignTradeUI.cs.md

using System;
using System.Collections.Generic;
using System.Linq;
using Blacksmith;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Trade UI only; CampaignEconomy owns all money and inventory mutations.
public sealed class CampaignTradeUI : MonoBehaviour
{
    sealed class Offer { public string source; public Blacksmith.Stack stack; public string label; }
    CampaignController owner;
    CampaignUI ui;
    BlacksmithView view;
    RectTransform panel;
    readonly Dictionary<InventorySlotView, Offer> slots = new();
    InventorySlotView stagedSlot;
    string category = "all", product, pawnTab = "pledge", feedback = "";
    bool selling;
    Offer staged;
    int quantity = 1, revision;
    BlacksmithCatalog Catalog => SmithingLoop.Instance.Catalog;
    SaveData Smith => SmithingLoop.Instance.SmithData;
    CampaignEconomy Economy => owner.Economy;

    public void Initialize(CampaignController controller, CampaignUI campaignUI)
    { owner = controller; ui = campaignUI; view = campaignUI.view; }

    public void ResetShop()
    { category = "all"; product = null; pawnTab = "pledge"; selling = false; staged = null; feedback = ""; }

    RectTransform Open(string title)
    {
        revision++;
        slots.Clear();
        stagedSlot = null;
        panel = ui.Open(title, false);
        panel.GetComponent<Image>().sprite = null;
        panel.GetComponent<Image>().color = BlacksmithView.Dark;
        panel.Find("Title").GetComponent<TMP_Text>().color = BlacksmithView.Cream;
        foreach (string part in new[] { "TopRoll", "BottomRoll" })
            if (panel.Find(part) != null) panel.Find(part).gameObject.SetActive(false);
        view.Text("Gold", panel, $"보유 {Economy.GoldText} G", 24,
            new(.61f, .86f), new(.84f, .96f), BlacksmithView.Cream);
        view.Text("TradeFeedback", panel, feedback, 19,
            new(.05f, .01f), new(.95f, .08f), BlacksmithView.Cream);
        view.Button("FinishTrade", panel, "거래 종료", new(.85f, .88f), new(.98f, .97f), ui.Close);
        return panel;
    }

    void Tabs(string[] ids, string[] labels, string selected, Action<string> change)
    {
        for (int i = 0; i < ids.Length; i++)
        {
            string id = ids[i];
            var button = view.Button("Tab_" + id, panel, labels[i],
                new(.03f + i * .23f, .77f), new(.24f + i * .23f, .85f),
                () => { staged = null; change(id); });
            button.image.color = id == selected ? BlacksmithView.Gold : new Color(.23f, .22f, .19f);
        }
    }

    Button Row(Transform parent, string name, string label, Sprite icon, Action click)
    {
        var button = Instantiate(ui.rowPrefab, parent);
        button.name = name;
        button.gameObject.SetActive(true);
        var text = button.GetComponentInChildren<TMP_Text>();
        text.text = label;
        RuntimeUIFactory.FitText(text, 20);
        var rect = text.rectTransform;
        rect.anchorMin = new(.19f, 0);
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        view.Image("ItemIcon", button.transform, null, Color.white,
            new(.01f, .12f), new(.18f, .88f), true).sprite = icon;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => click());
        return button;
    }

    void Completed(string text)
    {
        feedback = text;
        ui.RecordTrade();
        SmithingLoop.Instance.RefreshEquipment();
        owner.Commit();
    }

    bool Tool(ItemDefinition item) => CampaignEconomy.IsTradeTool(item);
    bool PurchaseWeaponOrTool(ItemDefinition item) => Tool(item) || item.material == MaterialKind.Weapon;
    bool Discovered(ItemDefinition item) => item != null && item.id != "golem_core" &&
        item.group != ItemGroup.Gathered && Economy.RecipeDiscovered(item.id);
    IEnumerable<ItemDefinition> Products() => Catalog.items
        .Where(item => item != null && item.id != "golem_core" && (Tool(item) || Discovered(item)))
        .GroupBy(item => item.id).Select(group => group.First())
        .Where(item => category == "all" || category == "tools" && PurchaseWeaponOrTool(item) ||
            category == "items" && !PurchaseWeaponOrTool(item));

    public void ShowShop() { selling = false; staged = null; DrawShop(); }
    void DrawShop()
    {
        Open(selling ? "장비 상점 · 판매" : "장비 상점 · 구매");
        Tabs(new[] { "buy", "sell" }, new[] { "구매", "판매" },
            selling ? "sell" : "buy", tab => { selling = tab == "sell"; DrawShop(); });
        if (selling) { DrawOwned(true); DrawStage(true); }
        else DrawPurchase();
    }

    // 핵심 분기: category != "bags" 판정.
    // 상태 변경: right.GetComponent<Image>().color 갱신.
    // 다음 연결: Blacksmith.BlacksmithView.Panel(string, UnityEngine.Transform, UnityEngine.Vector2, UnityEngine.Vector2, bool) 호출.
    void DrawPurchase()
    {
        DrawPurchaseInventory();
        var left = view.Panel("PurchaseDetails", panel, new(.28f, .11f), new(.58f, .73f), true);
        view.Image("BagArt", left, "bag_leather", new Color(1, 1, 1, .3f),
            Vector2.zero, Vector2.one, true).transform.SetAsFirstSibling();
        var right = view.Panel("ProductPanel", panel, new(.60f, .11f), new(.97f, .73f));
        right.GetComponent<Image>().color = new Color(.1f, .12f, .12f, .96f);
        string[] ids = { "all", "tools", "items", "bags" };
        string[] labels = { "전체", "도구·무기", "아이템", "배낭" };
        for (int i = 0; i < ids.Length; i++)
        {
            string id = ids[i];
            var b = view.Button("Category_" + id, right, labels[i],
                new(.02f + i * .24f, .87f), new(.24f + i * .24f, .98f),
                () => { category = id; product = null; DrawShop(); });
            b.image.color = category == id ? BlacksmithView.Gold : new Color(.25f, .25f, .22f);
        }
        var list = view.Scroll(right, "Products", new(.03f, .03f), new(.97f, .84f));
        if (category != "bags")
            foreach (var item in Products())
            {
                var chosen = item;
                int price = Tool(item) ? item.buyPrice : item.price;
                Row(list, "Goods_" + item.id,
                    $"{item.displayName}" + (QualityRules.AppliesTo(item) ? $" · 제작 숙련도 {Economy.RecipeMastery(item.id)}" : "") + "\n" +
                    (price > 0 ? $"{price} G" : "가격 미정 · 구매 불가"),
                    view.ItemArt(item), () => { product = chosen.id; DrawShop(); });
            }
        if (category == "all" || category == "bags")
            for (int level = 1; level <= 3; level++)
            {
                int chosen = level;
                Row(list, "Bag_" + level,
                    $"배낭 {level}단계 · {CampaignRules.BagCapacityKg(level):0}kg\n" +
                    (level <= owner.State.bagTier ? "보유함" : $"{CampaignRules.BagPrice(level)} G"),
                    ui.tradeBag ?? view.Art("bag_leather"),
                    () => { product = "bag:" + chosen; DrawShop(); });
            }
        if (string.IsNullOrEmpty(product))
        {
            view.Text("SelectHint", left, "오른쪽에서 상품을 선택하세요.", 25,
                new(.07f, .35f), new(.93f, .65f), BlacksmithView.Ink);
            return;
        }
        bool bag = product.StartsWith("bag:", StringComparison.Ordinal);
        int levelSelected = bag ? int.Parse(product.Substring(4)) : 0;
        var selected = bag ? null : Catalog.Item(product);
        if (!bag && (selected == null || !Products().Any(x => x.id == product))) return;
        string name = bag ? $"배낭 {levelSelected}단계" : selected.displayName;
        int cost = bag ? CampaignRules.BagPrice(levelSelected) :
            Tool(selected) ? selected.buyPrice : selected.price;
        bool owned = bag && levelSelected <= owner.State.bagTier;
        bool next = !bag || levelSelected == owner.State.bagTier + 1;
        view.Image("SelectedIcon", left, bag ? "bag_leather" : view.ItemArtKey(selected),
            Color.white, new(.35f, .68f), new(.65f, .92f), true);
        view.Text("SelectedName", left, name, 29,
            new(.07f, .57f), new(.93f, .68f), BlacksmithView.Ink,
            TextAlignmentOptions.Center);
        string details = bag
            ? $"최대 무게 {CampaignRules.BagCapacityKg(levelSelected):0}kg\n" +
                (owned ? "이미 보유했습니다." : next ? "순서대로 구매할 수 있습니다." : "이전 단계를 먼저 구매하세요.")
            : $"{selected.description}\n" + (QualityRules.AppliesTo(selected) ? $"제작 숙련도 {Economy.RecipeMastery(selected.id)} · " : "") + "가방에 지급";
        view.Text("ProductInfo", left,
            details + "\n" + (cost > 0 ? $"가격 {cost} G" : "유효한 가격이 없어 구매할 수 없습니다."),
            22, new(.07f, .26f), new(.93f, .55f), BlacksmithView.Ink);
        int shown = revision;
        var buy = view.Button("Purchase", left, "구매하기",
            new(.25f, .09f), new(.75f, .22f), () =>
            {
                if (shown != revision) return;
                bool success = bag ? next && Economy.BuyUpgrade("bag") :
                    Tool(selected) ? selected.buyPrice > 0 &&
                        Economy.BuyTool(selected.toolKind, selected.toolTier) :
                        Economy.BuyCatalogItem(selected.id);
                if (!success) feedback = "구매 불가: 금화·가격·보유 단계를 확인하세요.";
                else Completed(name + " 구매 완료 · 가방에 지급");
                DrawShop();
            });
        buy.interactable = !owned && next && cost > 0;
    }

    void DrawPurchaseInventory()
    {
        var owned = view.Panel("OwnedInventory", panel, new(.03f, .11f), new(.26f, .73f));
        owned.GetComponent<Image>().color = new Color(.32f, .20f, .16f, .95f);
        view.Text("OwnedTitle", owned, "보유 물품 · 가방/보관함", 20,
            new(.03f, .89f), new(.97f, .99f), BlacksmithView.Cream,
            TextAlignmentOptions.Center);
        var grid = view.Scroll(owned, "OwnedItems", new(.03f, .03f), new(.97f, .87f), 3, 82);
        var stock = Smith.bag.Select(stack => new Offer { source = "bag", stack = stack, label = "가방" })
            .Concat(Smith.chest.Select(stack => new Offer { source = "chest", stack = stack, label = "보관함" }))
            .Where(offer => offer.stack != null && offer.stack.count > 0 &&
                Catalog.Item(offer.stack.itemId) != null).ToArray();
        if (stock.Length == 0)
            view.Text("EmptyOwned", owned, "보유 물품이 없습니다.", 19,
                new(.06f, .39f), new(.94f, .61f), BlacksmithView.Cream,
                TextAlignmentOptions.Center);
        foreach (var offer in stock)
        {
            var item = Catalog.Item(offer.stack.itemId);
            var slot = view.Panel("Owned_" + offer.source + "_" + item.id, grid,
                Vector2.zero, Vector2.one);
            view.Image("Icon", slot, view.ItemArtKey(item), Color.white,
                new(.10f, .16f), new(.90f, .87f), true);
            view.Text("Count", slot, offer.stack.count.ToString(), 15,
                new(.50f, .01f), new(.98f, .26f), BlacksmithView.Cream,
                TextAlignmentOptions.Right);
            view.Text("Source", slot, offer.label, 12,
                new(.02f, .75f), new(.98f, .99f), BlacksmithView.Cream,
                TextAlignmentOptions.Center);
            var hover = slot.gameObject.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => view.ShowTooltip($"{offer.label} · {item.displayName} ×{offer.stack.count}"));
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => view.HideTooltip());
            hover.triggers.Add(enter);
            hover.triggers.Add(exit);
        }
    }

    IEnumerable<Offer> Owned(bool forShop)
    {
        bool allowed(Blacksmith.Stack stack)
        {
            if(stack==null||stack.count<=0)return false;
            var item=Catalog.Item(stack.itemId);
            return item!=null&&(forShop?CampaignEconomy.IsShopSaleTool(item):
                !CampaignEconomy.IsShopSaleTool(item));
        }
        foreach (var stack in Smith.bag.Where(allowed))
            yield return new Offer { source = "bag", stack = stack, label = "가방" };
        foreach (var stack in Smith.chest.Where(allowed))
            yield return new Offer { source = "chest", stack = stack, label = "보관함" };
        if (!forShop)
            foreach (var entry in Smith.equipment)
                if (entry != null && allowed(entry.stack))
                    yield return new Offer { source = "equip:" + entry.slot, stack = entry.stack,
                        label = "장착/" + entry.slot };
    }

    // 핵심 분기: offers.Length==0 판정.
    // 상태 변경: left.GetComponent<Image>().color 갱신.
    // 다음 연결: Blacksmith.BlacksmithView.Panel(string, UnityEngine.Transform, UnityEngine.Vector2, UnityEngine.Vector2, bool) 호출.
    void DrawOwned(bool forShop)
    {
        if (forShop)
        {
            // Keep the carried bag and storage chest visible on opposite sides.
            DrawShopOwnedPanel("TradeBag", "가방 · 판매 가능한 도구·무기", "bag",
                new(.03f, .37f), new(.48f, .74f), true);
            DrawShopOwnedPanel("TradeChest", "보관함 · 판매 가능한 도구·무기", "chest",
                new(.52f, .37f), new(.97f, .74f), false);
            return;
        }
        var left = view.Panel("TradeBag", panel, new(.03f, .11f), new(.51f, .74f));
        left.GetComponent<Image>().color = new Color(.32f, .20f, .16f, .95f);
        view.Image("BagArt", left, "bag_leather", new Color(1, 1, 1, .3f),
            Vector2.zero, Vector2.one, true).transform.SetAsFirstSibling();
        view.Text("InventoryTitle", left,
            forShop ? "판매 가능한 도구·무기" : "맡길 수 있는 아이템", 24,
            new(.04f, .88f), new(.96f, .98f), BlacksmithView.Cream);
        var grid = view.Scroll(left, "OwnedItems", new(.03f, .03f), new(.97f, .87f), 4, 92);
        left.gameObject.AddComponent<ItemDropTarget>().ConfigureProximity(
            drag => drag == stagedSlot && staged != null &&
                Owned(forShop).Any(x => x.source == staged.source && ReferenceEquals(x.stack, staged.stack)),
            _ => { staged = null; quantity = 1; if (forShop) DrawShop(); else ShowPawn("pawnPledge"); },
            left.GetComponent<Image>(), true);
        var offers=Owned(forShop).ToArray();
        if(offers.Length==0)
            view.Text("NoEligibleItems", left,
                forShop?"판매할 도구·무기가 없습니다.":"맡길 아이템이 없습니다.",
                22,new(.08f,.35f),new(.92f,.65f),BlacksmithView.Cream,
                TextAlignmentOptions.Center);
        foreach (var offered in offers)
        {
            var item = Catalog.Item(offered.stack.itemId);
            var button = Instantiate(ui.rowPrefab, grid);
            button.name = "TradeItem_" + offered.source + "_" + item.id;
            button.gameObject.SetActive(true);
            button.onClick.RemoveAllListeners();
            var count = button.GetComponentInChildren<TMP_Text>();
            count.text = offered.stack.count.ToString();
            RuntimeUIFactory.FitText(count, 18);
            count.alignment = TextAlignmentOptions.BottomRight;
            var icon = view.Image("Icon", button.transform, view.ItemArtKey(item),
                Color.white, new(.1f, .19f), new(.9f, .9f), true);
            view.Text("Source", button.transform, offered.label, 12,
                new(.02f, .83f), new(.98f, .99f), BlacksmithView.Cream,
                TextAlignmentOptions.Center);
            var slot = button.gameObject.AddComponent<InventorySlotView>();
            slot.background = button.image;
            slot.icon = icon;
            slot.count = count;
            slot.Bind(offered.stack, item, view.ItemArt(item), view.Art("bag_slot"), view.font,
                () => feedback = $"{item.displayName} · {offered.label} · 오른쪽으로 드래그하세요.",
                () => { }, _ => { }, entering =>
                {
                    if (entering) view.ShowTooltip($"{offered.label} · {item.displayName} ×{offered.stack.count}");
                    else view.HideTooltip();
                });
            slot.EnableTransferDrag(() => Owned(forShop).Any(x =>
                x.source == offered.source && ReferenceEquals(x.stack, offered.stack)));
            slot.SetDragPreviewQuantity(() => 1);
            slots.Add(slot, offered);
        }
        view.FillEmptyGridSlots(grid, offers.Length);
    }

    void DrawShopOwnedPanel(string name, string title, string source,
        Vector2 min, Vector2 max, bool showBagArt)
    {
        var container = view.Panel(name, panel, min, max);
        var background = container.GetComponent<Image>();
        background.color = showBagArt ? new Color(.32f, .20f, .16f, .95f) :
            new Color(.14f, .14f, .13f, .97f);
        if (showBagArt)
        {
            view.Image("BagArt", container, "bag_leather", new Color(1, 1, 1, .3f),
                Vector2.zero, Vector2.one, true).transform.SetAsFirstSibling();
            view.Image("BagTop", container, "bag_top", Color.white,
                new(.015f, .80f), new(.985f, 1f), true);
        }
        view.Text("InventoryTitle", container, title, 22,
            new(.04f, .84f), new(.96f, .97f), BlacksmithView.Cream,
            TextAlignmentOptions.Center);
        var grid = view.Scroll(container, "OwnedItems", new(.03f, .03f), new(.97f, .82f), 5, 82);
        container.gameObject.AddComponent<ItemDropTarget>().ConfigureProximity(
            drag => drag == stagedSlot && staged != null && staged.source == source &&
                Owned(true).Any(x => x.source == staged.source && ReferenceEquals(x.stack, staged.stack)),
            _ => { staged = null; quantity = 1; DrawShop(); }, background, true);
        var offers = Owned(true).Where(x => x.source == source).ToArray();
        if (offers.Length == 0)
            view.Text("NoEligibleItems", container, "판매할 도구·무기가 없습니다.", 20,
                new(.08f, .35f), new(.92f, .65f), BlacksmithView.Cream,
                TextAlignmentOptions.Center);
        foreach (var offered in offers)
        {
            var item = Catalog.Item(offered.stack.itemId);
            var button = Instantiate(ui.rowPrefab, grid);
            button.name = "TradeItem_" + source + "_" + item.id;
            button.gameObject.SetActive(true);
            button.onClick.RemoveAllListeners();
            var count = button.GetComponentInChildren<TMP_Text>();
            count.text = offered.stack.count.ToString();
            RuntimeUIFactory.FitText(count, 18);
            count.alignment = TextAlignmentOptions.BottomRight;
            var icon = view.Image("Icon", button.transform, view.ItemArtKey(item),
                Color.white, new(.1f, .19f), new(.9f, .9f), true);
            var slot = button.gameObject.AddComponent<InventorySlotView>();
            slot.background = button.image;
            slot.icon = icon;
            slot.count = count;
            slot.Bind(offered.stack, item, view.ItemArt(item), view.Art("bag_slot"), view.font,
                () => feedback = $"{item.displayName} · {offered.label} · 아래 판매 칸으로 드래그하세요.",
                () => { }, _ => { }, entering =>
                {
                    if (entering) view.ShowTooltip($"{offered.label} · {item.displayName} ×{offered.stack.count}");
                    else view.HideTooltip();
                });
            slot.EnableTransferDrag(() => Owned(true).Any(x =>
                x.source == offered.source && ReferenceEquals(x.stack, offered.stack)));
            slot.SetDragPreviewQuantity(() => 1);
            slots.Add(slot, offered);
        }
        view.FillEmptyGridSlots(grid, offers.Length);
    }

    // 핵심 분기: !slots.TryGetValue(drag, out var offered) 판정.
    // 상태 변경: background.color 갱신.
    // 다음 연결: Blacksmith.BlacksmithView.Panel(string, UnityEngine.Transform, UnityEngine.Vector2, UnityEngine.Vector2, bool) 호출.
    void DrawStage(bool forShop)
    {
        var drop = view.Panel(forShop ? "SaleDrop" : "PledgeDrop", panel,
            forShop ? new(.03f, .20f) : new(.55f, .29f),
            forShop ? new(.97f, .35f) : new(.96f, .74f));
        var background = drop.GetComponent<Image>();
        background.color = new Color(.13f, .17f, .17f, .96f);
        var target = drop.gameObject.AddComponent<ItemDropTarget>();
        target.ConfigureProximity(drag => slots.ContainsKey(drag) &&
            Owned(forShop).Any(x => x.source == slots[drag].source &&
                ReferenceEquals(x.stack, slots[drag].stack)),
            drag =>
            {
                if (!slots.TryGetValue(drag, out var offered)) return;
                staged = offered;
                quantity = 1;
                if (forShop) DrawShop(); else ShowPawn("pawnPledge");
            }, background, true);
        view.Text("DropHint", drop,
            forShop ? "판매할 도구·무기를 이곳에 놓으세요" : "맡길 아이템을 이곳에 놓으세요",
            forShop ? 20 : 23,
            forShop ? new(.03f, .76f) : new(.05f, .73f),
            forShop ? new(.97f, .98f) : new(.95f, .96f), BlacksmithView.Cream,
            TextAlignmentOptions.Center);
        if (staged == null)
        {
            view.Text("EmptyDrop", drop,
                forShop?"가방·보관함의 도구·무기를 드래그":"가방·보관함·장착 아이템을 드래그", 22,
                forShop ? new(.06f, .18f) : new(.08f, .35f),
                forShop ? new(.70f, .73f) : new(.92f, .65f), BlacksmithView.Cream,
                TextAlignmentOptions.Center);
            return;
        }
        var item = Catalog.Item(staged.stack.itemId);
        long assessed = (long)Economy.Price(staged.stack) * quantity;
        bool tooLarge = assessed > ((long)int.MaxValue * 1000 + 999) / 500;
        long amountMilli = tooLarge ? 0 : assessed * 500;
        var stagedButton = view.Button("StagedItem", drop, staged.stack.count.ToString(),
            forShop ? new(.03f, .21f) : new(.07f, .35f),
            forShop ? new(.12f, .72f) : new(.27f, .66f), () => { });
        var stagedIcon = view.Image("StagedIcon", stagedButton.transform, view.ItemArtKey(item),
            Color.white, new(.04f, .10f), new(.96f, .92f), true);
        var stagedCount = stagedButton.GetComponentInChildren<TMP_Text>();
        RuntimeUIFactory.FitText(stagedCount, 18);
        stagedCount.alignment = TextAlignmentOptions.BottomRight;
        stagedSlot = stagedButton.gameObject.AddComponent<InventorySlotView>();
        stagedSlot.background = stagedButton.image;
        stagedSlot.icon = stagedIcon;
        stagedSlot.count = stagedCount;
        stagedSlot.Bind(staged.stack, item, view.ItemArt(item), view.Art("bag_slot"), view.font,
            () => feedback = forShop ? "원래 가방 또는 보관함 목록으로 드래그하면 선택이 취소됩니다." :
                "왼쪽 목록으로 드래그하면 선택이 취소됩니다.", () => { }, _ => { },
            entering => { if (entering) view.ShowTooltip(item.displayName); else view.HideTooltip(); });
        stagedCount.text = quantity.ToString();
        stagedSlot.EnableTransferDrag(() => staged != null &&
            ReferenceEquals(staged.stack, stagedSlot.Stack));
        stagedSlot.SetDragPreviewQuantity(() => quantity);
        string info = tooLarge ? "수량이 너무 많아 거래할 수 없습니다." : forShop
            ? $"{item.displayName} · {staged.label}\n선택 {quantity}/{staged.stack.count}개\n판매 대금 {CampaignEconomy.FormatMilli(amountMilli)} G"
            : $"{item.displayName} · {staged.label}\n감정가 {assessed} G · 대출 원금 {CampaignEconomy.FormatMilli(amountMilli)} G\n" +
                $"일일 단리 {CampaignEconomy.FormatMilli(amountMilli / 20)} G · 다음 주간 빚 {Economy.NextDebtDay}일";
        view.Text("StageInfo", drop, info, forShop ? 19 : 21,
            forShop ? new(.14f, .12f) : new(.29f, .35f),
            forShop ? new(.52f, .74f) : new(.96f, .72f), BlacksmithView.Cream);
        view.Button("Less", drop, "−", forShop ? new(.54f, .25f) : new(.17f, .12f),
            forShop ? new(.61f, .64f) : new(.33f, .29f),
            () => { quantity = Mathf.Max(1, quantity - 1); if (forShop) DrawShop(); else ShowPawn("pawnPledge"); });
        view.Button("More", drop, "+", forShop ? new(.62f, .25f) : new(.35f, .12f),
            forShop ? new(.69f, .64f) : new(.51f, .29f),
            () => { quantity = Mathf.Min(staged.stack.count, quantity + 1); if (forShop) DrawShop(); else ShowPawn("pawnPledge"); });
        view.Button("All", drop, "전체", forShop ? new(.70f, .25f) : new(.53f, .12f),
            forShop ? new(.77f, .64f) : new(.85f, .29f),
            () => { quantity = staged.stack.count; if (forShop) DrawShop(); else ShowPawn("pawnPledge"); });
        int shown = revision;
        var confirm = view.Button("ConfirmTrade", panel, forShop ? "판매하기" : "물건 맡기기",
            new(.39f, .10f), new(.61f, .18f), () =>
            {
                if (shown != revision || staged == null) return;
                bool success = forShop
                    ? Economy.SellToShop(staged.source, staged.stack, quantity, out _)
                    : Economy.Pledge(staged.source, staged.stack, quantity, out _);
                if (!success) feedback = "거래 불가: 물품·가격·보관 공간을 확인하세요.";
                else { staged = null; Completed(forShop ? "판매 완료" : "물건 맡기기 완료"); }
                if (forShop) DrawShop(); else ShowPawn("pawnPledge");
            });
        confirm.interactable = amountMilli > 0;
    }

    // 핵심 분기: pawnTab == "pledge" 판정.
    // 상태 변경: pawnTab 갱신.
    // 다음 연결: CampaignTradeUI.Open(string) 호출.
    public void ShowPawn(string requestedMode, int requestedPage = 0)
    {
        pawnTab = requestedMode == "pawnBuy" || requestedMode == "pawnLegacy" ? "legacy" :
            requestedMode == "pawnRepay" ? "repay" : "pledge";
        Open(pawnTab == "pledge" ? "전당포 · 물건 맡기기" :
            pawnTab == "repay" ? "전당포 · 상환·반환" : "전당포 · 이전 보관품");
        Tabs(new[] { "pledge", "repay", "legacy" },
            new[] { "물건 맡기기", "상환·반환", "이전 보관품" }, pawnTab,
            id => ShowPawn(id == "repay" ? "pawnRepay" : id == "legacy" ? "pawnLegacy" : "pawnPledge"));
        if (pawnTab == "pledge")
        {
            DrawOwned(false);
            DrawStage(false);
            long principal = owner.State.pawnLoans?.Sum(l => l.principalMilli) ?? 0;
            long interest = owner.State.pawnLoans?.Sum(l => l.unpaidInterestMilli) ?? 0;
            view.Text("LoanTotals", panel,
                $"미상환 원금 {CampaignEconomy.FormatMilli(principal)} G · 미납 이자 {CampaignEconomy.FormatMilli(interest)} G",
                19, new(.55f, .19f), new(.96f, .27f), BlacksmithView.Cream);
        }
        else if (pawnTab == "repay") DrawRepayment();
        else DrawLegacy();
    }

    // 핵심 분기: loans.Count == 0 판정.
    // 상태 변경: row.gameObject.AddComponent<LayoutElement>().preferredHeight 갱신.
    // 다음 연결: Blacksmith.BlacksmithView.Scroll(UnityEngine.Transform, string, UnityEngine.Vector2, UnityEngine.Vector2, int… 호출.
    void DrawRepayment()
    {
        var list = view.Scroll(panel, "PawnLoans", new(.05f, .14f), new(.95f, .74f));
        var loans = owner.State.pawnLoans ?? new List<PawnLoan>();
        if (loans.Count == 0)
            view.Text("NoLoans", panel, "맡긴 물건이 없습니다.", 27,
                new(.1f, .36f), new(.9f, .63f), BlacksmithView.Cream,
                TextAlignmentOptions.Center);
        foreach (var loan in loans.ToArray())
        {
            var item = Catalog.Item(loan.collateral.itemId);
            if (item == null) continue;
            var row = view.Panel("Loan_" + loan.id, list, Vector2.zero, Vector2.one);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 120;
            view.Image("Icon", row, view.ItemArtKey(item), Color.white,
                new(.01f, .18f), new(.13f, .88f), true);
            view.Text("LoanDetails", row,
                $"{item.displayName} ×{loan.collateral.count}" + (QualityRules.AppliesTo(item) ? " · " + QualityRules.Name(loan.collateral.quality) : "") + "\n" +
                $"원금 {CampaignEconomy.FormatMilli(loan.principalMilli)} G · 미납 이자 {CampaignEconomy.FormatMilli(loan.unpaidInterestMilli)} G" +
                $" · 반환에 필요 {CampaignEconomy.FormatMilli(Economy.RepaymentMilli(loan))} G",
                20, new(.15f, .08f), new(.78f, .92f), BlacksmithView.Ink);
            int shown = revision;
            var button = view.Button("Repay", row, "상환·반환",
                new(.79f, .22f), new(.99f, .78f), () =>
                {
                    if (shown != revision) return;
                    if (!Economy.Repay(loan)) feedback = "상환 불가: 금화·보관 공간을 확인하세요.";
                    else Completed(item.displayName + " 반환 완료");
                    ShowPawn("pawnRepay");
                });
            button.interactable = Economy.GoldMilli >= Economy.RepaymentMilli(loan);
        }
    }

    // 핵심 분기: item == null 판정.
    // 상태 변경: layout.preferredHeight 갱신.
    // 다음 연결: Blacksmith.BlacksmithView.Text(string, UnityEngine.Transform, string, float, UnityEngine.Vector2, UnityEngine… 호출.
    void DrawLegacy()
    {
        view.Text("LegacyInfo", panel,
            "이전 세이브의 전당포 보관품입니다. 대출 기록으로 전환하지 않았습니다.",
            23, new(.05f, .66f), new(.95f, .74f), BlacksmithView.Cream);
        var list = view.Scroll(panel, "LegacyStock", new(.05f, .14f), new(.95f, .65f));
        foreach (var stack in owner.State.pawnStock.ToArray())
        {
            var item = Catalog.Item(stack.itemId);
            if (item == null) continue;
            var row = Row(list, "Legacy_" + item.id,
                $"{item.displayName} ×{stack.count} · 반환 가격 {Economy.Price(stack)} G/개",
                view.ItemArt(item), () => { });
            var layout = row.GetComponent<LayoutElement>();
            if (layout != null) layout.preferredHeight = 90;
            int shown = revision;
            view.Button("Recover", row.transform, "찾기",
                new(.78f, .12f), new(.98f, .88f), () =>
                {
                    if (shown != revision) return;
                    if (!Economy.PawnBuy(stack, 1)) feedback = "반환 불가: 금화·수량을 확인하세요.";
                    else Completed(item.displayName + " 이전 보관품 반환");
                    ShowPawn("pawnLegacy");
                });
        }
    }
}
