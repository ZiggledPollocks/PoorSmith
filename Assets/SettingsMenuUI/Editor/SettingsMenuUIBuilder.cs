#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SettingsMenuUI.Editor
{
    /// <summary>Builds the integrated UI prefab without replacing the user's scene.</summary>
    public static class SettingsMenuUIBuilder
    {
        private const string RootFolder = "Assets/SettingsMenuUI";
        private const string FontFolder = RootFolder + "/Fonts";
        public const string PrefabPath = "Assets/JinHo/Resources/UI/GameUI.prefab";
        private const string FontFilePath = FontFolder + "/NanumGothic.ttf";
        private const string FontAssetPath = FontFolder + "/NanumGothic Dynamic SDF.asset";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";

        private static readonly Color PageBackground = Hex("828282");
        private static readonly Color Frame = Hex("F4F4F2");
        private static readonly Color Selected = Hex("7B859C");
        private static readonly Color Tab = Hex("474E60");
        private static readonly Color Panel = Hex("596273");
        private static readonly Color ButtonColor = Hex("7C879F");
        private static readonly Color ButtonDisabled = Hex("687286");
        private static readonly Color Divider = Hex("4E5668");
        private static readonly Color White = Hex("F5F5F3");
        private static readonly Color ScrollTrack = Hex("16191F");
        private static readonly Color SliderOff = Hex("555E70");

        private static TMP_FontAsset font;

        [InitializeOnLoadMethod]
        private static void BuildOnceWhenImported()
        {
            EditorApplication.delayCall += () =>
            {
                if (!Application.isBatchMode && !EditorApplication.isPlayingOrWillChangePlaymode && !File.Exists(PrefabPath))
                {
                    BuildScene();
                }
            };
        }

        [MenuItem("Tools/batterMap UI/Build Integrated UI")]
        public static void BuildScene()
        {
            if (File.Exists(PrefabPath) && !Application.isBatchMode &&
                !EditorUtility.DisplayDialog("Rebuild UI", "GameUI.prefab의 수동 변경을 덮어쓰고 다시 생성할까요?", "다시 생성", "취소"))
                return;
            EnsureFolder(RootFolder);
            EnsureFolder(FontFolder);
            EnsureFolder("Assets/JinHo/Resources/UI");
            font = GetOrCreateKoreanFont();

            Scene scene = EditorSceneManager.NewPreviewScene();
            try
            {
                BuildPrefabContents(scene);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void BuildPrefabContents(Scene scene)
        {
            GameObject root = new GameObject("GameUI");
            SceneManager.MoveGameObjectToScene(root, scene);
            GameObject canvasObject = CreateCanvas();
            SceneManager.MoveGameObjectToScene(canvasObject, scene);
            canvasObject.name = "MenuCanvas";
            canvasObject.transform.SetParent(root.transform, false);
            canvasObject.GetComponent<Canvas>().sortingOrder = 300;
            RectTransform menu = CreateImage("MainMenu", (RectTransform)canvasObject.transform,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Hex("202735"));
            CreateText("Title", "batterMap", menu, new Vector2(0.2f, 0.65f), new Vector2(0.8f, 0.88f),
                Vector2.zero, Vector2.zero, 90f, TextAlignmentOptions.Center);
            CreateText("Subtitle", "탐험을 시작하세요", menu, new Vector2(0.2f, 0.57f), new Vector2(0.8f, 0.66f),
                Vector2.zero, Vector2.zero, 30f, TextAlignmentOptions.Center);
            Button play = CreateMenuButton("Play", "게임 시작", menu, 0.44f);
            Button openSettings = CreateMenuButton("Settings", "설정", menu, 0.32f);
            Button quit = CreateMenuButton("Quit", "종료", menu, 0.20f);
            CreateText("Help", "게임 중 Esc: 설정  |  E: 인벤토리", menu,
                new Vector2(0.1f, 0.04f), new Vector2(0.9f, 0.12f), Vector2.zero, Vector2.zero, 26f, TextAlignmentOptions.Center);
            RectTransform settingsMenu = CreateRect("SettingsMenu", canvasObject.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            CreateImage("Background", settingsMenu, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, PageBackground);

            RectTransform topTabs = CreateRect("TopTabs", settingsMenu, new Vector2(0f, 1f), Vector2.one, new Vector2(65f, -135f), new Vector2(-300f, -30f));
            CreateTopTabs(topTabs);

            RectTransform contentArea = CreateImage("ContentArea", settingsMenu, Vector2.zero, Vector2.one, new Vector2(20f, 180f), new Vector2(-20f, -115f), Frame);
            RectTransform gamePanel = CreatePanel("GameSettingsPanel", contentArea);
            RectTransform controlPanel = CreatePanel("ControlSettingsPanel", contentArea);
            RectTransform soundPanel = CreatePanel("SoundSettingsPanel", contentArea);

            GameSettingsController gameController = CreateGameSettingsPanel(gamePanel);
            ControlSettingsController controlController = CreateControlSettingsPanel(controlPanel);
            SoundSettingsController soundController = CreateSoundSettingsPanel(soundPanel);
            ConfigureTabController(settingsMenu, topTabs, gamePanel, controlPanel, soundPanel);
            SettingsPopupController popupController = CreateWarningPopup(settingsMenu);
            controlController.SetWarningPopup(popupController);

            RectTransform bottomButtons = CreateRect("BottomButtons", settingsMenu, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-620f, 30f), new Vector2(-20f, 135f));
            (Button saveButton, Button saveAndExitButton) = CreateBottomButtons(bottomButtons);
            SettingsSaveManager saveManager = settingsMenu.gameObject.AddComponent<SettingsSaveManager>();
            saveManager.Configure(gameController, controlController, soundController, saveButton, saveAndExitButton);
            GameStateManager stateManager = root.AddComponent<GameStateManager>();
            stateManager.Configure(GameState.MainMenu);
            UIManager uiManager = root.AddComponent<UIManager>();
            uiManager.Configure(settingsMenu.gameObject, controlController, stateManager,
                AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath));
            uiManager.UseExternalNavigation();
            Button back = CreateButton("BackButton", "돌아가기", settingsMenu, Vector2.zero, Vector2.zero,
                new Vector2(20f, 30f), new Vector2(280f, 135f), ButtonColor, 32f).GetComponent<Button>();
            Button toMenu = CreateButton("MainMenuButton", "메인 메뉴", settingsMenu, Vector2.zero, Vector2.zero,
                new Vector2(300f, 30f), new Vector2(560f, 135f), ButtonColor, 32f).GetComponent<Button>();
            root.AddComponent<GameUIController>().Configure(stateManager, uiManager, saveManager,
                controlController, soundController, menu.gameObject, play.GetComponentInChildren<TMP_Text>(),
                play, openSettings, quit, back, toMenu);

            gamePanel.gameObject.SetActive(true);
            controlPanel.gameObject.SetActive(false);
            soundPanel.gameObject.SetActive(false);
            settingsMenu.gameObject.SetActive(false);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"Integrated UI prefab created: {PrefabPath}");
        }

        private static Button CreateMenuButton(string name, string label, RectTransform parent, float y)
        {
            return CreateButton(name, label, parent, new Vector2(0.36f, y), new Vector2(0.64f, y + 0.09f),
                Vector2.zero, Vector2.zero, ButtonColor, 38f).GetComponent<Button>();
        }

        private static void CreateManagers(GameObject settingsRoot, ControlSettingsController controlController)
        {
            GameObject managers = new GameObject("Managers");

            GameObject stateObject = new GameObject("GameStateManager");
            stateObject.transform.SetParent(managers.transform, false);
            GameStateManager stateManager = stateObject.AddComponent<GameStateManager>();
            stateManager.Configure(GameState.Playing);

            GameObject uiObject = new GameObject("UIManager");
            uiObject.transform.SetParent(managers.transform, false);
            UIManager uiManager = uiObject.AddComponent<UIManager>();
            InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            uiManager.Configure(settingsRoot, controlController, stateManager, actions);
        }

        private static GameObject CreateCanvas()
        {
            GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvasObject;
        }

        private static void CreateEventSystem()
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        private static RectTransform CreatePanel(string name, RectTransform parent)
        {
            return CreateImage(name, parent, Vector2.zero, Vector2.one, new Vector2(16f, 28f), new Vector2(-16f, -28f), Panel);
        }

        private static void CreateTopTabs(RectTransform parent)
        {
            CreateTab("GameSettingsTab", "화면 설정", parent, 0f, 0.32f, true);
            CreateTab("ControlSettingsTab", "이동 키 설정", parent, 0.34f, 0.66f, false);
            CreateTab("SoundSettingsTab", "사운드 설정", parent, 0.68f, 1f, false);
        }

        private static void CreateTab(string name, string label, RectTransform parent, float minX, float maxX, bool selected)
        {
            RectTransform tab = CreateButton(name, label, parent, new Vector2(minX, 0f), new Vector2(maxX, 1f), Vector2.zero, Vector2.zero, selected ? Selected : Tab, 34f);
            Outline outline = tab.gameObject.AddComponent<Outline>();
            outline.effectColor = Frame;
            outline.effectDistance = new Vector2(5f, -5f);
            outline.enabled = selected;
        }

        private static void ConfigureTabController(RectTransform root, RectTransform tabsRoot, params RectTransform[] panels)
        {
            SettingsTabController controller = root.gameObject.AddComponent<SettingsTabController>();
            string[] names = { "GameSettingsTab", "ControlSettingsTab", "SoundSettingsTab" };
            var items = new List<SettingsTabItem>();
            for (int i = 0; i < names.Length; i++)
            {
                RectTransform tab = tabsRoot.Find(names[i]) as RectTransform;
                Button button = tab.GetComponent<Button>();
                Image background = tab.GetComponent<Image>();
                Outline outline = tab.GetComponent<Outline>();
                TMP_Text label = tab.Find("Text").GetComponent<TMP_Text>();
                SettingsTabItem item = tab.gameObject.AddComponent<SettingsTabItem>();
                item.Configure(controller, i, button, background, outline, label, panels[i].gameObject);
                item.ConfigureHover(1.14f, 0.12f);
                items.Add(item);
            }

            controller.Configure(items, 0);
        }

        private static GameSettingsController CreateGameSettingsPanel(RectTransform panel)
        {
            ScrollRect scrollRect;
            RectTransform viewport;
            Scrollbar scrollbar;
            RectTransform content = CreateScrollView("GameSettingsScrollView", panel, new Vector2(0f, 0f), Vector2.one, new Vector2(80f, 20f), new Vector2(-80f, -20f), true, out scrollRect, out viewport, out scrollbar);
            ConfigureVerticalContent(content, 24, 0f);

            RectTransform resolutionRow = CreateLayoutRow("ResolutionRow", content, 175f);
            CreateBadge("Label", "해상도", resolutionRow);
            TMP_Dropdown dropdown = CreateFunctionalDropdown("ResolutionDropdown", resolutionRow, new Vector2(0.33f, 0.31f), new Vector2(0.66f, 0.72f));
            Button applyResolution = CreateButton("ApplyButton", "적용하기", resolutionRow, new Vector2(0.73f, 0.30f), new Vector2(0.88f, 0.72f), Vector2.zero, Vector2.zero, ButtonColor, 27f).GetComponent<Button>();

            CreateLayoutDivider("Divider01", content);
            RectTransform modeRow = CreateLayoutRow("ScreenModeRow", content, 175f);
            CreateBadge("Label", "화면 모드", modeRow);
            Button previousMode = CreateArrowButton("PreviousButton", "◀", modeRow, 0.34f, 0.39f);
            TMP_Text modeText = CreateText("ValueText", "전체 화면", modeRow, new Vector2(0.40f, 0.22f), new Vector2(0.57f, 0.78f), Vector2.zero, Vector2.zero, 31f, TextAlignmentOptions.Center).GetComponent<TMP_Text>();
            Button nextMode = CreateArrowButton("NextButton", "▶", modeRow, 0.58f, 0.63f);
            Button applyMode = CreateButton("ApplyButton", "적용하기", modeRow, new Vector2(0.73f, 0.30f), new Vector2(0.88f, 0.72f), Vector2.zero, Vector2.zero, ButtonColor, 27f).GetComponent<Button>();

            CreateLayoutDivider("Divider02", content);
            RectTransform autoSaveRow = CreateLayoutRow("AutoSaveRow", content, 175f);
            CreateBadge("Label", "자동 저장", autoSaveRow);
            Button previousAuto = CreateArrowButton("PreviousButton", "◀", autoSaveRow, 0.38f, 0.43f);
            TMP_Text autoText = CreateText("ValueText", "켜짐", autoSaveRow, new Vector2(0.44f, 0.22f), new Vector2(0.54f, 0.78f), Vector2.zero, Vector2.zero, 31f, TextAlignmentOptions.Center).GetComponent<TMP_Text>();
            Button nextAuto = CreateArrowButton("NextButton", "▶", autoSaveRow, 0.55f, 0.60f);
            // The package has no world/inventory save implementation.
            autoSaveRow.gameObject.SetActive(false);
            content.Find("Divider02").gameObject.SetActive(false);

            GameSettingsController controller = panel.gameObject.AddComponent<GameSettingsController>();
            controller.Configure(dropdown, applyResolution, previousMode, nextMode, modeText, applyMode, previousAuto, nextAuto, autoText);
            DynamicVerticalScroll dynamicScroll = scrollRect.gameObject.AddComponent<DynamicVerticalScroll>();
            dynamicScroll.Configure(scrollRect, viewport, content, scrollbar);
            return controller;
        }

        private static ControlSettingsController CreateControlSettingsPanel(RectTransform panel)
        {
            ScrollRect scrollRect;
            RectTransform viewport;
            Scrollbar scrollbar;
            RectTransform content = CreateScrollView("ControlScrollView", panel, Vector2.zero, Vector2.one, new Vector2(80f, 20f), new Vector2(-80f, -20f), false, out scrollRect, out viewport, out scrollbar);
            ConfigureVerticalContent(content, 20, 0f);

            RectTransform header = CreateLayoutRow("Header", content, 82f);
            CreateText("ActionHeader", "액션", header, new Vector2(0.025f, 0f), new Vector2(0.66f, 1f), Vector2.zero, Vector2.zero, 29f, TextAlignmentOptions.Left);
            CreateText("MainKeyHeader", "메인 키", header, new Vector2(0.70f, 0f), new Vector2(0.83f, 1f), Vector2.zero, Vector2.zero, 29f, TextAlignmentOptions.Center);
            CreateText("SubKeyHeader", "서브 키", header, new Vector2(0.84f, 0f), new Vector2(0.97f, 1f), Vector2.zero, Vector2.zero, 29f, TextAlignmentOptions.Center);

            var rows = new List<ControlSettingsController.BindingRow>
            {
                CreateControlRow("MoveUpRow", "이동 - 상", "up", "W", "↑", content),
                CreateControlRow("MoveDownRow", "이동 - 하", "down", "S", "↓", content),
                CreateControlRow("MoveLeftRow", "이동 - 좌", "left", "A", "←", content),
                CreateControlRow("MoveRightRow", "이동 - 우", "right", "D", "→", content)
            };

            InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            ControlSettingsController controller = panel.gameObject.AddComponent<ControlSettingsController>();
            controller.Configure(actions, rows);
            return controller;
        }

        private static ControlSettingsController.BindingRow CreateControlRow(string name, string label, string partName, string main, string sub, RectTransform parent)
        {
            RectTransform row = CreateLayoutRow(name, parent, 115f);
            CreateText("ActionLabel", label, row, new Vector2(0.025f, 0f), new Vector2(0.66f, 1f), Vector2.zero, Vector2.zero, 32f, TextAlignmentOptions.Left);
            Button mainButton = CreateButton("MainKeyButton", main, row, new Vector2(0.73f, 0.18f), new Vector2(0.80f, 0.84f), Vector2.zero, Vector2.zero, ButtonColor, 30f).GetComponent<Button>();
            Button subButton = CreateButton("SubKeyButton", sub, row, new Vector2(0.86f, 0.18f), new Vector2(0.93f, 0.84f), Vector2.zero, Vector2.zero, ButtonColor, 34f).GetComponent<Button>();
            ConfigureKeyText(mainButton.GetComponentInChildren<TMP_Text>(true), 30f);
            ConfigureKeyText(subButton.GetComponentInChildren<TMP_Text>(true), 34f);
            CreateImage("Divider", row, Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, -2f), new Vector2(0f, 2f), Divider);
            return new ControlSettingsController.BindingRow
            {
                partName = partName,
                mainButton = mainButton,
                mainText = mainButton.GetComponentInChildren<TMP_Text>(true),
                subButton = subButton,
                subText = subButton.GetComponentInChildren<TMP_Text>(true)
            };
        }

        private static void ConfigureKeyText(TMP_Text text, float maximumSize)
        {
            text.enableAutoSizing = true;
            text.fontSizeMax = maximumSize;
            text.fontSizeMin = 14f;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.margin = new Vector4(7f, 3f, 7f, 3f);
        }

        private static SoundSettingsController CreateSoundSettingsPanel(RectTransform panel)
        {
            SoundSettingsController.ChannelView master = CreateVolumeRow("MasterVolumeRow", "전체 볼륨", 50, SoundChannel.Master, panel, 45f, 220f);
            CreateDivider("Divider01", panel, 230f);
            SoundSettingsController.ChannelView bgm = CreateVolumeRow("BGMVolumeRow", "배경 볼륨", 40, SoundChannel.BGM, panel, 240f, 415f);
            CreateDivider("Divider02", panel, 425f);
            SoundSettingsController.ChannelView sfx = CreateVolumeRow("SFXVolumeRow", "효과음", 70, SoundChannel.SFX, panel, 435f, 610f);
            SoundSettingsController controller = panel.gameObject.AddComponent<SoundSettingsController>();
            controller.Configure(master, bgm, sfx);
            return controller;
        }

        private static SoundSettingsController.ChannelView CreateVolumeRow(string name, string label, int value, SoundChannel channel, RectTransform parent, float top, float bottom)
        {
            RectTransform row = CreateRow(name, parent, top, bottom);
            CreateBadge("Label", label, row);
            Slider slider = CreateSegmentedSlider(name.Replace("Row", "Slider"), value, row);
            TMP_Text percent = CreateText("VolumePercentText", value + "%", row, new Vector2(0.60f, 0.22f), new Vector2(0.68f, 0.78f), Vector2.zero, Vector2.zero, 28f, TextAlignmentOptions.Center).GetComponent<TMP_Text>();
            Button mute = CreateButton("MuteButton", "♫", row, new Vector2(0.82f, 0.24f), new Vector2(0.89f, 0.76f), Vector2.zero, Vector2.zero, ButtonColor, 40f).GetComponent<Button>();
            return new SoundSettingsController.ChannelView
            {
                channel = channel,
                slider = slider,
                percentText = percent,
                muteButton = mute,
                muteButtonText = mute.GetComponentInChildren<TMP_Text>(true),
                audioSources = new AudioSource[0]
            };
        }

        private static void CreateSaveLoadPanel(RectTransform panel)
        {
            ScrollRect scrollRect;
            RectTransform viewport;
            Scrollbar scrollbar;
            RectTransform content = CreateScrollView("SaveLoadScrollView", panel, Vector2.zero, Vector2.one, new Vector2(80f, 20f), new Vector2(-80f, -20f), false, out scrollRect, out viewport, out scrollbar);
            ConfigureVerticalContent(content, 20, 0f);
            RectTransform header = CreateLayoutRow("Header", content, 100f);
            CreateText("SaveFileHeader", "세이브 파일", header, new Vector2(0.02f, 0f), new Vector2(0.23f, 1f), Vector2.zero, Vector2.zero, 30f, TextAlignmentOptions.Center);
            CreateText("DayHeader", "일자", header, new Vector2(0.24f, 0f), new Vector2(0.38f, 1f), Vector2.zero, Vector2.zero, 30f, TextAlignmentOptions.Center);
            CreateText("PlayTimeHeader", "플레이 타임", header, new Vector2(0.39f, 0f), new Vector2(0.58f, 1f), Vector2.zero, Vector2.zero, 30f, TextAlignmentOptions.Center);
        }

        private static (Button save, Button saveAndExit) CreateBottomButtons(RectTransform parent)
        {
            Button save = CreateButton("SaveButton", "설정 저장", parent, Vector2.zero, new Vector2(0.46f, 1f), Vector2.zero, Vector2.zero, Hex("929DB5"), 34f).GetComponent<Button>();
            Button exit = CreateButton("SaveAndExitButton", "저장 후 종료하기", parent, new Vector2(0.50f, 0f), Vector2.one, Vector2.zero, Vector2.zero, Hex("929DB5"), 32f).GetComponent<Button>();
            return (save, exit);
        }

        private static TMP_Dropdown CreateFunctionalDropdown(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject root = TMP_DefaultControls.CreateDropdown(new TMP_DefaultControls.Resources());
            SceneManager.MoveGameObjectToScene(root, parent.gameObject.scene);
            root.name = name;
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;

            TMP_Dropdown dropdown = root.GetComponent<TMP_Dropdown>();
            root.GetComponent<Image>().color = ButtonColor;
            Transform arrow = root.transform.Find("Arrow");
            if (arrow != null) arrow.GetComponent<Image>().enabled = false;
            CreateText("ArrowGlyph", "▼", rect, new Vector2(0.91f, 0f), Vector2.one,
                Vector2.zero, Vector2.zero, 24f, TextAlignmentOptions.Center);
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                text.font = font;
                text.color = White;
                text.fontSize = 25f;
                text.fontStyle = FontStyles.Bold;
            }

            Transform template = root.transform.Find("Template");
            if (template != null)
            {
                RectTransform templateRect = template.GetComponent<RectTransform>();
                templateRect.sizeDelta = new Vector2(220f, 360f);
                templateRect.anchoredPosition = new Vector2(0f, -8f);
                Image templateImage = template.GetComponent<Image>();
                if (templateImage != null)
                {
                    templateImage.color = Tab;
                }

                ScrollRect dropdownScroll = template.GetComponent<ScrollRect>();
                if (dropdownScroll != null)
                {
                    dropdownScroll.horizontal = false;
                    dropdownScroll.vertical = true;
                    dropdownScroll.movementType = ScrollRect.MovementType.Clamped;
                    dropdownScroll.scrollSensitivity = GameSettingsController.ResolutionScrollSensitivity;
                    dropdownScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
                }

                Transform itemBackground = template.Find("Viewport/Content/Item/Item Background");
                if (itemBackground != null)
                {
                    itemBackground.GetComponent<Image>().color = Selected;
                }

                RectTransform item = template.Find("Viewport/Content/Item") as RectTransform;
                if (item != null)
                {
                    item.sizeDelta = new Vector2(item.sizeDelta.x, 64f);
                }

                TMP_Text itemLabel = template.Find("Viewport/Content/Item/Item Label")?.GetComponent<TMP_Text>();
                if (itemLabel != null)
                {
                    itemLabel.fontSize = 28f;
                    itemLabel.margin = new Vector4(14f, 10f, 12f, 10f);
                }

                RectTransform dropdownContent = template.Find("Viewport/Content") as RectTransform;
                if (dropdownContent != null && item != null)
                {
                    dropdownContent.sizeDelta = new Vector2(dropdownContent.sizeDelta.x, item.rect.height);
                    VerticalLayoutGroup layout = dropdownContent.gameObject.AddComponent<VerticalLayoutGroup>();
                    layout.padding = new RectOffset(0, 0, 0, GameSettingsController.ResolutionContentBottomPadding);
                    layout.spacing = 0f;
                    layout.childAlignment = TextAnchor.UpperCenter;
                    layout.childControlWidth = true;
                    layout.childControlHeight = false;
                    layout.childForceExpandWidth = true;
                    layout.childForceExpandHeight = false;

                    ContentSizeFitter fitter = dropdownContent.gameObject.AddComponent<ContentSizeFitter>();
                    fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                    fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                }

                RectTransform dropdownScrollbar = template.Find("Scrollbar") as RectTransform;
                if (dropdownScrollbar != null)
                {
                    dropdownScrollbar.sizeDelta = new Vector2(30f, dropdownScrollbar.sizeDelta.y);
                    Image trackImage = dropdownScrollbar.GetComponent<Image>();
                    if (trackImage != null)
                    {
                        trackImage.color = Hex("1F2636");
                    }

                    Scrollbar scrollbar = dropdownScrollbar.GetComponent<Scrollbar>();
                    if (scrollbar != null && scrollbar.targetGraphic != null)
                    {
                        scrollbar.targetGraphic.color = Hex("B5C1DA");
                    }

                    dropdownScrollbar.gameObject.SetActive(true);
                }

                template.gameObject.SetActive(false);
            }

            dropdown.ClearOptions();
            var resolutionOptions = new List<string>(GameSettingsController.Presets.Count);
            for (int i = 0; i < GameSettingsController.Presets.Count; i++)
            {
                resolutionOptions.Add(GameSettingsController.Presets[i].DisplayName);
            }

            dropdown.AddOptions(resolutionOptions);
            int defaultResolutionIndex = 0;
            for (int i = 0; i < GameSettingsController.Presets.Count; i++)
            {
                if (GameSettingsController.Presets[i].Type == GameSettingsController.DefaultResolutionPresetType)
                {
                    defaultResolutionIndex = i;
                    break;
                }
            }

            dropdown.SetValueWithoutNotify(defaultResolutionIndex);
            return dropdown;
        }

        private static RectTransform CreateScrollView(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, bool autoHideScrollbar, out ScrollRect scrollRect, out RectTransform viewport, out Scrollbar scrollbar)
        {
            RectTransform root = CreateRect(name, parent, anchorMin, anchorMax, offsetMin, offsetMax);
            scrollRect = root.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 30f;

            viewport = CreateRect("Viewport", root, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-36f, 0f));
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = CreateRect("Content", viewport, new Vector2(0f, 1f), Vector2.one, Vector2.zero, Vector2.zero);
            content.pivot = new Vector2(0.5f, 1f);

            scrollbar = CreateScrollbar("VerticalScrollbar", root, new Vector2(1f, 0f), Vector2.one, new Vector2(-24f, 16f), Vector2.zero);
            scrollRect.viewport = viewport;
            scrollRect.content = content;
            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility = autoHideScrollbar ? ScrollRect.ScrollbarVisibility.AutoHide : ScrollRect.ScrollbarVisibility.Permanent;
            scrollRect.verticalScrollbarSpacing = 10f;
            return content;
        }

        private static void ConfigureVerticalContent(RectTransform content, int padding, float spacing)
        {
            VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, padding, padding);
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private static RectTransform CreateLayoutRow(string name, RectTransform parent, float height)
        {
            RectTransform row = CreateRect(name, parent, new Vector2(0f, 1f), Vector2.one, Vector2.zero, Vector2.zero);
            LayoutElement layout = row.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
            layout.flexibleHeight = 0f;
            return row;
        }

        private static void CreateLayoutDivider(string name, RectTransform parent)
        {
            RectTransform divider = CreateImage(name, parent, new Vector2(0f, 1f), Vector2.one, Vector2.zero, Vector2.zero, Divider);
            LayoutElement layout = divider.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 6f;
            layout.preferredHeight = 6f;
            layout.flexibleHeight = 0f;
        }

        private static RectTransform CreateRow(string name, RectTransform parent, float topInset, float bottomInset)
        {
            return CreateRect(name, parent, new Vector2(0f, 1f), Vector2.one, new Vector2(100f, -bottomInset), new Vector2(-100f, -topInset));
        }

        private static void CreateBadge(string name, string label, RectTransform row)
        {
            RectTransform badge = CreateImage(name, row, new Vector2(0.02f, 0.27f), new Vector2(0.19f, 0.73f), Vector2.zero, Vector2.zero, Selected);
            CreateText("Text", label, badge, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 29f, TextAlignmentOptions.Center);
        }

        private static void CreateDivider(string name, RectTransform parent, float topInset)
        {
            CreateImage(name, parent, new Vector2(0f, 1f), Vector2.one, new Vector2(115f, -topInset - 3f), new Vector2(-115f, -topInset + 3f), Divider);
        }

        private static Button CreateArrowButton(string name, string glyph, RectTransform parent, float minX, float maxX)
        {
            RectTransform button = CreateButton(name, glyph, parent, new Vector2(minX, 0.28f), new Vector2(maxX, 0.72f), Vector2.zero, Vector2.zero, Color.clear, 34f);
            button.GetComponent<Image>().raycastTarget = true;
            return button.GetComponent<Button>();
        }

        private static Slider CreateSegmentedSlider(string name, int percentage, RectTransform row)
        {
            RectTransform root = CreateImage(name, row, new Vector2(0.29f, 0.28f), new Vector2(0.58f, 0.70f), Vector2.zero, Vector2.zero, Selected);
            Slider slider = root.gameObject.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.value = percentage / 100f;

            CreateImage("Background", root, Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-8f, -8f), SliderOff);

            RectTransform fillArea = CreateRect("FillArea", root, Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-8f, -8f));
            RectTransform fill = CreateImage("Fill", fillArea, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, White);
            RectTransform handleArea = CreateRect("HandleSlideArea", root, Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, 0f));
            RectTransform handle = CreateImage("Handle", handleArea, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-5f, -18f), new Vector2(5f, 18f), White);
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = root.GetComponent<Image>();
            return slider;
        }

        private static SettingsPopupController CreateWarningPopup(RectTransform settingsMenu)
        {
            RectTransform overlay = CreateRect("PopupOverlay", settingsMenu, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            overlay.SetAsLastSibling();
            RectTransform popup = CreateImage("WarningPopup", overlay, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-230f, -75f), new Vector2(230f, 75f), new Color(0.12f, 0.15f, 0.22f, 0.96f));
            CanvasGroup group = popup.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            TMP_Text message = CreateText("MessageText", "이미 입력된 키", popup, Vector2.zero, Vector2.one, new Vector2(28f, 18f), new Vector2(-28f, -18f), 34f, TextAlignmentOptions.Center).GetComponent<TMP_Text>();
            SettingsPopupController controller = overlay.gameObject.AddComponent<SettingsPopupController>();
            controller.Configure(popup.gameObject, group, message);
            popup.gameObject.SetActive(false);
            return controller;
        }

        private static Scrollbar CreateScrollbar(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            RectTransform root = CreateImage(name, parent, anchorMin, anchorMax, offsetMin, offsetMax, ScrollTrack);
            Scrollbar scrollbar = root.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            RectTransform slidingArea = CreateRect("SlidingArea", root, Vector2.zero, Vector2.one, new Vector2(-7f, 12f), new Vector2(7f, -12f));
            RectTransform handle = CreateImage("Handle", slidingArea, new Vector2(0f, 0.72f), Vector2.one, Vector2.zero, Vector2.zero, Hex("A6B1C7"));
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handle.GetComponent<Image>();
            scrollbar.size = 0.24f;
            scrollbar.value = 1f;
            return scrollbar;
        }

        private static RectTransform CreateButton(string name, string label, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color, float fontSize)
        {
            RectTransform root = CreateImage(name, parent, anchorMin, anchorMax, offsetMin, offsetMax, color);
            Button button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = root.GetComponent<Image>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.88f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.disabledColor = ButtonDisabled;
            button.colors = colors;
            CreateText("Text", label, root, Vector2.zero, Vector2.one, new Vector2(8f, 4f), new Vector2(-8f, -4f), fontSize, TextAlignmentOptions.Center);
            return root;
        }

        private static RectTransform CreateText(string name, string value, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, float fontSize, TextAlignmentOptions alignment)
        {
            RectTransform rect = CreateRect(name, parent, anchorMin, anchorMax, offsetMin, offsetMax);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Bold;
            text.color = White;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return rect;
        }

        private static RectTransform CreateImage(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            RectTransform rect = CreateRect(name, parent, anchorMin, anchorMax, offsetMin, offsetMax);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return rect;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(gameObject, parent.gameObject.scene);
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.localScale = Vector3.one;
            return rect;
        }

        public static TMP_FontAsset GetOrCreateKoreanFont()
        {
            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null)
            {
                return existing;
            }

            const string systemFont = @"C:\Windows\Fonts\NanumGothic.ttf";
            if (!File.Exists(FontFilePath) && File.Exists(systemFont))
            {
                File.Copy(systemFont, FontFilePath, true);
                AssetDatabase.ImportAsset(FontFilePath, ImportAssetOptions.ForceSynchronousImport);
            }

            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(FontFilePath);
            if (sourceFont == null)
            {
                return TMP_Settings.defaultFontAsset;
            }

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(sourceFont);
            fontAsset.name = "NanumGothic Dynamic SDF";
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            Texture2D atlasTexture = fontAsset.atlasTexture;
            Material fontMaterial = fontAsset.material;
            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);
            if (atlasTexture != null)
            {
                AssetDatabase.AddObjectToAsset(atlasTexture, fontAsset);
            }
            if (fontMaterial != null)
            {
                AssetDatabase.AddObjectToAsset(fontMaterial, fontAsset);
            }
            AssetDatabase.SaveAssets();
            return fontAsset;
        }

        private static void EnsureFolder(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                AssetDatabase.Refresh();
            }
        }

        private static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out Color color);
            return color;
        }
    }
}
#endif
