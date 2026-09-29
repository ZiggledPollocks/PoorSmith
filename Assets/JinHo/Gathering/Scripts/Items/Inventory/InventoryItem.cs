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
