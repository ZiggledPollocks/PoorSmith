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
    RectTransform sourceContent, deliveryContent;
    TMP_Text detail, emptySource, transferHint;
    string selectedKey, transferMessage;
    readonly Dictionary<Blacksmith.Stack, InventorySlotView> sourceSlots = new();
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
        selectedKey = transferMessage = null;
        sourceSlots.Clear();
        deliverySlots.Clear();
        var panel = ui.Open(delivery ? "납품 상자" : "마을 인벤토리");
        panel.GetComponent<Image>().color = BlacksmithView.Cream;

        view.Button("TownBagTab", panel, "가방", new(.03f, .75f), new(.23f, .85f),
            () => { showChest = false; Show(delivery); });
        view.Button("TownChestTab", panel, "보관함", new(.25f, .75f), new(.45f, .85f),
            () => { showChest = true; Show(delivery); });

        sourceContent = view.Scroll(panel, "TownItems", new(.03f, .16f), new(.47f, .73f), 5, 105);
        emptySource = view.Text("EmptyBag", panel, "", 24,
            new(.05f, .33f), new(.45f, .58f), BlacksmithView.Ink);

        deliveryContent = null;
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

        detail = view.Text("TownItemInfo", panel,
            "아이템을 선택하면 이름·가격·보유량을 확인합니다.", 22,
            delivery ? new(.04f, .02f) : new(.53f, .27f),
            delivery ? new(.96f, .13f) : new(.96f, .72f), BlacksmithView.Ink);
        if (delivery)
            transferHint = view.Text("TransferHint", panel, "", 18,
                new(.03f, .13f), new(.97f, .19f), BlacksmithView.Ink);
        else
            transferHint = null;

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

        emptySource.text = showChest ? "보관함이 비어 있습니다." :
            "가방이 비어 있습니다.\n귀환한 채집물은 보관함에서 확인하세요.";
        emptySource.gameObject.SetActive(source.Count == 0);
        if (transferHint != null)
            transferHint.text = transferMessage ??
                "좌클릭 1개 · 길게/우클릭 전체 · 드래그로 이동";

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
        var stack = source.Concat(owner.State.delivery)
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
            : $"{def.displayName}\n보유 {sourceCount} · 판매 기준가 {owner.Economy.Price(stack)} G\n{def.description}";
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
                $"{(showChest ? "보관함" : "가방")}으로 돌려놓았습니다.");
        RefreshLists();
    }

    void DropArea(RectTransform content, List<Blacksmith.Stack> destination)
    {
        var target = content.parent.gameObject.AddComponent<ItemDropTarget>();
        target.Drop = drag =>
        {
            var data = SmithingLoop.Instance.SmithData;
            var source = owner.State.delivery.Contains(drag.Stack)
                ? owner.State.delivery
                : data.bag.Contains(drag.Stack) ? data.bag : data.chest;
            Transfer(source, destination, drag.Stack, drag.Stack.count);
        };
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
        button.gameObject.AddComponent<UiHoverOutline>();
        return slot;
    }
}
