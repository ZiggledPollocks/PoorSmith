// [코드 지도] ItemDropInteractable: 생성된 아이템을 일정 지연 뒤 플레이어 인벤토리 위치로 흡수하고 획득한다. 이름과 달리 IInteractable을 구현하지 않으며 F/클릭 대상 계약이 없다. 물리 틱에서 거리·이동을 계산하고 인벤토리에 전량 들어갈 때만 자신을 제거한다.
// 주요 함수: FixedUpdate, TryCollect, FindPickupTarget
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Gathering/Scripts/Items/Drops/ItemDropInteractable.cs.md

using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class ItemDropInteractable : MonoBehaviour
{
    [SerializeField] private ItemData itemData;

    [Header("Automatic Pickup")]
    [SerializeField, Min(0f)] private float pickupEnableDelay = 1.5f;
    [SerializeField, Min(0.01f)] private float attractionDistance = 4f;
    [SerializeField, Min(0.01f)] private float pickupDistance = 0.35f;
    [SerializeField, Min(0.01f)] private float attractionSpeed = 8.5f;
    [SerializeField, Range(0.05f, 1f)] private float minimumScaleMultiplier = 0.25f;
    [SerializeField, Min(0.1f)] private float pickupRetryDelay = 0.75f;

    private static InventorySystem cachedInventory;

    private int amount;
    private Transform pickupTarget;
    private InventorySystem inventory;
    private Rigidbody2D rb;
    private Vector3 originalScale;
    private bool isAttracting;
    private float nextPickupAttemptTime;
    private float pickupEnableTime;
    private System.Func<ItemData, int, bool> alternatePickup;

    public ItemData ItemData => itemData;
    public int Amount => amount;

    public void ConfigureItemData(ItemData value) => itemData = value;
    public void ConfigureAlternatePickup(System.Func<ItemData, int, bool> pickup) => alternatePickup = pickup;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        originalScale = transform.localScale;
        FindPickupTarget();
    }

    // 핵심 분기: Time.time < pickupEnableTime 판정.
    // 상태 변경: transform.localScale 갱신.
    // 다음 연결: ItemDropInteractable.FindPickupTarget() 호출.
    private void FixedUpdate()
    {
        if (Time.time < pickupEnableTime)
            return;

        if (itemData == null || amount <= 0 || !FindPickupTarget())
            return;

        float distance = Vector2.Distance(rb.position, pickupTarget.position);

        if (distance > attractionDistance)
        {
            if (isAttracting)
                StopAttraction();

            return;
        }

        if (distance <= pickupDistance && TryCollect())
            return;

        BeginAttraction();

        float scaleRatio = Mathf.InverseLerp(
            pickupDistance,
            attractionDistance,
            distance);
        Vector3 minimumScale = originalScale * minimumScaleMultiplier;
        transform.localScale = Vector3.Lerp(
            minimumScale,
            originalScale,
            scaleRatio);

        Vector2 nextPosition = Vector2.MoveTowards(
            rb.position,
            pickupTarget.position,
            attractionSpeed * Time.fixedDeltaTime);

        rb.MovePosition(nextPosition);
    }

    public void Initialize(int newAmount)
    {
        amount = Mathf.Max(0, newAmount);
        pickupEnableTime = Time.time + pickupEnableDelay;

        if (itemData != null)
        {
            gameObject.name = $"{itemData.ItemName} x{amount}";
        }
    }

    private bool FindPickupTarget()
    {
        if (inventory != null && pickupTarget != null)
            return true;

        if (cachedInventory == null)
        {
            cachedInventory = FindFirstObjectByType<InventorySystem>();
        }

        inventory = cachedInventory;
        pickupTarget = inventory != null ? inventory.transform : null;
        return inventory != null && pickupTarget != null;
    }

    private void BeginAttraction()
    {
        if (isAttracting)
            return;

        isAttracting = true;
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.WakeUp();
    }

    private void StopAttraction()
    {
        isAttracting = false;
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.Sleep();
        transform.localScale = originalScale;
    }

    private bool TryCollect()
    {
        if (Time.time < nextPickupAttemptTime)
            return false;

        if (alternatePickup?.Invoke(itemData, amount) != true && !inventory.TryAddItem(itemData, amount))
        {
            nextPickupAttemptTime = Time.time + pickupRetryDelay;
            return false;
        }

        inventory.GetComponent<FieldHud>()?.ShowPickup(itemData.ItemName,amount);
        amount = 0;
        Destroy(gameObject);
        return true;
    }

    private void OnValidate()
    {
        pickupEnableDelay = Mathf.Max(0f, pickupEnableDelay);
        attractionDistance = Mathf.Max(0.01f, attractionDistance);
        pickupDistance = Mathf.Clamp(
            pickupDistance,
            0.01f,
            attractionDistance);
        attractionSpeed = Mathf.Max(0.01f, attractionSpeed);
        pickupRetryDelay = Mathf.Max(0.1f, pickupRetryDelay);
    }
}
