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
public sealed partial class TitleSceneController : MonoBehaviour
{
    public const string ScenePath = "Assets/JinHo/Scenes/TitleScene.unity";

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
    string continueDestination;
    Button importButton;
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
        displayCandidates.Clear();
        foreach (var entry in ReleaseProgressFiles.Current(GameSavePaths.Root))
        { displayCandidates.Add(entry); choices.Add((entry.path, entry.Summary, entry.Valid)); }
        for (int i = 0; i < historyButtons.Length; i++)
        {
            bool available = i < choices.Count;
            historyButtons[i].gameObject.SetActive(true);
            historyButtons[i].GetComponentInChildren<TMP_Text>(true).text =
                available ? HistoryLabel(i) : "저장본이 없습니다";
        }
        bool canContinue = choices.Exists(c => c.valid) || RecoveryCandidate()?.Valid == true;
        continueButton.interactable = canContinue;
        // Fade the whole button, including its label, when no loadable save exists.
        CanvasGroup continueVisual = continueButton.GetComponent<CanvasGroup>();
        if (continueVisual == null) continueVisual = continueButton.gameObject.AddComponent<CanvasGroup>();
        continueVisual.alpha = canContinue ? 1f : 0.45f;
        continueButton.GetComponentInChildren<TMP_Text>(true).text = canContinue ? "이어하기" :
            choices.Count == 0 ? "이어하기 (저장 없음)" : "이어하기 (유효한 저장 없음)";
        if(RecoveryCandidate()!=null)continueButton.GetComponentInChildren<TMP_Text>(true).text="이어하기 (백업 복구)";
        RefreshSummary();
        for (int i = 0; i < historyButtons.Length; i++)
            historyButtons[i].interactable = i < choices.Count && choices[i].valid;
    }

    internal static (string path, string summary, bool valid) ReadChoice(string path, DateTime savedAt)
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
        background.color = mainMenu.GetComponent<Image>()?.color ?? new Color(.14f, .13f, .11f, 1f);

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
        startButton.GetComponentInChildren<TMP_Text>(true).text = "새 게임";
        if(string.IsNullOrWhiteSpace(newGameConfirmation.transform.Find("NewGameMessage").GetComponent<TMP_Text>().text))
            newGameConfirmation.transform.Find("NewGameMessage").GetComponent<TMP_Text>().text = ResetWarning;
        newGamePrompt = newGameConfirmation.transform.Find("NewGameMessage").GetComponent<TMP_Text>().text;
        historyPrompt = loadConfirmation.transform.Find("NewGameMessage").GetComponent<TMP_Text>().text;
        RefreshSavePresentation();
        BuildImportButton();
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
        foreach(var button in GetComponentsInChildren<Button>(true)) GameSoundButton.Attach(button);
        settingsMainMenuButton.onClick.AddListener(CloseSettings);
        RuntimeUIFactory.FitExistingText(mainMenu.transform.parent);
        InitializePresentation();
    }

    private void Update() => UpdateTitleNavigation();

    private void OpenSaveList()
    {
        if (loading || newGameConfirmation.activeSelf) return;
        RefreshSavePresentation();
        mainMenu.SetActive(false);
        if(!saveListScreen.activeSelf)GameSound.UI("Popup",this);
        saveListScreen.SetActive(true);
        Focus(FirstHistoryOrBack());
    }

    private void CloseSaveList()
    {
        if (loading) return;
        CancelLoadHistory();
        saveListScreen.SetActive(false);
        mainMenu.SetActive(true);
        Focus(saveListButton);
    }

    private void SelectHistory(int index)
    {
        if (loading || index < 0 || index >= choices.Count || !choices[index].valid) return;
        pendingHistoryIndex = index;
        loadConfirmation.transform.Find("NewGameMessage").GetComponent<TMP_Text>().text = SaveConfirmation(index);
        confirmLoadButton.gameObject.SetActive(true);
        confirmLoadButton.GetComponentInChildren<TMP_Text>(true).text = "예";
        cancelLoadButton.GetComponentInChildren<TMP_Text>(true).text = "취소";
        loadConfirmation.SetActive(true);
        Focus(cancelLoadButton);
        saveListBackButton.interactable = false;
        foreach (Button row in historyButtons) row.interactable = false;
    }

    private void CancelLoadHistory()
    {
        int previous=pendingHistoryIndex;
        pendingHistoryIndex = -1;
        if (loadConfirmation != null) loadConfirmation.SetActive(false);
        if (confirmLoadButton != null) confirmLoadButton.gameObject.SetActive(true);
        if (cancelLoadButton != null) cancelLoadButton.GetComponentInChildren<TMP_Text>(true).text = "아니요";
        if (saveListBackButton != null) saveListBackButton.interactable = true;
        for (int i = 0; i < historyButtons.Length; i++)
            historyButtons[i].interactable = i < choices.Count && choices[i].valid;
        if(previous>=0&&previous<historyButtons.Length)Focus(historyButtons[previous]);
    }

    private void ConfirmLoadHistory()
    {
        int index = pendingHistoryIndex;
        CancelLoadHistory();
        LoadHistory(index);
    }

    private void Play()
    {
        if (loading || newGameConfirmation.activeSelf) return;
        if (HasProgressFiles()) SetConfirmation(true);
        else ConfirmNewGame();
    }

    private static bool HasProgressFiles()
    {
        if (File.Exists(GameSavePaths.File(AutoSaveHistory.PrimaryName)) || File.Exists(GameSavePaths.File(AutoSaveHistory.PrimaryName)+".bak") ||
            AutoSaveHistory.List(GameSavePaths.Root).Count > 0) return true;
        for (int slot = 1; slot <= 4; slot++)
            if (File.Exists(GameSavePaths.File($"smithing-loop-slot-{slot}.json"))) return true;
        return false;
    }

    private void SetConfirmation(bool visible)
    {
        newGameConfirmation.SetActive(visible);
        if(importButton!=null)importButton.interactable=!visible;
        SetAuxiliaryInteractable(!visible);
        if(visible)Focus(cancelNewGameButton);
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
        RefreshSavePresentation();
        Focus(startButton);
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
        Focus(cancelLoadButton);
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
                StartTown(true);
                reset.Commit();
            }
        }
        catch (Exception e)
        {
            loading = false;
            StoryTutorial.CancelLaunch();
            ShowMainError("새 게임 초기화 실패 · 기존 저장을 보존했습니다: " + e.Message);
        }
    }

    private void Continue()
    {
        if(loading || newGameConfirmation.activeSelf || loadConfirmation.activeSelf)return;
        var recovery = RecoveryCandidate();
        if (recovery != null)
        {
            ReleaseDecisionUI.For(gameObject).Show("기본 저장본을 읽을 수 없습니다. 정상 백업으로 복구할까요?\n"+recovery.Summary,"백업 복구","취소",()=>
            {
                try { ReleaseProgressFiles.Install(recovery,GameSavePaths.Root);RefreshSavePresentation();Continue(); }
                catch(Exception e){Debug.LogWarning(e);ShowMainError("복구하지 못했습니다. 기존 저장은 보존했습니다.");}
            });FocusDecision();return;
        }
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
            var candidate=ReleaseProgressFiles.Read(choices[index].path);
            if(!candidate.Valid)throw new InvalidDataException(candidate.error);
            string json=candidate.json;
            continueDestination=ReleaseProgressFiles.Destination(candidate.progress);
            if (!Application.CanStreamedLevelBeLoaded(FieldSceneTravel.TownScenePath))
                throw new InvalidOperationException("마을 씬을 불러올 수 없습니다.");
            ReleaseProgressFiles.Install(candidate,GameSavePaths.Root);
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

    private void StartTown(bool newGame = false)
    {
        loading = true; startButton.interactable = false; continueButton.interactable = false;
        if(newGame) StoryTutorial.PrepareNewGame(); else StoryTutorial.PrepareContinue();
        SmithingLoop.DiscardPendingSceneTravel();
        if(newGame)CampaignController.StartAtTownSpawnOnNextScene();
        GameUIController.StartGameplayOnNextScene();
        SceneManager.LoadScene(newGame?FieldSceneTravel.TownScenePath:continueDestination??FieldSceneTravel.TownScenePath);
    }

    ReleaseProgressFiles.Candidate RecoveryCandidate()
    {
        string primary=GameSavePaths.File(AutoSaveHistory.PrimaryName);
        if(File.Exists(primary)&&ReleaseProgressFiles.Read(primary).Valid)return null;
        var backup=ReleaseProgressFiles.Read(primary+".bak");
        return backup.Valid?backup:null;
    }
    void BuildImportButton()
    {
        if(GameSavePaths.IsUITest || ReleaseProgressFiles.Legacy(Application.persistentDataPath).Count==0)return;
        importButton=Instantiate(saveListButton,mainMenu.transform,false);importButton.name="ImportLegacySave";
        SetAnchors((RectTransform)importButton.transform,new Vector2(.58f,.17f),new Vector2(.94f,.25f));
        importButton.GetComponentInChildren<TMP_Text>(true).text="이전 빌드 저장 가져오기";
        importButton.onClick.RemoveAllListeners();importButton.onClick.AddListener(()=>OfferImport(0));
    }
    void OfferImport(int index)
    {
        RememberMainFocus();
        var candidates=ReleaseProgressFiles.Legacy(Application.persistentDataPath);
        if(index>=candidates.Count)return;
        var candidate=candidates[index];
        // Unsupported or damaged legacy entries are reported without installing or resetting them.
        if(!candidate.Valid)
        {
            ReleaseDecisionUI.For(gameObject).Show(candidate.error+"\n원본 파일은 보존됩니다.",index+1<candidates.Count?"다음 저장본":"확인","취소",()=>OfferImport(index+1));
            return;
        }
        ReleaseDecisionUI.For(gameObject).Show($"이전 빌드 저장 {index+1}/{candidates.Count}\n{candidate.Summary}\n기존 파일은 보존되며 현재 진행을 교체합니다.","가져오기",index+1<candidates.Count?"다음 저장본":"취소",()=>
        {
            try{ReleaseProgressFiles.Install(candidate,GameSavePaths.Root,true);RefreshSavePresentation();}
            catch(Exception e){Debug.LogWarning(e);ShowMainError("저장을 가져오지 못했습니다. 기존 파일은 보존했습니다.");}
        },()=>OfferImport(index+1),onEscape:()=>{});
    }

    private void OpenSettings()
    {
        if (!loading){ RememberMainFocus();settings.OpenSettings();FocusSettings(); }
    }

    private void CloseSettings()
    {
        settingsSave.CancelEditing();
        settings.CloseSettings();
        RestoreMainFocus();
    }

    private void QuitGame() => settingsSave.SaveAndExit();

    private void OnDestroy()
    {
        DisposePresentation();
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
