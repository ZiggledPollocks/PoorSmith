// [코드 지도] SteelInteractable: SteelInteractable 자원의 Inspector 데이터와 도구 허용 정책을 유지한다. 실제 채집 횟수·확률·드롭·소진은 이 객체가 소유한 ResourceHarvestWorkflow에 위임한다.
// 주요 함수: Interact, CanInteract, CanUseTool
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Gathering/Scripts/Resources/SteelInteractable.cs.md

using UnityEngine;

public class SteelInteractable : MonoBehaviour, IInteractable, IResourceProvider, IResourceDropSpawnerReceiver
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
    [SerializeField, Min(1)] private int addItemInterval = 3;
    [SerializeField, Min(1)] private int maxInteractCount = 8;

    private readonly ResourceHarvestWorkflow harvest = new();

    public void SetItemDropSpawner(ItemDropSpawner spawner) => itemDropSpawner = spawner;

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
        return harvest.CanInteract();
    }

    public bool CanUseTool(ToolData toolData)
    {
        return toolData != null && toolData.ToolType == ToolType.Pickaxe;
    }

    public void Interact(PlayerInteraction interactionContext)
    {
        harvest.Interact(interactionContext, gameObject, ResourceData, ref itemDropSpawner,
            Tier, AddItemInterval, MaxInteractCount, oneTierGapDropChance, twoOrMoreTierGapDropChance,
            dropOffset, dropRadius, "강철", true);
    }

}
