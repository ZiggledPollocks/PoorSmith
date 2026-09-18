using System.Collections.Generic;
using UnityEngine;

public class ResourceSpawnZone2D : MonoBehaviour
{
    [Header("Spawn Area")]
    [SerializeField] private Collider2D spawnArea;
    [SerializeField] private Transform spawnParent;
    [SerializeField] private Camera playerCamera;

    [Header("Resource")]
    [SerializeField] private List<GameObject> resourcePrefabs = new();
    [SerializeField, Min(0)] private int targetResourceCount = 4;
    [SerializeField, Min(0f)] private float spawnInterval = 5f;

    [Header("Placement")]
    [SerializeField] private LayerMask groundLayers;
    [SerializeField, Min(1)] private int maximumPlacementAttempts = 50;
    [SerializeField, Min(0f)] private float imageSpacing = 0.25f;
    [SerializeField, Min(0f)] private float groundRayStartPadding = 1f;
    [SerializeField, Min(0.01f)] private float groundRayDistance = 50f;
    [SerializeField, Min(0f)] private float groundClearance = 0.02f;
    [SerializeField, Range(0f, 1f)] private float minimumGroundNormalY = 0.65f;

    [Header("Camera Visibility")]
    [SerializeField, Min(0f)] private float cameraBoundsPadding = 0.15f;
    [SerializeField, Min(0.02f)] private float visibilityRetryInterval = 0.25f;

    private readonly HashSet<GameObject> countedResources = new();
    private int resourceLayer;
    private int resourceLayerMask;
    private bool initialPopulationComplete;
    private bool replacementScheduled;
    private bool configurationWarningShown;
    private float nextSpawnTime;

    public int TargetResourceCount => targetResourceCount;
    public float SpawnInterval => spawnInterval;
    public Collider2D SpawnArea => spawnArea;
    public GameObject ResourcePrefab => resourcePrefabs.Count > 0 ? resourcePrefabs[0] : null;
    public int CurrentResourceCount => CountResourcesInArea();
    public bool InitialPopulationComplete => initialPopulationComplete;

    private void Awake() => InitializeReferences();

    private void OnEnable()
    {
        initialPopulationComplete = false;
        replacementScheduled = false;
        nextSpawnTime = Time.time;
    }

    private void Start() => TryPopulateInitialResources();

    private void Update()
    {
        if (!HasValidConfiguration())
            return;

        int currentCount = CountResourcesInArea();
        if (!initialPopulationComplete)
        {
            if (currentCount >= targetResourceCount)
            {
                initialPopulationComplete = true;
                replacementScheduled = false;
            }
            else if (Time.time >= nextSpawnTime)
            {
                TryPopulateInitialResources();
            }
            return;
        }

        if (currentCount >= targetResourceCount)
        {
            replacementScheduled = false;
            return;
        }

        if (!replacementScheduled)
        {
            replacementScheduled = true;
            nextSpawnTime = Time.time + spawnInterval;
            return;
        }

        if (Time.time < nextSpawnTime)
            return;

        if (TrySpawnResource())
        {
            replacementScheduled = CountResourcesInArea() < targetResourceCount;
            nextSpawnTime = Time.time + spawnInterval;
        }
        else
        {
            nextSpawnTime = Time.time + visibilityRetryInterval;
        }
    }

    public void Configure(Collider2D area, Transform parent, Camera camera,
        GameObject resourcePrefab, int targetCount, float interval, LayerMask groundMask)
    {
        spawnArea = area;
        spawnParent = parent;
        playerCamera = camera;
        resourcePrefabs.Clear();
        if (resourcePrefab != null)
            resourcePrefabs.Add(resourcePrefab);
        targetResourceCount = Mathf.Max(0, targetCount);
        spawnInterval = Mathf.Max(0f, interval);
        groundLayers = groundMask;
        InitializeReferences();
    }

    private void InitializeReferences()
    {
        if (spawnArea == null)
            spawnArea = GetComponent<Collider2D>();
        if (spawnParent == null)
            spawnParent = transform;
        if (playerCamera == null)
            playerCamera = Camera.main;

        resourceLayer = LayerMask.NameToLayer("Resource");
        resourceLayerMask = resourceLayer >= 0 ? 1 << resourceLayer : 0;
        if (groundLayers.value == 0)
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer >= 0)
                groundLayers = 1 << groundLayer;
        }
    }

    private bool HasValidConfiguration()
    {
        bool valid = spawnArea != null && resourcePrefabs.Count > 0
            && resourceLayer >= 0 && groundLayers.value != 0;
        if (!valid && !configurationWarningShown)
        {
            configurationWarningShown = true;
            Debug.LogWarning($"{name}: ResourceSpawnZone2D 설정을 확인하세요.", this);
        }
        return valid;
    }

    private void TryPopulateInitialResources()
    {
        if (!HasValidConfiguration())
            return;

        int missingCount = Mathf.Max(0, targetResourceCount - CountResourcesInArea());
        for (int i = 0; i < missingCount; i++)
        {
            if (!TrySpawnResource())
                break;
        }
        initialPopulationComplete = CountResourcesInArea() >= targetResourceCount;
        nextSpawnTime = Time.time + visibilityRetryInterval;
    }

    private bool TrySpawnResource()
    {
        Bounds areaBounds = spawnArea.bounds;
        GameObject fallbackPrefab = null;
        Vector3 fallbackPosition = default;

        for (int attempt = 0; attempt < maximumPlacementAttempts; attempt++)
        {
            GameObject prefab = resourcePrefabs[Random.Range(0, resourcePrefabs.Count)];
            if (prefab == null || !TryGetPrefabRendererBounds(prefab, out Bounds prefabBounds))
                continue;

            float halfWidth = prefabBounds.extents.x + imageSpacing;
            float minX = areaBounds.min.x + halfWidth;
            float maxX = areaBounds.max.x - halfWidth;
            if (minX > maxX)
                continue;

            float x = Random.Range(minX, maxX);
            Vector2 rayOrigin = new(x, areaBounds.max.y + groundRayStartPadding);
            float rayDistance = Mathf.Max(groundRayDistance, areaBounds.size.y + groundRayStartPadding * 2f);
            RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, rayDistance, groundLayers);
            if (hit.collider == null || hit.normal.y < minimumGroundNormalY)
                continue;

            Vector3 rendererOffset = prefabBounds.center - prefab.transform.position;
            float rootY = hit.point.y + groundClearance - (rendererOffset.y - prefabBounds.extents.y);
            Vector3 spawnPosition = new(x, rootY, prefab.transform.position.z);
            Bounds candidateBounds = new(spawnPosition + rendererOffset, prefabBounds.size);

            if (!IsFullyInsideSpawnArea(candidateBounds)
                || IsVisibleToPlayerCamera(candidateBounds))
                continue;

            if (OverlapsExistingResource(candidateBounds))
            {
                // Some zones cannot fit the target count with completely disjoint
                // renderer bounds. Keep a valid hidden/grounded fallback so the
                // configured target count can still be reached.
                fallbackPrefab = prefab;
                fallbackPosition = spawnPosition;
                continue;
            }

            Spawn(prefab, spawnPosition);
            return true;
        }

        if (fallbackPrefab != null)
        {
            Spawn(fallbackPrefab, fallbackPosition);
            return true;
        }

        return false;
    }

    private void Spawn(GameObject prefab, Vector3 position)
    {
        GameObject instance = Instantiate(prefab, position, prefab.transform.rotation, spawnParent);
        SetLayerRecursively(instance, resourceLayer);
    }

    private static bool TryGetPrefabRendererBounds(GameObject prefab, out Bounds combinedBounds)
    {
        Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            combinedBounds = default;
            return false;
        }
        combinedBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            combinedBounds.Encapsulate(renderers[i].bounds);
        return true;
    }

    private bool IsFullyInsideSpawnArea(Bounds bounds)
    {
        Vector2[] corners =
        {
            new(bounds.min.x, bounds.min.y), new(bounds.min.x, bounds.max.y),
            new(bounds.max.x, bounds.min.y), new(bounds.max.x, bounds.max.y)
        };
        foreach (Vector2 corner in corners)
        {
            if (!spawnArea.OverlapPoint(corner))
                return false;
        }
        return true;
    }

    private bool OverlapsExistingResource(Bounds bounds)
    {
        Vector2 size = bounds.size;
        size += Vector2.one * (imageSpacing * 2f);
        return Physics2D.OverlapBox(bounds.center, size, 0f, resourceLayerMask) != null;
    }

    private bool IsVisibleToPlayerCamera(Bounds bounds)
    {
        if (playerCamera == null)
            playerCamera = Camera.main;
        if (playerCamera == null || !playerCamera.isActiveAndEnabled)
            return false;
        if ((playerCamera.cullingMask & resourceLayerMask) == 0)
            return false;

        bounds.Expand(cameraBoundsPadding * 2f);
        return GeometryUtility.TestPlanesAABB(
            GeometryUtility.CalculateFrustumPlanes(playerCamera), bounds);
    }

    private int CountResourcesInArea()
    {
        if (spawnArea == null || resourceLayerMask == 0)
            return 0;

        countedResources.Clear();
        Collider2D[] colliders = Physics2D.OverlapBoxAll(
            spawnArea.bounds.center, spawnArea.bounds.size, 0f, resourceLayerMask);
        foreach (Collider2D resourceCollider in colliders)
        {
            if (!spawnArea.OverlapPoint(resourceCollider.bounds.center))
                continue;
            IResourceProvider provider = resourceCollider.GetComponentInParent<IResourceProvider>();
            if (provider is Component component)
                countedResources.Add(component.gameObject);
        }
        return countedResources.Count;
    }

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        target.layer = layer;
        foreach (Transform child in target.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
}
