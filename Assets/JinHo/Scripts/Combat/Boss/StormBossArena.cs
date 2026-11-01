// [코드 지도] StormBossArena: 보스 구역 안에 살아 있는 플레이어가 있는지 감지하고 보스 활성 상태를 전달한다. 보스 인스턴스가 없으면 프리팹으로 만들고 지면 위 시작 위치를 설정한다. 개별 Collider를 HashSet에 저장하여 플레이어의 여러 충돌체 진입을 구별한다.
// 주요 함수: EnsureBossExists, UpdateBossActivation, FindBossSpawnPosition
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/Combat/Boss/StormBossArena.cs.md

using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
/// <summary>Controls boss activation from arena occupancy.</summary>
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
    private Vector2 lastInside;

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
        var campaign = CampaignController.Instance;
        if (playerColliders.Count == 0 && boss != null && !boss.IsDead && campaign?.Ready == true && !campaign.UI.IsOpen && !other.GetComponentInParent<PlayerAssimilate>().IsDead)
            campaign.UI.Confirm("보스 구역을 나갈까요?", "나가면 보스의 체력과 공격 단계가 초기화됩니다.", () => { campaign.UI.Close(); boss.ResetEncounter(); }, () => campaign.Teleport(lastInside));
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

        lastInside = other.GetComponentInParent<PlayerAssimilate>().transform.position;
        playerColliders.Add(other);
        UpdateBossActivation();
    }

    private void UpdateBossActivation()
    {
        playerColliders.RemoveWhere(collider => collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy);

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
