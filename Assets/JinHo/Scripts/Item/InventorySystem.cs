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
        maxWeight = settingsWeight + settingsWeight * 0.2f;
    }

    private void Update()
    {
        logTimer += Time.deltaTime;

        if (logTimer >= logInterval)
        {
            logTimer = 0f;
            LogInventory();
        }
    }

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

    public bool TryAddItem(ItemData itemData, int amount = 1)
    {
        if (itemData == null || amount <= 0)
            return false;

        float currentWeight = GetTotalWeight();
        float addedWeight = itemData.Weight * amount;
        float weightAfterAdding = currentWeight + addedWeight;

        if (weightAfterAdding > maxWeight)
        {
            Debug.LogWarning(
                $"{itemData.ItemName} {amount}개를 담을 수 없습니다. " +
                $"예상 무게: {weightAfterAdding:0.##}, 한계 무게: {maxWeight:0.##}"
            );
            return false;
        }

        items ??= new List<InventoryItem>();
        InventoryItem existingItem = items.Find(i => i.itemData == itemData);

        if (existingItem != null)
        {
            existingItem.quantity += amount;
        }
        else
        {
            items.Add(new InventoryItem(itemData, amount));
        }

        Debug.Log(
            $"{itemData.ItemName} {amount}개를 획득했습니다. " +
            $"총 무게: {weightAfterAdding:0.##}/{maxWeight:0.##}"
        );
        InventoryChanged?.Invoke();
        return true;
    }

    public bool RemoveItem(ItemData itemData, int amount = 1)
    {
        if (itemData == null || amount <= 0 || items == null)
            return false;

        InventoryItem existingItem = items.Find(i => i.itemData == itemData);

        if (existingItem == null)
        {
            Debug.LogWarning($"{itemData.ItemName}이(가) 인벤토리에 없습니다.");
            return false;
        }

        if (existingItem.quantity < amount)
        {
            Debug.LogWarning(
                $"{itemData.ItemName}의 수량이 부족합니다. " +
                $"보유: {existingItem.quantity}, 요청: {amount}"
            );
            return false;
        }

        existingItem.quantity -= amount;

        if (existingItem.quantity <= 0)
        {
            items.Remove(existingItem);
        }

        Debug.Log(
            $"{itemData.ItemName} {amount}개를 제거했습니다. " +
            $"총 무게: {GetTotalWeight():0.##}/{maxWeight:0.##}"
        );
        InventoryChanged?.Invoke();
        return true;
    }

    public int GetItemCount(ItemData itemData)
    {
        if (itemData == null || items == null)
            return 0;

        InventoryItem existingItem = items.Find(i => i.itemData == itemData);
        return existingItem == null ? 0 : existingItem.quantity;
    }

    public bool HasItem(ItemData itemData, int amount = 1)
    {
        return GetItemCount(itemData) >= amount;
    }

    public float GetTotalWeight()
    {
        if (items == null)
            return 0f;

        float totalWeight = 0f;

        foreach (InventoryItem item in items)
        {
            if (item == null || item.itemData == null)
                continue;

            totalWeight += item.TotalWeight;
        }

        return totalWeight;
    }
}
