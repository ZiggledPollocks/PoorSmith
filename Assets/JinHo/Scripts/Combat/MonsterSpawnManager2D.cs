using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

[DisallowMultipleComponent]
public sealed class MonsterSpawnManager2D : MonoBehaviour
{
    [Serializable]
    private sealed class MonsterSpawnRule
    {
        [SerializeField] private GameObject prefab;
        [SerializeField, Min(0)] private int targetCount = 3;

        public GameObject Prefab => prefab;
        public int TargetCount => Mathf.Max(0, targetCount);
    }

    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Collider2D allowedSpawnArea;
    [SerializeField] private Transform spawnParent;

    [Header("Monster Prefabs")]
    [SerializeField] private List<MonsterSpawnRule> spawnRules = new();

    [Header("Spawn Timing")]
    [SerializeField, Min(0.01f)] private float spawnCheckInterval = 5f;
    [SerializeField] private bool spawnImmediately = true;

    [Header("Off-screen Position")]
    [SerializeField, Min(0f)] private float minimumDistanceOutsideCamera = 2f;
    [SerializeField, Min(0f)] private float maximumDistanceOutsideCamera = 8f;
    [SerializeField, Min(1)] private int maximumSpawnAttempts = 20;

    [Header("Ground Placement")]
    [SerializeField] private LayerMask groundLayers;
    [SerializeField, Min(0f)] private float groundRayStartHeight = 3f;
    [SerializeField, Min(0.01f)] private float groundRayDistance = 30f;
    [SerializeField, Min(0f)] private float groundClearance = 0.02f;

    [Header("Spacing")]
    [SerializeField] private LayerMask monsterLayers;
    [SerializeField, Min(0f)] private float minimumMonsterSpacing = 1.5f;

    private float nextSpawnCheckTime;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (groundLayers.value == 0)
        {
            int groundLayer = LayerMask.NameToLayer("Ground");

            if (groundLayer >= 0)
                groundLayers = 1 << groundLayer;
        }

        if (monsterLayers.value == 0)
        {
            int interactableLayer = LayerMask.NameToLayer("Interactable");

            if (interactableLayer >= 0)
                monsterLayers = 1 << interactableLayer;
        }
    }

    private void OnEnable()
    {
        nextSpawnCheckTime = spawnImmediately
            ? Time.time
            : Time.time + spawnCheckInterval;
    }

    private void Update()
    {
        if (Time.time < nextSpawnCheckTime)
            return;

        nextSpawnCheckTime = Time.time + spawnCheckInterval;
        SpawnMissingMonsters();
    }

    public void SpawnMissingMonsters()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;

            if (targetCamera == null)
            {
                Debug.LogWarning($"{name}: 사용할 Camera가 없습니다.", this);
                return;
            }
        }

        foreach (MonsterSpawnRule rule in spawnRules)
        {
            if (rule == null || rule.Prefab == null)
                continue;

            if (CountAliveMonsters(rule.Prefab) >= rule.TargetCount)
                continue;

            TrySpawn(rule);
        }
    }

    private bool TrySpawn(MonsterSpawnRule rule)
    {
        if (!IsValidMonsterPrefab(rule.Prefab))
            return false;

        float prefabHalfWidth = GetPrefabHalfWidth(rule.Prefab);
        float prefabBottomOffset = GetPrefabBottomOffset(rule.Prefab);

        for (int attempt = 0; attempt < maximumSpawnAttempts; attempt++)
        {
            if (!TryFindOffscreenGroundPosition(
                    prefabHalfWidth,
                    prefabBottomOffset,
                    out Vector2 spawnPosition))
            {
                continue;
            }

            if (IsTooCloseToAnotherMonster(spawnPosition))
                continue;

            Instantiate(
                rule.Prefab,
                spawnPosition,
                Quaternion.identity,
                spawnParent);
            return true;
        }

        Debug.LogWarning(
            $"{name}: 카메라 밖에서 '{rule.Prefab.name}'의 안전한 생성 위치를 찾지 못했습니다.",
            this);
        return false;
    }

    private bool TryFindOffscreenGroundPosition(
        float prefabHalfWidth,
        float prefabBottomOffset,
        out Vector2 spawnPosition)
    {
        GetCameraWorldBounds(out Vector2 cameraMin, out Vector2 cameraMax);

        bool useLeftSide = Random.value < 0.5f;
        float outsideDistance = Random.Range(
            minimumDistanceOutsideCamera,
            maximumDistanceOutsideCamera);

        float spawnX = useLeftSide
            ? cameraMin.x - outsideDistance - prefabHalfWidth
            : cameraMax.x + outsideDistance + prefabHalfWidth;

        Vector2 rayOrigin = new(
            spawnX,
            cameraMax.y + groundRayStartHeight);

        RaycastHit2D groundHit = Physics2D.Raycast(
            rayOrigin,
            Vector2.down,
            groundRayDistance,
            groundLayers);

        if (groundHit.collider == null)
        {
            spawnPosition = default;
            return false;
        }

        spawnPosition = new Vector2(
            spawnX,
            groundHit.point.y + prefabBottomOffset + groundClearance);

        if (allowedSpawnArea != null &&
            !allowedSpawnArea.OverlapPoint(spawnPosition))
        {
            return false;
        }

        return IsCompletelyOutsideCamera(
            spawnPosition,
            prefabHalfWidth,
            cameraMin,
            cameraMax);
    }

    private void GetCameraWorldBounds(
        out Vector2 cameraMin,
        out Vector2 cameraMax)
    {
        float distanceFromCamera = Mathf.Abs(targetCamera.transform.position.z);

        cameraMin = targetCamera.ViewportToWorldPoint(
            new Vector3(0f, 0f, distanceFromCamera));
        cameraMax = targetCamera.ViewportToWorldPoint(
            new Vector3(1f, 1f, distanceFromCamera));
    }

    private static bool IsCompletelyOutsideCamera(
        Vector2 position,
        float prefabHalfWidth,
        Vector2 cameraMin,
        Vector2 cameraMax)
    {
        float prefabLeft = position.x - prefabHalfWidth;
        float prefabRight = position.x + prefabHalfWidth;

        return prefabRight < cameraMin.x || prefabLeft > cameraMax.x;
    }

    private bool IsTooCloseToAnotherMonster(Vector2 spawnPosition)
    {
        if (minimumMonsterSpacing <= 0f)
            return false;

        Collider2D[] nearbyColliders = Physics2D.OverlapCircleAll(
            spawnPosition,
            minimumMonsterSpacing,
            monsterLayers);

        foreach (Collider2D nearbyCollider in nearbyColliders)
        {
            if (nearbyCollider != null &&
                nearbyCollider.GetComponentInParent<IDamageable>() != null)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsValidMonsterPrefab(GameObject prefab)
    {
        if (prefab.GetComponentInChildren<Collider2D>(true) == null)
        {
            Debug.LogWarning(
                $"{name}: '{prefab.name}' 프리팹에 Collider2D가 없습니다.",
                this);
            return false;
        }

        if (TryGetMonsterController(prefab, out _))
            return true;

        Debug.LogWarning(
            $"{name}: '{prefab.name}' 프리팹에 IDamageable 몬스터 컴포넌트가 없습니다.",
            this);
        return false;
    }

    private static int CountAliveMonsters(GameObject prefab)
    {
        if (!TryGetMonsterController(prefab, out MonoBehaviour prefabController))
            return 0;

        Type controllerType = prefabController.GetType();
        MonoBehaviour[] sceneBehaviours =
            FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        int aliveCount = 0;

        foreach (MonoBehaviour behaviour in sceneBehaviours)
        {
            if (behaviour == null || behaviour.GetType() != controllerType)
                continue;

            if (!HasPrefabInstanceName(
                    behaviour.transform,
                    prefab.name))
            {
                continue;
            }

            if (behaviour is IDamageable damageable && !damageable.IsDead)
                aliveCount++;
        }

        return aliveCount;
    }

    private static bool HasPrefabInstanceName(
        Transform target,
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

    private static bool TryGetMonsterController(
        GameObject target,
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

    private static float GetPrefabHalfWidth(GameObject prefab)
    {
        if (TryGetCombinedSpriteBounds(prefab, out Bounds spriteBounds))
            return Mathf.Max(0.01f, spriteBounds.extents.x);

        Collider2D prefabCollider =
            prefab.GetComponentInChildren<Collider2D>(true);

        return prefabCollider != null
            ? Mathf.Max(0.01f, prefabCollider.bounds.extents.x)
            : 0.5f;
    }

    private static float GetPrefabBottomOffset(GameObject prefab)
    {
        Collider2D prefabCollider =
            prefab.GetComponentInChildren<Collider2D>(true);

        if (prefabCollider != null)
        {
            return Mathf.Max(
                0f,
                prefab.transform.position.y - prefabCollider.bounds.min.y);
        }

        if (TryGetCombinedSpriteBounds(prefab, out Bounds spriteBounds))
        {
            return Mathf.Max(
                0f,
                prefab.transform.position.y - spriteBounds.min.y);
        }

        return 0f;
    }

    private static bool TryGetCombinedSpriteBounds(
        GameObject prefab,
        out Bounds combinedBounds)
    {
        SpriteRenderer[] renderers =
            prefab.GetComponentsInChildren<SpriteRenderer>(true);

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

    private void OnValidate()
    {
        spawnCheckInterval = Mathf.Max(0.01f, spawnCheckInterval);
        minimumDistanceOutsideCamera = Mathf.Max(
            0f,
            minimumDistanceOutsideCamera);
        maximumDistanceOutsideCamera = Mathf.Max(
            minimumDistanceOutsideCamera,
            maximumDistanceOutsideCamera);
        maximumSpawnAttempts = Mathf.Max(1, maximumSpawnAttempts);
        groundRayStartHeight = Mathf.Max(0f, groundRayStartHeight);
        groundRayDistance = Mathf.Max(0.01f, groundRayDistance);
        groundClearance = Mathf.Max(0f, groundClearance);
        minimumMonsterSpacing = Mathf.Max(0f, minimumMonsterSpacing);
    }

    private void OnDrawGizmosSelected()
    {
        if (targetCamera == null)
            return;

        GetCameraWorldBounds(out Vector2 cameraMin, out Vector2 cameraMax);
        float cameraHeight = cameraMax.y - cameraMin.y;
        float yCenter = (cameraMin.y + cameraMax.y) * 0.5f;

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(
            new Vector3(
                cameraMin.x -
                (minimumDistanceOutsideCamera + maximumDistanceOutsideCamera) * 0.5f,
                yCenter),
            new Vector3(
                maximumDistanceOutsideCamera - minimumDistanceOutsideCamera,
                cameraHeight));
        Gizmos.DrawWireCube(
            new Vector3(
                cameraMax.x +
                (minimumDistanceOutsideCamera + maximumDistanceOutsideCamera) * 0.5f,
                yCenter),
            new Vector3(
                maximumDistanceOutsideCamera - minimumDistanceOutsideCamera,
                cameraHeight));
    }
}
