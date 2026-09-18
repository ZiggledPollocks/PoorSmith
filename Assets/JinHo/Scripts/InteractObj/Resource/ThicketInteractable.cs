using UnityEngine;

public class ThicketInteractable : MonoBehaviour, IInteractable, IResourceProvider
{
    [Header("Resource")]
    [SerializeField] private ResourceData resourceData;
    [SerializeField, Min(1)] private int tier = 1;

    [Header("Tier Drop Chance")]
    [SerializeField, Range(0f, 1f)] private float oneTierGapDropChance = 0.75f;
    [SerializeField, Range(0f, 1f)] private float twoOrMoreTierGapDropChance = 0.4f;

    [Header("Drop")]
    [SerializeField] private ItemDropSpawner itemDropSpawner;
    [SerializeField] private Vector2 dropOffset = new(0f, 0.25f);
    [SerializeField, Min(0.1f)] private float dropRadius = 1f;

    [Header("Interaction")]
    [SerializeField, Min(1)] private int addItemInterval = 2;
    [SerializeField, Min(1)] private int maxInteractCount = 4;

    private int interactCount;
    private bool isDepleted;

    public ResourceData ResourceData => resourceData;
    public int Tier => Mathf.Max(1, tier);
    public int AddItemInterval
    {
        get => addItemInterval;
        set => addItemInterval = Mathf.Max(1, value);
    }
    public int MaxInteractCount => Mathf.Max(1, maxInteractCount);

    public bool CanInteract()
    {
        return !isDepleted;
    }

    public bool CanUseTool(ToolData toolData)
    {
        return toolData != null && toolData.ToolType == ToolType.Axe;
    }

    public void Interact(PlayerInteraction interactionContext)
    {
        if (isDepleted || interactionContext == null)
            return;

        if (ResourceData == null || itemDropSpawner == null)
        {
            Debug.LogWarning("덤불 ResourceData 또는 ItemDropSpawner가 연결되지 않았습니다.");
            return;
        }

        interactCount++;
        Debug.Log($"덤불 상호작용 횟수: {interactCount}");

        if (interactCount % AddItemInterval == 0)
        {
            if (ShouldDropResource(interactionContext.CurrentTool))
            {
                SpawnResourceDrops();
            }
        }

        if (interactCount >= MaxInteractCount)
        {
            isDepleted = true;
            Destroy(gameObject);
        }
    }

    private bool ShouldDropResource(ToolData toolData)
    {
        if (toolData == null)
            return false;

        int tierGap = Tier - toolData.Tier;

        if (tierGap <= 0)
            return true;

        float dropChance = tierGap == 1
            ? oneTierGapDropChance
            : twoOrMoreTierGapDropChance;

        bool shouldDrop = Random.value <= dropChance;

        Debug.Log(
            $"덤불 티어 {Tier}, 도구 티어 {toolData.Tier}: " +
            $"드롭 확률 {dropChance:P0} - {(shouldDrop ? "성공" : "실패")}"
        );

        return shouldDrop;
    }

    private void SpawnResourceDrops()
    {
        int dropCount = ResourceData.DropCount;
        float startAngle = Random.Range(0f, Mathf.PI * 2f);

        for (int i = 0; i < dropCount; i++)
        {
            if (!ResourceData.TryGetDrop(i, out GameObject prefab, out int amount))
                continue;

            Vector3 dropPosition = GetRandomDropPosition(i, dropCount, startAngle);
            itemDropSpawner.Spawn(prefab, dropPosition, amount);
        }
    }

    private Vector3 GetRandomDropPosition(
        int dropIndex,
        int dropCount,
        float startAngle)
    {
        int safeDropCount = Mathf.Max(1, dropCount);
        float angleStep = Mathf.PI * 2f / safeDropCount;

        if (safeDropCount == 1)
        {
            float angleJitter = Random.Range(-angleStep * 0.2f, angleStep * 0.2f);
            float angle = startAngle + angleJitter;
            float distance = Random.Range(dropRadius * 0.5f, dropRadius);
            Vector2 singleOffset = new(Mathf.Cos(angle), Mathf.Sin(angle));
            return transform.position + (Vector3)(dropOffset + singleOffset * distance);
        }

        const float minimumDropSeparation = 1.1f;
        float minimumRadius = minimumDropSeparation /
                              (2f * Mathf.Sin(Mathf.PI / safeDropCount));
        float arrangedRadius = Mathf.Max(dropRadius, minimumRadius);
        float arrangedAngle = startAngle + angleStep * dropIndex;

        Vector2 direction = new(Mathf.Cos(arrangedAngle), Mathf.Sin(arrangedAngle));
        Vector2 arrangedOffset = direction * arrangedRadius;

        return transform.position + (Vector3)(dropOffset + arrangedOffset);
    }
}
