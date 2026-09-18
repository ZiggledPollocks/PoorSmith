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
    public static bool BlocksGameplayInput => Instance != null &&
        (Instance.HasModal || Time.frameCount <= Instance.blockedThroughFrame);

    [Header("Flow")]
    [SerializeField] private bool showMainMenuOnStart;
    [SerializeField] private bool pauseWithInventory = true;
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
    private PlayerInputHandler input;
    private LiquidCircleGaugeHUD hud;
    private ToolSelectionHUD toolSelectionHud;
    private bool ownsPause;
    private float previousTimeScale;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLock;
    private int blockedThroughFrame = -1;
    private bool startedPlaying;
    public SoundSettingsController Sound => sound;
    public bool HasModal => (mainMenu != null && mainMenu.activeSelf) ||
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

        settings.OnSettingsOpened += RefreshPresentation;
        settings.OnSettingsClosed += HandleSettingsClosed;
        playButton.onClick.AddListener(Play);
        settingsButton.onClick.AddListener(OpenSettings);
        quitButton.onClick.AddListener(Quit);
        backButton.onClick.AddListener(CloseSettings);
        mainMenuButton.onClick.AddListener(ShowMainMenu);
        settingsSave.LoadSettings();
        foreach (GameAudioChannel channel in FindObjectsByType<GameAudioChannel>(FindObjectsSortMode.None))
            channel.Bind(sound);
        mainMenu.SetActive(showMainMenuOnStart);
        state.SetState(showMainMenuOnStart ? GameState.MainMenu : GameState.Playing);
        startedPlaying = !showMainMenuOnStart;
        RefreshPresentation();
    }

    private void Update()
    {
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

    public void Play()
    {
        if (IsTransitioning()) return;
        settings.CloseSettings();
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

    public void Quit() => settingsSave.SaveAndExit();

    private void HandleSettingsClosed()
    {
        blockedThroughFrame = Time.frameCount;
        RefreshPresentation();
    }

    private void RefreshPresentation()
    {
        blockedThroughFrame = Time.frameCount;
        hud?.SetVisible(!mainMenu.activeSelf && !settings.IsSettingsOpen);
        toolSelectionHud?.SetVisible(!mainMenu.activeSelf && !settings.IsSettingsOpen &&
            (inventory == null || !inventory.IsOpen) &&
            (assimilationOffering == null || !assimilationOffering.IsOpen));
        if (playLabel != null) playLabel.text = startedPlaying ? "계속하기" : "게임 시작";
        bool pause = mainMenu.activeSelf || settings.IsSettingsOpen ||
            (pauseWithInventory && inventory != null && inventory.IsOpen) ||
            (assimilationOffering != null && assimilationOffering.IsOpen);
        if (pause && !ownsPause)
        {
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
        if (settings != null)
        {
            settings.OnSettingsOpened -= RefreshPresentation;
            settings.OnSettingsClosed -= HandleSettingsClosed;
        }
        Instance = null;
    }
}
