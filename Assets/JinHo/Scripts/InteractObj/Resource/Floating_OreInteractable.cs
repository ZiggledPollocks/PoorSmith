using UnityEngine;

public class Floating_OreInteractable : MonoBehaviour, IInteractable, IResourceProvider
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
    [SerializeField, Min(1)] private int maxInteractCount = 8;

    private readonly ResourceHarvestWorkflow harvest = new();

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
            dropOffset, dropRadius, "부유석", false);
    }

}
