using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class StormBossArena : MonoBehaviour
{
    [Header("Arena")]
    [SerializeField] private BoxCollider2D arenaCollider;
    [SerializeField] private LayerMask groundLayers;

    [Header("Boss")]
    [SerializeField] private StormBossController boss;
    [SerializeField] private GameObject bossPrefab;
    [SerializeField, Min(0f)] private float bossGroundClearance = 0.15f;

    private readonly HashSet<Collider2D> playerColliders = new();

    public BoxCollider2D ArenaCollider => arenaCollider;
    public Bounds ArenaBounds => arenaCollider != null
        ? arenaCollider.bounds
        : new Bounds(transform.position, Vector3.zero);

    private void Awake()
    {
        arenaCollider ??= GetComponent<BoxCollider2D>();
        arenaCollider.isTrigger = true;
        EnsureBossExists();
    }

    private void OnTriggerEnter2D(Collider2D other) => RegisterPlayer(other);
    private void OnTriggerStay2D(Collider2D other) => RegisterPlayer(other);

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other == null || !playerColliders.Remove(other))
            return;

        UpdateBossActivation();
    }

    private void OnDisable()
    {
        playerColliders.Clear();
        if (boss != null)
            boss.SetArenaActive(false, null);
    }

    private void RegisterPlayer(Collider2D other)
    {
        if (other == null || other.GetComponentInParent<PlayerAssimilate>() == null)
            return;

        if (playerColliders.Add(other))
            UpdateBossActivation();
    }

    private void UpdateBossActivation()
    {
        playerColliders.RemoveWhere(collider => collider == null);

        PlayerAssimilate player = null;
        foreach (Collider2D collider in playerColliders)
        {
            player = collider.GetComponentInParent<PlayerAssimilate>();
            if (player != null && !player.IsDead)
                break;
            player = null;
        }

        if (boss != null)
            boss.SetArenaActive(player != null, player);
    }

    private void EnsureBossExists()
    {
        boss ??= GetComponentInChildren<StormBossController>(true);
        if (boss == null && bossPrefab != null)
        {
            GameObject instance = Instantiate(bossPrefab, transform);
            instance.name = bossPrefab.name;
            boss = instance.GetComponent<StormBossController>();
        }

        if (boss == null)
        {
            Debug.LogError($"{name}: Storm Boss prefab or controller is missing.", this);
            return;
        }

        boss.transform.position = FindBossSpawnPosition();
        boss.ConfigureArena(this, arenaCollider);
        boss.SetArenaActive(false, null);
    }

    private Vector2 FindBossSpawnPosition()
    {
        Bounds bounds = ArenaBounds;
        float x = bounds.center.x;
        Vector2 rayOrigin = new(x, bounds.max.y + 1f);
        float rayDistance = bounds.size.y + 2f;
        RaycastHit2D hit = Physics2D.Raycast(
            rayOrigin, Vector2.down, rayDistance, groundLayers);
        float y = hit.collider != null ? hit.point.y : bounds.min.y;
        return new Vector2(x, y + bossGroundClearance);
    }

    private void OnValidate()
    {
        arenaCollider ??= GetComponent<BoxCollider2D>();
        if (arenaCollider != null)
            arenaCollider.isTrigger = true;
        bossGroundClearance = Mathf.Max(0f, bossGroundClearance);
    }
}
