using System.Collections;
using SettingsMenuUI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>One owner for menu navigation, inventory, HUD, pause and settings.</summary>
[DefaultExecutionOrder(-80)]
[DisallowMultipleComponent]
public sealed class GameUIController : MonoBehaviour
{
    public static GameUIController Instance { get; private set; }
    public static bool ExternalActivity { get; set; }
    public static bool BlocksGameplayInput => ExternalActivity || Instance != null &&
        (Instance.HasModal || Time.frameCount <= Instance.blockedThroughFrame);

    [Header("Flow")]
    [SerializeField] private bool showMainMenuOnStart = true;
    [SerializeField] private bool pauseWithInventory = true;
    [SerializeField, Min(0f)] private float deathUiFadeDuration = 0.8f;
    [SerializeField, Min(0f)] private float fallbackDeathAnimationDuration = 1.2f;
    [SerializeField] private GameStateManager state;
    [SerializeField] private UIManager settings;
    [SerializeField] private SettingsSaveManager settingsSave;
    [SerializeField] private ControlSettingsController controls;
    [SerializeField] private SoundSettingsController sound;
    [Header("Views")]
    [SerializeField] private GameObject mainMenu;
    [SerializeField] private TMP_Text playLabel;
    [SerializeField] private Button playButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button backButton;
    [SerializeField] private Button mainMenuButton;
    [Header("Sorting (transition fade remains 1000)")]
    [SerializeField] private int hudSortingOrder = 100;
    [SerializeField] private int inventorySortingOrder = 200;
    [SerializeField] private int menuSortingOrder = 300;

    private InventoryUIController inventory;
    private AssimilationOfferingUIController assimilationOffering;
    private PlayerAssimilate playerAssimilation;
    private PlayerAnimationController playerAnimation;
    private PlayerInputHandler input;
    private LiquidCircleGaugeHUD hud;
    private ToolSelectionHUD toolSelectionHud;
    private GameObject deathScreen;
    private CanvasGroup deathCanvasGroup;
    private Button deathMainMenuButton;
    private Coroutine deathSequenceRoutine;
    private bool deathSequenceActive;
    private bool ownsPause;
    private float previousTimeScale;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLock;
    private int blockedThroughFrame = -1;
    private bool startedPlaying;
    public SoundSettingsController Sound => sound;
    public bool SettingsVisible => settings != null && settings.IsSettingsOpen;
    public bool MainMenuVisible => mainMenu != null && mainMenu.activeSelf;
    public bool HasModal => deathSequenceActive ||
        (mainMenu != null && mainMenu.activeSelf) ||
        (deathScreen != null && deathScreen.activeSelf) ||
        (settings != null && settings.IsSettingsOpen) ||
        (inventory != null && inventory.IsOpen) ||
        (assimilationOffering != null && assimilationOffering.IsOpen);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterBootstrap() => SceneManager.sceneLoaded += OnSceneLoaded;

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (Instance != null || FindFirstObjectByType<InventoryUIController>() == null) return;
        GameObject prefab = Resources.Load<GameObject>("UI/GameUI");
        if (prefab == null)
        {
            Debug.LogError("GameUI prefab missing. Run Tools/batterMap UI/Build Integrated UI.");
            return;
        }
        GameObject root = Instantiate(prefab);
        root.name = "GameUI";
        SceneManager.MoveGameObjectToScene(root, scene);
    }

    public void Configure(GameStateManager gameState, UIManager settingsManager,
        SettingsSaveManager save, ControlSettingsController bindings, SoundSettingsController audio,
        GameObject menu, TMP_Text label, Button play, Button openSettings, Button quit,
        Button back, Button toMainMenu)
    {
        state = gameState;
        settings = settingsManager;
        settingsSave = save;
        controls = bindings;
        sound = audio;
        mainMenu = menu;
        playLabel = label;
        playButton = play;
        settingsButton = openSettings;
        quitButton = quit;
        backButton = back;
        mainMenuButton = toMainMenu;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        inventory = FindFirstObjectByType<InventoryUIController>();
        toolSelectionHud = GetComponent<ToolSelectionHUD>();
        if (inventory != null)
        {
            input = inventory.GetComponent<PlayerInputHandler>();
            hud = inventory.GetComponent<LiquidCircleGaugeHUD>();
            playerAssimilation = inventory.GetComponent<PlayerAssimilate>();
            playerAnimation = inventory.GetComponent<PlayerAnimationController>();
            PlayerInput player = inventory.GetComponent<PlayerInput>();
            if (player != null) controls.SetInputActions(player.actions);
            inventory.AttachToUIRoot(transform, inventorySortingOrder);
            inventory.SetOpen(false);
            hud?.AttachToUIRoot(transform, hudSortingOrder);
        }
        assimilationOffering = FindFirstObjectByType<AssimilationOfferingUIController>();
        if (assimilationOffering != null)
        {
            assimilationOffering.AttachToUIRoot(transform, inventorySortingOrder);
            assimilationOffering.SetOpen(false);
        }
        if (EventSystem.current == null)
        {
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.transform.SetParent(transform, false);
        }
        foreach (Canvas canvas in GetComponentsInChildren<Canvas>(true))
            if (canvas.name == "MenuCanvas") canvas.sortingOrder = menuSortingOrder;

        EnsureDeathScreen();
        if (playerAssimilation != null)
        {
            playerAssimilation.Died -= HandlePlayerDied;
            playerAssimilation.Died += HandlePlayerDied;
        }

        settings.OnSettingsOpened += RefreshPresentation;
        settings.OnSettingsClosed += HandleSettingsClosed;
        playButton.onClick.AddListener(Play);
        settingsButton.onClick.AddListener(OpenSettings);
        quitButton.onClick.AddListener(Quit);
        backButton.onClick.AddListener(CloseSettings);
        mainMenuButton.onClick.AddListener(ShowMainMenu);
        deathMainMenuButton?.onClick.AddListener(ReturnToMainMenuAfterDeath);
        settingsSave.LoadSettings();
        foreach (GameAudioChannel channel in FindObjectsByType<GameAudioChannel>(FindObjectsSortMode.None))
            channel.Bind(sound);
        mainMenu.SetActive(showMainMenuOnStart);
        state.SetState(showMainMenuOnStart ? GameState.MainMenu : GameState.Playing);
        startedPlaying = !showMainMenuOnStart;
        RefreshPresentation();

        if (playerAssimilation != null && playerAssimilation.IsDead)
            HandlePlayerDied();
    }

    private void Update()
    {
        if (ExternalActivity) return;
        if (deathSequenceActive || (deathScreen != null && deathScreen.activeSelf)) return;

        bool inventoryPressed = input != null && input.ConsumeInventoryToggleInput();
        if (controls != null && controls.ShouldBlockSettingsToggle) return;
        if (IsTransitioning()) return;
        bool cancel = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
        if (cancel)
        {
            foreach (TMP_Dropdown dropdown in GetComponentsInChildren<TMP_Dropdown>())
                if (dropdown.IsExpanded) { dropdown.Hide(); return; }
            if (settings.IsSettingsOpen) CloseSettings();
            else if (assimilationOffering != null && assimilationOffering.IsOpen)
                CloseAssimilationOffering();
            else if (inventory != null && inventory.IsOpen) { inventory.SetOpen(false); RefreshPresentation(); }
            else if (!mainMenu.activeSelf) OpenSettings();
            return;
        }
        if (inventoryPressed && !mainMenu.activeSelf && !settings.IsSettingsOpen &&
            (assimilationOffering == null || !assimilationOffering.IsOpen) && inventory != null)
        {
            var campaign = CampaignController.Instance;
            if (campaign != null && campaign.Ready && campaign.InTown)
            {
                campaign.UI.ShowTownInventory();
                return;
            }
            inventory.Toggle();
            RefreshPresentation();
        }
    }

    private static bool IsTransitioning()
    {
        foreach (CaveEntranceInteractable entrance in FindObjectsByType<CaveEntranceInteractable>(FindObjectsSortMode.None))
            if (entrance.IsTransitioning) return true;
        return false;
    }

    private void EnsureDeathScreen()
    {
        if (deathScreen != null)
        {
            deathCanvasGroup ??= deathScreen.GetComponent<CanvasGroup>();
            return;
        }

        Canvas menuCanvas = null;
        foreach (Canvas canvas in GetComponentsInChildren<Canvas>(true))
        {
            if (canvas.name == "MenuCanvas")
            {
                menuCanvas = canvas;
                break;
            }
        }

        if (menuCanvas == null)
            return;

        deathScreen = new GameObject(
            "DeathScreen",
            typeof(RectTransform),
            typeof(Image),
            typeof(CanvasGroup));
        deathScreen.transform.SetParent(menuCanvas.transform, false);
        RectTransform deathRect = deathScreen.GetComponent<RectTransform>();
        StretchToParent(deathRect);

        Image dim = deathScreen.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.76f);
        dim.raycastTarget = true;

        deathCanvasGroup = deathScreen.GetComponent<CanvasGroup>();
        deathCanvasGroup.alpha = 0f;
        deathCanvasGroup.interactable = false;
        deathCanvasGroup.blocksRaycasts = false;

        TMP_Text deathLabel = CreateOverlayText(
            "DeathLabel",
            "죽었습니다",
            deathRect,
            new Vector2(0.15f, 0.53f),
            new Vector2(0.85f, 0.72f),
            76f);
        deathLabel.fontStyle = FontStyles.Bold;
        if (SceneManager.GetActiveScene().path == FieldSceneTravel.FieldScenePath)
            CreateOverlayText("FieldDeathPenaltyLabel",
                "가방의 모든 아이템과 금화 4%를 잃었습니다. 마을에서 다시 시작합니다.",
                deathRect, new Vector2(0.15f, 0.47f), new Vector2(0.85f, 0.54f), 28f);

        GameObject buttonObject = new("DeathMainMenuButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(deathRect, false);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.36f, 0.34f);
        buttonRect.anchorMax = new Vector2(0.64f, 0.45f);
        buttonRect.offsetMin = Vector2.zero;
        buttonRect.offsetMax = Vector2.zero;

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = new Color(0.22f, 0.26f, 0.35f, 0.98f);
        deathMainMenuButton = buttonObject.GetComponent<Button>();
        deathMainMenuButton.targetGraphic = buttonImage;

        TMP_Text buttonLabel = CreateOverlayText(
            "Text",
            "메인 화면으로 나가기",
            buttonRect,
            Vector2.zero,
            Vector2.one,
            32f);
        buttonLabel.fontStyle = FontStyles.Bold;

        deathScreen.SetActive(false);
    }

    private TMP_Text CreateOverlayText(
        string objectName,
        string text,
        RectTransform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        float fontSize)
    {
        GameObject textObject = new(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TMP_Text label = textObject.GetComponent<TMP_Text>();
        label.text = text;
        label.color = Color.white;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
        if (playLabel != null && playLabel.font != null)
            label.font = playLabel.font;
        return label;
    }

    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void HandlePlayerDied()
    {
        if (deathSequenceActive)
            return;

        EnsureDeathScreen();
        settings?.CloseSettings();
        inventory?.SetOpen(false);
        assimilationOffering?.SetOpen(false);
        if (mainMenu != null)
            mainMenu.SetActive(false);

        deathSequenceActive = true;
        state?.SetState(GameState.Dead);
        RefreshPresentation();
        deathSequenceRoutine = StartCoroutine(ShowDeathScreenAfterAnimation());
    }

    private IEnumerator ShowDeathScreenAfterAnimation()
    {
        float elapsed = 0f;
        float maximumWait = Mathf.Max(fallbackDeathAnimationDuration, 5f);

        if (playerAnimation != null)
        {
            while (!playerAnimation.IsDeathAnimationComplete && elapsed < maximumWait)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }
        else
        {
            while (elapsed < fallbackDeathAnimationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        EnsureDeathScreen();
        if (deathScreen == null)
        {
            deathSequenceRoutine = null;
            yield break;
        }

        deathCanvasGroup ??= deathScreen.GetComponent<CanvasGroup>();
        deathScreen.SetActive(true);
        deathScreen.transform.SetAsLastSibling();
        deathCanvasGroup.alpha = 0f;
        deathCanvasGroup.interactable = false;
        deathCanvasGroup.blocksRaycasts = false;
        RefreshPresentation();

        float fadeElapsed = 0f;
        while (fadeElapsed < deathUiFadeDuration)
        {
            fadeElapsed += Time.unscaledDeltaTime;
            float progress = deathUiFadeDuration <= 0f
                ? 1f
                : Mathf.Clamp01(fadeElapsed / deathUiFadeDuration);
            deathCanvasGroup.alpha = Mathf.SmoothStep(0f, 1f, progress);
            yield return null;
        }

        deathCanvasGroup.alpha = 1f;
        deathCanvasGroup.interactable = true;
        deathCanvasGroup.blocksRaycasts = true;
        deathSequenceRoutine = null;
    }

    private void ReturnToMainMenuAfterDeath()
    {
        settingsSave?.SaveSettings();
        if (SceneManager.GetActiveScene().path == FieldSceneTravel.FieldScenePath)
        {
            // Keep the death screen if the durable handoff cannot begin.
            ReleasePause();
            Time.timeScale = 1f;
            if (FieldSceneTravel.BeginToTown(showMainMenuOnArrival: true)) return;
            RefreshPresentation();
            Debug.LogError("Field death could not return to the town main menu.");
            return;
        }
        ReleasePause();
        Time.timeScale = 1f;
        Scene activeScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(activeScene.name);
    }

    public void Play()
    {
        if (IsTransitioning()) return;
        settings.CloseSettings();
        if (deathSequenceRoutine != null)
        {
            StopCoroutine(deathSequenceRoutine);
            deathSequenceRoutine = null;
        }
        deathSequenceActive = false;
        if (deathScreen != null)
        {
            deathScreen.SetActive(false);
            if (deathCanvasGroup != null)
                deathCanvasGroup.alpha = 0f;
        }
        mainMenu.SetActive(false);
        startedPlaying = true;
        state.SetState(GameState.Playing);
        RefreshPresentation();
    }

    public void OpenSettings()
    {
        if (IsTransitioning()) return;
        inventory?.SetOpen(false);
        assimilationOffering?.SetOpen(false);
        settings.OpenSettings();
        RefreshPresentation();
    }

    public void CloseInventory()
    {
        inventory?.SetOpen(false);
        RefreshPresentation();
    }

    public void OpenAssimilationOffering(AssimilationOfferingUIController requestedUI = null)
    {
        if (IsTransitioning() || mainMenu.activeSelf || settings.IsSettingsOpen)
            return;

        if (requestedUI != null)
            assimilationOffering = requestedUI;

        if (assimilationOffering == null)
            return;

        inventory?.SetOpen(false);
        assimilationOffering.SetOpen(true);
        RefreshPresentation();
    }

    public void CloseAssimilationOffering()
    {
        assimilationOffering?.SetOpen(false);
        RefreshPresentation();
    }

    public void CloseSettings()
    {
        if (controls.ShouldBlockSettingsToggle) return;
        settingsSave.SaveSettings();
        settings.CloseSettings();
    }

    public void ShowMainMenu()
    {
        if (IsTransitioning() || controls.ShouldBlockSettingsToggle) return;
        settingsSave.SaveSettings();
        settings.CloseSettings();
        inventory?.SetOpen(false);
        assimilationOffering?.SetOpen(false);
        mainMenu.SetActive(true);
        state.SetState(GameState.MainMenu);
        RefreshPresentation();
    }

    public void Quit() { if (CampaignController.Instance?.Ready == true) CampaignController.Instance.UI.ShowQuit(); else settingsSave.SaveAndExit(); }

    private void HandleSettingsClosed()
    {
        blockedThroughFrame = Time.frameCount;
        RefreshPresentation();
    }

    private void RefreshPresentation()
    {
        blockedThroughFrame = Time.frameCount;
        bool deathVisible = deathScreen != null && deathScreen.activeSelf;
        bool deathActive = deathSequenceActive || deathVisible;
        hud?.SetVisible(!deathActive && !mainMenu.activeSelf && !settings.IsSettingsOpen);
        toolSelectionHud?.SetVisible(!deathActive && !mainMenu.activeSelf && !settings.IsSettingsOpen &&
            (inventory == null || !inventory.IsOpen) &&
            (assimilationOffering == null || !assimilationOffering.IsOpen));
        if (playLabel != null) playLabel.text = startedPlaying ? "계속하기" : "게임 시작";
        bool pause = deathVisible || mainMenu.activeSelf || settings.IsSettingsOpen ||
            (pauseWithInventory && inventory != null && inventory.IsOpen) ||
            (assimilationOffering != null && assimilationOffering.IsOpen);
        if (pause && !ownsPause)
        {
            CombatHitFeedback2D.FinishHitStopBeforePause();
            previousTimeScale = Time.timeScale;
            previousCursorVisible = Cursor.visible;
            previousCursorLock = Cursor.lockState;
            ownsPause = true;
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else if (!pause) ReleasePause();
        input?.ClearGameplayInput();
        EventSystem.current?.SetSelectedGameObject(null);
    }

    private void ReleasePause()
    {
        if (!ownsPause) return;
        Time.timeScale = previousTimeScale;
        Cursor.visible = previousCursorVisible;
        Cursor.lockState = previousCursorLock;
        ownsPause = false;
    }

    private void OnDisable() => ReleasePause();

    private void OnDestroy()
    {
        if (Instance != this) return;
        if (playerAssimilation != null)
            playerAssimilation.Died -= HandlePlayerDied;
        if (settings != null)
        {
            settings.OnSettingsOpened -= RefreshPresentation;
            settings.OnSettingsClosed -= HandleSettingsClosed;
        }
        Instance = null;
    }
}
