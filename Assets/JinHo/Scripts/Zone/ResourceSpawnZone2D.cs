using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class ResourceSpawnZone2D : MonoBehaviour
{
    [Header("Spawn Area")]
    [SerializeField] private Collider2D spawnArea;
    [SerializeField] private Transform spawnParent;

    [Header("Resources")]
    [SerializeField] private List<GameObject> resourcePrefabs = new();
    [SerializeField, Min(0)] private int targetResourceCount = 10;
    [SerializeField, Min(0.01f)] private float spawnInterval = 5f;
    [SerializeField] private bool spawnImmediately = true;

    [Header("Placement")]
    [SerializeField, Min(1)] private int maximumPlacementAttempts = 30;
    [SerializeField, Min(0f)] private float imageSpacing = 0.1f;

    private readonly List<Collider2D> overlapResults = new();
    private readonly HashSet<Component> countedResources = new();
    private readonly List<Bounds> occupiedImageBounds = new();

    private ContactFilter2D resourceFilter;
    private int resourceLayer = -1;
    private float nextSpawnTime;

    public int CurrentResourceCount => CountResourcesInArea();

    private void Awake()
    {
        if (spawnArea == null)
            spawnArea = GetComponent<Collider2D>();

        resourceLayer = LayerMask.NameToLayer("Resource");

        if (resourceLayer < 0)
        {
            Debug.LogError(
                "Resource 레이어가 Project Settings에 등록되어 있지 않습니다.",
                this);
            enabled = false;
            return;
        }

        resourceFilter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = 1 << resourceLayer,
            useTriggers = true
        };
    }

    private void OnEnable()
    {
        nextSpawnTime = spawnImmediately
            ? Time.time
            : Time.time + spawnInterval;
    }

    private void Update()
    {
        if (Time.time < nextSpawnTime)
            return;

        nextSpawnTime = Time.time + spawnInterval;

        if (CountResourcesInArea() >= targetResourceCount)
            return;

        TrySpawnResource();
    }

    public bool TrySpawnResource()
    {
        if (!CanSpawn())
            return false;

        BuildOccupiedImageBounds();

        for (int attempt = 0; attempt < maximumPlacementAttempts; attempt++)
        {
            GameObject prefab = GetRandomValidPrefab();

            if (prefab == null)
                return false;

            if (!TryGetRandomPointInArea(out Vector2 spawnPosition))
                continue;

            if (!TryGetPrefabImageBounds(
                    prefab,
                    spawnPosition,
                    out Bounds candidateBounds))
            {
                continue;
            }

            if (!IsCompletelyInsideArea(candidateBounds) ||
                OverlapsExistingImage(candidateBounds))
            {
                continue;
            }

            GameObject instance = Instantiate(
                prefab,
                spawnPosition,
                Quaternion.identity,
                spawnParent);

            SetLayerRecursively(instance.transform, resourceLayer);
            return true;
        }

        Debug.LogWarning(
            $"{name}: {maximumPlacementAttempts}번 시도했지만 겹치지 않는 자원 위치를 찾지 못했습니다.",
            this);
        return false;
    }

    public int CountResourcesInArea()
    {
        if (spawnArea == null || !spawnArea.enabled || resourceLayer < 0)
            return 0;

        overlapResults.Clear();
        countedResources.Clear();
        spawnArea.Overlap(resourceFilter, overlapResults);

        foreach (Collider2D hit in overlapResults)
        {
            if (hit == null || hit == spawnArea)
                continue;

            IInteractable interactable = hit.GetComponentInParent<IInteractable>();

            if (interactable is Component resourceComponent)
                countedResources.Add(resourceComponent);
        }

        return countedResources.Count;
    }

    private bool CanSpawn()
    {
        if (spawnArea == null || !spawnArea.enabled)
        {
            Debug.LogWarning($"{name}: 사용할 Collider2D가 없습니다.", this);
            return false;
        }

        if (resourcePrefabs == null || resourcePrefabs.Count == 0)
        {
            Debug.LogWarning($"{name}: Resource Prefabs가 비어 있습니다.", this);
            return false;
        }

        return targetResourceCount > 0;
    }

    private GameObject GetRandomValidPrefab()
    {
        int startIndex = Random.Range(0, resourcePrefabs.Count);

        for (int offset = 0; offset < resourcePrefabs.Count; offset++)
        {
            GameObject prefab = resourcePrefabs[
                (startIndex + offset) % resourcePrefabs.Count];

            if (prefab != null &&
                prefab.GetComponentInChildren<SpriteRenderer>(true) != null)
            {
                return prefab;
            }
        }

        Debug.LogWarning(
            $"{name}: SpriteRenderer가 있는 유효한 Resource Prefab이 없습니다.",
            this);
        return null;
    }

    private bool TryGetRandomPointInArea(out Vector2 point)
    {
        Bounds areaBounds = spawnArea.bounds;

        for (int attempt = 0; attempt < maximumPlacementAttempts; attempt++)
        {
            point = new Vector2(
                Random.Range(areaBounds.min.x, areaBounds.max.x),
                Random.Range(areaBounds.min.y, areaBounds.max.y));

            if (spawnArea.OverlapPoint(point))
                return true;
        }

        point = default;
        return false;
    }

    private void BuildOccupiedImageBounds()
    {
        occupiedImageBounds.Clear();
        overlapResults.Clear();
        spawnArea.Overlap(resourceFilter, overlapResults);

        foreach (Collider2D hit in overlapResults)
        {
            if (hit == null || hit == spawnArea)
                continue;

            IInteractable interactable = hit.GetComponentInParent<IInteractable>();

            if (interactable is not Component resourceComponent)
                continue;

            if (TryGetCombinedRendererBounds(
                    resourceComponent.gameObject,
                    out Bounds imageBounds))
            {
                occupiedImageBounds.Add(imageBounds);
            }
        }
    }

    private bool TryGetPrefabImageBounds(
        GameObject prefab,
        Vector2 spawnPosition,
        out Bounds imageBounds)
    {
        if (!TryGetCombinedRendererBounds(prefab, out Bounds prefabBounds))
        {
            imageBounds = default;
            return false;
        }

        Vector3 offsetFromRoot =
            prefabBounds.center - prefab.transform.position;

        imageBounds = new Bounds(
            (Vector3)spawnPosition + offsetFromRoot,
            prefabBounds.size);
        imageBounds.Expand(imageSpacing * 2f);
        return true;
    }

    private static bool TryGetCombinedRendererBounds(
        GameObject target,
        out Bounds combinedBounds)
    {
        SpriteRenderer[] renderers =
            target.GetComponentsInChildren<SpriteRenderer>(true);

        combinedBounds = default;
        bool foundRenderer = false;

        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer == null || renderer.sprite == null)
                continue;

            if (!foundRenderer)
            {
                combinedBounds = renderer.bounds;
                foundRenderer = true;
                continue;
            }

            combinedBounds.Encapsulate(renderer.bounds);
        }

        return foundRenderer;
    }

    private bool IsCompletelyInsideArea(Bounds imageBounds)
    {
        Vector2 min = imageBounds.min;
        Vector2 max = imageBounds.max;

        return spawnArea.OverlapPoint(new Vector2(min.x, min.y)) &&
               spawnArea.OverlapPoint(new Vector2(min.x, max.y)) &&
               spawnArea.OverlapPoint(new Vector2(max.x, min.y)) &&
               spawnArea.OverlapPoint(new Vector2(max.x, max.y));
    }

    private bool OverlapsExistingImage(Bounds candidateBounds)
    {
        foreach (Bounds occupiedBounds in occupiedImageBounds)
        {
            if (candidateBounds.Intersects(occupiedBounds))
                return true;
        }

        return false;
    }

    private static void SetLayerRecursively(Transform target, int layer)
    {
        target.gameObject.layer = layer;

        for (int i = 0; i < target.childCount; i++)
            SetLayerRecursively(target.GetChild(i), layer);
    }

    private void OnValidate()
    {
        targetResourceCount = Mathf.Max(0, targetResourceCount);
        spawnInterval = Mathf.Max(0.01f, spawnInterval);
        maximumPlacementAttempts = Mathf.Max(1, maximumPlacementAttempts);
        imageSpacing = Mathf.Max(0f, imageSpacing);

        if (spawnArea == null)
            spawnArea = GetComponent<Collider2D>();
    }
}
