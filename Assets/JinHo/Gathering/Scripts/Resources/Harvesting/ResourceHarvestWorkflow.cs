// [코드 지도] ResourceHarvestWorkflow: 자원별로 중복되던 채집 횟수 누적, 등급 차이에 따른 드롭 확률, 드롭 배치, 소진 후 파괴를 담당한다. 자원 MonoBehaviour는 직렬화 데이터와 도구 허용 정책을 유지한다.
// 주요 함수: Interact, GetRandomDropPosition, ShouldDropResource
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Gathering/Scripts/Resources/Harvesting/ResourceHarvestWorkflow.cs.md

using UnityEngine;

public interface IResourceDropSpawnerReceiver
{
    void SetItemDropSpawner(ItemDropSpawner spawner);
}

/// <summary>Shared harvest sequence; resource components retain serialized identity and defaults.</summary>
public sealed class ResourceHarvestWorkflow
{
    private int interactCount;
    private bool isDepleted;

    public bool CanInteract()
    {
        return !isDepleted;
    }

    // 핵심 분기: isDepleted || interactionContext == null 판정.
    // 상태 변경: itemDropSpawner 갱신.
    // 다음 연결: CombatHitFeedback2D.PlayResource(UnityEngine.GameObject, bool, bool) 호출.
    public void Interact(PlayerInteraction interactionContext, GameObject owner, ResourceData resourceData,
        ref ItemDropSpawner itemDropSpawner, int tier, int addItemInterval, int maxInteractCount,
        float oneTierGapDropChance, float twoOrMoreTierGapDropChance, Vector2 dropOffset, float dropRadius,
        string label, bool resolveMissingSpawner)
    {
        if (isDepleted || interactionContext == null)
            return;

        if (resolveMissingSpawner && itemDropSpawner == null)
            itemDropSpawner = Object.FindFirstObjectByType<ItemDropSpawner>();

        if (resourceData == null || itemDropSpawner == null)
        {
            Debug.LogWarning($"{label} ResourceData 또는 ItemDropSpawner가 연결되지 않았습니다.");
            return;
        }

        var tool=interactionContext.CurrentTool;
        if(tool!=null&&tool.Tier<tier)
        {
            CombatHitFeedback2D.PlayResource(owner,tool.ToolType==ToolType.Axe,false);
            interactionContext.GetComponent<FieldHud>()?.ShowToolHint(
                $"이 {ToolName(tool.ToolType)}로는 채집이 어렵습니다", owner.transform.position);
            float chance=tool.Tier==1?.05f:.10f;
            if(Random.value<chance)SpawnResourceDrops(owner.transform.position,resourceData,itemDropSpawner,dropOffset,dropRadius);
            return; // An under-tier attempt never depletes the resource.
        }
        interactCount++;
        CombatHitFeedback2D.PlayResource(owner,tool?.ToolType==ToolType.Axe,true);
        Debug.Log($"{label} 상호작용 횟수: {interactCount}");

        if (interactCount % addItemInterval == 0)
        {
            if (ShouldDropResource(interactionContext.CurrentTool, tier, oneTierGapDropChance, twoOrMoreTierGapDropChance, label))
            {
                SpawnResourceDrops(owner.transform.position, resourceData, itemDropSpawner, dropOffset, dropRadius);
            }
        }

        if (interactCount >= maxInteractCount)
        {
            isDepleted = true;
            Object.Destroy(owner);
        }
    }

    private static string ToolName(ToolType type) => type == ToolType.Axe ? "도끼" : "곡괭이";

    // 핵심 분기: toolData == null 판정.
    private static bool ShouldDropResource(ToolData toolData, int tier, float oneTierGapDropChance,
        float twoOrMoreTierGapDropChance, string label)
    {
        if (toolData == null)
            return false;

        int tierGap = tier - toolData.Tier;

        if (tierGap <= 0)
            return true;

        float dropChance = tierGap == 1
            ? oneTierGapDropChance
            : twoOrMoreTierGapDropChance;

        bool shouldDrop = Random.value <= dropChance;

        Debug.Log(
            $"{label} 티어 {tier}, 도구 티어 {toolData.Tier}: " +
            $"드롭 확률 {dropChance:P0} - {(shouldDrop ? "성공" : "실패")}"
        );

        return shouldDrop;
    }

    private static void SpawnResourceDrops(Vector3 position, ResourceData resourceData,
        ItemDropSpawner itemDropSpawner, Vector2 dropOffset, float dropRadius)
    {
        int dropCount = resourceData.DropCount;
        float startAngle = Random.Range(0f, Mathf.PI * 2f);

        for (int i = 0; i < dropCount; i++)
        {
            if (!resourceData.TryGetDrop(i, out GameObject prefab, out int amount))
                continue;

            Vector3 dropPosition = GetRandomDropPosition(position, dropOffset, dropRadius, i, dropCount, startAngle);
            itemDropSpawner.Spawn(prefab, dropPosition, amount);
        }
    }

    // 핵심 분기: safeDropCount == 1 판정.
    private static Vector3 GetRandomDropPosition(
        Vector3 position, Vector2 dropOffset, float dropRadius, int dropIndex,
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
            return position + (Vector3)(dropOffset + singleOffset * distance);
        }

        const float minimumDropSeparation = 1.1f;
        float minimumRadius = minimumDropSeparation /
                              (2f * Mathf.Sin(Mathf.PI / safeDropCount));
        float arrangedRadius = Mathf.Max(dropRadius, minimumRadius);
        float arrangedAngle = startAngle + angleStep * dropIndex;

        Vector2 direction = new(Mathf.Cos(arrangedAngle), Mathf.Sin(arrangedAngle));
        Vector2 arrangedOffset = direction * arrangedRadius;

        return position + (Vector3)(dropOffset + arrangedOffset);
    }
}
