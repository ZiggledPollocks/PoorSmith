using UnityEngine;

public class ItemDropInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private ItemData itemData;

    private int amount;

    public ItemData ItemData => itemData;
    public int Amount => amount;

    public void Initialize(int newAmount)
    {
        amount = Mathf.Max(0, newAmount);

        if (itemData != null)
        {
            gameObject.name = $"{itemData.ItemName} x{amount}";
        }
    }

    public bool CanInteract()
    {
        return itemData != null && amount > 0;
    }

    public bool CanUseTool(ToolData toolData)
    {
        return true;
    }

    public void Interact(PlayerInteraction interactionContext)
    {
        if (!CanInteract() || interactionContext == null || interactionContext.Inventory == null)
            return;

        if (!interactionContext.Inventory.TryAddItem(itemData, amount))
            return;

        amount = 0;
        Destroy(gameObject);
    }
}
