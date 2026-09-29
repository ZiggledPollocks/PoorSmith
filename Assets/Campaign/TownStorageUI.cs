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

    public void Show(bool asDelivery)
    {
        delivery = asDelivery;
        if (!delivery) showChest = false;
        selectedKey = transferMessage = null;
        sourceSlots.Clear();
        chestSlots.Clear();
        deliverySlots.Clear();
        var panel = ui.Open(delivery ? "납품 상자" : "마을 인벤토리");
        panel.GetComponent<Image>().color = BlacksmithView.Cream;

        if (delivery)
        {
            view.Button("TownBagTab", panel, "가방", new(.03f, .75f), new(.23f, .85f),
                () => { showChest = false; Show(true); });
            view.Button("TownChestTab", panel, "보관함", new(.25f, .75f), new(.45f, .85f),
                () => { showChest = true; Show(true); });
        }
        else
        {
            view.Text("TownBagHeading", panel, "가방", 25, new(.03f, .75f), new(.47f, .85f), BlacksmithView.Ink);
            view.Text("TownChestHeading", panel, "보관함", 25, new(.53f, .75f), new(.97f, .85f), BlacksmithView.Ink);
        }

        sourceContent = view.Scroll(panel, "TownItems", delivery ? new(.03f, .16f) : new(.03f, .25f), new(.47f, .73f), 5, 105);
        emptySource = view.Text("EmptyBag", panel, "", 24,
            new(.05f, .33f), new(.45f, .58f), BlacksmithView.Ink);

        deliveryContent = chestContent = null;
        emptyChest = null;
        if (delivery)
        {
            deliveryContent = view.Scroll(panel, "DeliveryItems", new(.53f, .35f),
                new(.97f, .8f), 5, 105);
            DropArea(sourceContent, CurrentSource);
            DropArea(deliveryContent, owner.State.delivery);
            view.Text("DeliveryTitle", panel, "납품 예정 · 다음 날 아침 판매", 24,
                new(.53f, .80f), new(.96f, .87f), BlacksmithView.Ink);
            if (owner.State.pendingGold > 0)
                view.Button("CollectProceeds", panel,
                    $"판매 대금 {owner.State.pendingGold} G\n클릭하여 수령",
                    new(.58f, .19f), new(.92f, .32f),
                    () => { if (owner.Economy.Collect()) owner.Commit(); Show(true); });
            else
                view.Text("NoProceeds", panel, "수령할 대금이 없습니다.", 21,
                    new(.56f, .19f), new(.94f, .3f), BlacksmithView.Ink);
        }
        else
        {
            chestContent = view.Scroll(panel, "TownChestItems", new(.53f, .25f), new(.97f, .73f), 5, 105);
            emptyChest = view.Text("EmptyChest", panel, "보관함이 비어 있습니다.", 24,
                new(.55f, .33f), new(.95f, .58f), BlacksmithView.Ink);
            DropArea(sourceContent, SmithingLoop.Instance.SmithData.bag);
            DropArea(chestContent, SmithingLoop.Instance.SmithData.chest);
        }

        detail = view.Text("TownItemInfo", panel,
            "아이템을 선택하면 이름·가격·보유량을 확인합니다.", 22,
            new(.04f, .02f), delivery ? new(.96f, .13f) : new(.96f, .20f), BlacksmithView.Ink);
        transferHint = view.Text("TransferHint", panel, "", 18,
            delivery ? new(.03f, .13f) : new(.03f, .20f),
            delivery ? new(.97f, .19f) : new(.97f, .25f), BlacksmithView.Ink);

        RefreshLists();
    }

    List<Blacksmith.Stack> CurrentSource =>
        showChest ? SmithingLoop.Instance.SmithData.chest : SmithingLoop.Instance.SmithData.bag;

    void RefreshLists()
    {
        var source = CurrentSource;
        SyncRows(sourceContent, source, sourceSlots, false);
        if (delivery)
            SyncRows(deliveryContent, owner.State.delivery, deliverySlots, true);
        else
            SyncRows(chestContent, SmithingLoop.Instance.SmithData.chest, chestSlots, false);

        emptySource.text = showChest ? "보관함이 비어 있습니다." :
            "가방이 비어 있습니다.\n귀환한 채집물은 보관함에서 확인하세요.";
        emptySource.gameObject.SetActive(source.Count == 0);
        if (emptyChest != null)
            emptyChest.gameObject.SetActive(SmithingLoop.Instance.SmithData.chest.Count == 0);
        if (transferHint != null)
            transferHint.text = transferMessage ??
                (delivery ? "좌클릭 1개 · 길게/우클릭 전체 · 드래그로 이동" :
                    "가방과 보관함 사이에 아이템을 드래그해 옮기세요.");

        ShowSelectedDetail();
    }

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
                slot.count.text = stack.count.ToString();
                continue;
            }
            slot = Slot(content, stack, stacks, fromDelivery);
            if (slot != null) slots.Add(stack, slot);
        }
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
        text.fontSize = 19;
        text.alignment = TextAlignmentOptions.BottomRight;
        var icon = view.Image("Icon", button.transform, def.sprite, Color.white,
            new(.10f, .20f), new(.90f, .96f), true);
        var slot = button.gameObject.AddComponent<InventorySlotView>();
        slot.background = button.image;
        slot.icon = icon;
        slot.count = text;
        Action select = () => { selectedKey = stack.Key; ShowSelectedDetail(); };
        Action<int> move = count =>
        {
            var destination = fromDelivery ? CurrentSource : owner.State.delivery;
            Transfer(source, destination, stack, count);
        };
        slot.Bind(stack, def, view.Art(def.sprite), view.Art("bag_slot"), view.font,
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
        slot.EnableTransferDrag(() => source.Contains(stack) && stack.count > 0 &&
            (!delivery || (fromDelivery ? source == owner.State.delivery : source == CurrentSource)));
        button.gameObject.AddComponent<UiHoverOutline>();
        return slot;
    }
}
