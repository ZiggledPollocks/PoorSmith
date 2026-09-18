#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using SettingsMenuUI;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Batch-only smoke test. Run against an isolated copy, never the working editor.</summary>
public static class UIIntegrationSmokeTest
{
    private const string RunningKey = "BatterMap.UIValidation.Running";
    private static int stage;
    private static double nextStep;
    private static double deadline;

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch-mode project.");
        PlayerSettings.companyName = "BatterMapValidation";
        PlayerSettings.productName = "UIIntegrationSmokeTest";
        SettingsMenuUI.Editor.SettingsMenuUIBuilder.BuildScene();
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        SessionState.SetBool(RunningKey, true);
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    private static void Resume()
    {
        if (!SessionState.GetBool(RunningKey, false)) return;
        stage = 0;
        deadline = EditorApplication.timeSinceStartup + 90;
        nextStep = EditorApplication.timeSinceStartup + 3;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        Debug.Log("UI TEST PASS: " + message);
    }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup > deadline) { Finish(false, "Timeout"); return; }
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < nextStep) return;
        try
        {
            GameUIController ui = GameUIController.Instance;
            Require(ui != null, "runtime UI bootstrap");
            UIManager settings = ui.GetComponent<UIManager>();
            InventoryUIController inventory = Object.FindFirstObjectByType<InventoryUIController>();
            switch (stage++)
            {
                case 0:
                    Require(Time.timeScale == 1 && !ui.HasModal && !GameUIController.BlocksGameplayInput,
                        "game starts immediately without initial menu");
                    Require(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 1, "single EventSystem");
                    Require(ui.transform.Find("InventoryCanvas") != null, "inventory owned by shared UI");
                    Transform water = ui.transform.Find("HUDCanvas/CircleGauge/Water");
                    Require(water != null, "liquid gauge is attached to shared HUD");
                    Material liquidMaterial = water.GetComponent<Image>().material;
                    Require(liquidMaterial != null &&
                        liquidMaterial.GetFloat("_CircleRadius") <= 0.4451f,
                        "liquid circle is clipped inside the outline");
                    ui.ShowMainMenu();
                    Require(Time.timeScale == 0 && ui.HasModal && GameUIController.BlocksGameplayInput,
                        "main menu can still be opened and pauses gameplay");
                    CaptureUI(ui, "ui-main-menu.png");
                    ui.OpenSettings();
                    break;
                case 1:
                    Require(settings.IsSettingsOpen && Time.timeScale == 0, "settings open from main menu");
                    ControlSettingsController controls = ui.GetComponentInChildren<ControlSettingsController>(true);
                    PlayerInput player = inventory.GetComponent<PlayerInput>();
                    Require(ReferenceEquals(controls.MoveAction.actionMap.asset, player.actions), "rebindings target live PlayerInput.actions");
                    SettingsTabController tabs = ui.GetComponentInChildren<SettingsTabController>(true);
                    Require(tabs.Tabs.Count == 3, "only implemented settings tabs shown");
                    foreach (TMP_Text label in ui.GetComponentsInChildren<TMP_Text>(true))
                        Require(label.font != null, "font reference: " + label.name);
                    CaptureUI(ui, "ui-settings.png");
                    tabs.SelectTab(1);
                    CaptureUI(ui, "ui-controls.png");
                    string previousBindings = player.actions.SaveBindingOverridesAsJson();
                    Keyboard testKeyboard = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
                    controls.BeginRebind("left", false);
                    InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.J));
                    InputSystem.Update();
                    Require(!controls.IsRebinding, "interactive movement rebind completes");
                    bool hasJ = false;
                    foreach (InputBinding binding in controls.MoveAction.bindings)
                        if (binding.name == "left" && binding.overridePath == "<Keyboard>/j") hasJ = true;
                    Require(hasJ, "new key applied to live movement action");
                    Require(inventory.GetComponent<PlayerInputHandler>().MoveInput == Vector2.zero, "rebind input does not move player");
                    InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
                    InputSystem.Update();
                    player.actions.LoadBindingOverridesFromJson(previousBindings);
                    controls.SaveBindingOverrides();
                    tabs.SelectTab(2);
                    CaptureUI(ui, "ui-sound.png");
                    ui.Sound.LoadSavedSettings(0.65f, false, 0.4f, false, 0.3f, false);
                    Require(Mathf.Approximately(AudioListener.volume, 0.65f), "master audio volume applied");
                    var audioObject = new GameObject("Test SFX", typeof(AudioSource));
                    AudioSource audioSource = audioObject.GetComponent<AudioSource>();
                    audioSource.volume = 0.8f;
                    audioObject.AddComponent<GameAudioChannel>();
                    Require(Mathf.Approximately(audioSource.volume, 0.24f), "SFX channel preserves source volume");
                    Object.Destroy(audioObject);
                    ui.CloseSettings();
                    Require(!settings.IsSettingsOpen && Time.timeScale == 0, "back returns to paused main menu");
                    ui.Play();
                    Require(Time.timeScale == 1 && !ui.HasModal, "play resumes game");
                    break;
                case 2:
                    Require(!GameUIController.BlocksGameplayInput, "gameplay input resumes after close frame");
                    CameraLookAhead lookAhead = Object.FindFirstObjectByType<CameraLookAhead>();
                    CinemachinePositionComposer composer = lookAhead.GetComponent<CinemachinePositionComposer>();
                    float normalDamping = composer.Damping.y;
                    MethodInfo setDamping = typeof(CameraLookAhead).GetMethod(
                        "SetVerticalDamping", BindingFlags.Instance | BindingFlags.NonPublic);
                    for (int i = 0; i < 120; i++) setDamping.Invoke(lookAhead, new object[] { true });
                    Require(composer.Damping.y >= normalDamping * 0.79f,
                        "fall camera keeps smooth vertical damping");
                    float fallDamping = composer.Damping.y;
                    for (int i = 0; i < 180; i++) setDamping.Invoke(lookAhead, new object[] { false });
                    Require(composer.Damping.y > fallDamping && composer.Damping.y <= normalDamping + 0.001f,
                        "vertical damping moves back toward normal after falling");
                    ui.OpenSettings();
                    Require(settings.IsSettingsOpen && Time.timeScale == 0, "in-game settings pause");
                    ui.CloseSettings();
                    Require(Time.timeScale == 1, "closing in-game settings resumes");
                    // Exercise inventory through the same input request consumed by the manager.
                    typeof(PlayerInputHandler).GetProperty("InventoryTogglePressed").SetValue(
                        inventory.GetComponent<PlayerInputHandler>(), true);
                    break;
                case 3:
                    Require(inventory.IsOpen && Time.timeScale == 0, "inventory toggle pauses game");
                    ui.OpenSettings();
                    Require(!inventory.IsOpen && settings.IsSettingsOpen, "settings and inventory are mutually exclusive");
                    ui.ShowMainMenu();
                    Require(Time.timeScale == 0 && ui.HasModal && !settings.IsSettingsOpen, "return to main menu");
                    ui.Play();
                    ui.GetComponentInChildren<SettingsSaveManager>(true).LoadSettings();
                    Require(Mathf.Approximately(ui.Sound.MasterVolume, 0.65f), "saved volume round trip");
                    Finish(true, "All UI integration smoke checks passed.");
                    return;
            }
            nextStep = EditorApplication.timeSinceStartup + 1;
        }
        catch (Exception exception) { Finish(false, exception.ToString()); }
    }

    private static void Finish(bool success, string message)
    {
        SessionState.SetBool(RunningKey, false);
        EditorApplication.update -= Tick;
        Debug.Log((success ? "UI SMOKE SUCCESS: " : "UI SMOKE FAILURE: ") + message);
        EditorApplication.Exit(success ? 0 : 1);
    }

    private static void CaptureUI(GameUIController ui, string filename)
    {
        Canvas canvas = ui.transform.Find("MenuCanvas").GetComponent<Canvas>();
        var cameraObject = new GameObject("UI Test Capture Camera", typeof(Camera));
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.enabled = false;
        camera.orthographic = true;
        camera.orthographicSize = 5;
        camera.cullingMask = 1 << 5;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        var target = new RenderTexture(1920, 1080, 24);
        var texture = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        var layers = new Dictionary<GameObject, int>();
        RenderTexture previousTarget = RenderTexture.active;
        try
        {
            foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true))
            {
                layers.Add(child.gameObject, child.gameObject.layer);
                child.gameObject.layer = 5;
            }
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases();
            foreach (TMP_Text label in canvas.GetComponentsInChildren<TMP_Text>())
                label.ForceMeshUpdate();
            Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            texture.Apply();
            File.WriteAllBytes(Path.Combine(Application.dataPath, "..", filename), texture.EncodeToPNG());
        }
        finally
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
            foreach (var entry in layers) entry.Key.layer = entry.Value;
            RenderTexture.active = previousTarget;
            camera.targetTexture = null;
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(cameraObject);
        }
    }
}
#endif
