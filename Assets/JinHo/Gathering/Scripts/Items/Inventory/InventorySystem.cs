// [코드 지도] InventorySystem: 아이템 종류별 수량 목록과 무게 제한을 관리한다. 획득은 ItemDropInteractable, 제거·제물 이동은 인벤토리/제물 UI가 요청한다. 상태 변경 후 InventoryChanged로 UI에 알리며 월드 드롭 생성은 하지 않는다.
// 주요 함수: TryAddItem, RemoveItem, LogInventory
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Gathering/Scripts/Items/Inventory/InventorySystem.cs.md

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class InventorySystem : MonoBehaviour
{
    [SerializeField] private List<InventoryItem> items = new();
    [SerializeField, Min(0f)] private float maxWeight;
    [SerializeField] private float logInterval = 5f;

    private float logTimer;
    public float settingsWeight = 100f;

    public IReadOnlyList<InventoryItem> Items => items;
    public float MaxWeight => maxWeight;
    public float CurrentWeight => GetTotalWeight();
    public event Action InventoryChanged;

    void Awake()
    {
        maxWeight = settingsWeight * 1.05f;
    }

    public void ConfigureCapacity(float capacity,float allowance=.05f)
    {settingsWeight=Mathf.Max(0,capacity);maxWeight=settingsWeight*(1+Mathf.Clamp01(allowance));InventoryChanged?.Invoke();}

    private void Update()
    {
        logTimer += Time.deltaTime;

        if (logTimer >= logInterval)
        {
            logTimer = 0f;
            LogInventory();
        }
    }

    // 핵심 분기: items == null || items.Count == 0 판정.
    // 다음 연결: InventorySystem.GetTotalWeight() 호출.
    public void LogInventory()
    {
        float totalWeight = GetTotalWeight();

        if (items == null || items.Count == 0)
        {
            Debug.Log($"인벤토리 비어 있음 | 총 무게: {totalWeight:0.##}/{maxWeight:0.##}");
            return;
        }

        StringBuilder logText = new("현재 인벤토리: ");

        foreach (InventoryItem item in items)
        {
            if (item == null || item.itemData == null)
                continue;

            logText.Append(
                $"{item.itemData.ItemName}(수량: {item.quantity}, " +
                $"단위 무게: {item.itemData.Weight:0.##}, " +
                $"합계 무게: {item.TotalWeight:0.##}) "
            );
        }

        logText.Append($"| 총 무게: {totalWeight:0.##}/{maxWeight:0.##}");
        Debug.Log(logText.ToString());
    }

    public void AddItem(ItemData itemData, int amount = 1)
    {
        TryAddItem(itemData, amount);
    }

    // 핵심 분기: itemData == null || amount <= 0 판정.
    // 상태 변경: items 갱신.
    // 다음 연결: InventorySystem.GetTotalWeight() 호출.
    public bool TryAddItem(ItemData itemData, int amount = 1)
    {
        if (itemData == null || amount <= 0)
            return false;

        float currentWeight = GetTotalWeight();
        float weightAfterAdding = currentWeight + itemData.Weight * amount;

        // A float capacity such as 100 * 1.05 can be slightly below the
        // mathematically exact limit. Admit an item at that limit, but not
        // one that is meaningfully overweight.
        if (!InventoryStackLedger.FitsWeight(currentWeight, itemData.Weight, amount, maxWeight))
        {
            Debug.LogWarning(
                $"{itemData.ItemName} {amount}개를 담을 수 없습니다. " +
                $"예상 무게: {weightAfterAdding:0.##}, 한계 무게: {maxWeight:0.##}"
            );
            return false;
        }

        items ??= new List<InventoryItem>();
        if (!InventoryStackLedger.TryAdd(items, itemData, amount,
            x => x.itemData, x => x.quantity, (x, n) => x.quantity = n,
            (data, n) => new InventoryItem(data, n))) return false;

        Debug.Log(
            $"{itemData.ItemName} {amount}개를 획득했습니다. " +
            $"총 무게: {weightAfterAdding:0.##}/{maxWeight:0.##}"
        );
        InventoryChanged?.Invoke();
        return true;
    }

    // 핵심 분기: itemData == null || amount <= 0 || items == null 판정.
    // 상태 변경: x.quantity 갱신.
    // 다음 연결: InventoryStackLedger.Count<TStack, TKey>(System.Collections.Generic.IEnumerable<TStack>, TKey, System.Func<TS… 호출.
    public bool RemoveItem(ItemData itemData, int amount = 1)
    {
        if (itemData == null || amount <= 0 || items == null)
            return false;

        long available = InventoryStackLedger.Count(items, itemData, x => x.itemData, x => x.quantity);
        if (available == 0)
        {
            Debug.LogWarning($"{itemData.ItemName}이(가) 인벤토리에 없습니다.");
            return false;
        }

        if (available < amount)
        {
            Debug.LogWarning(
                $"{itemData.ItemName}의 수량이 부족합니다. " +
                $"보유: {available}, 요청: {amount}"
            );
            return false;
        }

        if (!InventoryStackLedger.TryRemove(items, itemData, amount,
            x => x.itemData, x => x.quantity, (x, n) => x.quantity = n)) return false;

        Debug.Log(
            $"{itemData.ItemName} {amount}개를 제거했습니다. " +
            $"총 무게: {GetTotalWeight():0.##}/{maxWeight:0.##}"
        );
        InventoryChanged?.Invoke();
        return true;
    }

    public void RestoreSnapshot(IReadOnlyList<InventoryItem> snapshot)
    {
        // Restore previously owned items even if carrying capacity was subsequently reduced.
        items=InventoryStackLedger.CopySnapshot(snapshot,
            item => item?.itemData != null && item.quantity > 0,
            item => new InventoryItem(item.itemData, item.quantity));
        InventoryChanged?.Invoke();
    }

    public bool TryConsume(IReadOnlyList<InventoryItem> costs)
    {
        if(costs==null)return false;
        var totals=new Dictionary<ItemData,int>();
        foreach(var cost in costs)
        {
            if(cost==null||cost.itemData==null||cost.quantity<=0)return false;
            if(!InventoryRequirements.TryAdd(totals,cost.itemData,cost.quantity))return false;
        }
        if(!InventoryStackLedger.TryConsume(items,totals,
            x=>x.itemData,x=>x.quantity,(x,n)=>x.quantity=n))return false;
        InventoryChanged?.Invoke();return true;
    }

    public int GetItemCount(ItemData itemData)
    {
        if (itemData == null || items == null)
            return 0;

        return (int)Math.Min(int.MaxValue,
            InventoryStackLedger.Count(items, itemData, x => x.itemData, x => x.quantity));
    }

    public bool HasItem(ItemData itemData, int amount = 1)
    {
        return GetItemCount(itemData) >= amount;
    }

    public float GetTotalWeight()
    {
        return InventoryStackLedger.TotalWeight(items,
            item => item.itemData == null ? 0f : item.TotalWeight);
    }
}
