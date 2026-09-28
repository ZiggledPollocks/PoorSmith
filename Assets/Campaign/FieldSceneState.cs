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
    float explorationClock, saveClock;
    bool returningAfterDeath;

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

    IEnumerator Start()
    {
        while (SmithingLoop.Instance == null || !SmithingLoop.Instance.Initialized)
            yield return null;
        health = GetComponent<PlayerAssimilate>();
        inventory = GetComponent<InventorySystem>();
        exploration = GetComponent<CampaignExploration>();
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
        float restored = Mathf.Clamp(state.health,
            1f, health.MaxAssimilation);
        health.Assimilate(restored - health.CurrentAssimilation);
        health.Died += OnDeath;
        Ready = true;
    }

    void Update()
    {
        if (!Ready || returningAfterDeath || SmithingLoop.Instance == null ||
            GameUIController.BlocksGameplayInput || SmithingLoop.Instance.InShop) return;
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

    void OnDeath()
    {
        if (returningAfterDeath || inventory == null) return;
        returningAfterDeath = true;
        inventory.RestoreSnapshot(System.Array.Empty<InventoryItem>());
        var state = SmithingLoop.Instance.Campaign;
        state.gold -= Mathf.FloorToInt(state.gold * .04f);
        state.health = health.MaxAssimilation;
        SmithingLoop.Instance.RequestAutosave();
        StartCoroutine(ReturnAfterDeath());
    }

    IEnumerator ReturnAfterDeath()
    {
        yield return new WaitForSecondsRealtime(2.5f);
        // The field death screen owns a pause. Release it before the travel fade
        // captures the previous time scale, otherwise town arrives frozen.
        GameUIController.Instance?.Play();
        if (!FieldSceneTravel.BeginToTown())
            Debug.LogError("Field death return failed; check the town build scene and save state.");
    }

    void OnDestroy()
    {
        if (health != null) health.Died -= OnDeath;
    }
}
