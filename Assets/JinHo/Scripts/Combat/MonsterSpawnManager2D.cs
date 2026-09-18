using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

[DisallowMultipleComponent]
public sealed class MonsterSpawnManager2D : MonoBehaviour
{
    private enum SpawnSurface { Ground, Ceiling }

    [Serializable]
    private sealed class MonsterSpawnRule
    {
        [SerializeField] private GameObject prefab;
        [SerializeField, Min(0)] private int minimumCount = 1;
        [SerializeField, Min(0)] private int maximumCount = 3;
        [SerializeField] private string spawnAreaName;
        [SerializeField] private Collider2D spawnArea;
        [SerializeField] private SpawnSurface spawnSurface;
        [NonSerialized] private int sessionTargetCount = -1;
        [NonSerialized] private bool initialPopulationComplete;
        [NonSerialized] private bool replacementScheduled;
        [NonSerialized] private float nextSpawnTime;

        public GameObject Prefab => prefab;
        public string SpawnAreaName => spawnAreaName;
        public Collider2D SpawnArea => spawnArea;
        public SpawnSurface Surface => spawnSurface;
        public bool InitialPopulationComplete
        {
            get => initialPopulationComplete;
            set => initialPopulationComplete = value;
        }
        public bool ReplacementScheduled
        {
            get => replacementScheduled;
            set => replacementScheduled = value;
        }
        public float NextSpawnTime
        {
            get => nextSpawnTime;
            set => nextSpawnTime = value;
        }

        public int TargetCount
        {
            get
            {
                if (sessionTargetCount < 0)
                {
                    int minimum = Mathf.Max(0, minimumCount);
                    int maximum = Mathf.Max(minimum, maximumCount);
                    sessionTargetCount = Random.Range(minimum, maximum + 1);
                }

                return sessionTargetCount;
            }
        }

        public MonsterSpawnRule(GameObject monsterPrefab, string areaName,
            int minimum, int maximum, SpawnSurface surface)
        {
            prefab = monsterPrefab;
            spawnAreaName = areaName;
            minimumCount = minimum;
            maximumCount = maximum;
            spawnSurface = surface;
            spawnArea = null;
            sessionTargetCount = -1;
        }

        public void SetSpawnArea(Collider2D area)
        {
            spawnArea = area;
        }

        public void ResetRuntimeState(float firstSpawnTime)
        {
            sessionTargetCount = -1;
            initialPopulationComplete = false;
            replacementScheduled = false;
            nextSpawnTime = firstSpawnTime;
        }
    }

    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Collider2D allowedSpawnArea;
    [SerializeField] private Transform spawnParent;
    [SerializeField] private string spawnAreaRootName = "MonsterSpawn";

    [Header("Monster Prefabs")]
    [SerializeField] private List<MonsterSpawnRule> spawnRules = new();

    [Header("Spawn Timing")]
    [SerializeField, Min(0.01f)] private float spawnCheckInterval = 8f;
    [SerializeField] private bool spawnImmediately = true;

    [Header("Off-screen Position")]
    [SerializeField, Min(1)] private int maximumSpawnAttempts = 40;
    [SerializeField, Min(0f)] private float cameraBoundsPadding = 0.15f;
    [SerializeField, Min(0.02f)] private float visibilityRetryInterval = 0.25f;

    [Header("Surface Placement")]
    [SerializeField] private LayerMask groundLayers;
    [SerializeField, Min(0f)] private float groundRayStartHeight = 3f;
    [SerializeField, Min(0.01f)] private float groundRayDistance = 30f;
    [FormerlySerializedAs("groundClearance")]
    [SerializeField, Min(0f)] private float surfaceClearance = 0.02f;
    [FormerlySerializedAs("minimumGroundNormalY")]
    [SerializeField, Range(0f, 1f)] private float minimumSurfaceNormalY = 0.2f;

    [Header("Spacing")]
    [SerializeField] private LayerMask monsterLayers;
    [SerializeField, Min(0f)] private float minimumMonsterSpacing = 3f;

    private readonly HashSet<string> unresolvedAreaWarnings = new();
    private int monsterLayer = -1;

    private void Awake()
    {
#if UNITY_EDITOR
        EnsureEditorDefaultRules();
#endif
        ClampConfiguration();
        InitializeReferences();
        ResolveSpawnAreas();
    }

    private void OnEnable()
    {
        float firstSpawnTime = spawnImmediately
            ? Time.time
            : Time.time + spawnCheckInterval;

        foreach (MonsterSpawnRule rule in spawnRules)
            rule?.ResetRuntimeState(firstSpawnTime);
    }

    private void Start()
    {
        InitializeReferences();
        ResolveSpawnAreas();

        foreach (MonsterSpawnRule rule in spawnRules)
            TryPopulateInitialMonsters(rule);
    }

    private void Update()
    {
        InitializeReferences();
        ResolveSpawnAreas();

        foreach (MonsterSpawnRule rule in spawnRules)
            UpdateRule(rule);
    }

    public void SpawnMissingMonsters()
    {
        InitializeReferences();
        ResolveSpawnAreas();

        foreach (MonsterSpawnRule rule in spawnRules)
            TryPopulateInitialMonsters(rule);
    }

    private void InitializeReferences()
    {
        if (spawnParent == null)
            spawnParent = transform;
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (groundLayers.value == 0)
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer >= 0)
                groundLayers = 1 << groundLayer;
        }

        monsterLayer = LayerMask.NameToLayer("Interactable");
        if (monsterLayers.value == 0 && monsterLayer >= 0)
            monsterLayers = 1 << monsterLayer;
    }

    private void UpdateRule(MonsterSpawnRule rule)
    {
        if (!HasValidConfiguration(rule, out Collider2D spawnArea))
            return;

        int currentCount = CountAliveMonsters(rule);
        int targetCount = rule.TargetCount;
        if (!rule.InitialPopulationComplete)
        {
            if (currentCount >= targetCount)
            {
                rule.InitialPopulationComplete = true;
                rule.ReplacementScheduled = false;
            }
            else if (Time.time >= rule.NextSpawnTime)
            {
                TryPopulateInitialMonsters(rule);
            }
            return;
        }

        if (currentCount >= targetCount)
        {
            rule.ReplacementScheduled = false;
            return;
        }

        if (!rule.ReplacementScheduled)
        {
            rule.ReplacementScheduled = true;
            rule.NextSpawnTime = Time.time + spawnCheckInterval;
            return;
        }

        if (Time.time < rule.NextSpawnTime)
            return;

        if (TrySpawn(rule, spawnArea))
        {
            rule.ReplacementScheduled = CountAliveMonsters(rule) < targetCount;
            rule.NextSpawnTime = Time.time + spawnCheckInterval;
        }
        else
        {
            rule.NextSpawnTime = Time.time + visibilityRetryInterval;
        }
    }

    private void TryPopulateInitialMonsters(MonsterSpawnRule rule)
    {
        if (!HasValidConfiguration(rule, out Collider2D spawnArea))
            return;

        int missingCount = Mathf.Max(0,
            rule.TargetCount - CountAliveMonsters(rule));
        for (int i = 0; i < missingCount; i++)
        {
            if (!TrySpawn(rule, spawnArea))
                break;
        }

        rule.InitialPopulationComplete =
            CountAliveMonsters(rule) >= rule.TargetCount;
        rule.NextSpawnTime = Time.time + visibilityRetryInterval;
    }

    private bool HasValidConfiguration(MonsterSpawnRule rule,
        out Collider2D spawnArea)
    {
        spawnArea = null;
        if (rule == null || rule.Prefab == null || groundLayers.value == 0)
            return false;

        if (targetCamera == null)
        {
            Debug.LogWarning($"{name}: 사용할 Camera가 없습니다.", this);
            return false;
        }

        spawnArea = rule.SpawnArea != null
            ? rule.SpawnArea
            : allowedSpawnArea;
        if (spawnArea != null)
            return true;

        WarnUnresolvedArea(rule);
        return false;
    }
    private bool TrySpawn(MonsterSpawnRule rule, Collider2D spawnArea)
    {
        if (!IsValidMonsterPrefab(rule.Prefab) ||
            !TryGetPrefabRendererBounds(rule.Prefab, out Bounds prefabBounds))
        {
            return false;
        }

        Bounds areaBounds = spawnArea.bounds;
        bool hasFallback = false;
        Vector3 fallbackPosition = default;

        for (int attempt = 0; attempt < maximumSpawnAttempts; attempt++)
        {
            float halfWidth = prefabBounds.extents.x;
            float minX = areaBounds.min.x + halfWidth;
            float maxX = areaBounds.max.x - halfWidth;
            if (minX > maxX)
                break;

            float x = Random.Range(minX, maxX);
            if (!TryGetSurfacePosition(rule.Surface, areaBounds,
                    rule.Prefab, prefabBounds, x,
                    out Vector3 spawnPosition, out Bounds candidateBounds))
            {
                continue;
            }

            if (!IsFullyInsideSpawnArea(spawnArea, candidateBounds) ||
                IsVisibleToPlayerCamera(candidateBounds))
            {
                continue;
            }

            if (OverlapsExistingMonster(candidateBounds))
            {
                hasFallback = true;
                fallbackPosition = spawnPosition;
                continue;
            }

            SpawnMonster(rule.Prefab, spawnPosition);
            return true;
        }

        if (hasFallback)
        {
            SpawnMonster(rule.Prefab, fallbackPosition);
            return true;
        }

        return false;
    }

    private bool TryGetSurfacePosition(SpawnSurface surface, Bounds areaBounds,
        GameObject prefab, Bounds prefabBounds, float x,
        out Vector3 spawnPosition, out Bounds candidateBounds)
    {
        Vector3 rendererOffset = prefabBounds.center - prefab.transform.position;
        float rayDistance = Mathf.Max(groundRayDistance,
            areaBounds.size.y + groundRayStartHeight * 2f);
        RaycastHit2D hit;
        float rootY;

        if (surface == SpawnSurface.Ceiling)
        {
            Vector2 rayOrigin = new(x, areaBounds.min.y - groundRayStartHeight);
            hit = Physics2D.Raycast(rayOrigin, Vector2.up,
                rayDistance, groundLayers);
            if (hit.collider == null || hit.normal.y > -minimumSurfaceNormalY)
            {
                spawnPosition = default;
                candidateBounds = default;
                return false;
            }

            rootY = hit.point.y - surfaceClearance -
                    (rendererOffset.y + prefabBounds.extents.y);
        }
        else
        {
            Vector2 rayOrigin = new(x, areaBounds.max.y + groundRayStartHeight);
            hit = Physics2D.Raycast(rayOrigin, Vector2.down,
                rayDistance, groundLayers);
            if (hit.collider == null || hit.normal.y < minimumSurfaceNormalY)
            {
                spawnPosition = default;
                candidateBounds = default;
                return false;
            }

            rootY = hit.point.y + surfaceClearance -
                    (rendererOffset.y - prefabBounds.extents.y);
        }

        spawnPosition = new Vector3(x, rootY, prefab.transform.position.z);
        candidateBounds = new Bounds(
            spawnPosition + rendererOffset, prefabBounds.size);
        return true;
    }

    private static bool TryGetPrefabRendererBounds(GameObject prefab,
        out Bounds combinedBounds)
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

    private static bool IsFullyInsideSpawnArea(Collider2D spawnArea,
        Bounds bounds)
    {
        Vector2[] corners =
        {
            new(bounds.min.x, bounds.min.y),
            new(bounds.min.x, bounds.max.y),
            new(bounds.max.x, bounds.min.y),
            new(bounds.max.x, bounds.max.y)
        };

        foreach (Vector2 corner in corners)
        {
            if (!spawnArea.OverlapPoint(corner))
                return false;
        }

        return true;
    }

    private bool IsVisibleToPlayerCamera(Bounds bounds)
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
        if (targetCamera == null || !targetCamera.isActiveAndEnabled)
            return false;
        if ((targetCamera.cullingMask & monsterLayers.value) == 0)
            return false;

        bounds.Expand(cameraBoundsPadding * 2f);
        return GeometryUtility.TestPlanesAABB(
            GeometryUtility.CalculateFrustumPlanes(targetCamera), bounds);
    }

    private bool OverlapsExistingMonster(Bounds bounds)
    {
        Vector2 size = bounds.size;
        size += Vector2.one * (minimumMonsterSpacing * 2f);
        Collider2D[] overlaps = Physics2D.OverlapBoxAll(
            bounds.center, size, 0f, monsterLayers);

        foreach (Collider2D overlap in overlaps)
        {
            if (overlap == null)
                continue;

            IDamageable damageable = overlap.GetComponentInParent<IDamageable>();
            if (damageable != null && damageable is not PlayerAssimilate)
                return true;
        }

        return false;
    }

    private void SpawnMonster(GameObject prefab, Vector3 position)
    {
        GameObject instance = Instantiate(
            prefab, position, prefab.transform.rotation, spawnParent);
        if (monsterLayer >= 0)
            SetLayerRecursively(instance, monsterLayer);
    }

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        target.layer = layer;
        foreach (Transform child in target.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
    private void ResolveSpawnAreas()
    {
        Transform root = FindSpawnAreaRoot();
        if (root == null)
            return;

        Collider2D[] colliders = root.GetComponentsInChildren<Collider2D>(true);
        foreach (MonsterSpawnRule rule in spawnRules)
        {
            if (rule == null || rule.Prefab == null || rule.SpawnArea != null)
                continue;

            string expectedName = string.IsNullOrWhiteSpace(rule.SpawnAreaName)
                ? rule.Prefab.name
                : rule.SpawnAreaName;
            string normalizedExpected = NormalizeName(expectedName);
            string normalizedPrefab = NormalizeName(rule.Prefab.name);

            foreach (Collider2D collider in colliders)
            {
                string normalizedCollider = NormalizeName(collider.name);
                if (normalizedCollider == normalizedExpected ||
                    normalizedCollider == normalizedPrefab ||
                    normalizedCollider == normalizedExpected + "spawn" ||
                    normalizedCollider == normalizedExpected + "spawnzone")
                {
                    rule.SetSpawnArea(collider);
                    unresolvedAreaWarnings.Remove(expectedName);
                    break;
                }
            }
        }
    }

    private Transform FindSpawnAreaRoot()
    {
        string normalizedRootName = NormalizeName(spawnAreaRootName);
        Transform[] transforms = FindObjectsByType<Transform>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (Transform candidate in transforms)
        {
            if (NormalizeName(candidate.name) == normalizedRootName)
                return candidate;
        }

        return null;
    }

    private void WarnUnresolvedArea(MonsterSpawnRule rule)
    {
        string areaName = string.IsNullOrWhiteSpace(rule.SpawnAreaName)
            ? rule.Prefab.name
            : rule.SpawnAreaName;

        if (!unresolvedAreaWarnings.Add(areaName))
            return;

        Debug.LogWarning(
            $"{name}: '{spawnAreaRootName}/{areaName}' BoxCollider2D를 찾지 못했습니다. " +
            "MonsterSpawn 자식 이름과 Scene 저장 상태를 확인하세요.",
            this);
    }

    private static string NormalizeName(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        StringBuilder builder = new(value.Length);
        foreach (char character in value)
        {
            if (char.IsLetterOrDigit(character))
                builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }

    private static int CountAliveMonsters(MonsterSpawnRule rule)
    {
        if (!TryGetMonsterController(rule.Prefab, out MonoBehaviour prefabController))
            return 0;

        Type controllerType = prefabController.GetType();
        MonoBehaviour[] sceneBehaviours =
            FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        int aliveCount = 0;

        foreach (MonoBehaviour behaviour in sceneBehaviours)
        {
            if (behaviour == null || behaviour.GetType() != controllerType)
                continue;

            if (!HasPrefabInstanceName(behaviour.transform, rule.Prefab.name))
                continue;


            if (behaviour is IDamageable damageable && !damageable.IsDead)
                aliveCount++;
        }

        return aliveCount;
    }

    private static bool HasPrefabInstanceName(Transform target,
        string prefabName)
    {
        string cloneName = $"{prefabName}(Clone)";
        while (target != null)
        {
            if (target.name == prefabName || target.name == cloneName)
                return true;

            target = target.parent;
        }

        return false;
    }

    private bool IsValidMonsterPrefab(GameObject prefab)
    {
        if (prefab.GetComponentInChildren<Collider2D>(true) == null)
        {
            Debug.LogWarning(
                $"{name}: '{prefab.name}' 프리팹에 Collider2D가 없습니다.", this);
            return false;
        }

        if (TryGetMonsterController(prefab, out _))
            return true;

        Debug.LogWarning(
            $"{name}: '{prefab.name}' 프리팹에 IDamageable 몬스터 컴포넌트가 없습니다.",
            this);
        return false;
    }

    private static bool TryGetMonsterController(GameObject target,
        out MonoBehaviour monsterController)
    {
        MonoBehaviour[] behaviours =
            target.GetComponentsInChildren<MonoBehaviour>(true);

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour is IDamageable && behaviour is not PlayerAssimilate)
            {
                monsterController = behaviour;
                return true;
            }
        }

        monsterController = null;
        return false;
    }

#if UNITY_EDITOR
    private void EnsureEditorDefaultRules()
    {
        if (spawnRules.Count > 0)
            return;

        AddEditorRule(
            "Assets/JinHo/Prefab/Monster/Deer.prefab",
            "deer", 1, 3, SpawnSurface.Ground);
        AddEditorRule(
            "Assets/JinHo/Prefab/Monster/VampireBat.prefab",
            "VampireBat", 2, 2, SpawnSurface.Ceiling);
        AddEditorRule(
            "Assets/JinHo/Prefab/Monster/MossSlime.prefab",
            "MossSlime", 3, 3, SpawnSurface.Ground);
        AddEditorRule(
            "Assets/JinHo/Prefab/Monster/StoneGolem.prefab",
            "StoneGolem", 1, 1, SpawnSurface.Ground);

        if (spawnRules.Count > 0)
            UnityEditor.EditorUtility.SetDirty(this);
    }

    private void AddEditorRule(string assetPath, string areaName,
        int minimumCount, int maximumCount, SpawnSurface surface)
    {
        GameObject prefab =
            UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefab == null)
            return;

        spawnRules.Add(new MonsterSpawnRule(
            prefab, areaName, minimumCount, maximumCount, surface));
    }
#endif

    private void OnValidate()
    {
#if UNITY_EDITOR
        EnsureEditorDefaultRules();
#endif
        ClampConfiguration();
    }

    private void ClampConfiguration()
    {
        spawnCheckInterval = Mathf.Max(8f, spawnCheckInterval);
        maximumSpawnAttempts = Mathf.Max(40, maximumSpawnAttempts);
        cameraBoundsPadding = Mathf.Max(0f, cameraBoundsPadding);
        visibilityRetryInterval = Mathf.Max(0.02f, visibilityRetryInterval);
        groundRayStartHeight = Mathf.Max(0f, groundRayStartHeight);
        groundRayDistance = Mathf.Max(0.01f, groundRayDistance);
        surfaceClearance = Mathf.Max(0f, surfaceClearance);
        minimumSurfaceNormalY = Mathf.Clamp01(minimumSurfaceNormalY);
        minimumMonsterSpacing = Mathf.Max(3f, minimumMonsterSpacing);
    }

    private void OnDrawGizmosSelected()
    {
        foreach (MonsterSpawnRule rule in spawnRules)
        {
            if (rule == null || rule.SpawnArea == null)
                continue;

            Bounds bounds = rule.SpawnArea.bounds;
            Gizmos.color = rule.Surface == SpawnSurface.Ceiling
                ? new Color(0.5f, 0.8f, 1f, 1f)
                : new Color(0.4f, 1f, 0.4f, 1f);
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }
    }
}
