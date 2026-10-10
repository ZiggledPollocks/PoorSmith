// [코드 지도] FieldSceneTravel: 마을·필드 씬 이동 전후 플레이어 위치, 저장 스냅샷과 복구를 조정한다.
// 주요 함수: ChangeScene, WarpWithinField, GroundedArrival
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/Campaign/Travel/FieldSceneTravel.cs.md

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Fades between the authored town and the standalone gathering map.</summary>
public sealed class FieldSceneTravel : MonoBehaviour
{
    public const string FieldScenePath = "Assets/JinHo/Scenes/FieldMapStructureTest.unity";
    public const string TownScenePath = "Assets/JinHo/Scenes/SampleScene.unity";
    public static readonly Vector2 FieldArrival = new(0f, 3f);
    const float MaximumArrivalGroundCorrection = 3.5f;

    static readonly RaycastHit2D[] arrivalHits=new RaycastHit2D[64];
    static readonly Collider2D[] arrivalOverlaps=new Collider2D[32];
    // Query marker-relative feet, not the old player position. Never set grounded;
    // PlayerMovement detects the resulting contact/cast after input resumes.
    internal static Vector2 GroundedArrival(Transform player,Vector2 marker,Vector2? safeMarker=null)
    {
        if(player==null)return marker;
        Collider2D body=null;
        foreach(var c in player.GetComponents<Collider2D>())if(c.enabled&&!c.isTrigger){body=c;break;}
        if(body==null)return marker;
        Physics2D.SyncTransforms();
        var movement=player.GetComponent<PlayerMovement>();
        int mask=movement!=null&&movement.GroundLayers.value!=0?movement.GroundLayers.value:LayerMask.GetMask("Ground");
        if(mask==0)return marker;
        float normal=movement!=null?movement.MinimumGroundNormalY:.65f;
        Vector2 fallback=safeMarker??FieldArrival;
        if(safeMarker==null&&CampaignController.Instance is { } town)
            fallback=SmithyInterior.Instance is { } smithy&&smithy.Contains(marker)?(Vector2)smithy.entry.position:town.townSpawn;
        else if(safeMarker==null&&FindFirstObjectByType<CaveEntranceBackgroundTransition>() is { } cave)
            fallback=cave.SafeArrivalFor(marker);
        if(TryArrivalFloor(body,player,marker,mask,normal,out var result))return result;
        if(TryArrivalFloor(body,player,fallback,mask,normal,out result))return result;
        // Search immediately around the authored safe point, never another cave floor.
        for(int i=1;i<=4;i++)foreach(int direction in new[]{-1,1})
            if(TryArrivalFloor(body,player,fallback+Vector2.right*(i*direction),mask,normal,out result))return result;
        Debug.LogWarning("지역 도착 지점의 바닥을 찾지 못해 기본 지점을 사용합니다.");return fallback;
    }
    static bool TryArrivalFloor(Collider2D body,Transform player,Vector2 marker,int mask,float normal,out Vector2 result)
    {
        result=marker;Bounds bounds=body.bounds;
        Vector2 displacement=marker-(Vector2)player.position;
        float feet=bounds.min.y+displacement.y,centerX=bounds.center.x+displacement.x;
        var filter=new ContactFilter2D{useLayerMask=true,layerMask=mask,useTriggers=false};
        float highest=float.NegativeInfinity;
        for(int probe=-1;probe<=1;probe++)
        {
            float nearest=float.PositiveInfinity,surface=0;
            // A ray starting in a composite cave ceiling returns an inside hit and
            // hides its floor. Also probe from the feet/body interior and reject distance zero.
            for(int level=0;level<3;level++)
            {
                float rise=level==0?.1f:level==1?bounds.size.y*.5f:MaximumArrivalGroundCorrection+.1f;
                var origin=new Vector2(centerX+probe*bounds.extents.x*.9f,feet+rise);
                int count=Physics2D.Raycast(origin,Vector2.down,filter,arrivalHits,rise+MaximumArrivalGroundCorrection+.1f);
                for(int i=0;i<count;i++)
                {
                    var hit=arrivalHits[i];
                    if(hit.collider==null||hit.distance<=.001f||hit.collider==body||hit.collider.attachedRigidbody==body.attachedRigidbody&&body.attachedRigidbody!=null||hit.normal.y<normal)continue;
                    float delta=hit.point.y+.02f-feet;
                    if(Mathf.Abs(delta)>MaximumArrivalGroundCorrection||Mathf.Abs(delta)>=nearest)continue;
                    nearest=Mathf.Abs(delta);surface=hit.point.y;
                }
            }
            if(!float.IsPositiveInfinity(nearest))highest=Mathf.Max(highest,surface);
        }
        if(float.IsNegativeInfinity(highest))return false;
        result=new Vector2(marker.x,marker.y+highest+.02f-feet);
        Vector2 center=(Vector2)bounds.center+result-(Vector2)player.position;
        int overlaps=Physics2D.OverlapBox(center,(Vector2)bounds.size-Vector2.one*.04f,0,filter,arrivalOverlaps);
        for(int i=0;i<overlaps;i++)if(arrivalOverlaps[i]!=body&&!(body.attachedRigidbody!=null&&arrivalOverlaps[i].attachedRigidbody==body.attachedRigidbody))return false;
        return true;
    }

    // Match CampaignController.Teleport's initial camera placement. Cinemachine
    // resumes tracking from this position after the scene fade or warp.
    internal static void AlignCameraToPlayer(Transform player)
    {
        var camera = Camera.main;
        if (player == null || camera == null) return;
        camera.transform.position = new Vector3(player.position.x, player.position.y + 3f,
            camera.transform.position.z);
    }

    static FieldSceneTravel instance;
    Image overlay;
    bool busy;
    float previousTimeScale;
    bool previousExternalActivity;

    public static bool Busy => instance != null && instance.busy;

    public static bool BeginToField()
    {
        return Begin(FieldScenePath, true);
    }

    public static bool BeginToTown()
    {
        return Begin(TownScenePath, false);
    }

    public static bool BeginToTown(bool showMainMenuOnArrival)
    {
        return Begin(TownScenePath, false, showMainMenuOnArrival);
    }

    public static bool BeginFieldWarp(Vector2 destination)
    {
        if (Busy || SceneManager.GetActiveScene().path != FieldScenePath ||
            SmithingLoop.Instance?.Initialized != true || SmithingLoop.Instance.SaveBlocked ||
            FindFirstObjectByType<FieldSceneState>()?.Ready != true) return false;
        Ensure().StartCoroutine(instance.WarpWithinField(destination));
        return true;
    }

    // 핵심 분기: player == null 판정.
    // 상태 변경: busy 갱신.
    // 다음 연결: FieldSceneTravel.Fade(float, float) 호출.
    IEnumerator WarpWithinField(Vector2 destination)
    {
        busy = true;
        previousTimeScale = Time.timeScale;
        previousExternalActivity = GameUIController.ExternalActivity;
        GameUIController.ExternalActivity = true;
        Time.timeScale = 0f;
        overlay.gameObject.SetActive(true);
        try
        {
            yield return Fade(0f, 1f);
            var player = FindFirstObjectByType<FieldSceneState>();
            if (player == null) yield break;
            destination=GroundedArrival(player.transform,destination);
            var target = new Vector3(destination.x, destination.y, player.transform.position.z);
            var old = player.transform.position;
            player.transform.position = target;
            var body = player.GetComponent<Rigidbody2D>();
            if (body != null) { body.position = destination; body.linearVelocity = Vector2.zero; body.angularVelocity=0f; }
            Unity.Cinemachine.CinemachineCore.OnTargetObjectWarped(player.transform, target - old);
            foreach (var camera in FindObjectsByType<Unity.Cinemachine.CinemachineVirtualCameraBase>(FindObjectsSortMode.None))
                camera.PreviousStateIsValid = false;
            player.GetComponent<PlayerMovement>()?.ResetAfterTeleport();
            player.GetComponent<PlayerInputHandler>()?.ClearGameplayInput();
            Physics2D.SyncTransforms();
            FindFirstObjectByType<CaveEntranceBackgroundTransition>()?.RefreshImmediatelyAfterTeleport();
            FindFirstObjectByType<FieldRegionCameraBounds>()?.Refresh();
            AlignCameraToPlayer(player.transform);
            SmithingLoop.Instance.RequestAutosave();
            yield return null;
            yield return Fade(1f, 0f);
        }
        finally
        {
            overlay.color = new Color(0f, 0f, 0f, 0f);
            overlay.gameObject.SetActive(false);
            Time.timeScale = previousTimeScale;
            GameUIController.ExternalActivity = previousExternalActivity;
            busy = false;
        }
    }

    static bool Begin(string destinationPath, bool toField, bool showMainMenuOnArrival = false)
    {
        var loop = SmithingLoop.Instance;
        if (Busy || loop == null || !loop.Initialized || loop.SaveBlocked ||
            !Application.CanStreamedLevelBeLoaded(destinationPath) ||
            !loop.TryPrepareSceneTravel(destinationPath, toField)) return false;
        try
        {
            Ensure().StartCoroutine(instance.ChangeScene(destinationPath, toField, loop, showMainMenuOnArrival));
            return true;
        }
        catch (Exception e)
        {
            loop.CancelPreparedSceneTravel();
            Debug.LogError("Field scene travel could not start: " + e.Message);
            return false;
        }
    }

    // 핵심 분기: state == null || inventory == null || SceneManager.GetActiveScene().path != FieldScenePath 판정.
    // 상태 변경: state.health 갱신.
    public static void CaptureFieldHealth(CampaignState state, InventorySystem inventory)
    {
        if (state == null || inventory == null ||
            SceneManager.GetActiveScene().path != FieldScenePath) return;
        // Startup autosave must not replace the loaded value with the new actor Awake default.
        if (inventory.GetComponent<FieldSceneState>()?.Ready != true) return;
        var health = inventory.GetComponent<PlayerAssimilate>();
        if (health != null && health.IsDead)
        {
            state.health = health.MaxAssimilation;
            state.sceneName = System.IO.Path.GetFileNameWithoutExtension(TownScenePath);
            // The town scene owns its authored spawn; a field save cannot know its coordinates.
            state.hasPosition = false;
            state.inTown = true;
            return;
        }
        if (health != null && !health.IsDead)
            state.health = health.RemainingHealth;
        state.sceneName = SceneManager.GetActiveScene().name;
        state.hasPosition = true;
        state.x = inventory.transform.position.x;
        state.y = inventory.transform.position.y;
        state.inTown = false;
    }

    // 핵심 분기: instance != null 판정.
    // 상태 변경: instance 갱신.
    static FieldSceneTravel Ensure()
    {
        if (instance != null) return instance;
        var root = new GameObject("FieldSceneTravel", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        instance = root.AddComponent<FieldSceneTravel>();
        DontDestroyOnLoad(root);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32760;
        var child = new GameObject("SceneFade", typeof(RectTransform), typeof(Image));
        child.transform.SetParent(root.transform, false);
        var rect = (RectTransform)child.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        instance.overlay = child.GetComponent<Image>();
        instance.overlay.color = new Color(0f, 0f, 0f, 0f);
        instance.overlay.raycastTarget = true;
        child.SetActive(false);
        return instance;
    }

    // 핵심 분기: operation == null 판정.
    // 상태 변경: busy 갱신.
    // 다음 연결: FieldSceneTravel.Fade(float, float) 호출.
    IEnumerator ChangeScene(string path, bool toField, SmithingLoop sourceLoop, bool showMainMenuOnArrival)
    {
        busy = true;
        string sourcePath = sourceLoop.gameObject.scene.path;
        bool destinationReady = false;
        previousTimeScale = Time.timeScale;
        previousExternalActivity = GameUIController.ExternalActivity;
        GameUIController.ExternalActivity = true;
        Time.timeScale = 0f;
        overlay.gameObject.SetActive(true);
        try
        {
            yield return Fade(0f, 1f);
            var operation = SceneManager.LoadSceneAsync(path, LoadSceneMode.Single);
            if (operation == null)
            {
                Debug.LogError("Scene travel could not load: " + path);
                yield return Fade(1f, 0f);
                yield break;
            }
            yield return operation;

            float deadline = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < deadline &&
                   (SmithingLoop.Instance == null || !SmithingLoop.Instance.Initialized ||
                    (toField && FindFirstObjectByType<FieldSceneState>()?.Ready != true) ||
                    (!toField && (CampaignController.Instance == null ||
                                  !CampaignController.Instance.Ready))))
                yield return null;

            if (SmithingLoop.Instance == null || !SmithingLoop.Instance.Initialized ||
                SmithingLoop.Instance.SaveBlocked ||
                (toField && FindFirstObjectByType<FieldSceneState>()?.Ready != true) ||
                (!toField && (CampaignController.Instance == null ||
                              !CampaignController.Instance.Ready)))
            {
                Debug.LogError("Scene travel initialized incompletely: " + path);
                yield return Fade(1f, 0f);
                yield break;
            }

            if (toField)
            {
                var inventory = FindFirstObjectByType<InventorySystem>();
                if (inventory == null)
                {
                    Debug.LogError("Gathering map player is missing");
                    yield return Fade(1f, 0f);
                    yield break;
                }
                else
                {
                    var player = inventory.transform;
                    Vector3 oldPosition = player.position;
                    player.position = new Vector3(FieldArrival.x, FieldArrival.y,
                        player.position.z);
                    Vector2 groundedArrival = GroundedArrival(player, FieldArrival);
                    player.position = new Vector3(groundedArrival.x, groundedArrival.y,
                        player.position.z);
                    Unity.Cinemachine.CinemachineCore.OnTargetObjectWarped(player,
                        player.position - oldPosition);
                    var rb = player.GetComponent<Rigidbody2D>();
                    if (rb != null) { rb.position = groundedArrival; rb.linearVelocity = Vector2.zero; rb.angularVelocity=0f; }
                    foreach (var vcam in FindObjectsByType<Unity.Cinemachine.CinemachineVirtualCameraBase>(FindObjectsSortMode.None))
                        vcam.PreviousStateIsValid = false;
                    player.GetComponent<PlayerMovement>()?.ResetAfterTeleport();
                    player.GetComponent<PlayerInputHandler>()?.ClearGameplayInput();
                    Physics2D.SyncTransforms();
                    FindFirstObjectByType<CaveEntranceBackgroundTransition>()
                        ?.RefreshImmediatelyAfterTeleport();
                    FindFirstObjectByType<FieldRegionCameraBounds>()?.Refresh();
                    AlignCameraToPlayer(player);
                }
            }
            else
            {
                var campaign = CampaignController.Instance;
                campaign.Teleport(campaign.townSpawn);
            }
            destinationReady = true;
            var gameUI = GameUIController.Instance;
            gameUI?.Play();
            if (showMainMenuOnArrival)
            {
                // Rebase the destination menu pause after the transition's zero time scale.
                Time.timeScale = 1f;
                gameUI?.ShowMainMenu();
                if (!SmithingLoop.Instance.SaveProgress())
                    Debug.LogWarning("Town respawn is active, but its save could not be refreshed.");
            }
            yield return null; // Camera and background move while still opaque.
            yield return Fade(1f, 0f);
        }
        finally
        {
            if (!destinationReady)
            {
                if (sourceLoop != null && SceneManager.GetActiveScene().path == sourcePath)
                    sourceLoop.CancelPreparedSceneTravel();
                else
                    SmithingLoop.DiscardPendingSceneTravel();
            }
            if (overlay != null)
            {
                overlay.color = new Color(0f, 0f, 0f, 0f);
                overlay.gameObject.SetActive(false);
            }
            Time.timeScale = showMainMenuOnArrival && destinationReady &&
                GameUIController.Instance?.MainMenuVisible == true ? 0f : previousTimeScale;
            GameUIController.ExternalActivity = previousExternalActivity;
            busy = false;
        }
    }

    IEnumerator Fade(float from, float to)
    {
        float elapsed = 0f;
        while (elapsed < 0.2f)
        {
            elapsed += Time.unscaledDeltaTime;
            overlay.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, elapsed / 0.2f));
            yield return null;
        }
        overlay.color = new Color(0f, 0f, 0f, to);
    }

    void OnDestroy()
    {
        if (instance != this) return;
        if (busy)
        {
            Time.timeScale = previousTimeScale;
            GameUIController.ExternalActivity = previousExternalActivity;
        }
        instance = null;
    }
}
