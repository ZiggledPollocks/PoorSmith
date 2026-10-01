// [코드 지도] TitleSceneController: 타이틀의 새 게임·이어하기·저장본 선택과 확인 화면을 연결한다.
// 주요 함수: Start, RefreshSavePresentation, ConfirmNewGame
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/UI/Navigation/TitleSceneController.cs.md

using System;
using System.Collections.Generic;
using System.IO;
using SettingsMenuUI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Connects the title scene's authored menu and settings controls to game flow.</summary>
[DisallowMultipleComponent]
public sealed class TitleSceneController : MonoBehaviour
{
    public const string ScenePath = "Assets/Scenes/TitleScene.unity";

    [SerializeField] private GameStateManager state;
    [SerializeField] private UIManager settings;
    [SerializeField] private SettingsSaveManager settingsSave;
    [SerializeField] private ControlSettingsController controls;
    [SerializeField] private GameObject mainMenu;
    [SerializeField] private Button startButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button[] historyButtons;
    [SerializeField] private GameObject newGameConfirmation;
    [SerializeField] private Button confirmNewGameButton;
    [SerializeField] private Button cancelNewGameButton;
    [SerializeField] private TMP_Text saveStatus;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button backButton;
    [SerializeField] private Button settingsMainMenuButton;

    private bool loading;
    private GameObject saveListScreen;
    private Button saveListButton;
    private Button saveListBackButton;
    private TMP_Text saveListStatus;
    private GameObject loadConfirmation;
    private Button confirmLoadButton;
    private Button cancelLoadButton;
    private int pendingHistoryIndex = -1;
    private string newGamePrompt;
    private string historyPrompt;
    private readonly List<(string path, string summary, bool valid)> choices = new();

    // 핵심 분기: choices.Count >= historyButtons.Length 판정.
    // 상태 변경: historyButtons[i].GetComponentInChildren<TMP_Text>(true).text 갱신.
    // 다음 연결: AutoSaveHistory.List(string) 호출.
    private void RefreshSavePresentation()
    {
        choices.Clear();
        foreach (AutoSaveHistory.Entry entry in AutoSaveHistory.List(GameSavePaths.Root))
        {
            if (choices.Count >= historyButtons.Length) break;
            choices.Add(ReadChoice(entry.Path, entry.SavedAt));
        }
        string primary = GameSavePaths.File(AutoSaveHistory.PrimaryName);
        if (choices.Count == 0 && File.Exists(primary))
            choices.Add(ReadChoice(primary, File.GetLastWriteTime(primary)));
        for (int i = 0; i < historyButtons.Length; i++)
        {
            bool visible = i < choices.Count;
            historyButtons[i].gameObject.SetActive(visible);
            if (visible) historyButtons[i].GetComponentInChildren<TMP_Text>(true).text = choices[i].summary;
        }
        bool canContinue = choices.Exists(c => c.valid);
        continueButton.interactable = canContinue;
        // Fade the whole button, including its label, when no loadable save exists.
        CanvasGroup continueVisual = continueButton.GetComponent<CanvasGroup>();
        if (continueVisual == null) continueVisual = continueButton.gameObject.AddComponent<CanvasGroup>();
        continueVisual.alpha = canContinue ? 1f : 0.45f;
        continueButton.GetComponentInChildren<TMP_Text>(true).text = canContinue ? "이어하기" :
            choices.Count == 0 ? "이어하기 (저장 없음)" : "이어하기 (유효한 저장 없음)";
        for (int i = 0; i < historyButtons.Length; i++)
            historyButtons[i].interactable = i < choices.Count && choices[i].valid;
    }

    private static (string path, string summary, bool valid) ReadChoice(string path, DateTime savedAt)
    {
        try
        {
            string json = File.ReadAllText(path);
            if (!SmithingLoop.IsValidSave(json)) throw new InvalidDataException("Invalid progress");
            SmithingLoop.Progress save = JsonUtility.FromJson<SmithingLoop.Progress>(json);
            string location = save.campaign == null || save.campaign.inTown ? "마을" :
                string.IsNullOrEmpty(save.campaign.sceneName) ? "채집 필드" : save.campaign.sceneName;
            return (path, $"{save.smith.day}일 · {savedAt:MM/dd HH:mm} · {location}", true);
        }
        catch { return (path, $"손상된 저장본 · {savedAt:MM/dd HH:mm} (불러올 수 없음)", false); }
    }

    public void Configure(GameStateManager gameState, UIManager settingsManager,
        SettingsSaveManager save, ControlSettingsController bindings, GameObject menu,
        Button start, Button openSettings, Button quit, Button back, Button toMainMenu)
    {
        state = gameState;
        settings = settingsManager;
        settingsSave = save;
        controls = bindings;
        mainMenu = menu;
        startButton = start;
        settingsButton = openSettings;
        quitButton = quit;
        backButton = back;
        settingsMainMenuButton = toMainMenu;
    }

    // 기존 타이틀 버튼과 여덟 저장 행을 재사용해 별도의 저장 목록 화면을 만든다.
    private void BuildSaveListScreen()
    {
        GameObject menuPrefab = Resources.Load<GameObject>("UI/Screens/TitleMainMenu");
        GameObject listPrefab = Resources.Load<GameObject>("UI/Screens/TitleSaveList");
        if (menuPrefab != null && listPrefab != null)
        {
            // The authored scene stays intact; screen instances bind to the editable prefab hierarchy.
            GameObject sceneMenu = mainMenu;
            sceneMenu.SetActive(false);
            mainMenu = Instantiate(menuPrefab, sceneMenu.transform.parent, false);
            mainMenu.name = "TitleMainMenu";
            startButton = mainMenu.transform.Find("Play").GetComponent<Button>();
            continueButton = mainMenu.transform.Find("Continue").GetComponent<Button>();
            settingsButton = mainMenu.transform.Find("Settings").GetComponent<Button>();
            quitButton = mainMenu.transform.Find("Quit").GetComponent<Button>();
            saveListButton = mainMenu.transform.Find("OpenSaveList").GetComponent<Button>();
            saveListButton.GetComponentInChildren<TMP_Text>(true).text = "저장 목록";
            newGameConfirmation = mainMenu.transform.Find("NewGameConfirmation").gameObject;
            confirmNewGameButton = newGameConfirmation.transform.Find("ConfirmNewGame").GetComponent<Button>();
            cancelNewGameButton = newGameConfirmation.transform.Find("CancelNewGame").GetComponent<Button>();
            saveStatus = null;

            saveListScreen = Instantiate(listPrefab, sceneMenu.transform.parent, false);
            saveListScreen.name = "TitleSaveList";
            saveListBackButton = saveListScreen.transform.Find("SaveListBack").GetComponent<Button>();
            saveListBackButton.GetComponentInChildren<TMP_Text>(true).text = "뒤로가기";
            saveListStatus = null;
            saveListScreen.transform.Find("Title").GetComponent<TMP_Text>().text = "저장 목록";
            historyButtons = new Button[8];
            for (int i = 0; i < historyButtons.Length; i++)
                historyButtons[i] = saveListScreen.transform.Find($"AutoSaveDay{i + 1}").GetComponent<Button>();
            loadConfirmation = saveListScreen.transform.Find("LoadConfirmation").gameObject;
            loadConfirmation.transform.Find("NewGameMessage").GetComponent<TMP_Text>().text =
                "이 저장본으로 게임을 시작합니까";
            confirmLoadButton = loadConfirmation.transform.Find("ConfirmNewGame").GetComponent<Button>();
            cancelLoadButton = loadConfirmation.transform.Find("CancelNewGame").GetComponent<Button>();
            loadConfirmation.SetActive(false);
            saveListScreen.SetActive(false);
            return;
        }

        Debug.LogWarning("Title screen prefabs are missing; using the scene UI fallback.");
        saveListButton = Instantiate(continueButton, mainMenu.transform, false);
        saveListButton.name = "OpenSaveList";
        saveListButton.GetComponentInChildren<TMP_Text>(true).text = "저장 목록";
        RectTransform menuButtonRect = (RectTransform)saveListButton.transform;
        menuButtonRect.anchorMin = new Vector2(.06f, .11f);
        menuButtonRect.anchorMax = new Vector2(.34f, .19f);
        menuButtonRect.offsetMin = menuButtonRect.offsetMax = Vector2.zero;

        var screen = new GameObject("SaveListScreen", typeof(RectTransform), typeof(Image));
        screen.transform.SetParent(mainMenu.transform.parent, false);
        saveListScreen = screen;
        RectTransform screenRect = (RectTransform)screen.transform;
        screenRect.anchorMin = Vector2.zero;
        screenRect.anchorMax = Vector2.one;
        screenRect.offsetMin = screenRect.offsetMax = Vector2.zero;
        Image background = screen.GetComponent<Image>();
        background.color = mainMenu.GetComponent<Image>()?.color ?? new Color(.08f, .1f, .15f, 1f);

        TMP_Text title = Instantiate(saveStatus, screen.transform, false);
        title.name = "SaveListTitle";
        title.text = "저장 목록";
        RuntimeUIFactory.FitText(title, 48f);
        title.alignment = TextAlignmentOptions.Center;
        SetAnchors((RectTransform)title.transform, new Vector2(.25f, .84f), new Vector2(.75f, .94f));

        saveListStatus = Instantiate(saveStatus, screen.transform, false);
        saveListStatus.name = "SaveListStatus";
        saveListStatus.alignment = TextAlignmentOptions.Center;
        SetAnchors((RectTransform)saveListStatus.transform, new Vector2(.2f, .055f), new Vector2(.8f, .13f));

        for (int i = 0; i < historyButtons.Length; i++)
        {
            RectTransform row = (RectTransform)historyButtons[i].transform;
            row.SetParent(screen.transform, false);
            float top = .8f - i * .079f;
            SetAnchors(row, new Vector2(.22f, top - .069f), new Vector2(.78f, top));
        }

        saveListBackButton = Instantiate(settingsButton, screen.transform, false);
        saveListBackButton.name = "SaveListBack";
        saveListBackButton.GetComponentInChildren<TMP_Text>(true).text = "뒤로가기";
        SetAnchors((RectTransform)saveListBackButton.transform,
            new Vector2(.035f, .86f), new Vector2(.2f, .94f));

        loadConfirmation = Instantiate(newGameConfirmation, screen.transform, false);
        loadConfirmation.name = "LoadConfirmation";
        loadConfirmation.transform.Find("NewGameMessage").GetComponent<TMP_Text>().text =
            "이 저장본으로 게임을 시작합니까";
        confirmLoadButton = loadConfirmation.transform.Find("ConfirmNewGame").GetComponent<Button>();
        cancelLoadButton = loadConfirmation.transform.Find("CancelNewGame").GetComponent<Button>();
        loadConfirmation.SetActive(false);
        screen.SetActive(false);
    }

    private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    // 상태 변경: Time.timeScale 갱신.
    // 다음 연결: SettingsMenuUI.GameStateManager.SetState(SettingsMenuUI.GameState) 호출.
    private void Start()
    {
        Time.timeScale = 1f;
        GameUIController.ExternalActivity = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        state.SetState(GameState.MainMenu);
        mainMenu.SetActive(true);
        settingsSave.LoadSettings();
        newGameConfirmation.SetActive(false);
        BuildSaveListScreen();
        startButton.GetComponentInChildren<TMP_Text>(true).text = "플레이하기";
        newGamePrompt = newGameConfirmation.transform.Find("NewGameMessage").GetComponent<TMP_Text>().text;
        historyPrompt = loadConfirmation.transform.Find("NewGameMessage").GetComponent<TMP_Text>().text;
        RefreshSavePresentation();
        startButton.onClick.AddListener(Play);
        continueButton.onClick.AddListener(Continue);
        saveListButton.onClick.AddListener(OpenSaveList);
        saveListBackButton.onClick.AddListener(CloseSaveList);
        confirmLoadButton.onClick.AddListener(ConfirmLoadHistory);
        cancelLoadButton.onClick.AddListener(CancelLoadHistory);
        confirmNewGameButton.onClick.AddListener(ConfirmNewGame);
        cancelNewGameButton.onClick.AddListener(CancelNewGame);
        for (int i = 0; i < historyButtons.Length; i++)
        {
            int index = i;
            historyButtons[i].onClick.AddListener(() => SelectHistory(index));
        }
        settingsButton.onClick.AddListener(OpenSettings);
        quitButton.onClick.AddListener(QuitGame);
        backButton.onClick.AddListener(CloseSettings);
        settingsMainMenuButton.onClick.AddListener(CloseSettings);
        RuntimeUIFactory.FitExistingText(mainMenu.transform.parent);
    }

    private void Update()
    {
        if (loading || !(Keyboard.current?.escapeKey.wasPressedThisFrame == true ||
            Gamepad.current?.buttonEast.wasPressedThisFrame == true)) return;
        if (settings != null && settings.IsSettingsOpen &&
            (controls == null || !controls.ShouldBlockSettingsToggle))
            CloseSettings();
        else if (loadConfirmation != null && loadConfirmation.activeSelf) CancelLoadHistory();
        else if (saveListScreen != null && saveListScreen.activeSelf) CloseSaveList();
    }

    private void OpenSaveList()
    {
        if (loading || newGameConfirmation.activeSelf) return;
        RefreshSavePresentation();
        mainMenu.SetActive(false);
        saveListScreen.SetActive(true);
    }

    private void CloseSaveList()
    {
        if (loading) return;
        CancelLoadHistory();
        saveListScreen.SetActive(false);
        mainMenu.SetActive(true);
    }

    private void SelectHistory(int index)
    {
        if (loading || index < 0 || index >= choices.Count || !choices[index].valid) return;
        pendingHistoryIndex = index;
        loadConfirmation.transform.Find("NewGameMessage").GetComponent<TMP_Text>().text = historyPrompt;
        confirmLoadButton.gameObject.SetActive(true);
        confirmLoadButton.GetComponentInChildren<TMP_Text>(true).text = "예";
        cancelLoadButton.GetComponentInChildren<TMP_Text>(true).text = "아니요";
        loadConfirmation.SetActive(true);
        saveListBackButton.interactable = false;
        foreach (Button row in historyButtons) row.interactable = false;
    }

    private void CancelLoadHistory()
    {
        pendingHistoryIndex = -1;
        if (loadConfirmation != null) loadConfirmation.SetActive(false);
        if (confirmLoadButton != null) confirmLoadButton.gameObject.SetActive(true);
        if (cancelLoadButton != null) cancelLoadButton.GetComponentInChildren<TMP_Text>(true).text = "아니요";
        if (saveListBackButton != null) saveListBackButton.interactable = true;
        for (int i = 0; i < historyButtons.Length; i++)
            historyButtons[i].interactable = i < choices.Count && choices[i].valid;
    }

    private void ConfirmLoadHistory()
    {
        int index = pendingHistoryIndex;
        CancelLoadHistory();
        LoadHistory(index);
    }

    private void Play()
    {
        if (loading) return;
        if (HasProgressFiles()) SetConfirmation(true);
        else ConfirmNewGame();
    }

    private static bool HasProgressFiles()
    {
        if (File.Exists(GameSavePaths.File(AutoSaveHistory.PrimaryName)) ||
            AutoSaveHistory.List(GameSavePaths.Root).Count > 0) return true;
        for (int slot = 1; slot <= 4; slot++)
            if (File.Exists(GameSavePaths.File($"smithing-loop-slot-{slot}.json"))) return true;
        return false;
    }

    private void SetConfirmation(bool visible)
    {
        newGameConfirmation.SetActive(visible);
        startButton.interactable = !visible;
        continueButton.interactable = !visible && choices.Exists(c => c.valid);
        saveListButton.interactable = !visible;
        settingsButton.interactable = !visible;
        quitButton.interactable = !visible;
        foreach (Button row in historyButtons) row.interactable = !visible;
    }

    private void CancelNewGame()
    {
        newGameConfirmation.transform.Find("NewGameMessage").GetComponent<TMP_Text>().text = newGamePrompt;
        confirmNewGameButton.gameObject.SetActive(true);
        cancelNewGameButton.GetComponentInChildren<TMP_Text>(true).text = "취소";
        SetConfirmation(false);
    }

    private void ShowMainError(string message)
    {
        newGameConfirmation.transform.Find("NewGameMessage").GetComponent<TMP_Text>().text = message;
        confirmNewGameButton.gameObject.SetActive(false);
        cancelNewGameButton.GetComponentInChildren<TMP_Text>(true).text = "닫기";
        SetConfirmation(true);
        Debug.LogWarning(message);
    }

    private void ShowHistoryError(string message)
    {
        loadConfirmation.transform.Find("NewGameMessage").GetComponent<TMP_Text>().text = message;
        confirmLoadButton.gameObject.SetActive(false);
        cancelLoadButton.GetComponentInChildren<TMP_Text>(true).text = "닫기";
        loadConfirmation.SetActive(true);
        saveListBackButton.interactable = false;
        foreach (Button row in historyButtons) row.interactable = false;
        Debug.LogWarning(message);
    }

    // 핵심 분기: loading 판정.
    // 상태 변경: 오류가 있으면 확인 팝업에 표시.
    // 다음 연결: AutoSaveHistory.BeginNewGame(string) 호출.
    private void ConfirmNewGame()
    {
        if (loading) return;
        if (!Application.CanStreamedLevelBeLoaded(FieldSceneTravel.TownScenePath))
        { ShowMainError("마을 씬을 불러올 수 없습니다."); return; }
        try
        {
            using (var reset = AutoSaveHistory.BeginNewGame(GameSavePaths.Root))
            {
                settingsSave.SaveSettings();
                settings.CloseSettings();
                StartTown();
                reset.Commit();
            }
        }
        catch (Exception e)
        {
            loading = false;
            ShowMainError("새 게임 초기화 실패 · 기존 저장을 보존했습니다: " + e.Message);
        }
    }

    private void Continue()
    {
        int index = choices.FindIndex(c => c.valid);
        if (index < 0) { ShowMainError("유효한 자동 저장본이 없습니다."); return; }
        LoadHistory(index);
    }

    // 핵심 분기: loading || index < 0 || index >= choices.Count 판정.
    // 상태 변경: loading 갱신.
    // 다음 연결: SmithingLoop.IsValidSave(string) 호출.
    private void LoadHistory(int index)
    {
        if (loading || index < 0 || index >= choices.Count) return;
        try
        {
            string json = File.ReadAllText(choices[index].path);
            if (!SmithingLoop.IsValidSave(json)) throw new InvalidDataException("손상된 저장본입니다.");
            if (!Application.CanStreamedLevelBeLoaded(FieldSceneTravel.TownScenePath))
                throw new InvalidOperationException("마을 씬을 불러올 수 없습니다.");
            if (choices[index].path != GameSavePaths.File(AutoSaveHistory.PrimaryName))
                new FileTextStore(GameSavePaths.File(AutoSaveHistory.PrimaryName)).Write(json);
            settingsSave.SaveSettings(); settings.CloseSettings();
            StartTown();
        }
        catch (Exception e)
        {
            loading = false; startButton.interactable = true;
            continueButton.interactable = choices.Exists(c => c.valid);
            string message = "저장본을 불러오지 못했습니다: " + e.Message;
            if (saveListScreen != null && saveListScreen.activeSelf) ShowHistoryError(message);
            else ShowMainError(message);
        }
    }

    private void StartTown()
    {
        loading = true; startButton.interactable = false; continueButton.interactable = false;
        SmithingLoop.DiscardPendingSceneTravel();
        CampaignController.StartAtTownSpawnOnNextScene();
        GameUIController.StartGameplayOnNextScene();
        SceneManager.LoadScene(FieldSceneTravel.TownScenePath);
    }

    private void OpenSettings()
    {
        if (!loading) settings.OpenSettings();
    }

    private void CloseSettings()
    {
        settingsSave.SaveSettings();
        settings.CloseSettings();
    }

    private void QuitGame() => settingsSave.SaveAndExit();

    private void OnDestroy()
    {
        startButton?.onClick.RemoveListener(Play);
        continueButton?.onClick.RemoveListener(Continue);
        saveListButton?.onClick.RemoveListener(OpenSaveList);
        saveListBackButton?.onClick.RemoveListener(CloseSaveList);
        confirmLoadButton?.onClick.RemoveListener(ConfirmLoadHistory);
        cancelLoadButton?.onClick.RemoveListener(CancelLoadHistory);
        confirmNewGameButton?.onClick.RemoveListener(ConfirmNewGame);
        cancelNewGameButton?.onClick.RemoveListener(CancelNewGame);
        settingsButton?.onClick.RemoveListener(OpenSettings);
        quitButton?.onClick.RemoveListener(QuitGame);
        backButton?.onClick.RemoveListener(CloseSettings);
        settingsMainMenuButton?.onClick.RemoveListener(CloseSettings);
    }
}
