// [코드 지도] FieldSceneState: 필드 씬 준비·복귀와 숲/동굴 층별 시간 경과 동화율을 관리한다.
// 주요 함수: Start, Update, OnDeath
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/Campaign/Field/FieldSceneState.cs.md

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Continues the non-crafting campaign state in the standalone gathering map.</summary>
[RequireComponent(typeof(CampaignExploration))]
public sealed class FieldSceneState : MonoBehaviour
{
    PlayerAssimilate health;
    InventorySystem inventory;
    CampaignExploration exploration;
    CaveEntranceBackgroundTransition caveEntrance;
    CaveFloorOcclusion caveFloors;
    float explorationClock, saveClock;
    float assimilationClock;
    int assimilationRegion = -1;
    bool deathHandled;

    public bool Ready { get; private set; }
    public float MovementMultiplier
    {
        get
        {
            if (inventory == null) return 1f;
            float overweight = inventory.CurrentWeight > inventory.settingsWeight ? -.1f : 0f;
            return 1f + overweight + (GetComponent<CampaignCombat>()?.WindArmorBonus ?? 0f);
        }
    }

    // 핵심 분기: health == null 판정.
    // 상태 변경: health 갱신.
    // 다음 연결: CampaignExploration.Restore(System.Collections.Generic.IEnumerable<string>) 호출.
    IEnumerator Start()
    {
        while (SmithingLoop.Instance == null || !SmithingLoop.Instance.Initialized)
            yield return null;
        health = GetComponent<PlayerAssimilate>();
        inventory = GetComponent<InventorySystem>();
        exploration = GetComponent<CampaignExploration>();
        caveEntrance = FindFirstObjectByType<CaveEntranceBackgroundTransition>();
        caveFloors = FindFirstObjectByType<CaveFloorOcclusion>();
        if (health == null) yield break;
        var state = SmithingLoop.Instance.Campaign;
        state.visitedCells ??= new List<string>();
        state.usedAltars ??= new List<string>();
        exploration.Restore(state.visitedCells);
        if (state.hasPosition && state.sceneName == gameObject.scene.name)
        {
            Vector3 previous = transform.position;
            Vector3 destination = new Vector3(state.x, state.y, previous.z);
            transform.position = destination;
            var rb = GetComponent<Rigidbody2D>();
            if (rb != null) { rb.position = new Vector2(state.x, state.y); rb.linearVelocity = Vector2.zero; }
            Unity.Cinemachine.CinemachineCore.OnTargetObjectWarped(transform, destination - previous);
            GetComponent<PlayerMovement>()?.ResetAfterTeleport();
            GetComponent<PlayerInputHandler>()?.ClearGameplayInput();
            Physics2D.SyncTransforms();
            // Saves made before the entrance fade may resume in the old visible
            // seam. Land those upper-cave positions on the new interior plateau.
            FindFirstObjectByType<CaveEntranceBackgroundTransition>()
                ?.RefreshImmediatelyAfterTeleport(true);
            FindFirstObjectByType<FieldRegionCameraBounds>()?.Refresh();
        }
        else
        {
            // Opening the authored field scene directly should start with the
            // player's feet resting on its cave floor, just like scene travel.
            Vector2 marker = transform.position;
            Vector2 grounded = FieldSceneTravel.GroundedArrival(transform, marker);
            if (grounded != marker)
            {
                Vector3 previous = transform.position;
                transform.position = new Vector3(grounded.x, grounded.y, previous.z);
                var rb = GetComponent<Rigidbody2D>();
                if (rb != null) { rb.position = grounded; rb.linearVelocity = Vector2.zero; }
                Unity.Cinemachine.CinemachineCore.OnTargetObjectWarped(transform,
                    transform.position - previous);
                GetComponent<PlayerMovement>()?.ResetAfterTeleport();
                Physics2D.SyncTransforms();
                FindFirstObjectByType<FieldRegionCameraBounds>()?.Refresh();
            }
        }
        FieldSceneTravel.AlignCameraToPlayer(transform);
        // A save without a player position has no trustworthy health snapshot yet.
        // Use the same fresh-start rule as the town scene.
        health.RestoreRemainingHealth(state.hasPosition ? state.health : health.MaxAssimilation);
        GetComponent<LiquidCircleGaugeHUD>()?.SyncRestoredValue();
        health.Died += OnDeath;
        Ready = true;
    }

    void Update()
    {
        if (!Ready || deathHandled || SmithingLoop.Instance == null) return;
        UpdateFieldAssimilation();
        if (GameUIController.BlocksGameplayInput || SmithingLoop.Instance.InShop) return;
        var state = SmithingLoop.Instance.Campaign;
        state.playSeconds += Time.deltaTime;
        explorationClock += Time.deltaTime;
        if (explorationClock >= .25f)
        {
            explorationClock = 0f;
            exploration.Reveal(transform.position, state);
        }
        saveClock += Time.unscaledDeltaTime;
        if (saveClock >= 30f)
        {
            saveClock = 0f;
            SmithingLoop.Instance.RequestAutosave();
        }
    }

    // One timer owns field exposure; changing region or floor starts its new interval afresh.
    void UpdateFieldAssimilation()
    {
        if (health == null || health.IsDead) return;
        bool insideCave = caveEntrance != null && caveEntrance.IsInsideCave;
        int floor = insideCave && caveFloors != null
            ? Mathf.Clamp(caveFloors.GetFloorIndex(transform.position.y), 0, 3) : 0;
        int region = insideCave ? floor + 1 : 0;
        if (assimilationRegion != region)
        {
            assimilationRegion = region;
            assimilationClock = 0f;
        }

        float interval = region switch
        {
            0 => 5f,     // Forest
            1 or 2 => 4f, // Cave floors 1 and 2
            3 => 3.5f,   // Cave floor 3
            _ => 3f      // Cave floor 4
        };
        assimilationClock += Time.deltaTime;
        while (assimilationClock >= interval && !health.IsDead)
        {
            assimilationClock -= interval;
            health.Assimilate(1);
        }
    }

    void OnDeath()
    {
        if (deathHandled || inventory == null) return;
        deathHandled = true;
        inventory.RestoreSnapshot(System.Array.Empty<InventoryItem>());
        var state = SmithingLoop.Instance.Campaign;
        state.gold -= Mathf.FloorToInt(state.gold * .04f);
        state.health = health.MaxAssimilation;
        SmithingLoop.Instance.SmithData.hp = health.MaxAssimilation;
        // Death is a durable town respawn, even if the player quits at the death screen.
        // CaptureFieldHealth keeps this town destination while the field player is dead.
        if (!SmithingLoop.Instance.SaveProgress(commitFieldItems: true))
            Debug.LogError("Field death could not be saved; the existing save was preserved.");
    }

    void OnDestroy()
    {
        if (health != null) health.Died -= OnDeath;
    }
}
