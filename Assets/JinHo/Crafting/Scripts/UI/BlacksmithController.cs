// [코드 지도] BlacksmithController: 화면 상태, 입력, 제작 흐름, 결과·수면·보관함 UI를 조정한다.
// 주요 함수: DrawEquipment, DrawStation, ChestSlot
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Crafting/Scripts/UI/BlacksmithController.cs.md

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Blacksmith
{
    public class BlacksmithController : MonoBehaviour
    {
        public BlacksmithCatalog catalog;
        public BlacksmithView view;
        public bool loadSavedGame = true;
        // Optional host session: the standalone scene retains its own save behavior.
        public static Func<BlacksmithCatalog, SaveData> SessionLoader;
        public static BlacksmithCatalog SessionCatalog;
        public static Action<SaveData> SessionWriter;
        public bool IsHosted => SessionLoader != null;
        public bool CanLeave => Inventory != null && !Crafting.Active && State != ScreenState.Animating && State != ScreenState.Result;

        public void PrepareToLeave()
        {
            if (CanLeave)
            {
                Inventory.ReturnAll();
                Persist();
            }
        }

        public InventoryService Inventory { get; private set; }
#if UNITY_EDITOR
        public void RefreshEditorGrant() => Render();
#endif
        public CraftingService Crafting { get; private set; }
        public ScreenState State { get; private set; }
        public bool IsSleeping => State == ScreenState.Sleep || sleepInProgress;
        bool sleepInProgress;

        // Screen prefabs provide editable placement while the controller keeps item data and callbacks.
        void LateUpdate()
        {
            if (view == null || view.stage == null) return;
            string screen = State switch
            {
                ScreenState.Home => "SmithyHome",
                ScreenState.Workshop => "SmithyWorkshop",
                ScreenState.Chest => equipmentMode ? "SmithyEquipmentRack" : "SmithyChest",
                ScreenState.Station => "SmithyStation",
                ScreenState.Selecting or ScreenState.Fuel => "SmithySelection",
                ScreenState.Playing or ScreenState.Animating => "SmithyCrafting",
                ScreenState.Result => "SmithyResult",
                ScreenState.Recipes or ScreenState.Codex => "SmithyRecipes",
                ScreenState.Sleep => "SmithySleep",
                _ => null
            };
            ScreenLayoutTemplate.Apply(screen, view.stage);
            ScreenLayoutTemplate.Apply(screen + "Overlay", view.overlay);
        }
        public Station CurrentStation { get; private set; }
        public ToolKind CurrentTool { get; private set; }

        ScreenState returnState;
        RectTransform inventoryGrid, selectedGrid, autoList, playingRoot;
        Vector2 materialScrollPosition = new(0f, 1f), recipeScrollPosition = new(0f, 1f);
        string query = "", feedback = "";
        int group = -1, material = -1, bagPage;
        bool equipmentMode, discardMode;
        HashSet<string> discard = new HashSet<string>();
        Stack held;
        bool heldBag;
        CraftResult result;
        TMP_Text counter;
        Image guide, progressImage, workIcon, heldToolImage;
        CraftGestureInput gesture;
        float timer, quenchCenter;
        bool quenchStarted;
        Coroutine toolFeedback;
        Vector2 heldToolRestPosition;
        Quaternion heldToolRestRotation;
        AudioSource feedbackAudio;
        AudioClip[] feedbackClips;
        readonly string[] stationNames =
        {
            "작업대",
            "도구 걸이",
            "모루",
            "담금질",
            "용광로"
        };
        readonly string[] toolNames =
        {
            "칼",
            "대패",
            "톱",
            "숫돌",
            "망치"
        };
        readonly string[] toolArt =
        {
            "knife",
            "plane",
            "saw",
            "whetstone",
            "hammer"
        };
        readonly string[] stationBackgrounds =
        {
            "StationWorkbench",
            "StationTools",
            "StationAnvil",
            "StationQuench",
            "StationFurnace"
        };
        void Start()
        {
            if (SessionCatalog != null)
                catalog = SessionCatalog;
            var data = SessionLoader != null ? SessionLoader(catalog) : loadSavedGame ? BlacksmithSave.Load(catalog) : BlacksmithSave.CreateDemo(catalog);
            Inventory = new InventoryService(data, catalog);
            Crafting = new CraftingService(Inventory);
            view.Build();
            State = ScreenState.Home;
            Render();
            if (!IsHosted && BlacksmithSave.WritesBlocked)
                Say("저장 파일을 읽지 못했습니다. 원본 보호를 위해 저장을 중단했습니다.");
        }

        // 핵심 분기: state == ScreenState.Home && SmithingLoop.Instance?.InteriorPanel == true 판정.
        // 상태 변경: State 갱신.
        // 다음 연결: SmithingLoop.Travel() 호출.
        public void SetState(ScreenState state)
        {
            if (state == ScreenState.Home && SmithingLoop.Instance?.InteriorPanel == true)
            {
                State = ScreenState.Home;
                SmithingLoop.Instance.Travel();
                return;
            }

            if (IsHosted && CampaignController.Instance != null && !Inventory.Data.night && (state == ScreenState.Station || state == ScreenState.Playing))
            {
                Say("낮에는 채집 시간입니다. 침대에서 낮잠을 자면 제작할 수 있습니다.");
                return;
            }

            State = state;
            query = "";
            group = material = -1;
            view.HideTooltip();
            Render();
        }

        void Say(string message)
        {
            feedback = message;
            view.notice.text = message;
        }

        // 핵심 분기: old 판정.
        // 상태 변경: view.stage.anchoredPosition 갱신.
        // 다음 연결: Blacksmith.BlacksmithView.Clear(UnityEngine.Transform) 호출.
        void Render()
        {
            if (State == ScreenState.Selecting || State == ScreenState.Fuel)
            {
                var materialScroller = inventoryGrid ? inventoryGrid.parent.parent.GetComponent<ScrollRect>() : null;
                var recipeScroller = autoList ? autoList.parent.parent.GetComponent<ScrollRect>() : null;
                if (materialScroller) materialScrollPosition = materialScroller.normalizedPosition;
                if (recipeScroller) recipeScrollPosition = recipeScroller.normalizedPosition;
            }
            BlacksmithView.Clear(view.stage);
            BlacksmithView.Clear(view.overlay);
            view.hud.SetAsLastSibling();
            view.HideTooltip();
            view.stage.anchoredPosition = Vector2.zero;
            view.status.text = $"대장간   /   {(State == ScreenState.Home ? "전체 보기" : State == ScreenState.Chest ? (equipmentMode ? "장비 거치대" : "상자") : State == ScreenState.Sleep ? "침대" : State == ScreenState.Workshop ? "제작실" : stationNames[(int)CurrentStation])}     {(State == ScreenState.Home || State == ScreenState.Chest || State == ScreenState.Sleep ? $"{Inventory.Data.day}일 · {(Inventory.Data.night ? "밤" : "아침")}" : "")}     동화율 {Inventory.Data.maxHp - Inventory.Data.hp}/{Inventory.Data.maxHp}     연료 {Inventory.Data.fuel}/50";
            view.notice.text = feedback;
            // Rebuild only transient HUD controls; status, feedback and tooltip stay stable.
            var old = view.hud.Find("Controls");
            if (old)
            {
                old.gameObject.SetActive(false);
                Destroy(old.gameObject);
            }

            var controls = view.Full("Controls", view.hud);
            bool workbenchCorner = CurrentStation == Station.Workbench &&
                (State == ScreenState.Station || State == ScreenState.Selecting || State == ScreenState.Playing);
            if (State != ScreenState.Sleep)
            {
                var back = view.Button("Back", controls, "← 대장간",
                    workbenchCorner ? new Vector2(.012f, .085f) : new Vector2(.008f, .938f),
                    workbenchCorner ? new Vector2(.165f, .151f) : new Vector2(.145f, .989f), Back);
                back.interactable = State != ScreenState.Animating && State != ScreenState.Result;
            }
            view.Text("RecipeShortcut", controls, "레시피 [Tab]", 20, new Vector2(.82f, .938f), new Vector2(.928f, .989f), null, TextAlignmentOptions.Right);
            var recipes = view.Button("Recipes", controls, "", new Vector2(.935f, .938f), new Vector2(.985f, .989f), ToggleRecipes, "recipe_icon");
            recipes.interactable = State != ScreenState.Animating && State != ScreenState.Result;
            if (State == ScreenState.Home)
            {
                DrawHome();
                return;
            }

            if (State == ScreenState.Workshop)
            {
                DrawWorkshop();
                return;
            }

            if (State == ScreenState.Chest || State == ScreenState.Sleep)
                view.Image("InteriorPanelBackdrop", view.stage, "stone_wall", new Color(.3f, .25f, .2f), Vector2.zero, Vector2.one);
            else
                DrawStation();
            if (State == ScreenState.Station)
                DrawNavigation();
            if (State == ScreenState.Selecting || State == ScreenState.Fuel)
                DrawSelection();
            else if (State == ScreenState.Playing)
                DrawMinigame();
            else if (State == ScreenState.Result)
                DrawResult();
            else if (State == ScreenState.Chest)
                DrawChest();
            else if (State == ScreenState.Recipes || State == ScreenState.Codex)
                DrawRecipes();
            else if (State == ScreenState.Sleep)
                DrawSleep();
        }

        void DrawHome()
        {
            var home = GetComponent<SmithyHomeView>() ?? gameObject.AddComponent<SmithyHomeView>();
            home.Draw(this);
            var dial = view.Rect("SmithyDayDial", view.stage, new Vector2(.81f, .77f), new Vector2(.98f, .94f)).gameObject.AddComponent<DayDialGraphic>();
            dial.Night = Inventory.Data.night;
            dial.raycastTarget = false;
            view.Text("SmithyDay", view.stage, "Day " + Inventory.Data.day + (CampaignController.Instance != null ? $" · {CampaignController.Instance.Economy.GoldText} G" : ""), 22, new Vector2(.80f, .71f), new Vector2(.98f, .78f), null, TextAlignmentOptions.Center);
        }

        public void OpenEquipmentRack()
        {
            equipmentMode = true;
            discardMode = false;
            discard.Clear();
            SetState(ScreenState.Chest);
        }

        public void OpenStorageChest()
        {
            equipmentMode = false;
            SetState(ScreenState.Chest);
        }

        // 핵심 분기: !IsHosted 판정.
        // 상태 변경: view.Image("WorkshopBackground", view.stage, null, Color.white, Vector2.zero, Vector2.one).sprite 갱신.
        // 다음 연결: Blacksmith.BlacksmithView.Image(string, UnityEngine.Transform, string, UnityEngine.Color, UnityEngine.Vector2… 호출.
        void DrawWorkshop()
        {
            // All six pictures share a 196 x 108 canvas. Preserve their native alignment.
            view.Image("WorkshopBackground", view.stage, null, Color.white, Vector2.zero, Vector2.one).sprite = view.WorkshopArt("crafting_bg");
            foreach (var layer in new[] { "craftingtable", "tools", "anvil", "waterbucket", "furnace" })
                view.Image("Workshop_" + layer, view.stage, null, Color.white, Vector2.zero, Vector2.one).sprite = view.WorkshopArt(layer);

            WorkshopFacility("Workbench", "작업대", Station.Workbench, new Vector2(.025f, .093f), new Vector2(.311f, .704f));
            WorkshopFacility("ToolRack", "도구 걸이", Station.Tools, new Vector2(.347f, .454f), new Vector2(.587f, .704f));
            WorkshopFacility("Anvil", "모루", Station.Anvil, new Vector2(.296f, .093f), new Vector2(.566f, .306f));
            WorkshopFacility("Quench", "담금질", Station.Quench, new Vector2(.255f, .083f), new Vector2(.332f, .25f));
            WorkshopFacility("Furnace", "용광로", Station.Furnace, new Vector2(.612f, .093f), new Vector2(.969f, .92f));
            view.Button("WorkshopChest", view.stage, "상자", new Vector2(.015f, .02f), new Vector2(.13f, .095f), OpenStorageChest);

            if (!IsHosted)
                view.Button("SupplyRecipeMaterials", view.stage, "테스트 재료 받기", new Vector2(.06f, .58f), new Vector2(.25f, .65f), () =>
                {
                    BlacksmithSave.SupplyRecipeMaterials(Inventory.Data, catalog);
                    Persist();
                    Say("레시피에 필요한 채집 재료를 보관함에 30개까지 보충했습니다.");
                    Render();
                });
        }

        // 핵심 분기: !background.sprite 판정.
        // 상태 변경: background.raycastTarget 갱신.
        // 다음 연결: Blacksmith.BlacksmithView.Image(string, UnityEngine.Transform, string, UnityEngine.Color, UnityEngine.Vector2… 호출.
        void DrawStation()
        {
            var background = view.Image("StationBackground", view.stage,
                stationBackgrounds[(int)CurrentStation], Color.white, Vector2.zero, Vector2.one);
            // The new backgrounds contain scenery only. Every action below remains a real UI button.
            if (!background.sprite)
                view.Image("BackgroundFallback", view.stage, "stone_wall", Color.white, Vector2.zero, Vector2.one);
            if (CurrentStation == Station.Furnace && State == ScreenState.Station)
            {
                background.raycastTarget = true;
                background.gameObject.AddComponent<StationPanInput>().target = view.stage;
            }

            Vector2 objectMin, objectMax;
            switch (CurrentStation)
            {
                case Station.Workbench:
                    objectMin = new Vector2(.23f, .12f); objectMax = new Vector2(.77f, .55f); break;
                case Station.Tools:
                    objectMin = new Vector2(.24f, .11f); objectMax = new Vector2(.77f, .45f); break;
                case Station.Anvil:
                    objectMin = new Vector2(.28f, .20f); objectMax = new Vector2(.74f, .73f); break;
                case Station.Quench:
                    objectMin = new Vector2(.34f, .18f); objectMax = new Vector2(.66f, .70f); break;
                default:
                    objectMin = new Vector2(.39f, .35f); objectMax = new Vector2(.61f, .70f); break;
            }
            var objectButton = view.Button("StationObject", view.stage, "", objectMin, objectMax,
                () => SetState(ScreenState.Selecting));
            objectButton.image.color = new Color(1, 1, 1, .005f);
            objectButton.gameObject.AddComponent<UiHoverOutline>();
            objectButton.interactable = State == ScreenState.Station && CurrentStation != Station.Tools;
            if (CurrentStation == Station.Workbench)
            {
                var book = view.Button("CodexBook", view.stage, "", new Vector2(.11f, .57f), new Vector2(.29f, .83f), () =>
                {
                    returnState = State;
                    SetState(ScreenState.Codex);
                });
                book.image.color = new Color(1, 1, 1, .005f);
                book.gameObject.AddComponent<UiHoverOutline>();
                book.interactable = State == ScreenState.Station;
                if (State == ScreenState.Station)
                    view.Button("OpenCodex", view.stage, "도감 보기", new Vector2(.12f, .53f), new Vector2(.28f, .59f), () =>
                    {
                        returnState = State;
                        SetState(ScreenState.Codex);
                    });
            }

            if (CurrentStation == Station.Tools)
            {
                Vector2[] toolMin =
                {
                    new Vector2(.18f, .53f), new Vector2(.29f, .16f),
                    new Vector2(.60f, .52f), new Vector2(.65f, .16f),
                    new Vector2(.40f, .52f)
                };
                Vector2[] toolMax =
                {
                    new Vector2(.27f, .84f), new Vector2(.40f, .38f),
                    new Vector2(.71f, .83f), new Vector2(.75f, .38f),
                    new Vector2(.50f, .84f)
                };
                for (int i = 0; i < 5; i++)
                {
                    if (i == (int)CurrentTool && State != ScreenState.Station)
                        continue;
                    int n = i;
                    var b = view.Button("Tool_" + i, view.stage, "", toolMin[i], toolMax[i], () =>
                    {
                        CurrentTool = (ToolKind)n;
                        SetState(ScreenState.Selecting);
                        Say(toolNames[n] + " 작업 · 보관함의 재료를 작업 칸에 놓으세요.");
                    }, toolArt[i]);
                    b.interactable = State == ScreenState.Station;
                    b.image.color = CurrentTool == (ToolKind)i ? new Color(1, .75f, .3f) : Color.white;
                    if (State == ScreenState.Station)
                        view.Text("ToolName", view.stage, toolNames[i], 18,
                            new Vector2(toolMin[i].x, toolMin[i].y - .045f),
                            new Vector2(toolMax[i].x, toolMin[i].y), null, TextAlignmentOptions.Center);
                }
            }

            if (CurrentStation == Station.Furnace)
            {
                var fuel = view.Button("FuelGrate", view.stage, "", new Vector2(.36f, .10f),
                    new Vector2(.64f, .34f), () => SetState(ScreenState.Fuel));
                fuel.image.color = new Color(1, 1, 1, .005f);
                fuel.gameObject.AddComponent<UiHoverOutline>();
                fuel.interactable = State == ScreenState.Station;
                if (State == ScreenState.Station)
                    view.Button("AddFuel", view.stage, $"연료 넣기  {Inventory.Data.fuel}/50",
                        new Vector2(.39f, .08f), new Vector2(.61f, .15f), () => SetState(ScreenState.Fuel));
            }

            if (State == ScreenState.Station && CurrentStation != Station.Tools)
                view.Button("OpenMaterials", view.stage,
                    CurrentStation == Station.Furnace ? "도가니 · 재료 선택" : "재료 선택",
                    new Vector2(.38f, .85f), new Vector2(.62f, .91f), () => SetState(ScreenState.Selecting));
        }

        void WorkshopFacility(string id, string label, Station station, Vector2 min, Vector2 max)
        {
            var button = view.Button("Facility_" + id, view.stage, "", min, max, () =>
            {
                CurrentStation = station;
                SetState(ScreenState.Station);
            });
            button.image.color = new Color(1f, 1f, 1f, .01f);
            button.gameObject.AddComponent<UiHoverOutline>();
            view.Text("FacilityName_" + id, view.stage, label, 20,
                new Vector2(min.x, Mathf.Max(.10f, min.y)),
                new Vector2(max.x, Mathf.Min(.94f, min.y + .06f)), null, TextAlignmentOptions.Center);
        }

        // 상태 변경: CurrentStation 갱신.
        // 다음 연결: Blacksmith.BlacksmithView.Button(string, UnityEngine.Transform, string, UnityEngine.Vector2, UnityEngine.Vect… 호출.
        void DrawNavigation()
        {
            var previous = view.Button("PreviousStation", view.overlay, "‹", new Vector2(.012f, .46f), new Vector2(.073f, .56f), () =>
            {
                CurrentStation = (Station)(((int)CurrentStation + 4) % 5);
                Render();
            });
            RuntimeUIFactory.FitText(previous.GetComponentInChildren<TMP_Text>(), 44);
            var next = view.Button("NextStation", view.overlay, "›", new Vector2(.927f, .46f), new Vector2(.988f, .56f), () =>
            {
                CurrentStation = (Station)(((int)CurrentStation + 1) % 5);
                Render();
            });
            RuntimeUIFactory.FitText(next.GetComponentInChildren<TMP_Text>(), 44);
            view.Text("PreviousLabel", view.overlay, stationNames[((int)CurrentStation + 4) % 5], 16,
                new Vector2(.006f, .405f), new Vector2(.11f, .455f), null, TextAlignmentOptions.Center);
            view.Text("NextLabel", view.overlay, stationNames[((int)CurrentStation + 1) % 5], 16,
                new Vector2(.89f, .405f), new Vector2(.994f, .455f), null, TextAlignmentOptions.Center);
            view.Button("ScrollRecipe", view.overlay, "", new Vector2(.84f, .74f),
                new Vector2(.94f, .87f), ToggleRecipes, "recipe_icon");
        }

        // 핵심 분기: State == ScreenState.Fuel 판정.
        // 상태 변경: inventoryGrid 갱신.
        // 다음 연결: Blacksmith.BlacksmithView.Image(string, UnityEngine.Transform, string, UnityEngine.Color, UnityEngine.Vector2… 호출.
        void DrawSelection()
        {
            view.Image("SelectionShade", view.overlay, null, new Color(0, 0, 0, .48f),
                Vector2.zero, new Vector2(1, .93f));
            view.Text("SelectionStation", view.overlay,
                CurrentStation == Station.Tools ? "도구 걸이 · " + toolNames[(int)CurrentTool] : stationNames[(int)CurrentStation], 31,
                new Vector2(.34f, .81f), new Vector2(.66f, .89f), null, TextAlignmentOptions.Center);
            var panel = view.Panel("InventoryPanel", view.overlay, new Vector2(.675f, .21f), new Vector2(.985f, .86f));
            view.Text("Heading", panel, State == ScreenState.Fuel ? "연료 보관함" : "재료 보관함", 26, new Vector2(.03f, .88f), new Vector2(.90f, .99f));
            DrawFilters(panel, () => RefreshInventory(false));
            inventoryGrid = view.Scroll(panel, "InventoryScroll", new Vector2(.025f, .03f), new Vector2(.99f, .58f), 4, 103);
            string[] materialNames =
            {
                "전체",
                "목재",
                "석재",
                "철",
                "무기",
                "방어구",
                "기타"
            };
            var materialButtons = new List<Button>();
            for (int i = 0; i < 7; i++)
            {
                int n = i;
                var filter = view.Button("MaterialFilter" + i, panel, materialNames[i], new Vector2(.018f + i * .139f, .59f), new Vector2(.143f + i * .139f, .67f), () =>
                {
                    material = n - 1;
                    for (int k = 0; k < materialButtons.Count; k++)
                        view.HighlightChoice(materialButtons[k], material == k - 1);
                    RefreshInventory(false);
                });
                RuntimeUIFactory.FitText(filter.GetComponentInChildren<TMP_Text>(), 14);
                view.HighlightChoice(filter, material == i - 1);
                materialButtons.Add(filter);
            }

            RefreshInventory(false);
            view.Button("CloseSelection", view.overlay, "닫기 · 재료 반환", new Vector2(.675f, .12f), new Vector2(.825f, .19f), () =>
            {
                ReturnSelectedMaterials(true);
            });
            if (State == ScreenState.Fuel)
            {
                var fuelPanel = view.Panel("FuelStatus", view.overlay,
                    new Vector2(.19f, .30f), new Vector2(.60f, .74f));
                ConfigureMaterialDrop(fuelPanel, fuelPanel.GetComponent<Image>());
                view.Text("FuelValue", fuelPanel, $"연료  {Inventory.Data.fuel} / 50", 35,
                    new Vector2(.07f, .72f), new Vector2(.93f, .93f), null, TextAlignmentOptions.Center);
                view.Image("FuelTrack", fuelPanel, null, new Color(.18f, .16f, .13f),
                    new Vector2(.11f, .47f), new Vector2(.89f, .61f));
                view.Image("FuelFill", fuelPanel, null, BlacksmithView.Gold,
                    new Vector2(.11f, .47f), new Vector2(.11f + .78f * Inventory.Data.fuel / 50f, .61f));
                view.Text("FuelRules", fuelPanel,
                    "목재 +3   석탄 +6   목재 부산물 +1\n가득 차면 더 넣을 수 없습니다.", 23,
                    new Vector2(.07f, .08f), new Vector2(.93f, .41f), null, TextAlignmentOptions.Center);
                return;
            }

            view.Button("Reset", view.overlay, "초기화", new Vector2(.83f, .12f), new Vector2(.985f, .19f), () =>
            {
                ReturnSelectedMaterials(false);
            });
            var work = view.Panel("SelectedMaterials", view.overlay, new Vector2(.325f, .25f), new Vector2(.655f, .49f));
            view.Text("SelectionLabel", work, "선택한 재료 · 클릭하면 반환", 21, new Vector2(.02f, .73f), new Vector2(.98f, .98f));
            selectedGrid = view.Rect("Slots", work, new Vector2(.04f, .05f), new Vector2(.97f, .73f));
            var selectionBackground = selectedGrid.gameObject.AddComponent<Image>();
            selectionBackground.color = new Color(.18f, .16f, .13f, .35f);
            ConfigureMaterialDrop(selectedGrid, selectionBackground);
            var grid = selectedGrid.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(108, 108);
            grid.spacing = new Vector2(8, 8);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            foreach (var s in Inventory.Selection.ToArray())
            {
                InventorySlotView selectedSlot = null;
                selectedSlot = CreateSlot(selectedGrid, s, () =>
                {
                    ReturnSelected(s, selectedSlot, 1);
                });
                selectedSlot.EnableTransferDrag(() => Inventory.Selection.Contains(s) && s.count > 0);
                selectedSlot.gameObject.AddComponent<ItemDropTarget>().ConfigureProximity(
                    CanDropMaterial, DropMaterial, selectedSlot.background, true);
            }
            view.FillEmptyGridSlots(selectedGrid, Inventory.Selection.Count, 1);
            bool canStart = Crafting.CanBegin(CurrentStation, CurrentTool, out string reason);
            if (IsHosted && CampaignController.Instance != null && !Inventory.Data.night)
            {
                canStart = false;
                reason = "낮에는 제작할 수 없습니다. 침대에서 밤으로 전환하세요.";
            }
            var start = view.Button("StartCraft", view.overlay, "시작하기",
                new Vector2(.375f, .14f), new Vector2(.605f, .23f), () => StartCraft(null));
            view.Emphasize(start);
            start.interactable = canStart;
            if (!canStart)
                view.Text("StartUnavailable", view.overlay, reason, 19,
                    new Vector2(.325f, .075f), new Vector2(.655f, .135f),
                    BlacksmithView.Cream, TextAlignmentOptions.Center);
            var paper = view.Panel("AutomaticRecipes", view.overlay, new Vector2(.03f, .20f), new Vector2(.30f, .84f), true);
            view.Text("AutoTitle", paper, "숙련 제작", 29, new Vector2(.04f, .86f), new Vector2(.96f, .98f), BlacksmithView.Ink);
            autoList = view.Scroll(paper, "Recipes", new Vector2(.02f, .035f), new Vector2(.98f, .86f));
            var unlocked = catalog.recipes.Where(r => r.enabled && r.station == CurrentStation && (r.station != Station.Tools || r.tool == CurrentTool) && Crafting.Mastery(r) >= 2).ToArray();
            if (unlocked.Length == 0)
                view.Text("Locked", paper, "동일 레시피를 익히면\n숙련도 2에서 자동 제작 해금\n\n일반 제작 6회 / 장비 제작 4회", 21, new Vector2(.06f, .30f), new Vector2(.94f, .74f), BlacksmithView.Ink);
            foreach (var recipe in unlocked)
                AddAutomatic(recipe);
            // Rebuilding the selection screen after adding an item preserves both list positions.
            Canvas.ForceUpdateCanvases();
            inventoryGrid.parent.parent.GetComponent<ScrollRect>().normalizedPosition = materialScrollPosition;
            autoList.parent.parent.GetComponent<ScrollRect>().normalizedPosition = recipeScrollPosition;
        }

        // 핵심 분기: !storageOnly 판정.
        // 상태 변경: group 갱신.
        // 다음 연결: Blacksmith.BlacksmithView.Button(string, UnityEngine.Transform, string, UnityEngine.Vector2, UnityEngine.Vect… 호출.
        void DrawFilters(Transform panel, Action refresh, bool storageOnly = false)
        {
            string[] names =
            {
                "채집물",
                "제작물",
                "무기·방어구"
            };
            var buttons = new List<Button>();
            for (int i = 0; i < (storageOnly ? 2 : 3); i++)
            {
                int n = i;
                var b = view.Button("Group" + i, panel, names[i], new Vector2(.02f + i * .325f, .77f), new Vector2(.32f + i * .325f, .88f), () =>
                {
                    group = group == n ? -1 : n;
                    for (int k = 0; k < buttons.Count; k++)
                        view.HighlightChoice(buttons[k], group == k);
                    refresh();
                });
                RuntimeUIFactory.FitText(b.GetComponentInChildren<TMP_Text>(), 17);
                view.HighlightChoice(b, group == i);
                buttons.Add(b);
            }

            if (!storageOnly)
                view.Search(panel, value =>
                {
                    query = value;
                    refresh();
                }, new Vector2(.025f, .675f), new Vector2(.98f, .765f)).SetTextWithoutNotify(query);
        }

        IEnumerable<Stack> Filter(IEnumerable<Stack> list)
        {
            return list.Where(s =>
            {
                var d = catalog.Item(s.itemId);
                return (State != ScreenState.Chest || IsEquipment(s) == equipmentMode)
                    && (group < 0 || (int)d.group == group)
                    && (material < 0 || (int)d.material == material)
                    && (string.IsNullOrEmpty(query) || d.displayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    && (State != ScreenState.Fuel || d.fuelValue > 0);
            });
        }

        // 핵심 분기: !inventoryGrid 판정.
        // 상태 변경: sourceSlot 갱신.
        // 다음 연결: Blacksmith.BlacksmithView.Clear(UnityEngine.Transform) 호출.
        void RefreshInventory(bool chest)
        {
            if (!inventoryGrid)
                return;
            BlacksmithView.Clear(inventoryGrid);
            view.HideTooltip();
            var visible = Filter(Inventory.Data.chest).ToArray();
            foreach (var s in visible)
            {
                if (chest)
                {
                    ChestSlot(inventoryGrid, s, false);
                    continue;
                }

                InventorySlotView sourceSlot = null;
                sourceSlot = CreateSlot(inventoryGrid, s, () =>
                {
                    if (State == ScreenState.Fuel)
                    {
                        AddSelectedFuel(s);
                    }
                    else
                        SelectMaterial(s, sourceSlot);
                });
                sourceSlot.EnableTransferDrag(() => Inventory.Data.chest.Contains(s) && s.count > 0);
                sourceSlot.SetDragPreviewQuantity(() => 1);
                if (State == ScreenState.Selecting)
                    sourceSlot.gameObject.AddComponent<ItemDropTarget>().ConfigureProximity(
                        drag => Inventory.Selection.Contains(drag.Stack),
                        drag => ReturnSelected(drag.Stack, drag, drag.Stack.count),
                        sourceSlot.background, true);
            }

            if (chest)
                for (int i = visible.Length; i < Math.Max(30, ((visible.Length + 6) / 6) * 6); i++)
                    AddEmptyTransfer(inventoryGrid, false);
            else if (State == ScreenState.Selecting)
            {
                var viewport = inventoryGrid.parent.gameObject;
                (viewport.GetComponent<ItemDropTarget>() ?? viewport.AddComponent<ItemDropTarget>()).ConfigureProximity(
                    drag => Inventory.Selection.Contains(drag.Stack),
                    drag => ReturnSelected(drag.Stack, drag, drag.Stack.count),
                    viewport.GetComponent<Image>());
            }
        }

        bool CanDropMaterial(InventorySlotView drag) =>
            State == ScreenState.Selecting && Inventory.Data.chest.Contains(drag.Stack) && drag.Stack.count > 0;

        void ConfigureMaterialDrop(Transform area, Graphic feedbackGraphic)
        {
            var target = area.gameObject.AddComponent<ItemDropTarget>();
            if (State == ScreenState.Fuel)
                target.ConfigureProximity(
                    drag => Inventory.Data.chest.Contains(drag.Stack) &&
                        catalog.Item(drag.Stack.itemId).fuelValue > 0 && Inventory.Data.fuel < 50,
                    drag => AddSelectedFuel(drag.Stack), feedbackGraphic);
            else
                target.ConfigureProximity(CanDropMaterial, DropMaterial, feedbackGraphic);
        }

        void DropMaterial(InventorySlotView drag) => SelectMaterial(drag.Stack, drag);

        void SelectMaterial(Stack stack, InventorySlotView source)
        {
            var from = UiCenter(source.transform);
            if (!Inventory.Select(stack, CurrentStation))
            {
                Say("넣을 수 없습니다");
                return;
            }
            Render();
            FlyMaterial(stack.itemId, from, UiCenter(selectedGrid));
        }

        void ReturnSelected(Stack stack, InventorySlotView source, int count)
        {
            var from = UiCenter(source.transform);
            if (!Inventory.Transfer(Inventory.Selection, Inventory.Data.chest, stack, count)) return;
            Render();
            FlyMaterial(stack.itemId, from, UiCenter(inventoryGrid));
        }

        void AddSelectedFuel(Stack stack)
        {
            if (!Inventory.AddFuel(stack))
                Say("연료를 더 넣을 수 없습니다.");
            Persist();
            Render();
        }

        InventorySlotView CreateSlot(Transform parent, Stack stack, Action click, Action right = null, Action<InventorySlotView> drop = null, bool bag = false)
        {
            var item = catalog.Item(stack.itemId);
            return view.Slot(parent, stack, item, click, right, drop, enter =>
            {
                if (enter)
                    view.ShowTooltip(ItemInfo(stack));
                else
                    view.HideTooltip();
            }, bag);
        }

        string ItemInfo(Stack s)
        {
            var d = catalog.Item(s.itemId);
            float m = QualityRules.Multiplier(d, s.quality);
            int price = Mathf.RoundToInt(d.price * m * (QualityRules.AppliesTo(d) && s.quality == Quality.Master ? 2 : 1));
            string info = $"{d.displayName}   ×{s.count}\n판매 가격: {price}G";
            if (d.toolTier > 0)
                info += $"\n도구 티어 {d.toolTier}";
            if (QualityRules.AppliesTo(d))
                info += $"   {QualityRules.Name(s.quality)}\n공격력 {d.attack * m:0.##} · 방어력 {d.defense * m:0.##} · 공격 속도 {d.EffectiveAttackSpeed:0.##}";
            return info + "\n" + d.description + (string.IsNullOrEmpty(d.specialEffect) ? "" : "\n" + d.specialEffect);
        }

        // 핵심 분기: !Inventory.FillRecipe(recipe, batches) 판정.
        // 상태 변경: row.title.text 갱신.
        // 다음 연결: Blacksmith.RecipeEntryView.Bind(TMPro.TMP_FontAsset) 호출.
        void AddAutomatic(RecipeDefinition recipe)
        {
            var row = Instantiate(view.recipePrefab, autoList);
            row.gameObject.SetActive(true);
            row.Bind(view.font);
            int batches = 1;
            row.title.text = recipe.displayName + " ×" + recipe.outputCount;
            row.ingredients.text = Ingredients(recipe);
            row.quantity.text = "1";
            row.minus.onClick.AddListener(() =>
            {
                batches = Math.Max(1, batches - 1);
                row.quantity.text = batches.ToString();
            });
            row.plus.onClick.AddListener(() =>
            {
                batches = Math.Min(999, batches + 1);
                row.quantity.text = batches.ToString();
            });
            row.craft.onClick.AddListener(() =>
            {
                if (!Inventory.FillRecipe(recipe, batches))
                {
                    Say("재료가 부족합니다.");
                    StartCoroutine(Blink(row.title));
                    return;
                }

                if (CurrentStation == Station.Furnace)
                    Render();
                else
                    StartCraft(recipe);
            });
        }

        IEnumerator Blink(TMP_Text text)
        {
            for (int i = 0; i < 4; i++)
            {
                if (!text)
                    yield break;
                text.color = i % 2 == 0 ? Color.red : BlacksmithView.Ink;
                yield return new WaitForSecondsRealtime(.13f);
            }
        }

        string Ingredients(RecipeDefinition r)
        {
            string value = string.Join(" + ", r.ingredients.Select(x => catalog.Item(x.itemId).displayName + " ×" + x.count));
            if (r.station == Station.Tools)
                value += $"\n{toolNames[(int)r.tool]}";
            if (r.station == Station.Anvil)
                value += "\n위/아래/좌/우/중앙: " + string.Join("/", r.anvilHits);
            return value;
        }

        // 핵심 분기: IsHosted && CampaignController.Instance != null && !Inventory.Data.night 판정.
        // 상태 변경: b.gameObject.AddComponent<LayoutElement>().preferredHeight 갱신.
        // 다음 연결: Blacksmith.BlacksmithController.Say(string) 호출.
        void StartCraft(RecipeDefinition automatic, RecipeDefinition chosen = null)
        {
            if (IsHosted && CampaignController.Instance != null && !Inventory.Data.night)
            {
                Say("낮에는 제작할 수 없습니다. 침대에서 밤으로 전환하세요.");
                return;
            }

            if (automatic == null && chosen == null && CurrentStation == Station.Workbench)
            {
                var candidates = catalog.recipes.Where(r => r.enabled && r.station == CurrentStation && Crafting.Batches(r) > 0).ToArray();
                if (candidates.Length > 1)
                {
                    var existing = view.overlay.Find("ChooseCraftResult");
                    if (existing)
                        Destroy(existing.gameObject);
                    var p = view.Panel("ChooseCraftResult", view.overlay, new Vector2(.26f, .30f), new Vector2(.68f, .75f), true);
                    view.Text("Title", p, "제작할 결과물을 선택하세요", 27, new Vector2(.04f, .78f), new Vector2(.96f, .96f), BlacksmithView.Ink);
                    var list = view.Scroll(p, "PossibleResults", new Vector2(.04f, .22f), new Vector2(.96f, .75f));
                    foreach (var r in candidates)
                    {
                        var b = view.Button("Choose_" + r.id, list, r.displayName + " ×" + (r.outputCount * Crafting.Batches(r)), Vector2.zero, Vector2.one, () =>
                        {
                            Destroy(p.gameObject);
                            StartCraft(null, r);
                        });
                        b.gameObject.AddComponent<LayoutElement>().preferredHeight = 65;
                    }

                    view.Button("CancelResultChoice", p, "취소", new Vector2(.32f, .03f), new Vector2(.68f, .18f), () => Destroy(p.gameObject));
                    return;
                }
            }

            if (!Crafting.Begin(CurrentStation, CurrentTool, out var error, chosen ?? automatic))
            {
                Say(error);
                return;
            }

            timer = 0;
            quenchStarted = false;
            quenchCenter = UnityEngine.Random.Range(.65f, .87f);
            if (automatic != null)
            {
                Crafting.ApplyAutomatic(automatic);
                StartCoroutine(ResolveAnimation(true));
                return;
            }

            if (CurrentStation == Station.Furnace || (CurrentStation == Station.Workbench && !Crafting.NeedsTiming()))
                StartCoroutine(ResolveAnimation(true));
            else
                SetState(ScreenState.Playing);
        }

        // 핵심 분기: CurrentStation == Station.Workbench 판정.
        // 상태 변경: playingRoot 갱신.
        // 다음 연결: Blacksmith.BlacksmithView.Rect(string, UnityEngine.Transform, UnityEngine.Vector2, UnityEngine.Vector2, Unity… 호출.
        void DrawMinigame()
        {
            playingRoot = view.Rect("Minigame", view.overlay, new Vector2(.18f, .16f), new Vector2(.82f, .80f));
            var s = Inventory.Selection[0];
            string icon = view.ItemArtKey(catalog.Item(s.itemId));
            if (CurrentStation == Station.Workbench)
            {
                var r = Crafting.Matching(false);
                if (r != null)
                    icon = view.ItemArtKey(catalog.Item(r.outputId));
            }

            workIcon = view.Image("Workpiece", playingRoot, icon, Color.white, new Vector2(.28f, .15f), new Vector2(.72f, .83f), true);
            counter = view.Text("Counter", playingRoot, "", 27, new Vector2(.02f, .84f), new Vector2(.98f, 1), null, TextAlignmentOptions.Center);
            if (CurrentStation == Station.Tools)
            {
                workIcon.raycastTarget = true;
                gesture = workIcon.gameObject.AddComponent<CraftGestureInput>();
                gesture.Click = () =>
                {
                    if (CurrentTool == ToolKind.Hammer)
                        ProcessStroke();
                };
                gesture.Upstroke = () =>
                {
                    if (CurrentTool == ToolKind.Knife || CurrentTool == ToolKind.Plane)
                        ProcessStroke();
                };
                gesture.Roundtrip = () =>
                {
                    if (CurrentTool == ToolKind.Saw || CurrentTool == ToolKind.Whetstone)
                        ProcessStroke();
                };
                heldToolImage = view.Image("HeldTool", playingRoot, toolArt[(int)CurrentTool], Color.white, new Vector2(.76f, 0), new Vector2(.95f, .49f), true);
                heldToolRestPosition = heldToolImage.rectTransform.anchoredPosition;
                heldToolRestRotation = heldToolImage.rectTransform.localRotation;
                counter.text = $"{toolNames[(int)CurrentTool]} · 재료를 가공하세요";
                view.Button("FinishTool", view.overlay, "그만하기", new Vector2(.75f, .08f), new Vector2(.94f, .16f), () => StartCoroutine(ResolveAnimation(true)));
                Say(CurrentTool == ToolKind.Hammer ? "재료를 클릭하세요." : CurrentTool == ToolKind.Saw || CurrentTool == ToolKind.Whetstone ? "재료를 누른 채 위아래로 왕복하세요." : "재료 위에서 아래 → 위로 드래그하세요.");
            }
            else if (CurrentStation == Station.Anvil)
            {
                Vector2[] pos =
                {
                    new Vector2(.5f, .72f),
                    new Vector2(.5f, .22f),
                    new Vector2(.29f, .47f),
                    new Vector2(.71f, .47f),
                    new Vector2(.5f, .47f)
                };
                for (int i = 0; i < 5; i++)
                {
                    int n = i;
                    var b = view.Button("HammerPoint_" + i, playingRoot, "", pos[i] - new Vector2(.045f, .075f), pos[i] + new Vector2(.045f, .075f), () =>
                    {
                        if (!Crafting.Hit(n))
                        {
                            Say("달궈진 금속만 가공할 수 있습니다.");
                            return;
                        }

                        counter.text = "위/아래/좌/우/중앙  " + string.Join(" / ", Crafting.Hits);
                        workIcon.rectTransform.localScale = new Vector3(1 + Crafting.Hits.Sum() * .06f, 1 - Crafting.Hits.Sum() * .035f, 1);
                        workIcon.color = Color.Lerp(Color.white, new Color(1, .63f, .31f), Crafting.Hits.Sum() / 5f);
                        PlayImpactPulse(pos[n], BlacksmithView.Gold);
                        PlayFeedbackSound(0);
                        if (Crafting.Hits.Sum() == 5)
                            StartCoroutine(ResolveAnimation(true));
                    }, "ring");
                }

                counter.text = "5개의 지점을 두드리세요 · 총 5회";
                view.Button("FinishInvalid", view.overlay, "가공 종료", new Vector2(.77f, .08f), new Vector2(.94f, .15f), () => StartCoroutine(ResolveAnimation(true)));
            }
            else if (CurrentStation == Station.Quench)
            {
                workIcon.raycastTarget = true;
                gesture = workIcon.gameObject.AddComponent<CraftGestureInput>();
                gesture.Click = () =>
                {
                    quenchStarted = true;
                    timer = 0;
                };
                gesture.Released = () => FinishQuench();
                var gauge = view.Panel("QuenchGauge", playingRoot, new Vector2(.83f, .08f), new Vector2(.90f, .78f));
                view.Image("OrangeZone", gauge, null, new Color(1, .5f, .1f), new Vector2(.05f, quenchCenter - .16f), new Vector2(.95f, quenchCenter + .16f));
                view.Image("YellowZone", gauge, null, Color.yellow, new Vector2(.05f, quenchCenter - .10f), new Vector2(.95f, quenchCenter + .10f));
                view.Image("GreenZone", gauge, null, Color.green, new Vector2(.05f, quenchCenter - .04f), new Vector2(.95f, quenchCenter + .04f));
                progressImage = view.Image("GaugeMarker", gauge, null, Color.white, new Vector2(-.1f, 0), new Vector2(1.1f, .025f));
                counter.text = "재료를 누르고 있다가 노랑·초록에서 놓으세요";
            }
            else
                DrawTimingTarget();
        }

        // 핵심 분기: State != ScreenState.Playing 판정.
        // 상태 변경: counter.text 갱신.
        // 다음 연결: Blacksmith.CraftingService.Stroke() 호출.
        void ProcessStroke()
        {
            if (State != ScreenState.Playing)
                return;
            if (!Crafting.Stroke())
            {
                Say("이 도구로 가공할 수 없는 재료입니다.");
                return;
            }

            counter.text = $"{toolNames[(int)CurrentTool]} · 가공 완료 · 그만하기를 누르세요";
            workIcon.rectTransform.localRotation = Quaternion.Euler(0, 0, Crafting.Strokes % 2 == 0 ? -3 : 3);
            PlayImpactPulse(new Vector2(.5f, .48f), BlacksmithView.Cream);
            PlayFeedbackSound(CurrentTool == ToolKind.Hammer ? 0 : 1);
            if (Application.isPlaying && heldToolImage != null)
            {
                if (toolFeedback != null)
                    StopCoroutine(toolFeedback);
                heldToolImage.rectTransform.anchoredPosition = heldToolRestPosition;
                heldToolImage.rectTransform.localRotation = heldToolRestRotation;
                toolFeedback = StartCoroutine(AnimateToolStroke(heldToolImage, CurrentTool));
            }
        }

        void PlayImpactPulse(Vector2 point, Color color)
        {
            if (Application.isPlaying && playingRoot != null)
                StartCoroutine(AnimateImpactPulse(playingRoot, point, color));
        }

        IEnumerator AnimateImpactPulse(Transform parent, Vector2 point, Color color)
        {
            var ring = view.Image("WorkImpact", parent, "ring", color,
                point - new Vector2(.055f, .08f), point + new Vector2(.055f, .08f), true);
            ring.raycastTarget = false;
            for (float elapsed = 0; elapsed < .22f; elapsed += Time.unscaledDeltaTime)
            {
                if (ring == null)
                    yield break;
                float t = Mathf.Clamp01(elapsed / .22f);
                ring.rectTransform.localScale = Vector3.one * (.55f + t * 1.35f);
                ring.color = new Color(color.r, color.g, color.b, 1 - t);
                yield return null;
            }
            if (ring != null)
                Destroy(ring.gameObject);
        }

        // 핵심 분기: tool == null 판정.
        // 상태 변경: elapsed 갱신.
        IEnumerator AnimateToolStroke(Image tool, ToolKind kind)
        {
            var rect = tool.rectTransform;
            var start = rect.anchoredPosition;
            var rotation = rect.localRotation;
            for (float elapsed = 0; elapsed < .18f; elapsed += Time.unscaledDeltaTime)
            {
                if (tool == null)
                    yield break;
                float wave = Mathf.Sin(Mathf.Clamp01(elapsed / .18f) * Mathf.PI);
                rect.anchoredPosition = start + (kind == ToolKind.Hammer ? Vector2.down * 28 :
                    kind == ToolKind.Knife || kind == ToolKind.Plane ? Vector2.up * 26 : Vector2.left * 24) * wave;
                rect.localRotation = rotation * Quaternion.Euler(0, 0, (kind == ToolKind.Hammer ? -14 : 8) * wave);
                yield return null;
            }
            if (tool != null)
            {
                rect.anchoredPosition = start;
                rect.localRotation = rotation;
            }
            toolFeedback = null;
        }

        void DrawTimingTarget()
        {
            var old = playingRoot.Find("TimingTarget");
            if (old)
            {
                old.gameObject.SetActive(false);
                Destroy(old.gameObject);
            }

            float x = .36f + (Crafting.Completed % 3) * .14f, y = .32f + (Crafting.Completed % 2) * .22f;
            var b = view.Button("TimingTarget", playingRoot, "", new Vector2(x - .05f, y - .09f), new Vector2(x + .05f, y + .09f), () => ScoreTiming(), "disc");
            guide = view.Image("ShrinkingRing", b.transform, "ring", Color.white, Vector2.zero, Vector2.one, true);
            counter.text = $"조립 {Crafting.Completed} / {Crafting.Targets}   점수 {Crafting.Score}";
            timer = 0;
            workIcon.color = Color.Lerp(new Color(.03f, .03f, .03f), Color.white, (float)Crafting.Completed / Crafting.Targets);
        }

        void ScoreTiming()
        {
            if (State != ScreenState.Playing || CurrentStation != Station.Workbench)
                return;
            float distance = Mathf.Abs(1 - timer / 1.4f);
            int previous = Crafting.Score;
            Crafting.Timing(distance);
            Say(Crafting.Score - previous == 4 ? "완벽 +4" : Crafting.Score - previous == 2 ? "성공 +2" : "실패 +0");
            if (Crafting.Completed >= Crafting.Targets)
                StartCoroutine(ResolveAnimation(true));
            else
                DrawTimingTarget();
        }

        void FinishQuench()
        {
            if (State != ScreenState.Playing || !quenchStarted)
                return;
            quenchStarted = false;
            bool success = Mathf.Abs(timer / 3f - quenchCenter) <= .10f;
            PlayFeedbackSound(2);
            StartCoroutine(ResolveAnimation(success));
        }

        // 핵심 분기: CurrentStation == Station.Furnace 판정.
        // 상태 변경: State 갱신.
        // 다음 연결: Blacksmith.BlacksmithController.Render() 호출.
        IEnumerator ResolveAnimation(bool success)
        {
            State = ScreenState.Animating;
            Render();
            var icon = view.Image("Processing", view.overlay, view.ItemArtKey(catalog.Item(Inventory.Selection[0].itemId)), Color.white, new Vector2(.43f, .37f), new Vector2(.57f, .62f), true);
            var steam = CurrentStation == Station.Quench ? view.Image("QuenchSteam", view.overlay,
                "ring", new Color(.85f, .95f, 1, 0), new Vector2(.43f, .32f), new Vector2(.57f, .52f), true) : null;
            float duration = CurrentStation == Station.Furnace ? 2.5f : 1.1f;
            for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                icon.color = Color.Lerp(Color.white, CurrentStation == Station.Furnace ? new Color(1, .35f, .04f) :
                    CurrentStation == Station.Quench ? new Color(.75f, .86f, 1) : BlacksmithView.Gold, t);
                icon.rectTransform.localScale = Vector3.one * (1 + .06f * Mathf.Sin(elapsed * 18));
                if (CurrentStation == Station.Furnace)
                    icon.rectTransform.anchoredPosition = Vector2.down * (t * 52);
                if (CurrentStation == Station.Quench)
                {
                    icon.rectTransform.anchoredPosition = Vector2.down * (Mathf.Sin(t * Mathf.PI) * 48);
                    steam.rectTransform.localScale = Vector3.one * (.6f + t * 1.5f);
                    steam.color = new Color(.85f, .95f, 1, Mathf.Sin(t * Mathf.PI) * .8f);
                }
                yield return null;
            }

            result = Crafting.Finish(success);
            Persist();
            SetState(ScreenState.Result);
            if (result != null && result.discovered && result.stack != null && Application.isPlaying)
            {
                PlayFeedbackSound(3);
                StartCoroutine(AnimateDiscovery(result.stack.itemId));
            }
        }

        void PlayFeedbackSound(int kind)
        {
            if (!Application.isPlaying)
                return;
            if (feedbackAudio == null)
            {
                feedbackAudio = gameObject.AddComponent<AudioSource>();
                feedbackAudio.playOnAwake = false;
                feedbackAudio.spatialBlend = 0;
                feedbackClips = new AudioClip[4];
            }
            if (feedbackClips[kind] == null)
                feedbackClips[kind] = CreateFeedbackClip(kind);
            feedbackAudio.PlayOneShot(feedbackClips[kind], .22f);
        }

        // 상태 변경: noise 갱신.
        static AudioClip CreateFeedbackClip(int kind)
        {
            const int rate = 22050;
            float duration = kind == 2 ? .42f : kind == 3 ? .38f : .18f;
            var samples = new float[Mathf.CeilToInt(rate * duration)];
            uint noise = 2463534242;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)rate;
                noise ^= noise << 13;
                noise ^= noise >> 17;
                noise ^= noise << 5;
                float hiss = ((noise & 65535) / 32767.5f) - 1;
                float envelope = Mathf.Clamp01(t * 90) * Mathf.Exp(-t * (kind == 2 ? 6 : kind == 3 ? 5 : 20));
                float tone = kind == 0 ? Mathf.Sin(2 * Mathf.PI * 370 * t) * .7f + hiss * .3f :
                    kind == 1 ? hiss * .48f + Mathf.Sin(2 * Mathf.PI * 130 * t) * .16f :
                    kind == 2 ? hiss * .48f :
                    Mathf.Sin(2 * Mathf.PI * 660 * t) * .40f + Mathf.Sin(2 * Mathf.PI * 990 * t) * .23f;
                samples[i] = Mathf.Clamp(tone * envelope, -1, 1);
            }
            var clip = AudioClip.Create("SmithyFeedback_" + kind, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        // 핵심 분기: item == null 판정.
        // 상태 변경: elapsed 갱신.
        // 다음 연결: Blacksmith.BlacksmithCatalog.Item(string) 호출.
        IEnumerator AnimateDiscovery(string itemId)
        {
            var item = catalog.Item(itemId);
            if (item == null)
                yield break;
            var paper = view.Panel("FirstDiscovery", view.overlay, new Vector2(.25f, .22f), new Vector2(.75f, .78f), true);
            paper.SetAsLastSibling();
            view.Text("DiscoveryTitle", paper, "새 레시피 발견", 38,
                new Vector2(.08f, .77f), new Vector2(.92f, .94f), BlacksmithView.Ink, TextAlignmentOptions.Center);
            view.Image("DiscoveryItem", paper, view.ItemArtKey(item), Color.white,
                new Vector2(.37f, .34f), new Vector2(.63f, .74f), true);
            view.Text("DiscoveryHint", paper, item.displayName + "\n레시피 지도를 확인하세요", 26,
                new Vector2(.06f, .08f), new Vector2(.94f, .32f), BlacksmithView.Ink, TextAlignmentOptions.Center);
            for (float elapsed = 0; elapsed < .24f; elapsed += Time.unscaledDeltaTime)
            {
                if (paper == null || State != ScreenState.Result)
                    yield break;
                float t = Mathf.Clamp01(elapsed / .24f);
                paper.localScale = new Vector3(Mathf.SmoothStep(.08f, 1, t), 1, 1);
                yield return null;
            }
            yield return new WaitForSecondsRealtime(.75f);
            for (float elapsed = 0; elapsed < .20f; elapsed += Time.unscaledDeltaTime)
            {
                if (paper == null || State != ScreenState.Result)
                    yield break;
                float t = Mathf.Clamp01(elapsed / .20f);
                paper.localScale = new Vector3(Mathf.SmoothStep(1, .08f, t), 1, 1);
                yield return null;
            }
            if (paper != null)
                Destroy(paper.gameObject);
        }

        // 핵심 분기: result.stack != null 판정.
        // 상태 변경: CurrentStation 갱신.
        // 다음 연결: Blacksmith.BlacksmithView.Panel(string, UnityEngine.Transform, UnityEngine.Vector2, UnityEngine.Vector2, bool) 호출.
        void DrawResult()
        {
            var p = view.Panel("ResultPanel", view.overlay, new Vector2(.29f, .19f), new Vector2(.71f, .81f), true);
            if (result.success)
                view.Text("Title", p, "제작 완료", 36, new Vector2(.08f, .78f), new Vector2(.92f, .96f), BlacksmithView.Ink, TextAlignmentOptions.Center);
            if (result.stack != null)
                view.Image("ResultIcon", p, view.ItemArtKey(catalog.Item(result.stack.itemId)), Color.white, new Vector2(.30f, .38f), new Vector2(.70f, .77f), true);
            string resultText = result.success
                ? $"{catalog.Item(result.stack.itemId).displayName} ×{result.stack.count}" +
                  (QualityRules.AppliesTo(catalog.Item(result.stack.itemId)) ? "\n" + QualityRules.Name(result.stack.quality) : "") +
                  (result.returned.Count > 0 ? "\n숙련 보너스: " + string.Join(" / ", result.returned.Select(s => catalog.Item(s.itemId).displayName + (QualityRules.AppliesTo(catalog.Item(s.itemId)) ? " " + QualityRules.Name(s.quality) : "") + " ×" + s.count)) : "")
                : "실패했습니다.";
            view.Text("ResultName", p, resultText, 27,
                result.success ? new Vector2(.04f, .20f) : new Vector2(.12f, .38f),
                result.success ? new Vector2(.96f, .38f) : new Vector2(.88f, .62f),
                BlacksmithView.Ink, TextAlignmentOptions.Center);
            view.Emphasize(view.Button("Collect", p, "확인", new Vector2(.24f, .05f), new Vector2(.76f, .18f), () =>
            {
                bool discovered = result.discovered;
                var next = result.stack != null && catalog.Item(result.stack.itemId).heated && result.success ? (CurrentStation == Station.Furnace ? Station.Anvil : CurrentStation == Station.Anvil ? Station.Quench : CurrentStation) : CurrentStation;
                if (next != CurrentStation)
                {
                    CurrentStation = next;
                    var source = Inventory.Data.chest.Find(s => s.Key == result.stack.Key);
                    if (source != null)
                        Inventory.Transfer(Inventory.Data.chest, Inventory.Selection, source, Math.Min(source.count, result.stack.count));
                    SetState(ScreenState.Selecting);
                }
                else
                    SetState(ScreenState.Station);
                Say(discovered ? "새 레시피를 발견했습니다! 두루마리에서 확인하세요." : result.message ?? "결과물을 보관함에 넣었습니다.");
            }));
        }

        // The world station selects the content; the player cannot switch stations inside this panel.
        void DrawChest()
        {
            var bag = view.Panel("Bag", view.overlay, new Vector2(.065f, .14f), new Vector2(.455f, .85f));
            view.Image("BagLeather", bag, "bag_leather", Color.white,
                new Vector2(.025f, .025f), new Vector2(.975f, .83f));
            view.Image("BagTop", bag, "bag_top", Color.white,
                new Vector2(.015f, .75f), new Vector2(.985f, 1));
            view.Text("BagTitle", bag, equipmentMode ? "장착 장비" : "가방 아이템", 23,
                new Vector2(.10f, .67f), new Vector2(.64f, .73f));

            if (equipmentMode)
                DrawEquipment(bag);
            else
            {
                var visibleBag = Inventory.Data.bag.Where(s => !IsEquipment(s)).ToArray();
                int pages = Math.Max(1, (visibleBag.Length + 19) / 20);
                bagPage = Mathf.Clamp(bagPage, 0, pages - 1);
                view.Button("NextBagPage", bag, $"{bagPage + 1}/{pages}쪽 →",
                    new Vector2(.66f, .67f), new Vector2(.98f, .77f), () =>
                    {
                        bagPage = (bagPage + 1) % pages;
                        Render();
                    });
                var grid = view.Scroll(bag, "BagItems", new Vector2(.10f, .045f), new Vector2(.94f, .65f), 5, 86);
                foreach (var stack in visibleBag.Skip(bagPage * 20).Take(20))
                    ChestSlot(grid, stack, true);
                for (int i = visibleBag.Skip(bagPage * 20).Take(20).Count(); i < 20; i++)
                    AddEmptyTransfer(grid, true);
                ConfigureChestDropArea(grid, Inventory.Data.bag, Inventory.Data.chest);
            }

            var chest = view.Panel("ChestPanel", view.overlay, new Vector2(.52f, .14f), new Vector2(.975f, .85f));
            chest.GetComponent<Image>().color = new Color(.14f, .14f, .13f, .97f);
            if (equipmentMode)
                DrawRackInventory(chest);
            else
            {
                view.Text("ChestTitle", chest, "보관함 아이템", 28,
                    new Vector2(.02f, .89f), new Vector2(.95f, .99f));
                DrawFilters(chest, () => RefreshInventory(true), true);
                inventoryGrid = view.Scroll(chest, "ChestItems", new Vector2(.025f, .045f), new Vector2(.99f, .755f), 6, 96);
                RefreshInventory(true);
                ConfigureChestDropArea(inventoryGrid, Inventory.Data.chest, Inventory.Data.bag);
                string[] categories = { "전체", "목재", "석재", "철", "기타" };
                int[] kinds = { -1, (int)MaterialKind.Wood, (int)MaterialKind.Stone,
                    (int)MaterialKind.Iron, (int)MaterialKind.Other };
                var categoryButtons = new List<Button>();
                for (int i = 0; i < categories.Length; i++)
                {
                    int kind = kinds[i];
                    var button = view.Button("Material" + i, view.overlay, categories[i],
                        new Vector2(.51f + i * .09f, .085f), new Vector2(.595f + i * .09f, .15f), () =>
                        {
                            material = kind;
                            for (int k = 0; k < categoryButtons.Count; k++)
                                view.HighlightChoice(categoryButtons[k], material == kinds[k]);
                            RefreshInventory(true);
                        });
                    RuntimeUIFactory.FitText(button.GetComponentInChildren<TMP_Text>(), 18);
                    view.HighlightChoice(button, material == kind);
                    categoryButtons.Add(button);
                }
            }

            view.Button("CancelHeld", view.overlay, held == null ? "선택 해제" : "들기 취소",
                new Vector2(.20f, .085f), new Vector2(.34f, .15f), () =>
                {
                    held = null;
                    discardMode = false;
                    discard.Clear();
                    Render();
                });
        }

        bool IsEquipment(Stack stack)
        {
            var definition = stack == null ? null : catalog.Item(stack.itemId);
            return definition != null && !string.IsNullOrEmpty(definition.equipmentSlot);
        }

        // 핵심 분기: equipped != null 판정.
        // 상태 변경: inventoryGrid 갱신.
        // 다음 연결: Blacksmith.BlacksmithView.Text(string, UnityEngine.Transform, string, float, UnityEngine.Vector2, UnityEngine… 호출.
        void DrawRackInventory(Transform panel)
        {
            view.Text("RackTitle", panel, "장비 목록 · 클릭하여 장착", 28,
                new Vector2(.03f, .88f), new Vector2(.96f, .98f));
            inventoryGrid = view.Scroll(panel, "EquipmentItems", new Vector2(.025f, .045f),
                new Vector2(.99f, .83f), 6, 96);
            foreach (var stack in Inventory.Data.bag.Where(IsEquipment).ToArray())
                ChestSlot(inventoryGrid, stack, true);
            foreach (var stack in Inventory.Data.chest.Where(IsEquipment).ToArray())
                ChestSlot(inventoryGrid, stack, false);
            int shown = Inventory.Data.bag.Count(IsEquipment) + Inventory.Data.chest.Count(IsEquipment);
            for (int i = shown; i < Math.Max(30, ((shown + 6) / 6) * 6); i++)
                view.Image("EquipmentEmpty", inventoryGrid, "slot", Color.white, Vector2.zero, Vector2.one).raycastTarget = false;
            var viewport = inventoryGrid.parent.gameObject;
            var target = viewport.GetComponent<ItemDropTarget>() ?? viewport.AddComponent<ItemDropTarget>();
            target.ConfigureProximity(drag =>
                (Inventory.Data.bag.Contains(drag.Stack) || Inventory.Data.chest.Contains(drag.Stack) ||
                 Inventory.Data.equipment.Any(e => ReferenceEquals(e.stack, drag.Stack))) &&
                IsEquipment(drag.Stack) && drag.Stack.count > 0 && !discardMode, drag =>
                {
                    var equipped = Inventory.Data.equipment.Find(e => ReferenceEquals(e.stack, drag.Stack));
                    if (equipped != null)
                    {
                        Inventory.Unequip(equipped.slot);
                        Persist();
                        Render();
                        return;
                    }
                    var from = Inventory.Data.bag.Contains(drag.Stack) ? Inventory.Data.bag : Inventory.Data.chest;
                    var to = from == Inventory.Data.bag ? Inventory.Data.chest : Inventory.Data.bag;
                    if (!Inventory.Transfer(from, to, drag.Stack, drag.Stack.count)) return;
                    Persist();
                    Render();
                }, viewport.GetComponent<Image>());
        }

        // 핵심 분기: discardMode && !bag 판정.
        // 상태 변경: slot 갱신.
        // 다음 연결: Blacksmith.BlacksmithController.CreateSlot(UnityEngine.Transform, Blacksmith.Stack, System.Action, System.Act… 호출.
        void ChestSlot(Transform parent, Stack stack, bool bag)
        {
            InventorySlotView slot = null;
            var source = bag ? Inventory.Data.bag : Inventory.Data.chest;
            slot = CreateSlot(parent, stack, () =>
            {
                if (discardMode && !bag)
                {
                    if (!discard.Add(stack.Key))
                        discard.Remove(stack.Key);
                    slot.background.color = discard.Contains(stack.Key) ? Color.red : Color.white;
                    return;
                }

                if (equipmentMode)
                {
                    if (!Inventory.Equip(stack))
                        Say("이 칸에 장착할 수 없습니다.");
                    Persist();
                    Render();
                    return;
                }

                // In the storage chest, clicking a chest item puts one in the bag.
                if (!bag)
                {
                    if (Inventory.Transfer(Inventory.Data.chest, Inventory.Data.bag, stack, 1))
                    {
                        held = null;
                        Persist();
                        Render();
                        Say("가방으로 1개 옮겼습니다.");
                    }
                    else
                        Say("아이템을 옮길 수 없습니다.");
                    return;
                }

                if (held != null)
                {
                    var from = heldBag ? Inventory.Data.bag : Inventory.Data.chest;
                    var original = from.Find(x => x.Key == held.Key);
                    if (heldBag == bag)
                    {
                        if (original != null)
                            Inventory.Reorder(from, original, stack);
                    }
                    else
                        Inventory.Transfer(from, source, original, Math.Min(held.count, original?.count ?? 0));
                    held = null;
                    Persist();
                    Render();
                    return;
                }

                held = stack.Copy(slot.LongPress ? stack.count : 1);
                heldBag = bag;
                Say($"{catalog.Item(stack.itemId).displayName} ×{held.count} 들기 · 반대편 칸을 클릭하세요.");
            }, () =>
            {
                // Bag right-click is a single-item shortcut; rack/chest right-click keeps its existing batch action.
                int amount = bag && !equipmentMode ? 1 : stack.count;
                if (!Inventory.Transfer(source, bag ? Inventory.Data.chest : Inventory.Data.bag, stack, amount))
                {
                    Say("아이템을 옮길 수 없습니다.");
                    return;
                }
                held = null;
                Persist();
                Render();
                if (bag && !equipmentMode)
                    Say("보관함으로 1개 옮겼습니다.");
            }, drag =>
            {
                MoveDropped(drag, source, stack);
                held = null;
                Persist();
                Render();
            }, bag);
            slot.EnableTransferDrag(() => source.Contains(stack) && stack.count > 0 && !discardMode,
                drag => source.Contains(drag.Stack));
            if (equipmentMode)
                slot.gameObject.AddComponent<ItemDropTarget>().ConfigureProximity(
                    drag => Inventory.Data.equipment.Any(e => ReferenceEquals(e.stack, drag.Stack)),
                    drag =>
                    {
                        var entry = Inventory.Data.equipment.Find(e => ReferenceEquals(e.stack, drag.Stack));
                        if (entry == null) return;
                        Inventory.Unequip(entry.slot);
                        Persist();
                        Render();
                    }, slot.background, true);
            if (discard.Contains(stack.Key))
                slot.background.color = Color.red;
        }

        void ConfigureChestDropArea(RectTransform content, List<Stack> destination, List<Stack> source)
        {
            var viewport = content.parent.gameObject;
            var target = viewport.GetComponent<ItemDropTarget>() ?? viewport.AddComponent<ItemDropTarget>();
            target.ConfigureProximity(
                drag => source.Contains(drag.Stack) && drag.Stack.count > 0 && !discardMode,
                drag =>
                {
                    if (!Inventory.Transfer(source, destination, drag.Stack, drag.Stack.count)) return;
                    held = null;
                    Persist();
                    Render();
                }, viewport.GetComponent<Image>());
        }

        void AddEmptyTransfer(Transform parent, bool bag)
        {
            var go = view.Button("EmptySlot", parent, "", Vector2.zero, Vector2.one, () =>
            {
                if (held == null)
                    return;
                var from = heldBag ? Inventory.Data.bag : Inventory.Data.chest;
                var original = from.Find(x => x.Key == held.Key);
                Inventory.Transfer(from, bag ? Inventory.Data.bag : Inventory.Data.chest, original, Math.Min(held.count, original?.count ?? 0));
                held = null;
                Persist();
                Render();
            }, bag ? "bag_slot" : "slot");
            var destination = bag ? Inventory.Data.bag : Inventory.Data.chest;
            go.gameObject.AddComponent<ItemDropTarget>().ConfigureProximity(
                drag => !discardMode && drag.Stack.count > 0 &&
                    (bag ? Inventory.Data.chest.Contains(drag.Stack) : Inventory.Data.bag.Contains(drag.Stack)),
                drag =>
                {
                    MoveDropped(drag, destination, null);
                    held = null;
                    Persist();
                    Render();
                }, go.image, true);
        }

        // 핵심 분기: equipped != null 판정.
        // 상태 변경: stack 갱신.
        // 다음 연결: Blacksmith.InventoryService.Unequip(string) 호출.
        void MoveDropped(InventorySlotView drag, List<Stack> destination, Stack before)
        {
            var stack = drag.Stack;
            int amount = stack.count;
            var equipped = Inventory.Data.equipment.Find(e => ReferenceEquals(e.stack, stack));
            if (equipped != null)
            {
                string key = stack.Key;
                Inventory.Unequip(equipped.slot);
                stack = Inventory.Data.chest.Find(s => s.Key == key);
            }

            var from = Inventory.Data.bag.Contains(stack) ? Inventory.Data.bag : Inventory.Data.chest;
            if (from == destination)
            {
                if (before != null)
                    Inventory.Reorder(from, stack, before);
            }
            else
                Inventory.Transfer(from, destination, stack, amount);
        }

        // 핵심 분기: def != null && def.bow 판정.
        // 상태 변경: slots[3] 갱신.
        // 다음 연결: Blacksmith.BlacksmithCatalog.Item(string) 호출.
        void DrawEquipment(Transform parent)
        {
            string[] slots =
            {
                "Head",
                "Weapon",
                "Armor",
                "Shield",
                "Legs",
                "Pickaxe",
                "Feet",
                "Axe"
            };
            string[] names =
            {
                "머리",
                "무기",
                "몸통",
                "방패",
                "하의",
                "곡괭이",
                "신발",
                "도끼"
            };
            var weapon = Inventory.Data.equipment.Find(x => x.slot == "Weapon");
            var def = weapon == null ? null : catalog.Item(weapon.stack.itemId);
            if (def != null && def.bow)
            {
                slots[3] = "Arrow";
                names[3] = "화살";
            }
            bool hideOffhand = def != null && def.twoHanded && !def.bow &&
                def.id.IndexOf("hammer", StringComparison.OrdinalIgnoreCase) >= 0;

            var portrait = view.Rect("PlayerPortrait", parent, new Vector2(.52f, .19f),
                new Vector2(.94f, .72f));
            portrait.gameObject.AddComponent<Image>().color = new Color(.67f, .47f, .35f);
            var character = view.Rect("PlayerIdle", portrait, new Vector2(.08f, .08f),
                new Vector2(.92f, .92f)).gameObject.AddComponent<Image>();
            var player = FindFirstObjectByType<PlayerAnimationController>();
            character.sprite = view.rackIdleSprite != null ? view.rackIdleSprite :
                player != null ? player.GetComponentInChildren<SpriteRenderer>()?.sprite : null;
            character.color = character.sprite != null ? Color.white : new Color(.3f, .25f, .22f);
            character.preserveAspect = true;
            character.raycastTarget = false;

            for (int i = 0; i < slots.Length; i++)
            {
                if (i == 3 && hideOffhand)
                    continue;
                string key = slots[i];
                var e = Inventory.Data.equipment.Find(x => x.slot == key);
                float x = i % 2 == 0 ? .105f : .325f;
                float y = .565f - (i / 2) * .125f;
                bool locked = key == "Shield" && def != null && def.twoHanded || key == "Arrow" && (def == null || !def.bow);
                var b = view.Button("Equip_" + key, parent, locked ? "잠김" : e == null ? names[i] : "", new Vector2(x, y), new Vector2(x + .17f, y + .12f), () =>
                {
                    Inventory.Unequip(key);
                    Persist();
                    Render();
                }, e == null ? "bag_slot" : view.ItemArtKey(catalog.Item(e.stack.itemId)));
                var buttonLabel = b.GetComponentInChildren<TMP_Text>();
                if (buttonLabel != null) RuntimeUIFactory.FitText(buttonLabel, 16);
                b.interactable = !locked;
                b.gameObject.AddComponent<UiHoverOutline>();
                var target = b.gameObject.AddComponent<ItemDropTarget>();
                target.ConfigureProximity(drag => !locked &&
                    catalog.Item(drag.Stack.itemId).equipmentSlot == key &&
                    (Inventory.Data.bag.Contains(drag.Stack) || Inventory.Data.chest.Contains(drag.Stack)), drag =>
                {
                    if (Inventory.Equip(drag.Stack))
                    {
                        Persist();
                        Render();
                    }
                }, b.image, true);
                target.Hover = enter =>
                {
                    if (enter && e != null)
                        view.ShowTooltip(ItemInfo(e.stack));
                    else
                        view.HideTooltip();
                };
                if (e != null)
                {
                    b.gameObject.SetActive(false);
                    var slot = CreateSlot(parent, e.stack, () =>
                    {
                        Inventory.Unequip(key);
                        Persist();
                        Render();
                    }, null, drag =>
                    {
                        if (catalog.Item(drag.Stack.itemId).equipmentSlot == key)
                        {
                            Inventory.Equip(drag.Stack);
                            Persist();
                            Render();
                        }
                    }, true);
                    var rect = (RectTransform)slot.transform;
                    rect.anchorMin = new Vector2(x, y);
                    rect.anchorMax = new Vector2(x + .17f, y + .12f);
                    rect.offsetMin = rect.offsetMax = Vector2.zero;
                    slot.EnableTransferDrag(() => Inventory.Data.equipment.Any(entry =>
                        entry.slot == key && ReferenceEquals(entry.stack, e.stack)));
                    slot.gameObject.AddComponent<ItemDropTarget>().ConfigureProximity(
                        drag => catalog.Item(drag.Stack.itemId).equipmentSlot == key &&
                            (Inventory.Data.bag.Contains(drag.Stack) || Inventory.Data.chest.Contains(drag.Stack)),
                        drag =>
                        {
                            if (!Inventory.Equip(drag.Stack)) return;
                            Persist();
                            Render();
                        }, slot.background, true);
                }

                if (e != null)
                    view.Text("EquipLabel", parent, names[i], 16, new Vector2(x, y - .035f), new Vector2(x + .17f, y), null, TextAlignmentOptions.Center);
            }

            view.Text("Stats", parent, Inventory.EquipmentStats() + "\n장비 클릭: 장착 / 해제", 17,
                new Vector2(.10f, .015f), new Vector2(.94f, .155f));
        }

        // 핵심 분기: IsHosted && CampaignController.Instance != null && !Inventory.Data.night 판정.
        // 상태 변경: CurrentStation 갱신.
        // 다음 연결: Blacksmith.BlacksmithController.Say(string) 호출.
        public void PrepareRecipe(RecipeDefinition recipe)
        {
            if (IsHosted && CampaignController.Instance != null && !Inventory.Data.night)
            {
                Say("낮에는 제작할 수 없습니다. 침대에서 밤으로 전환하세요.");
                return;
            }

            if (Crafting.Active)
            {
                Say("가공 중에는 재료를 바꿀 수 없습니다.");
                return;
            }

            if (!Inventory.FillRecipe(recipe, 1))
            {
                Say("레시피 재료가 부족합니다.");
                return;
            }

            CurrentStation = recipe.station;
            CurrentTool = recipe.tool;
            SetState(ScreenState.Selecting);
        }

        void ToggleRecipes()
        {
            if (State == ScreenState.Animating || State == ScreenState.Result)
                return;
            if (State == ScreenState.Recipes || State == ScreenState.Codex)
            {
                State = returnState;
                Render();
                return;
            }

            returnState = State;
            SetState(ScreenState.Recipes);
        }

        readonly RecipeBookState recipeBookState = new RecipeBookState();
        void DrawRecipes()
        {
            view.overlay.SetAsLastSibling();
            var panel = view.Panel("RecipeBook", view.overlay, Vector2.zero, Vector2.one, true);
            panel.gameObject.AddComponent<RecipeBookView>().Initialize(this, recipeBookState, ToggleRecipes);
        }

        void DrawSleep()
        {
            var panel = view.Panel("SleepConfirmation", view.overlay, new Vector2(.28f, .31f), new Vector2(.72f, .69f));
            view.Text("Question", panel, Inventory.Data.night ? "잠을 자고 다음 날 아침을 맞이할까요?" : "낮잠을 자면 채집 시간이 지나 밤이 됩니다.", 28, new Vector2(.05f, .39f), new Vector2(.95f, .92f), null, TextAlignmentOptions.Center);
            view.Emphasize(view.Button("SleepYes", panel, "잠자기", new Vector2(.08f, .08f), new Vector2(.46f, .29f), () => StartCoroutine(Sleep())));
            view.Button("SleepNo", panel, "취소", new Vector2(.54f, .08f), new Vector2(.92f, .29f), () => SetState(ScreenState.Home));
        }

        // 핵심 분기: Inventory.Data.night 판정.
        // 상태 변경: State 갱신.
        // 다음 연결: Blacksmith.BlacksmithController.Render() 호출.
        IEnumerator Sleep()
        {
            sleepInProgress = true;
            State = ScreenState.Animating;
            Render();
            if (Inventory.Data.night)
            {
                Inventory.Data.day++;
                Inventory.Data.night = false;
            }
            else
                Inventory.Data.night = true;
            Inventory.Data.hp = Inventory.Data.maxHp;
            Persist();
            var fade = view.Panel("DayTransition", view.overlay, Vector2.zero, Vector2.one);
            fade.GetComponent<Image>().color = Inventory.Data.night ? new Color(.04f, .08f, .17f) : new Color(.45f, .65f, .75f);
            int due = CampaignController.Instance != null ? Mathf.Max(0, CampaignController.Instance.State.lastDebtDay + 7 - Inventory.Data.day) : 7;
            view.Text("Day", fade, $"{Inventory.Data.day}일 · {(Inventory.Data.night ? "밤" : "아침")}\nD-{due}", 64, Vector2.zero, Vector2.one, null, TextAlignmentOptions.Center);
            yield return new WaitForSecondsRealtime(1.5f);
            sleepInProgress = false;
            SetState(ScreenState.Home);
            Say("동화율이 0으로 돌아왔습니다.");
        }

        // 핵심 분기: State == ScreenState.Animating || State == ScreenState.Result 판정.
        // 상태 변경: held 갱신.
        // 다음 연결: Blacksmith.BlacksmithController.ToggleRecipes() 호출.
        void Back()
        {
            if (State == ScreenState.Animating || State == ScreenState.Result)
                return;
            if (State == ScreenState.Recipes || State == ScreenState.Codex)
            {
                ToggleRecipes();
                return;
            }

            if (State == ScreenState.Playing)
            {
                Say("가공을 마친 뒤 나갈 수 있습니다.");
                return;
            }

            if (State == ScreenState.Selecting)
            {
                ReturnSelectedMaterials(true);
                return;
            }

            Inventory.ReturnAll();
            held = null;
            discardMode = false;
            discard.Clear();
            Persist();
            SetState(State == ScreenState.Selecting || State == ScreenState.Fuel ? ScreenState.Station : State == ScreenState.Station ? ScreenState.Workshop : ScreenState.Home);
        }

        Vector3 UiCenter(Transform target)
        {
            Canvas.ForceUpdateCanvases();
            if (target is RectTransform rect)
                return rect.TransformPoint(rect.rect.center);
            var overlay = view.overlay;
            return overlay.TransformPoint(overlay.rect.center);
        }

        Vector3 OverlayPoint(float x, float y)
        {
            var rect = view.overlay;
            return rect.TransformPoint(new Vector3(rect.rect.xMin + rect.rect.width * x,
                rect.rect.yMin + rect.rect.height * y, 0));
        }

        void ReturnSelectedMaterials(bool close)
        {
            var itemIds = Inventory.Selection.Select(s => s.itemId).Take(4).ToArray();
            var from = UiCenter(selectedGrid);
            Inventory.ReturnAll();
            if (close)
            {
                Persist();
                SetState(ScreenState.Station);
            }
            else
                Render();
            var to = close ? OverlayPoint(.82f, .48f) : UiCenter(inventoryGrid);
            for (int i = 0; i < itemIds.Length; i++)
                FlyMaterial(itemIds[i], from + Vector3.right * (i * 15), to + Vector3.up * (i * 12));
        }

        void FlyMaterial(string itemId, Vector3 from, Vector3 to)
        {
            if (!Application.isPlaying || view == null || view.hud == null)
                return;
            var item = catalog.Item(itemId);
            if (item == null || view.ItemArt(item) == null)
                return;
            var ghost = view.Image("MaterialFlight", view.hud, view.ItemArtKey(item), Color.white,
                new Vector2(.5f, .5f), new Vector2(.5f, .5f), true);
            ghost.rectTransform.sizeDelta = new Vector2(76, 76);
            ghost.rectTransform.position = from;
            ghost.transform.SetAsLastSibling();
            StartCoroutine(AnimateMaterialFlight(ghost, from, to));
        }

        IEnumerator AnimateMaterialFlight(Image ghost, Vector3 from, Vector3 to)
        {
            const float duration = .28f;
            for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                if (ghost == null)
                    yield break;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = t * t * (3 - 2 * t);
                ghost.rectTransform.position = Vector3.Lerp(from, to, eased) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 32);
                ghost.rectTransform.localScale = Vector3.one * (1 + .18f * Mathf.Sin(t * Mathf.PI));
                yield return null;
            }
            if (ghost != null)
                Destroy(ghost.gameObject);
        }

        // 핵심 분기: Inventory == null 판정.
        // 상태 변경: timer 갱신.
        // 다음 연결: Blacksmith.BlacksmithController.ToggleRecipes() 호출.
        void Update()
        {
            if (Inventory == null)
                return;
            if (Keyboard.current != null)
            {
                if (Keyboard.current.tabKey.wasPressedThisFrame)
                    ToggleRecipes();
                if (Keyboard.current.escapeKey.wasPressedThisFrame)
                    Back();
            }

            if (State != ScreenState.Playing)
                return;
            if (CurrentStation == Station.Workbench)
            {
                timer += Time.unscaledDeltaTime;
                if (guide)
                    guide.rectTransform.localScale = Vector3.one * Mathf.Max(.65f, 2 - timer / 1.4f);
                if (timer > 1.85f)
                    ScoreTiming();
            }

            if (CurrentStation == Station.Quench && gesture && gesture.Holding)
            {
                timer += Time.unscaledDeltaTime;
                float value = Mathf.Clamp01(timer / 3f);
                var r = progressImage.rectTransform;
                r.anchorMin = new Vector2(-.1f, value);
                r.anchorMax = new Vector2(1.1f, value + .025f);
                workIcon.rectTransform.localPosition = new Vector3(0, -value * 65, 0);
                if (value >= 1)
                    FinishQuench();
            }
        }

        public void Persist()
        {
            if (!loadSavedGame && !IsHosted)
                return;
            // Persist a snapshot with uncommitted selection returned. A process exit
            // during selection never destroys the user's ingredients.
            var snapshot = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(Inventory.Data));
            if (!Crafting.Active)
                foreach (var s in Inventory.Selection)
                    InventoryService.Add(snapshot.chest, s);
            if (IsHosted)
                SessionWriter?.Invoke(snapshot);
            else
                BlacksmithSave.Write(snapshot);
        }

        void OnApplicationQuit()
        {
            if (Inventory != null)
            {
                if (Crafting.Active)
                {
                    if (CurrentStation == Station.Furnace)
                        Inventory.Data.fuel = Math.Min(50, Inventory.Data.fuel + Inventory.Selection.Sum(s => s.count) * 3);
                    foreach (var s in Inventory.Selection)
                        InventoryService.Add(Inventory.Data.chest, s);
                    Inventory.Selection.Clear();
                }

                Persist();
            }
        }
    }
}
