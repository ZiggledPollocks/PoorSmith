using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Fades between the authored town and the standalone gathering map.</summary>
public sealed class FieldSceneTravel : MonoBehaviour
{
    public const string FieldScenePath = "Assets/Scenes/FieldMapStructureTest.unity";
    public const string TownScenePath = "Assets/Scenes/SampleScene.unity";
    public static readonly Vector2 FieldArrival = new(0f, 3f);

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

    public static bool BeginFieldWarp(Vector2 destination)
    {
        if (Busy || SceneManager.GetActiveScene().path != FieldScenePath ||
            SmithingLoop.Instance?.Initialized != true || SmithingLoop.Instance.SaveBlocked ||
            FindFirstObjectByType<FieldSceneState>()?.Ready != true) return false;
        Ensure().StartCoroutine(instance.WarpWithinField(destination));
        return true;
    }

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
            var target = new Vector3(destination.x, destination.y, player.transform.position.z);
            var old = player.transform.position;
            player.transform.position = target;
            var body = player.GetComponent<Rigidbody2D>();
            if (body != null) { body.position = destination; body.linearVelocity = Vector2.zero; }
            Unity.Cinemachine.CinemachineCore.OnTargetObjectWarped(player.transform, target - old);
            foreach (var camera in FindObjectsByType<Unity.Cinemachine.CinemachineVirtualCameraBase>(FindObjectsSortMode.None))
                camera.PreviousStateIsValid = false;
            player.GetComponent<PlayerMovement>()?.ResetAfterTeleport();
            player.GetComponent<PlayerInputHandler>()?.ClearGameplayInput();
            Physics2D.SyncTransforms();
            FindFirstObjectByType<CaveEntranceBackgroundTransition>()?.RefreshImmediatelyAfterTeleport();
            FindFirstObjectByType<FieldRegionCameraBounds>()?.Refresh();
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

    static bool Begin(string destinationPath, bool toField)
    {
        var loop = SmithingLoop.Instance;
        if (Busy || loop == null || !loop.Initialized || loop.SaveBlocked ||
            !Application.CanStreamedLevelBeLoaded(destinationPath) ||
            !loop.TryPrepareSceneTravel(destinationPath, toField)) return false;
        try
        {
            Ensure().StartCoroutine(instance.ChangeScene(destinationPath, toField, loop));
            return true;
        }
        catch (Exception e)
        {
            loop.CancelPreparedSceneTravel();
            Debug.LogError("Field scene travel could not start: " + e.Message);
            return false;
        }
    }

    public static void CaptureFieldHealth(CampaignState state, InventorySystem inventory)
    {
        if (state == null || inventory == null ||
            SceneManager.GetActiveScene().path != FieldScenePath) return;
        var health = inventory.GetComponent<PlayerAssimilate>();
        if (health != null && !health.IsDead)
            state.health = health.CurrentAssimilation;
        state.sceneName = SceneManager.GetActiveScene().name;
        state.hasPosition = true;
        state.x = inventory.transform.position.x;
        state.y = inventory.transform.position.y;
        state.inTown = false;
    }

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

    IEnumerator ChangeScene(string path, bool toField, SmithingLoop sourceLoop)
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
                    (!toField && (CampaignController.Instance == null ||
                                  !CampaignController.Instance.Ready))))
                yield return null;

            if (SmithingLoop.Instance == null || !SmithingLoop.Instance.Initialized ||
                SmithingLoop.Instance.SaveBlocked ||
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
                    Unity.Cinemachine.CinemachineCore.OnTargetObjectWarped(player,
                        player.position - oldPosition);
                    var rb = player.GetComponent<Rigidbody2D>();
                    if (rb != null) { rb.position = FieldArrival; rb.linearVelocity = Vector2.zero; }
                    foreach (var vcam in FindObjectsByType<Unity.Cinemachine.CinemachineVirtualCameraBase>(FindObjectsSortMode.None))
                        vcam.PreviousStateIsValid = false;
                    player.GetComponent<PlayerMovement>()?.ResetAfterTeleport();
                    player.GetComponent<PlayerInputHandler>()?.ClearGameplayInput();
                    Physics2D.SyncTransforms();
                    FindFirstObjectByType<CaveEntranceBackgroundTransition>()
                        ?.RefreshImmediatelyAfterTeleport();
                    FindFirstObjectByType<FieldRegionCameraBounds>()?.Refresh();
                }
            }
            else
            {
                var campaign = CampaignController.Instance;
                campaign.Teleport(campaign.townSpawn);
            }
            destinationReady = true;
            GameUIController.Instance?.Play();
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
            Time.timeScale = previousTimeScale;
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
