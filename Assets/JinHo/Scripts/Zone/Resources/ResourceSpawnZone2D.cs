using System.Collections.Generic;
using UnityEngine;

public class ResourceSpawnZone2D : MonoBehaviour
{
    private sealed class ResourceSlot
    {
        public GameObject Prefab;
        public GameObject Instance;
        public float RespawnAt = -1f;
    }

    [Header("Spawn Area")]
    [SerializeField] private Collider2D spawnArea;
    [SerializeField] private Transform spawnParent;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private ItemDropSpawner itemDropSpawner;

    [Header("Resource")]
    [SerializeField] private List<GameObject> resourcePrefabs = new();
    [SerializeField, Min(0)] private int targetResourceCount = 4;
    [SerializeField, Min(0f)] private float spawnInterval = 5f;
    [SerializeField] private bool requireOffscreenRespawn = true;

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

    private readonly List<ResourceSlot> slots = new();
    private int resourceLayer;
    private int resourceLayerMask;
    private bool initialPopulationComplete;
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
        initialPopulationComplete = slots.Count >= targetResourceCount;
        nextSpawnTime = Time.time;
    }

    private void Start() => TryPopulateInitialResources();

    private void Update()
    {
        if (!HasValidConfiguration())
            return;

        if (slots.Count < targetResourceCount && Time.time >= nextSpawnTime)
        {
            TryPopulateInitialResources();
            nextSpawnTime = Time.time + visibilityRetryInterval;
        }

        foreach (ResourceSlot slot in slots)
        {
            if (slot.Instance != null)
                continue;
            if (slot.RespawnAt < 0f)
                slot.RespawnAt = Time.time + spawnInterval;
            if (Time.time < slot.RespawnAt)
                continue;
            slot.Instance = TrySpawnResource(slot.Prefab, !requireOffscreenRespawn);
            slot.RespawnAt = slot.Instance == null
                ? Time.time + visibilityRetryInterval
                : -1f;
        }
    }

    public void Configure(Collider2D area, Transform parent, Camera camera,
        GameObject resourcePrefab, int targetCount, float interval, LayerMask groundMask)
    {
        Configure(area, parent, camera,
            resourcePrefab == null ? System.Array.Empty<GameObject>() : new[] { resourcePrefab },
            targetCount, interval, groundMask, true);
    }

    public void Configure(Collider2D area, Transform parent, Camera camera,
        IReadOnlyList<GameObject> prefabs, int targetCount, float interval,
        LayerMask groundMask, bool offscreenRespawn)
    {
        spawnArea = area;
        spawnParent = parent;
        playerCamera = camera;
        resourcePrefabs.Clear();
        if (prefabs != null)
        {
            foreach (GameObject prefab in prefabs)
                if (prefab != null)
                    resourcePrefabs.Add(prefab);
        }
        targetResourceCount = Mathf.Max(0, targetCount);
        spawnInterval = Mathf.Max(0f, interval);
        groundLayers = groundMask;
        requireOffscreenRespawn = offscreenRespawn;
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
        if (itemDropSpawner == null)
            itemDropSpawner = FindFirstObjectByType<ItemDropSpawner>();

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

        while (slots.Count < targetResourceCount)
        {
            GameObject prefab = resourcePrefabs[slots.Count % resourcePrefabs.Count];
            GameObject instance = TrySpawnResource(prefab, false);
            if (instance == null)
                break;
            slots.Add(new ResourceSlot { Prefab = prefab, Instance = instance });
        }
        initialPopulationComplete = slots.Count >= targetResourceCount;
        nextSpawnTime = Time.time + visibilityRetryInterval;
    }

    private GameObject TrySpawnResource(GameObject prefab, bool allowVisible)
    {
        Bounds areaBounds = spawnArea.bounds;

        for (int attempt = 0; attempt < maximumPlacementAttempts; attempt++)
        {
            if (prefab == null || !TryGetPrefabRendererBounds(prefab, out Bounds prefabBounds))
                continue;

            float halfWidth = prefabBounds.extents.x + imageSpacing;
            float minX = areaBounds.min.x + halfWidth;
            float maxX = areaBounds.max.x - halfWidth;
            if (minX > maxX)
                continue;

            float x = Random.Range(minX, maxX);
            // A cave zone may span several zigzag corridors with solid rock above it.
            // Start inside a sampled open pocket instead of raycasting from its top wall.
            float sampleY = Random.Range(areaBounds.min.y, areaBounds.max.y);
            Vector2 rayOrigin = new(x, sampleY);
            if (Physics2D.OverlapPoint(rayOrigin, groundLayers) != null)
                continue;
            float rayDistance = Mathf.Max(groundRayDistance, areaBounds.size.y + groundRayStartPadding * 2f);
            RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, rayDistance, groundLayers);
            if (hit.collider == null || hit.normal.y < minimumGroundNormalY)
                continue;

            Vector3 rendererOffset = prefabBounds.center - prefab.transform.position;
            float rootY = hit.point.y + groundClearance - (rendererOffset.y - prefabBounds.extents.y);
            Vector3 spawnPosition = new(x, rootY, prefab.transform.position.z);
            Bounds candidateBounds = new(spawnPosition + rendererOffset, prefabBounds.size);

            if (!IsFullyInsideSpawnArea(candidateBounds)
                || (!allowVisible && IsVisibleToPlayerCamera(candidateBounds)))
                continue;

            if (Physics2D.OverlapBox(candidateBounds.center,
                new Vector2(candidateBounds.size.x * 0.7f, candidateBounds.size.y * 0.8f),
                0f, groundLayers) != null)
                continue;

            if (OverlapsExistingResource(candidateBounds))
                continue;

            return Spawn(prefab, spawnPosition);
        }

        return null;
    }

    private GameObject Spawn(GameObject prefab, Vector3 position)
    {
        GameObject instance = Instantiate(prefab, position, prefab.transform.rotation, spawnParent);
        SetLayerRecursively(instance, resourceLayer);
        if (itemDropSpawner == null)
            itemDropSpawner = FindFirstObjectByType<ItemDropSpawner>();
        IResourceDropSpawnerReceiver resource = instance.GetComponent<IResourceDropSpawnerReceiver>();
        if (resource != null)
            resource.SetItemDropSpawner(itemDropSpawner);
        return instance;
    }

    private static bool TryGetPrefabRendererBounds(GameObject prefab, out Bounds combinedBounds)
    {
        return SpawnGeometry.TryGetPrefabRendererBounds(prefab, out combinedBounds);
    }

    private bool IsFullyInsideSpawnArea(Bounds bounds)
    {
        return SpawnGeometry.IsFullyInsideSpawnArea(spawnArea, bounds);
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
        int count = 0;
        foreach (ResourceSlot slot in slots)
            if (slot.Instance != null)
                count++;
        return count;
    }

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        target.layer = layer;
        foreach (Transform child in target.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
}
