// [코드 지도] TownStorageUI: 마을 가방·보관함·납품 목록과 아이템 이동 입력을 연결한다.
// 주요 함수: Show, Slot, SyncRows
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/Campaign/UI/TownStorageUI.cs.md

using System;
using System.Collections.Generic;
using System.Linq;
using Blacksmith;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class TownStorageUI : MonoBehaviour
{
    CampaignController owner;
    CampaignUI ui;
    BlacksmithView view;
    bool delivery, showChest;
    RectTransform sourceContent, chestContent, deliveryContent;
    TMP_Text detail, emptySource, emptyChest, transferHint;
    string selectedKey, transferMessage;
    readonly Dictionary<Blacksmith.Stack, InventorySlotView> sourceSlots = new();
    readonly Dictionary<Blacksmith.Stack, InventorySlotView> chestSlots = new();
    readonly Dictionary<Blacksmith.Stack, InventorySlotView> deliverySlots = new();

    public void Initialize(CampaignController c, CampaignUI u)
    {
        owner = c;
        ui = u;
        view = u.view;
    }

    // 핵심 분기: !delivery 판정.
    // 상태 변경: delivery 갱신.
    // 다음 연결: CampaignUI.Open(string) 호출.
    public void Show(bool asDelivery)
    {
        delivery = asDelivery;
        if (!delivery) showChest = false;
        selectedKey = transferMessage = null;
        sourceSlots.Clear();
        chestSlots.Clear();
        deliverySlots.Clear();
        var panel = ui.Open(delivery ? "납품 상자" : "마을 인벤토리");
        if (delivery)
        {
            panel.GetComponent<Image>().sprite = null;
            panel.GetComponent<Image>().color = BlacksmithView.Dark;
            panel.Find("Title").GetComponent<TMP_Text>().color = BlacksmithView.Cream;
            var bagPanel = view.Panel("DeliveryBagPanel", panel, new(.03f, .11f), new(.51f, .74f));
            bagPanel.GetComponent<Image>().color = new Color(.32f, .20f, .16f, .95f);
            view.Image("BagArt", bagPanel, "bag_leather", new Color(1, 1, 1, .3f),
                Vector2.zero, Vector2.one, true).transform.SetAsFirstSibling();
            bagPanel.SetAsFirstSibling();
            var deliveryPanel = view.Panel("DeliveryStagePanel", panel, new(.55f, .29f), new(.96f, .74f));
            deliveryPanel.GetComponent<Image>().color = new Color(.13f, .17f, .17f, .96f);
            deliveryPanel.SetAsFirstSibling();
        }
        else
        {
            // Match the weapon shop's dark backdrop while keeping both inventory grids visible.
            var background = panel.GetComponent<Image>();
            background.sprite = null;
            background.color = BlacksmithView.Dark;
            panel.Find("Title").GetComponent<TMP_Text>().color = BlacksmithView.Cream;
            foreach (string roll in new[] { "TopRoll", "BottomRoll" })
                if (panel.Find(roll) is Transform decoration)
                    decoration.gameObject.SetActive(false);

            var bagPanel = view.Panel("TownBagPanel", panel, new(.025f, .235f), new(.485f, .855f));
            bagPanel.GetComponent<Image>().color = new Color(.32f, .20f, .16f, .95f);
            view.Image("BagArt", bagPanel, "bag_leather", new Color(1f, 1f, 1f, .3f),
                Vector2.zero, Vector2.one, true).transform.SetAsFirstSibling();
            bagPanel.SetAsFirstSibling();

            var chestPanel = view.Panel("TownChestPanel", panel, new(.515f, .235f), new(.975f, .855f));
            chestPanel.GetComponent<Image>().color = new Color(.14f, .14f, .13f, .97f);
            chestPanel.SetAsFirstSibling();
        }

        if (delivery)
        {
            var bagTab = view.Button("TownBagTab", panel, "가방", new(.03f, .77f), new(.24f, .85f),
                () => { showChest = false; Show(true); });
            var chestTab = view.Button("TownChestTab", panel, "보관함", new(.26f, .77f), new(.47f, .85f),
                () => { showChest = true; Show(true); });
            bagTab.image.color = showChest ? new Color(.23f, .22f, .19f) : BlacksmithView.Gold;
            chestTab.image.color = showChest ? BlacksmithView.Gold : new Color(.23f, .22f, .19f);
        }
        else
        {
            view.Text("TownBagHeading", panel, "가방", 25, new(.03f, .75f), new(.47f, .85f), BlacksmithView.Cream);
            view.Text("TownChestHeading", panel, "보관함", 25, new(.53f, .75f), new(.97f, .85f), BlacksmithView.Cream);
        }

        // Both sides use the same chest-style grid bounds so transfer targets line up.
        sourceContent = delivery
            ? view.Scroll(panel, "TownItems", new(.05f, .24f), new(.49f, .72f), 4, 92)
            : view.Scroll(panel, "TownItems", new(.03f, .25f), new(.47f, .73f), 5, 105);
        emptySource = view.Text("EmptyBag", panel, "", 24,
            delivery ? new(.06f, .39f) : new(.05f, .33f),
            delivery ? new(.48f, .62f) : new(.45f, .58f),
            BlacksmithView.Cream);

        deliveryContent = chestContent = null;
        emptyChest = null;
        if (delivery)
        {
            deliveryContent = view.Scroll(panel, "DeliveryItems", new(.57f, .33f),
                new(.94f, .68f), 4, 92);
            DropArea(sourceContent, CurrentSource);
            DropArea(deliveryContent, owner.State.delivery);
            view.Text("DeliveryTitle", panel, "납품 예정 · 다음 날 아침 판매", 24,
                new(.56f, .69f), new(.95f, .74f), BlacksmithView.Cream);
            if (owner.State.pendingGold > 0)
                view.Button("CollectProceeds", panel,
                    $"판매 대금 {owner.State.pendingGold} G\n클릭하여 수령",
                    new(.58f, .15f), new(.92f, .26f),
                    () => { if (owner.Economy.Collect()) owner.Commit(); Show(true); });
            else
                view.Text("NoProceeds", panel, "수령할 대금이 없습니다.", 21,
                    new(.56f, .15f), new(.94f, .26f), BlacksmithView.Cream);
        }
        else
        {
            chestContent = view.Scroll(panel, "TownChestItems", new(.53f, .25f), new(.97f, .73f), 5, 105);
            emptyChest = view.Text("EmptyChest", panel, "보관함이 비어 있습니다.", 24,
                new(.55f, .33f), new(.95f, .58f), BlacksmithView.Cream);
            DropArea(sourceContent, SmithingLoop.Instance.SmithData.bag);
            DropArea(chestContent, SmithingLoop.Instance.SmithData.chest);
        }

        detail = view.Text("TownItemInfo", panel,
            "아이템을 선택하면 이름·가격·보유량을 확인합니다.", 22,
            delivery ? new(.05f, .09f) : new(.04f, .02f),
            delivery ? new(.49f, .22f) : new(.96f, .20f),
            BlacksmithView.Cream);
        transferHint = view.Text("TransferHint", panel, "", 18,
            delivery ? new(.03f, .01f) : new(.03f, .20f),
            delivery ? new(.97f, .07f) : new(.97f, .25f),
            BlacksmithView.Cream);

        RefreshLists();
    }

    List<Blacksmith.Stack> CurrentSource =>
        showChest ? SmithingLoop.Instance.SmithData.chest : SmithingLoop.Instance.SmithData.bag;

    // 핵심 분기: delivery 판정.
    // 상태 변경: emptySource.text 갱신.
    // 다음 연결: TownStorageUI.SyncRows(UnityEngine.RectTransform, System.Collections.Generic.List<Blacksmith.Stack>, System.C… 호출.
    void RefreshLists()
    {
        var source = CurrentSource;
        SyncRows(sourceContent, source, sourceSlots, false);
        if (delivery)
            SyncRows(deliveryContent, owner.State.delivery, deliverySlots, true);
        else
            SyncRows(chestContent, SmithingLoop.Instance.SmithData.chest, chestSlots, false);

        emptySource.text = showChest ? "보관함이 비어 있습니다." :
            "가방이 비어 있습니다.\n채집한 물품은 귀환 후에도 가방에 남습니다.";
        emptySource.gameObject.SetActive(source.Count == 0);
        if (emptyChest != null)
            emptyChest.gameObject.SetActive(SmithingLoop.Instance.SmithData.chest.Count == 0);
        if (transferHint != null)
            transferHint.text = transferMessage ??
                (delivery ? "좌클릭 1개 · 길게/우클릭 전체 · 드래그로 이동" :
                    "가방과 보관함 사이에 아이템을 드래그해 옮기세요.");

        ShowSelectedDetail();
    }

    // 핵심 분기: content == null 판정.
    // 상태 변경: slot.count.text 갱신.
    // 다음 연결: TownStorageUI.Slot(UnityEngine.Transform, Blacksmith.Stack, System.Collections.Generic.List<Blacksmith.Stack>… 호출.
    void SyncRows(RectTransform content, List<Blacksmith.Stack> stacks,
        Dictionary<Blacksmith.Stack, InventorySlotView> slots, bool fromDelivery)
    {
        if (content == null) return;
        foreach (var pair in slots.ToArray())
        {
            if (stacks.Contains(pair.Key)) continue;
            pair.Value.gameObject.SetActive(false);
            Destroy(pair.Value.gameObject);
            slots.Remove(pair.Key);
        }
        foreach (var stack in stacks)
        {
            if (slots.TryGetValue(stack, out var slot))
            {
                if (slot.count != null) slot.count.text = stack.count.ToString();
                continue;
            }
            slot = Slot(content, stack, stacks, fromDelivery);
            if (slot != null) slots.Add(stack, slot);
        }
        view.FillEmptyGridSlots(content, slots.Count);
    }

    void ShowSelectedDetail()
    {
        if (string.IsNullOrEmpty(selectedKey) || detail == null) return;
        var source = CurrentSource;
        var stack = source.Concat(delivery ? owner.State.delivery : SmithingLoop.Instance.SmithData.chest)
            .FirstOrDefault(item => item.Key == selectedKey);
        if (stack == null)
        {
            detail.text = "아이템 이동이 완료되었습니다.";
            return;
        }
        var def = SmithingLoop.Instance.Catalog.Item(stack.itemId);
        if (def == null) return;
        int sourceCount = source.Where(item => item.Key == selectedKey).Sum(item => item.count);
        int deliveryCount = owner.State.delivery
            .Where(item => item.Key == selectedKey).Sum(item => item.count);
        detail.text = delivery
            ? $"{def.displayName} · {(showChest ? "보관함" : "가방")} {sourceCount}개 · 납품 예정 {deliveryCount}개\n판매 기준가 {owner.Economy.Price(stack)} G · {def.description}"
            : $"{def.displayName}\n가방 {SmithingLoop.Instance.SmithData.bag.Where(item => item.Key == selectedKey).Sum(item => item.count)} · 보관함 {SmithingLoop.Instance.SmithData.chest.Where(item => item.Key == selectedKey).Sum(item => item.count)} · 판매 기준가 {owner.Economy.Price(stack)} G\n{def.description}";
    }

    void Transfer(List<Blacksmith.Stack> source, List<Blacksmith.Stack> target,
        Blacksmith.Stack stack, int count)
    {
        if (source == target || stack == null || count <= 0) return;
        var def = SmithingLoop.Instance.Catalog.Item(stack.itemId);
        string key = stack.Key;
        if (!new InventoryService(SmithingLoop.Instance.SmithData,
                SmithingLoop.Instance.Catalog).Transfer(source, target, stack, count))
            return;
        owner.Commit();
        selectedKey = key;
        transferMessage = $"{def?.displayName ?? stack.itemId} {count}개를 " +
            (target == owner.State.delivery ? "납품 예정으로 옮겼습니다." :
                target == SmithingLoop.Instance.SmithData.chest ? "보관함으로 옮겼습니다." : "가방으로 옮겼습니다.");
        RefreshLists();
    }

    // 다음 연결: Blacksmith.ItemDropTarget.ConfigureProximity(System.Func<Blacksmith.InventorySlotView, bool>, System.Action<B… 호출.
    void DropArea(RectTransform content, List<Blacksmith.Stack> destination)
    {
        var viewport = content.parent.gameObject;
        var target = viewport.GetComponent<ItemDropTarget>() ?? viewport.AddComponent<ItemDropTarget>();
        List<Blacksmith.Stack> Origin(InventorySlotView drag)
        {
            var data = SmithingLoop.Instance.SmithData;
            return owner.State.delivery.Contains(drag.Stack)
                ? owner.State.delivery
                : data.bag.Contains(drag.Stack) ? data.bag :
                    data.chest.Contains(drag.Stack) ? data.chest : null;
        }
        target.ConfigureProximity(
            drag => Origin(drag) != null && Origin(drag) != destination && drag.Stack.count > 0 &&
                (delivery ? (destination == owner.State.delivery ? Origin(drag) == CurrentSource :
                    Origin(drag) == owner.State.delivery) :
                    (Origin(drag) == SmithingLoop.Instance.SmithData.bag ||
                     Origin(drag) == SmithingLoop.Instance.SmithData.chest)),
            drag => Transfer(Origin(drag), destination, drag.Stack, drag.Stack.count),
            viewport.GetComponent<Image>());
    }

    // 핵심 분기: def == null 판정.
    // 상태 변경: button.name 갱신.
    // 다음 연결: Blacksmith.BlacksmithCatalog.Item(string) 호출.
    InventorySlotView Slot(Transform parent, Blacksmith.Stack stack,
        List<Blacksmith.Stack> source, bool fromDelivery)
    {
        var def = SmithingLoop.Instance.Catalog.Item(stack.itemId);
        if (def == null) return null;
        var button = Instantiate(ui.rowPrefab, parent);
        button.name = "TownItem_" + stack.itemId;
        button.onClick.RemoveAllListeners();
        var text = button.GetComponentInChildren<TMP_Text>();
        text.text = stack.count.ToString();
        RuntimeUIFactory.FitText(text, 19);
        text.alignment = TextAlignmentOptions.BottomRight;
        var icon = view.Image("Icon", button.transform, view.ItemArtKey(def), Color.white,
            new(.10f, .20f), new(.90f, .96f), true);
        var slot = button.gameObject.AddComponent<InventorySlotView>();
        slot.background = button.image;
        slot.icon = icon;
        slot.count = delivery ? text : null;
        Action select = () => { selectedKey = stack.Key; ShowSelectedDetail(); };
        Action<int> move = count =>
        {
            var destination = fromDelivery ? CurrentSource : owner.State.delivery;
            Transfer(source, destination, stack, count);
        };
        slot.Bind(stack, def, view.ItemArt(def), view.Art("bag_slot"), view.font,
            () => { if (delivery) move(slot.LongPress ? stack.count : 1); else select(); },
            () => { if (delivery) move(stack.count); else select(); },
            drag =>
            {
                if (!delivery) return;
                var data = SmithingLoop.Instance.SmithData;
                var origin = owner.State.delivery.Contains(drag.Stack) ? owner.State.delivery :
                    data.bag.Contains(drag.Stack) ? data.bag : data.chest;
                var target = fromDelivery ? owner.State.delivery : CurrentSource;
                Transfer(origin, target, drag.Stack, drag.Stack.count);
            },
            enter =>
            {
                if (enter && detail != null)
                    detail.text = $"{def.displayName} · {stack.count}개 · {owner.Economy.Price(stack)} G";
                else if (!enter)
                    ShowSelectedDetail();
            });
        // Town inventory grids show icons only; quantity is available in the details and drag preview.
        if (!delivery)
        {
            text.gameObject.SetActive(false);
            Destroy(text.gameObject);
        }
        slot.EnableTransferDrag(() => source.Contains(stack) && stack.count > 0 &&
            (!delivery || (fromDelivery ? source == owner.State.delivery : source == CurrentSource)));
        button.gameObject.AddComponent<UiHoverOutline>();
        return slot;
    }
}
