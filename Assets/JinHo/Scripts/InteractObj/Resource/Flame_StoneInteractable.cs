using UnityEngine;

public class Flame_StoneInteractable : MonoBehaviour, IInteractable, IResourceProvider
{
    [Header("Resource")]
    [SerializeField] private ResourceData resourceData;
    [SerializeField, Min(1)] private int tier = 3;

    [Header("Tier Drop Chance")]
    [SerializeField, Range(0f, 1f)] private float oneTierGapDropChance = 0.75f;
    [SerializeField, Range(0f, 1f)] private float twoOrMoreTierGapDropChance = 0.4f;

    [Header("Drop")]
    [SerializeField] private ItemDropSpawner itemDropSpawner;
    [SerializeField] private Vector2 dropOffset = new(0f, 0.25f);
    [SerializeField, Min(0.1f)] private float dropRadius = 1f;

    [Header("Interaction")]
    [SerializeField, Min(1)] private int addItemInterval = 3;
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
        return toolData != null && toolData.ToolType == ToolType.Pickaxe;
    }

    public void Interact(PlayerInteraction interactionContext)
    {
        if (isDepleted || interactionContext == null)
            return;

        if (ResourceData == null || itemDropSpawner == null)
        {
            Debug.LogWarning("화염석 ResourceData 또는 ItemDropSpawner가 연결되지 않았습니다.");
            return;
        }

        interactCount++;
        Debug.Log($"화염석 상호작용 횟수: {interactCount}");

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
            $"화염석 티어 {Tier}, 도구 티어 {toolData.Tier}: " +
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
        float angleStep = Mathf.PI * 2f / Mathf.Max(1, dropCount);
        float angleJitter = Random.Range(-angleStep * 0.2f, angleStep * 0.2f);
        float angle = startAngle + angleStep * dropIndex + angleJitter;
        float distance = Random.Range(dropRadius * 0.5f, dropRadius);

        Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
        Vector2 randomOffset = direction * distance;

        return transform.position + (Vector3)(dropOffset + randomOffset);
    }
}
