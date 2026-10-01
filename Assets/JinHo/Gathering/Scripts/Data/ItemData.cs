// [코드 지도] ItemData: 아이템의 정적 정의를 보관하는 ScriptableObject다. 실제 보유 수량은 InventoryItem, 목록과 무게 제한은 InventorySystem이 관리한다. 따라서 같은 아이템 정의를 여러 스택이 공유할 수 있다.
// 주요 함수: ConfigureBridge, ItemId, ItemName
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Gathering/Scripts/Data/ItemData.cs.md

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

    [Header("Restrictions")]
    [Tooltip("활성화하면 인벤토리 UI에서 이 아이템을 버릴 수 없습니다.")]
    [SerializeField] private bool preventDiscard;

    [Tooltip("활성화하면 AssissZone UI의 StoneBasket에 이 아이템을 담을 수 없습니다.")]
    [SerializeField] private bool preventStoneBasketOffering;

    [Header("UI")]
    [SerializeField] private Sprite icon;

    public void ConfigureBridge(string id,string label,string info,float mass,Sprite art)
    {itemId=id;itemName=label;description=info;weight=Mathf.Max(0,mass);icon=art;}

    public string ItemId => itemId;
    public string ItemName => itemName;
    public string Description => description;
    public float Weight => weight;
    public float DiscountAssimilationRate => discountAssimilationRate;
    public bool CanDiscard => !preventDiscard;
    public bool CanOfferToStoneBasket => !preventStoneBasketOffering;
    public Sprite Icon => icon;
}
