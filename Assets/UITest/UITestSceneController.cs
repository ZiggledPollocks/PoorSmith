using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Blacksmith;
using SettingsMenuUI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Isolated UI preview. Production UI receives a clone of one project asset.</summary>
[DefaultExecutionOrder(-10000)]
public sealed class UITestSceneController : MonoBehaviour
{
    [SerializeField] TextAsset fixture;
    [SerializeField] BlacksmithController blacksmith;
    [SerializeField] GameObject gameUiPrefab;
    [SerializeField] CampaignRules rules;
    [SerializeField] Button campaignRowPrefab;
    [SerializeField] Sprite mapPlayer, mapWarp, mapAltar, mapEntrance, tradeBag;
    [SerializeField] Sprite backpackSprite, paperSprite, gridSprite, selectionSprite;
    [SerializeField] Sprite stoneBasketSprite, gaugeOutlineSprite;
    [SerializeField] Shader liquidShader;

    SmithingLoop.Progress snapshot;
    SmithingLoop loop;
    CampaignController campaign;
    CampaignUI campaignUI;
    InventoryUIController fieldBag;
    PlayerInfoUI playerInfo;
    AssimilationOfferingUIController offering;
    FieldHud fieldHud;
    LiquidCircleGaugeHUD gauge;
    GameUIController gameUI;
    Canvas smithCanvas;
    StoryTutorialView storyPreview;
    string selected = "닫힘";
    string resultItemId;
    bool ready;
    bool escapeWasHeld;
    Vector2 menuScroll;

    [Serializable] sealed class SettingsFixture
    {
        public string settingsJson = "";
        public string resultItemId = "";
    }
    sealed class MemoryTextStore : ITextStore
    {
        readonly string data;
        public MemoryTextStore(string value) { data = value; }
        public bool Exists => !string.IsNullOrEmpty(data);
        public string Read() => data;
        public void Write(string value) { }
        public void Flush() { }
    }

    static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Field(object target, string name, object value)
    {
        var member = target.GetType().GetField(name, PrivateInstance);
        if (member == null) throw new MissingFieldException(target.GetType().Name, name);
        member.SetValue(target, value);
    }
    static void Property(object target, string name, object value)
    {
        var member = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
        if (member == null) throw new MissingMemberException(target.GetType().Name, name);
        member.SetValue(target, value);
    }
    static void StaticProperty(Type type, string name, object value)
    {
        var member = type.GetProperty(name, BindingFlags.Static | BindingFlags.Public);
        if (member == null) throw new MissingMemberException(type.Name, name);
        member.SetValue(null, value);
    }

    void Awake()
    {
        if (fixture == null || blacksmith == null || rules == null || campaignRowPrefab == null || gameUiPrefab == null)
        {
            Debug.LogError("UITestScene: fixture, blacksmith, rules and row prefab are required.");
            enabled = false;
            return;
        }
        snapshot = JsonUtility.FromJson<SmithingLoop.Progress>(fixture.text);
        resultItemId = JsonUtility.FromJson<SettingsFixture>(fixture.text)?.resultItemId;
        if (snapshot?.smith == null || snapshot.campaign == null || snapshot.version != 1)
        {
            Debug.LogError("UITestScene: invalid read-only fixture.");
            enabled = false;
            return;
        }
        // BlacksmithController already has an in-memory host hook. No disk writer is installed.
        BlacksmithController.SessionCatalog = blacksmith.catalog;
        BlacksmithController.SessionLoader = _ => snapshot.smith;
        BlacksmithController.SessionWriter = _ => { };
        blacksmith.loadSavedGame = false;
        smithCanvas = blacksmith.view.GetComponent<Canvas>();
    }

    IEnumerator Start()
    {
        // SceneLoaded bootstraps have completed by this point. The test scene itself
        // contains no InventorySystem, so production save loading never starts.
        yield return null;
        var content = Resources.Load<SmithingLoopContent>("SmithingLoopContent");
        if (content == null || content.catalog == null)
        {
            Debug.LogError("UITestScene: SmithingLoopContent is missing.");
            yield break;
        }
        var player = new GameObject("UITest Memory Player");
        player.SetActive(false);
        player.transform.position = new Vector3(-100, 3, 0);
        var inventory = player.AddComponent<InventorySystem>();
        var assimilation = player.AddComponent<PlayerAssimilate>();
        fieldBag = player.AddComponent<InventoryUIController>();
        Field(fieldBag, "backpackSprite", backpackSprite);
        Field(fieldBag, "paperSprite", paperSprite);
        Field(fieldBag, "gridSprite", gridSprite);
        Field(fieldBag, "selectionSprite", selectionSprite);
        offering = player.AddComponent<AssimilationOfferingUIController>();
        Field(offering, "backpackSprite", backpackSprite);
        Field(offering, "paperSprite", paperSprite);
        Field(offering, "gridSprite", gridSprite);
        Field(offering, "selectionSprite", selectionSprite);
        Field(offering, "stoneBasketSprite", stoneBasketSprite);
        Field(offering, "liquidShader", liquidShader);
        gauge = player.AddComponent<LiquidCircleGaugeHUD>();
        Field(gauge, "outlineSprite", gaugeOutlineSprite);
        Field(gauge, "liquidShader", liquidShader);
        fieldHud = player.AddComponent<FieldHud>();
        Field(fieldHud, "font", blacksmith.view.font);
        player.SetActive(true);
        playerInfo = player.GetComponent<PlayerInfoUI>();
        fieldBag.AttachToUIRoot(player.transform, 200); // The selector owns E in this preview.
        inventory.enabled = false; // The preview changes memory only; suppress periodic inventory logging.
        assimilation.RestoreRemainingHealth(Mathf.Clamp(snapshot.campaign.health, 1, assimilation.MaxAssimilation));
        inventory.ConfigureCapacity(1000000, 0);
        var bridge = new SmithingItemBridge(content);
        // The native field bag shows twenty slots. Keep the entire fixture on disk,
        // and fill only the visible page to avoid thousands of unused runtime objects.
        foreach (var stack in (snapshot.field == null ? Enumerable.Empty<SmithingLoop.FieldStack>() : snapshot.field).Take(20))
        {
            if (stack == null || stack.count <= 0) continue;
            var item = bridge.Resolve(stack.id);
            if (item != null) inventory.TryAddItem(item, stack.count);
        }

        var loopObject = new GameObject("UITest Read-Only Smithing Session");
        loop = loopObject.AddComponent<SmithingLoop>();
        loop.enabled = false;
        Field(loop, "content", content);
        Field(loop, "progress", snapshot);
        Field(loop, "inventory", inventory);
        Field(loop, "itemBridge", bridge);
        Property(loop, "SaveBlocked", true);
        Property(loop, "Initialized", true);
        StaticProperty(typeof(SmithingLoop), "Instance", loop);

        var campaignObject = new GameObject("UITest Campaign UI");
        campaign = campaignObject.AddComponent<CampaignController>();
        campaign.enabled = false;
        campaign.rules = rules;
        var exploration = campaignObject.AddComponent<CampaignExploration>();
        exploration.Restore(snapshot.campaign.visitedCells == null ? Enumerable.Empty<string>() : snapshot.campaign.visitedCells);
        campaignUI = campaignObject.AddComponent<CampaignUI>();
        campaignUI.view = blacksmith.view;
        campaignUI.rowPrefab = campaignRowPrefab;
        campaignUI.mapPlayer = mapPlayer;
        campaignUI.mapWarp = mapWarp;
        campaignUI.mapAltar = mapAltar;
        campaignUI.mapEntrance = mapEntrance;
        campaignUI.tradeBag = tradeBag;
        Property(campaign, "Player", player.transform);
        Property(campaign, "Inventory", inventory);
        Property(campaign, "Exploration", exploration);
        Property(campaign, "UI", campaignUI);
        Property(campaign, "Ready", true);
        campaignUI.Build(campaign);

        // The copied production prefab starts inactive. Replace its persistence
        // dependencies before any Awake/OnEnable/Start can touch PlayerPrefs.
        var gameUiObject = Instantiate(gameUiPrefab);
        gameUiObject.name = "UITest GameUI";
        if (gameUiObject.activeSelf)
        {
            Debug.LogError("UITestScene: GameUI test prefab must be inactive.");
            Destroy(gameUiObject);
            yield break;
        }
        var settingsSave = gameUiObject.GetComponentInChildren<SettingsSaveManager>(true);
        var controls = gameUiObject.GetComponentInChildren<ControlSettingsController>(true);
        gameUI = gameUiObject.GetComponent<GameUIController>();
        if (settingsSave == null || controls == null || gameUI == null)
        {
            Debug.LogError("UITestScene: production GameUI dependencies are missing.");
            Destroy(gameUiObject);
            yield break;
        }
        var settingsData = JsonUtility.FromJson<SettingsFixture>(fixture.text);
        Field(settingsSave, "store", new MemoryTextStore(settingsData?.settingsJson));
        Field(controls, "inputActions", null);
        Field(gameUI, "showMainMenuOnStart", false);
        gameUiObject.SetActive(true);
        yield return null; // Let the production GameUIController.Start build all screens.
        gameUI.enabled = false; // The test selector owns Escape and screen selection.
        gameUI.Play();

        // Native field inventory/HUD scripts are constructed on the memory player.
        fieldBag.SetOpen(false);
        offering.SetOpen(false);
        gauge.SetVisible(false);
        fieldHud.enabled = false;
        player.transform.Find("FieldStatusCanvas")?.gameObject.SetActive(false);
        ready = true;
        ClosePreview();
    }

    void ClosePreview()
    {
        selected = "닫힘";
        if(storyPreview!=null){storyPreview.CancelPresentation();storyPreview.gameObject.SetActive(false);Destroy(storyPreview.gameObject);storyPreview=null;}
        playerInfo?.Close();
        campaignUI?.Close();
        gameUI?.Play();
        fieldBag?.SetOpen(false);
        offering?.SetOpen(false);
        gauge?.SetVisible(false);
        if (smithCanvas != null) smithCanvas.enabled = false;
        var campaignCanvas = campaignUI?.transform.Find("CampaignCanvas")?.GetComponent<Canvas>();
        if (campaignCanvas != null) campaignCanvas.enabled = false;
        var fieldCanvas = fieldHud?.transform.Find("FieldStatusCanvas");
        if (fieldCanvas != null) fieldCanvas.gameObject.SetActive(false);
        if (fieldHud != null) fieldHud.enabled = false;
        GameUIController.ExternalActivity = false;
        Time.timeScale = 1;
    }

    void Show(string name)
    {
        if (!ready) return;
        ClosePreview();
        selected = name;
        if(name.StartsWith("튜토리얼",StringComparison.Ordinal)){ShowStoryPreview(name);return;}
        bool smith = name.StartsWith("대장간", StringComparison.Ordinal);
        smithCanvas.enabled = smith;
        var campaignCanvas = campaignUI.transform.Find("CampaignCanvas")?.GetComponent<Canvas>();
        if (campaignCanvas != null) campaignCanvas.enabled = !smith && !name.StartsWith("필드", StringComparison.Ordinal);
        var fieldCanvas = fieldHud.transform.Find("FieldStatusCanvas");
        if (fieldCanvas != null) fieldCanvas.gameObject.SetActive(name == "필드 · HUD");
        fieldHud.enabled = name == "필드 · HUD";
        campaign.Player.position = new Vector3(name.StartsWith("필드", StringComparison.Ordinal) ? 0 : -100, 3, 0);
        if (smith)
        {
            if (name == "대장간 · 장비 거치대") { blacksmith.OpenEquipmentRack(); return; }
            if (name == "대장간 · 보관함") { blacksmith.OpenStorageChest(); return; }
            if (name == "대장간 · 결과")
            {
                var item = blacksmith.catalog.Item(resultItemId) ??
                    blacksmith.catalog.items.FirstOrDefault(x => x != null && x.group == ItemGroup.Crafted);
                if (item == null) return;
                Field(blacksmith, "result", new CraftResult
                {
                    stack = new Blacksmith.Stack(item.id, 1), success = true,
                    message = "UI test preview"
                });
                blacksmith.SetState(ScreenState.Result);
                return;
            }
            var state = name switch
            {
                "대장간 · 작업실" => ScreenState.Workshop,
                "대장간 · 작업대" => ScreenState.Station,
                "대장간 · 재료 선택" => ScreenState.Selecting,
                "대장간 · 연료" => ScreenState.Fuel,
                "대장간 · 레시피" => ScreenState.Recipes,
                "대장간 · 도감" => ScreenState.Codex,
                "대장간 · 수면" => ScreenState.Sleep,
                _ => ScreenState.Home
            };
            blacksmith.SetState(state);
            return;
        }
        if (name == "메뉴 · 설정") { gameUI.OpenSettings(); return; }
        if (name == "메뉴 · 저장본")
        {
            gameUI.OpenSettings();
            gameUI.GetComponentInChildren<SettingsTabController>(true)?.SelectTab(3);
            return;
        }
        if (name == "필드 · 가방") fieldBag.SetOpen(true);
        else if (name == "필드 · 제물") offering.SetOpen(true);
        else if (name == "필드 · 동화율") gauge.SetVisible(true);
        else if (name == "마을 · 대화") campaignUI.ShowDialogue("equipment");
        else if (name == "마을 · 인벤토리") campaignUI.ShowTownInventory();
        else if (name == "마을 · 납품") campaignUI.ShowStock("delivery");
        else if (name == "마을 · 장비 상점") campaignUI.ShowShop("equipment");
        else if (name == "마을 · 전당포") campaignUI.ShowStock("pawnSell");
        else if (name == "마을 · 시설 상점") campaignUI.ShowShop("facility");
        else if (name == "마을 · 지도") campaignUI.ShowMap(false);
        else if (name is "마을 · 플레이어 정보" or "필드 · 플레이어 정보")
            StartCoroutine(OpenPlayerInfoPreview(name));
        else if (name == "마을 · 빚") campaignUI.ShowDebt();
        else if (name == "마을 · 게임 오버") campaignUI.ShowGameOver();
    }

    // Reuse the production dialogue/placement rules; the fixture writer remains memory-only.
    void ShowStoryPreview(string name)
    {
        smithCanvas.enabled=true;Property(blacksmith,"CurrentStation",Station.Tools);blacksmith.SetState(ScreenState.Station);
        var host=new GameObject("Story UI Preview",typeof(RectTransform));storyPreview=host.AddComponent<StoryTutorialView>();
        storyPreview.Build(blacksmith.view.rackIdleSprite,blacksmith.view.font);
        storyPreview.SetObjective("톱을 선택해보자.");
        storyPreview.StartCoroutine(storyPreview.Speak(new[]{"에고 망치: 톱을 선택해서 나무 원목 하나로 판자를 만들어보렴."},false,true));
        StartCoroutine(FocusStoryPreview(name));
    }
    IEnumerator FocusStoryPreview(string name)
    {
        yield return null;
        if(storyPreview==null)yield break;
        var button=blacksmith.view.stage.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.name=="Tool_2");
        if(button!=null)storyPreview.Focus(button.transform as RectTransform);
        if(name=="튜토리얼 · 행동 안내")storyPreview.Reject();
    }
    IEnumerator OpenPlayerInfoPreview(string name)
    {
        // ClosePreview lets GameUI block input for its current frame.
        yield return null;
        if (selected == name) playerInfo?.Open();
    }

    void Update()
    {
        bool escapeHeld = Keyboard.current?.escapeKey.isPressed == true;
        if (ready && escapeHeld && !escapeWasHeld)
            ClosePreview();
        if (ready && Keyboard.current?.eKey.wasPressedThisFrame == true &&
            (selected is "마을 · 인벤토리" or "필드 · 가방"))
            ClosePreview();
        escapeWasHeld = escapeHeld;
    }

    void LateUpdate()
    {
        if (!ready) return;
#if UNITY_EDITOR
        // The editor's global God Mode button can create a writable session.
        var godButton = FindFirstObjectByType<EditorGodModeButton>();
        if (godButton != null) godButton.enabled = false;
#endif
        // These native save/exit actions have no read-only provider in the game.
        // Keep the controls visible where the UI creates them, but never invoke them.
        foreach (var button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (button.name is "RecoverySaves" or "WriteSlot" or "ReadSlot" or
                "SaveAndQuit" or "QuitWithoutSave" or "MainMenuSaves" or
                "MainMenuButton" or "SaveAndExitButton" or "Quit" or "SaveButton")
                button.interactable = false;
    }

    void OnGUI()
    {
        if (!ready) return;
        GUILayout.BeginArea(new Rect(8, 8, 235, Mathf.Max(200, Screen.height - 16)), GUI.skin.box);
        menuScroll = GUILayout.BeginScrollView(menuScroll);
        GUILayout.Label("UI TEST · READ ONLY");
        GUILayout.Label(selected);
        foreach (var name in new[]
        {
            "메뉴 · 설정", "메뉴 · 저장본", "대장간 · 홈", "대장간 · 작업실", "대장간 · 보관함", "대장간 · 작업대",
            "대장간 · 장비 거치대", "대장간 · 재료 선택", "대장간 · 연료",
            "대장간 · 레시피", "대장간 · 도감", "대장간 · 결과", "대장간 · 수면",
            "마을 · HUD", "마을 · 대화",
            "마을 · 인벤토리", "마을 · 플레이어 정보", "마을 · 납품",
            "마을 · 장비 상점", "마을 · 전당포", "마을 · 시설 상점", "마을 · 지도",
            "마을 · 빚", "마을 · 게임 오버", "필드 · 가방", "필드 · 플레이어 정보", "필드 · 제물",
            "필드 · 동화율", "필드 · HUD", "튜토리얼 · 제작 설명", "튜토리얼 · 행동 안내"
        }) if (GUILayout.Button(name)) Show(name);
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    void OnDestroy()
    {
        BlacksmithController.SessionLoader = null;
        BlacksmithController.SessionWriter = null;
        BlacksmithController.SessionCatalog = null;
        GameUIController.ExternalActivity = false;
        Time.timeScale = 1;
    }
}

