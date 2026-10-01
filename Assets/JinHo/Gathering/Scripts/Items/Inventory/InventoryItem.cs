// [코드 지도] InventoryItem: 인벤토리 한 항목의 아이템 정의와 수량을 묶는 일반 C
// 주요 함수: InventoryItem
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Gathering/Scripts/Items/Inventory/InventoryItem.cs.md

using System;

[Serializable]
public class InventoryItem
{
    public ItemData itemData;
    public int quantity;

    public InventoryItem(ItemData itemData, int quantity)
    {
        this.itemData = itemData;
        this.quantity = quantity;
    }

    public float TotalWeight
    {
        get
        {
            return itemData.Weight * quantity;
        }
    }
}
