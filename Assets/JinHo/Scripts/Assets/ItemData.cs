using UnityEngine;

[CreateAssetMenu(
    fileName = "NewItem",
    menuName = "Game/Item"
)]
public class ItemData : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string itemId;
    [SerializeField] private string itemName;

    [TextArea]
    [SerializeField] private string description;

    [Header("Properties")]
    [SerializeField] private float weight;

    [SerializeField] private float discountAssimilationRate; // 할인 동화율

    [Header("UI")]
    [SerializeField] private Sprite icon;

    public string ItemId => itemId;
    public string ItemName => itemName;
    public string Description => description;
    public float Weight => weight;
    public float DiscountAssimilationRate => discountAssimilationRate;
    public Sprite Icon => icon;
}
