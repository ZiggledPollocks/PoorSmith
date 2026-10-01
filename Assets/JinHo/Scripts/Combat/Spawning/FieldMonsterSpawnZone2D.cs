// [코드 지도] FieldMonsterSpawnZone2D: 지역별 몬스터 스폰 후보 위치와 허용 조건을 제공한다.
// 주요 함수: TryChoosePosition, Update, PopulateInitialSlots
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/Combat/Spawning/FieldMonsterSpawnZone2D.cs.md

using System.Collections.Generic;
using UnityEngine;

/// <summary>One authored field region and one monster species with per-spawn-point respawn timers.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class FieldMonsterSpawnZone2D : MonoBehaviour
{
    private sealed class SpawnSlot
    {
        public Vector3 Position;
        public Bounds VisualBounds;
        public GameObject Instance;
        public IDamageable Health;
        public bool Dead;
        public float HiddenSeconds;
    }

    [SerializeField] private BoxCollider2D spawnArea;
    [SerializeField] private GameObject monsterPrefab;
    [SerializeField] private Camera playerCamera;
    [SerializeField, Min(0)] private int targetCount = 2;
    [SerializeField, Min(0f)] private float respawnSeconds = 180f;
    [SerializeField] private bool attachToCeiling;
    [SerializeField] private bool forestRegion;
    [SerializeField] private LayerMask groundLayers = 1 << 6;
    [SerializeField, Min(0f)] private float surfaceClearance = 0.08f;
    [SerializeField, Min(0f)] private float minimumSpacing = 3f;
    [SerializeField, Min(1)] private int placementAttempts = 100;
    [SerializeField, Min(0.02f)] private float retryInterval = 0.5f;
    [Tooltip("Use the same X coordinate at the centre of this authored zone on every visit.")]
    [SerializeField] private bool fixedSpawnAtCenter;

    private readonly List<SpawnSlot> slots = new();
    private float nextPlacementTime;
    private int monsterLayer;
    private CaveEntranceBackgroundTransition caveEntrance;

    public int TargetCount => targetCount;
    public int AliveCount
    {
        get
        {
            int count = 0;
            foreach (SpawnSlot slot in slots)
                if (!slot.Dead && slot.Instance != null)
                    count++;
            return count;
        }
    }
    public float RespawnSeconds => respawnSeconds;
    public GameObject MonsterPrefab => monsterPrefab;
    public BoxCollider2D SpawnArea => spawnArea;

    public void Configure(BoxCollider2D area, GameObject prefab, Camera camera,
        int count, float respawnDelay, bool ceiling, LayerMask groundMask)
    {
        spawnArea = area;
        monsterPrefab = prefab;
        playerCamera = camera;
        targetCount = Mathf.Max(0, count);
        respawnSeconds = Mathf.Max(0f, respawnDelay);
        attachToCeiling = ceiling;
        groundLayers = groundMask;
    }

    private void Awake()
    {
        spawnArea ??= GetComponent<BoxCollider2D>();
        playerCamera ??= Camera.main;
        monsterLayer = LayerMask.NameToLayer("Interactable");
        caveEntrance = FindFirstObjectByType<CaveEntranceBackgroundTransition>();
        if (groundLayers.value == 0)
            groundLayers = LayerMask.GetMask("Ground");
    }

    private void Start() => PopulateInitialSlots();

    // 핵심 분기: monsterPrefab == null || spawnArea == null || groundLayers.value == 0 판정.
    // 상태 변경: nextPlacementTime 갱신.
    // 다음 연결: FieldMonsterSpawnZone2D.PopulateInitialSlots() 호출.
    private void Update()
    {
        if (monsterPrefab == null || spawnArea == null || groundLayers.value == 0)
            return;
        if (caveEntrance == null)
            return;

        if (slots.Count < targetCount && Time.time >= nextPlacementTime)
        {
            PopulateInitialSlots();
            nextPlacementTime = Time.time + retryInterval;
        }

        foreach (SpawnSlot slot in slots)
        {
            if (!slot.Dead)
            {
                if (slot.Instance != null && slot.Health is Component component && component != null
                    && !slot.Health.IsDead)
                    continue;
                slot.Dead = true;
                slot.HiddenSeconds = 0f;
            }

            // Notion's timer advances only while the original spawn point is off camera.
            if (IsVisible(slot.VisualBounds))
                continue;
            slot.HiddenSeconds += Time.deltaTime;
            if (slot.HiddenSeconds < respawnSeconds)
                continue;

            if (slot.Instance != null)
            {
                foreach (Collider2D collider in slot.Instance.GetComponentsInChildren<Collider2D>())
                    collider.enabled = false;
                Destroy(slot.Instance);
                slot.Instance = null;
                continue;
            }

            if (OverlapsLivingMonster(slot.VisualBounds))
                continue;

            slot.Instance = Spawn(slot.Position);
            if (slot.Instance == null)
                continue;
            slot.Health = FindMonsterHealth(slot.Instance);
            slot.Dead = false;
            slot.HiddenSeconds = 0f;
        }
    }

    // 핵심 분기: monsterPrefab == null || spawnArea == null || groundLayers.value == 0 || caveEntrance == null || !SpawnGeomet… 판정.
    // 상태 변경: Position 갱신.
    // 다음 연결: SpawnGeometry.TryGetPrefabRendererBounds(UnityEngine.GameObject, out UnityEngine.Bounds) 호출.
    private void PopulateInitialSlots()
    {
        if (monsterPrefab == null || spawnArea == null || groundLayers.value == 0
            || caveEntrance == null
            || !SpawnGeometry.TryGetPrefabRendererBounds(monsterPrefab, out Bounds prefabBounds)
            || FindMonsterHealth(monsterPrefab) == null)
            return;

        while (slots.Count < targetCount)
        {
            if (!TryChoosePosition(prefabBounds, out Vector3 position, out Bounds visualBounds))
                break;
            GameObject instance = Spawn(position);
            if (instance == null)
                break;
            slots.Add(new SpawnSlot
            {
                Position = position,
                VisualBounds = visualBounds,
                Instance = instance,
                Health = FindMonsterHealth(instance),
            });
        }
    }

    // 핵심 분기: Physics2D.OverlapPoint(origin, groundLayers) != null 판정.
    // 상태 변경: surfaceY 갱신.
    // 다음 연결: SpawnGeometry.IsFullyInsideSpawnArea(UnityEngine.Collider2D, UnityEngine.Bounds) 호출.
    private bool TryChoosePosition(Bounds prefabBounds, out Vector3 position, out Bounds visualBounds)
    {
        Bounds area = spawnArea.bounds;
        Vector3 rendererOffset = prefabBounds.center - monsterPrefab.transform.position;
        float minX = area.min.x + prefabBounds.extents.x;
        float maxX = area.max.x - prefabBounds.extents.x;
        int attempts = fixedSpawnAtCenter
            ? Mathf.Min(placementAttempts, Mathf.CeilToInt(area.size.y * 2f) + 1)
            : placementAttempts;
        for (int attempt = 0; attempt < attempts && minX <= maxX; attempt++)
        {
            float x = fixedSpawnAtCenter ? Mathf.Clamp(area.center.x, minX, maxX) : Random.Range(minX, maxX);
            // Sample open air inside a corridor; the enclosing BoxCollider may
            // span several cave tiers separated by solid rock.
            float sampleY = fixedSpawnAtCenter
                ? area.center.y + (attempt % 2 == 0 ? 1f : -1f) * ((attempt + 1) / 2) * .5f
                : Random.Range(area.min.y, area.max.y);
            Vector2 origin = new(x, sampleY);
            if (Physics2D.OverlapPoint(origin, groundLayers) != null)
                continue;
            RaycastHit2D hit = Physics2D.Raycast(origin,
                attachToCeiling ? Vector2.up : Vector2.down,
                area.size.y + 1f, groundLayers);
            if (hit.collider == null ||
                (attachToCeiling ? hit.normal.y > -0.5f : hit.normal.y < 0.5f))
                continue;

            float surfaceY = hit.point.y;
            if (fixedSpawnAtCenter && !attachToCeiling)
            {
                // A large guard can span several one-tile stair treads. Place its
                // feet on the highest tread under its width, not inside that tread.
                float left = x + rendererOffset.x - prefabBounds.extents.x + .1f;
                float right = x + rendererOffset.x + prefabBounds.extents.x - .1f;
                for (int sample = 0; sample < 5; sample++)
                {
                    float sampleX = Mathf.Lerp(left, right, sample / 4f);
                    RaycastHit2D support = Physics2D.Raycast(
                        new Vector2(sampleX, origin.y + 1f), Vector2.down,
                        area.size.y + 2f, groundLayers);
                    if (support.collider != null && support.normal.y >= .5f)
                        surfaceY = Mathf.Max(surfaceY, support.point.y);
                }
            }
            float y = attachToCeiling
                ? hit.point.y - surfaceClearance - rendererOffset.y - prefabBounds.extents.y
                : surfaceY + surfaceClearance - rendererOffset.y + prefabBounds.extents.y;
            Vector3 candidate = new(x, y, monsterPrefab.transform.position.z);
            Bounds bounds = new(candidate + rendererOffset, prefabBounds.size);
            if (!SpawnGeometry.IsFullyInsideSpawnArea(spawnArea, bounds)
                || !IsInsideAssignedRegion(bounds)
                // A fixed guardian must already occupy its authored passage when a visit starts.
                || (!fixedSpawnAtCenter && IsVisible(bounds))
                || OverlapsLivingMonster(bounds))
                continue;
            if (Physics2D.OverlapBox(bounds.center,
                new Vector2(bounds.size.x * 0.7f, bounds.size.y * 0.8f),
                0f, groundLayers) != null)
                continue;

            position = candidate;
            visualBounds = bounds;
            return true;
        }

        position = default;
        visualBounds = default;
        return false;
    }

    private bool IsVisible(Bounds bounds)
    {
        playerCamera ??= Camera.main;
        if (playerCamera == null || !playerCamera.isActiveAndEnabled)
            return false;
        return GeometryUtility.TestPlanesAABB(
            GeometryUtility.CalculateFrustumPlanes(playerCamera), bounds);
    }

    private bool IsInsideAssignedRegion(Bounds bounds)
    {
        if (caveEntrance == null)
            return false;
        Vector2[] corners =
        {
            new(bounds.min.x, bounds.min.y), new(bounds.min.x, bounds.max.y),
            new(bounds.max.x, bounds.min.y), new(bounds.max.x, bounds.max.y)
        };
        foreach (Vector2 corner in corners)
            if (caveEntrance.IsCaveWorldPosition(corner) == forestRegion)
                return false;
        return true;
    }

    private bool OverlapsLivingMonster(Bounds bounds)
    {
        int layerMask = monsterLayer < 0 ? 0 : 1 << monsterLayer;
        if (layerMask == 0)
            return false;
        Vector2 size = bounds.size + Vector3.one * (minimumSpacing * 2f);
        foreach (Collider2D collider in Physics2D.OverlapBoxAll(bounds.center, size, 0f, layerMask))
        {
            IDamageable health = FindMonsterHealth(collider.gameObject);
            if (health != null && !health.IsDead)
                return true;
        }
        return false;
    }

    private GameObject Spawn(Vector3 position)
    {
        GameObject instance = Instantiate(monsterPrefab, position, monsterPrefab.transform.rotation, transform);
        FieldMonsterRegionLimiter2D limiter = instance.AddComponent<FieldMonsterRegionLimiter2D>();
        if (!limiter.Configure(caveEntrance, forestRegion))
        {
            Destroy(instance);
            return null;
        }
        if (monsterLayer >= 0)
            SpawnHierarchyLayers.SetRecursively(instance, monsterLayer);
        return instance;
    }

    private static IDamageable FindMonsterHealth(GameObject target)
    {
        if (target == null)
            return null;
        foreach (MonoBehaviour behaviour in target.GetComponentsInChildren<MonoBehaviour>(true))
            if (behaviour is IDamageable health && behaviour is not PlayerAssimilate)
                return health;
        return null;
    }

}
