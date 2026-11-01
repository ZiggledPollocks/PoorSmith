using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Blacksmith;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Sequences dialogue beats inside the existing town/field. No new gameplay scene.</summary>
public sealed class StoryTutorial : MonoBehaviour
{
    enum Launch { None, New, Continue }
    static Launch pending;
    public static StoryTutorial Instance { get; private set; }
    public static bool Cinematic => Instance != null && Instance.locked;
    // 로고 전에는 돈 HUD만 남기고, 큰 대화·확인창은 불필요한 필드 HUD를 가린다.
    public static bool MoneyHudOnly => Guiding && Instance.inIntro;
    public static bool HideGameplayHud => MoneyHudOnly || Instance != null && Instance.view != null && Instance.view.CoversGameplayHud;
    public static bool Speaking => Instance != null && Instance.view != null && Instance.view.Speaking;
    public static bool Guiding => Instance != null && Instance.isActiveAndEnabled && Instance.Progress?.tutorialCompleted == false;
    public static bool HammerRevealed => Instance?.Progress?.tutorialCompleted == true || Instance?.Progress?.applied?.Contains("ego-hammer-revealed") == true;
    StoryProgress Progress => SmithingLoop.Instance?.Campaign.story;
    SmithingLoop Loop => SmithingLoop.Instance;
    BlacksmithController Shop => FindFirstObjectByType<BlacksmithController>();
    Transform Player => FindFirstObjectByType<InventorySystem>()?.transform;
    StoryTutorialView view;
    Dictionary<string, string[]> dialogue;
    readonly HashSet<string> actions = new();
    readonly HashSet<string> allowedButtons = new();
    readonly Dictionary<string, int> events = new();
    GameUIController.Activity dialogueActivity;
    bool skipQueued;
    Unity.Cinemachine.CinemachineBrain introCameraBrain;
    bool introCameraIgnoreTime; Unity.Cinemachine.CinemachineBrain.UpdateMethods introCameraUpdate;
    Animator introAnimator; float introAnimatorSpeed; AnimatorUpdateMode introAnimatorMode;
    readonly RaycastHit2D[] introHits = new RaycastHit2D[16];
    bool locked, ending, priorExternal, inIntro, skipping;
    Coroutine tutorialRoutine;
    float priorTime, nextFocus;
    string focusName, focusItem, taskId, craftCheckpoint;
    int storyScene = 1;
    GameObject stage, door, restingHammer;

    Vector2 beforeStage;
    Unity.Cinemachine.CinemachineBrain stageBrain;
    bool stageBrainEnabled;
    readonly List<(Behaviour component, bool enabled)> actorComponents = new();
    Rigidbody2D actorBody;
    bool actorSimulated, endingPaid, reminderOwnsLock;
    Transform posedVisual; Vector3 originalVisualScale; Coroutine poseRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { Instance = null; pending = Launch.None; }
    public static void PrepareNewGame() => pending = Launch.New;
    public static void PrepareContinue()
    {
        // A loaded snapshot replaces this persistent sequence before old predicates see new services.
        if (Instance != null)
        {
            var previous = Instance;
            previous.StopAllCoroutines(); previous.Unlock(); previous.ExitStage(); previous.ThawActor();
            previous.enabled = false; Instance = null; Destroy(previous.gameObject);
        }
        pending = Launch.Continue;
    }
    public static void CancelLaunch() => pending = Launch.None;

    // Called after a validated load and before the old basic-equipment migrations.
    public static void ConfigureSession(SmithingLoop loop, InventorySystem bag)
    {
        if (loop.SaveBlocked) { pending = Launch.None; return; }
        StoryProgress.RemovePawnDebt(loop.Campaign);
        if (loop.Campaign.story != null) loop.Campaign.story.applied ??= new List<string>();
        if (pending == Launch.New)
        {
            loop.Campaign.story = new StoryProgress { enabled = true, startingDay = loop.SmithData.day };
            loop.Campaign.toolsLinked = loop.Campaign.starterSwordLinked = true;
            bag.RestoreSnapshot(Array.Empty<InventoryItem>());
            loop.RequestAutosave();
        }
        else if (pending == Launch.Continue)
        {
            // Continue never awards essentials or completes unfinished tutorial steps.
            // Legacy unfinished saves restart the earliest safe segment with once-only grants.
            if(loop.Campaign.story?.enabled==true&&!loop.Campaign.story.tutorialCompleted)
                StoryProgress.PrepareResume(loop.Campaign,loop.SmithData,loop.Catalog);
        }
        pending = Launch.None;
    }
    public static void Attach()
    {
        if (Instance != null || SmithingLoop.Instance?.Campaign.story?.enabled != true) return;
        var go = new GameObject("StoryTutorial", typeof(RectTransform));
        DontDestroyOnLoad(go); Instance = go.AddComponent<StoryTutorial>();
    }
    IEnumerator Start()
    {
        while (CampaignController.Instance?.Ready != true) yield return null;
        var playerIdle = CampaignController.Instance.GetComponent<BlacksmithView>()?.rackIdleSprite;
        if (playerIdle == null) playerIdle = Player.GetComponent<SpriteRenderer>()?.sprite;
        view = gameObject.AddComponent<StoryTutorialView>();
        view.Build(playerIdle, GameTextFonts.Primary);
        dialogue = ReadDialogue(Resources.Load<TextAsset>("Story/Dialogue"));
        view.SkipConfirmed += SkipStory; view.Cue += OnCue;
        view.ReminderFinished += EndReminder;
        if (Progress?.tutorialCompleted == false) tutorialRoutine = StartCoroutine(ResumeTutorial());
        else if (Progress?.endingSeen != true) yield return ParkHammer(false);
    }
    internal static Dictionary<string, string[]> ReadDialogue(TextAsset source)
    {
        if (source == null) throw new InvalidOperationException("Story/Dialogue is missing");
        var result = new Dictionary<string, string[]>();
        string key = null; var lines = new List<string>();
        foreach (string raw in source.text.Replace("\r", "").Split('\n'))
        {
            string line = raw.Trim();
            if (line.StartsWith("# "))
            {
                if (key != null) result.Add(key, lines.ToArray());
                key = line.Substring(2); lines.Clear();
            }
            else if (line.Length > 0 && !line.StartsWith("//")) lines.Add(line);
        }
        if (key != null) result.Add(key, lines.ToArray());
        return result;
    }
    IEnumerator Say(string id)
    {
        int digits=0;while(digits<id.Length&&char.IsDigit(id[digits]))digits++;
        if(digits>0&&int.TryParse(id.Substring(0,digits),out int scene))storyScene=scene;
        if (!dialogue.TryGetValue(id, out var lines)) throw new InvalidOperationException("Unknown story dialogue: " + id);
        Lock(); view.Focus(null);
        // Explanation highlights identify the next control; Lock still rejects its input.
        focusName=id switch { "5Saw"=>"Tool_2", "5Begin"=>"StartCraft", "5Recipe"=>"ScrollRecipe", "5Prepare"=>"Prepare_notion_plank_1", "5Knife"=>"Tool_0", "5Blade"=>"Tool_3", "7Materials"=>"OpenMaterials", _=>null };
        focusItem=null;
        if (id == "4Voice") view.Sound("hum");
        bool small = id.StartsWith("5") && id != "5Start" || id.StartsWith("7") && id != "7Praise" && id != "7Workbench" || id.StartsWith("11") || id.StartsWith("12");
        yield return view.Speak(lines, true, small); Unlock();
    }
    void Lock()
    {
        if (locked) return;
        dialogueActivity=GameUIController.Acquire(this,null,true,true,5000);
        locked = true;
        Player?.GetComponent<PlayerInputHandler>()?.ClearGameplayInput();
    }
    void Unlock()
    {
        if (!locked) return;
        dialogueActivity?.Dispose();dialogueActivity=null;locked = false;
        Player?.GetComponent<PlayerInputHandler>()?.ClearGameplayInput();
    }
    void Task(string id, string goal, string[] allowed, string[] buttons = null, string focus = null, string item = null)
    {
        Unlock(); taskId = id; if (Progress != null) Progress.step = id;
        actions.Clear(); foreach (string action in allowed) actions.Add(action);
        allowedButtons.Clear(); foreach (string name in buttons ?? Array.Empty<string>()) allowedButtons.Add(name);
        focusName = focus; focusItem = item; view.SetObjective(goal); view.SetSkipAvailable(true); RefreshInputTargets(); Loop?.RequestAutosave();
    }
    IEnumerator Until(Func<bool> condition)
    {
        while (Loop == null || !Loop.Initialized || view.Reminding || reminderOwnsLock || !condition()) yield return null;
        view.CloseActionGuide(); focusName = focusItem = null;
    }
    // Invalid input borrows the existing dialogue, without advancing its coroutine or task.
    public static void Reject()
    {
        var self=Instance;
        if(self==null||self.view==null||self.skipping||self.reminderOwnsLock||self.view.Reminding||self.view.ConfirmationOpen)return;
        self.reminderOwnsLock=!self.locked;self.Lock();self.view.Reject();
    }
    void EndReminder()
    {
        if(!reminderOwnsLock)return;
        // Root shutdown cannot host a coroutine; release the borrowed lock synchronously.
        if(!isActiveAndEnabled){reminderOwnsLock=false;Unlock();return;}
        StartCoroutine(ReleaseReminderInput());
    }
    IEnumerator ReleaseReminderInput()
    {
        // Consume the restoration frame only; an automatic reminder must not wait for a held Space.
        yield return null;
        while(view.ConfirmationOpen&&reminderOwnsLock)yield return null;
        if(!reminderOwnsLock)yield break; // Skip already released this borrowed lock.
        Player?.GetComponent<PlayerInputHandler>()?.ClearGameplayInput();if(!skipping)Unlock();reminderOwnsLock=false;
    }
    // Permission queries are side-effect free: hover/drag previews never borrow the reminder lock.
    public static bool CanUseAction(string action)
    {
        if (!Guiding && !Cinematic) return true;
        bool optionalPickaxe = Instance.taskId == "rack" && action != null &&
            action.StartsWith("equip:", StringComparison.Ordinal) &&
            Instance.Loop?.Catalog?.Item(action.Substring(6))?.equipmentSlot == "Pickaxe";
        return !Cinematic && (Instance.actions.Contains(action) || optionalPickaxe ||
            action == "craft-gesture" && Instance.taskId?.StartsWith("working-", StringComparison.Ordinal) == true);
    }
    public static bool AllowAction(string action)
    {
        if (CanUseAction(action)) return true;
        Reject(); return false;
    }
    public static bool CanUseButton(string name)
    {
        if (!Guiding && !Cinematic) return true;
        return !Cinematic && (Instance.allowedButtons.Contains(name) ||
            Instance.taskId == "offering" || Instance.taskId == "delivery" || Instance.taskId == "sleep" ||
            (Instance.taskId == "forest" || Instance.taskId == "home") && (name.Contains("Yes") || name.Contains("No")) ||
            Instance.taskId == "rack" && name.StartsWith("Equip_", StringComparison.Ordinal));
    }
    public static bool AllowButton(string name)
    {
        if (CanUseButton(name)) return true;
        Reject(); return false;
    }
    public static bool CanUseSlot(InventorySlotView slot, bool right = false) =>
        !Guiding && !Cinematic || !Cinematic &&
        (Instance.taskId == "offering" || Instance.taskId == "delivery" || Instance.taskId == "sleep" ||
         CanUseAction(right ? slot.TutorialRightAction : slot.TutorialAction));
    public static bool CanUseDrop(ItemDropTarget target, InventorySlotView source)
    {
        if (!Guiding && !Cinematic) return true;
        if (target.TutorialAction != null) return CanUseAction(target.TutorialAction(source));
        var button = target.GetComponent<Button>();
        return button != null ? CanUseButton(button.name) : CanUseSlot(source);
    }
    public static bool IsActionableButton(Button button) => button != null && button.isActiveAndEnabled &&
        button.IsInteractable() && (button.GetComponent<ItemDropTarget>()?.ClickAvailable?.Invoke() ?? true);
    public static bool AllowInteract(IInteractable target)
    {
        if (!Guiding) return true;
        string action = target switch
        {
            CampaignWorldObject world => "world:" + world.kind,
            SmithyStation station => "station:" + station.kind,
            StoryTutorialDoor _ => "door",
            FieldSceneReturnGate _ => "return",
            FieldCavePassageInteractable _ => "cave",
            StoryForestCaveGate _ => "cave",
            AssissZone _ => "altar",
            _ => "other"
        };
        return AllowAction(action);
    }
    public static void Notify(string action)
    {
        if (Instance == null) return;
        Instance.events[action] = Instance.CountEvent(action) + 1;
        // Crafting calls Notify after committing costs/output, before Persist takes its snapshot.
        if(action.StartsWith("craft:",StringComparison.Ordinal)&&Instance.craftCheckpoint!=null)
        {Instance.Mark(Instance.craftCheckpoint);Instance.craftCheckpoint=null;}
        if(action=="work-started"&&Instance.taskId?.StartsWith("craft-",StringComparison.Ordinal)==true)
            Instance.view.CloseActionGuide();
    }
    int CountEvent(string id) => events.TryGetValue(id, out int n) ? n : 0;
    static string[] A(params string[] values) => values;
    int ChestCount(string id) => Shop != null && Loop?.InShop == true
        ? Shop.Inventory.Data.chest.Where(s => s.itemId == id).Sum(s => s.count)
        : Loop.SmithData.chest.Where(s => s.itemId == id).Sum(s => s.count);
    int BagCount(string id)
    {
        var bag = Player?.GetComponent<InventorySystem>();
        var item = Loop?.FieldItemFor(new Blacksmith.Stack(id, 1));
        return bag != null && item != null ? bag.GetItemCount(item) : 0;
    }
    void Grant(string id, int count)
    {
        if (Loop.Catalog.Item(id) == null) throw new InvalidOperationException("Missing story item: " + id);
        var data = Loop.InShop && Shop != null ? Shop.Inventory.Data : Loop.SmithData;
        InventoryService.Add(data.chest, new Blacksmith.Stack(id, count));
        if (!data.acquiredItems.Contains(id)) data.acquiredItems.Add(id);
        if (Loop.InShop && Shop != null)
        {
            Shop.Persist();
            Shop.RefreshDayNightDisplay();
        }
        Loop.RequestAutosave();
    }
    bool Done(string key)=>Progress.applied.Contains("checkpoint:"+key);
    void Mark(string key)
    {if(!Done(key)){Progress.applied.Add("checkpoint:"+key);Loop.RequestAutosave();}}
    void Checkpoint(int next)
    {Progress.safeScene=Math.Max(Progress.safeScene,next);Loop.RequestAutosave();}
    // Scripted navigation must not invoke user buttons guarded by the current tutorial lock.
    void OpenTutorialToolRack()
    {
        if(Shop==null)return;
        if(Shop.CurrentStation==Station.Tools&&Shop.State==ScreenState.Station)return;
        Shop.OpenTutorialTools();
    }
    // Resume presentation/navigation only. Unlike skip repair, this never grants or equips items.
    IEnumerator ResumeTutorial()
    {
        int next=Mathf.Clamp(Progress.safeScene,1,13);
        if(next<=1){yield return RunTutorial();yield break;}
        if(next>=5&&next<=10||next==13)
        {
            if(CampaignController.Instance==null)
            {actions.Add("travel:town");FieldSceneTravel.BeginToTown();while(FieldSceneTravel.Busy||CampaignController.Instance?.Ready!=true)yield return null;}
            CampaignController.Instance.Teleport(SmithyInterior.Instance.entry.position);
            if(next<=7)
            {
                yield return AutoOpenWorkshop();Shop.SetState(ScreenState.Workshop);
                OpenTutorialToolRack();
                if(next==6)
                {
                    Shop.OpenTutorialTools(ToolKind.Whetstone);
                    Task("blade","나무 판자를 숫돌에 넣어보자.",A("select:plank"),item:"plank");
                }
            }
        }
        if(next==12&&!Done("home")&&SceneManager.GetActiveScene().path!=FieldSceneTravel.FieldScenePath)
        {actions.Add("travel:field");FieldSceneTravel.BeginToField();while(FieldSceneTravel.Busy||FindFirstObjectByType<FieldSceneState>()?.Ready!=true)yield return null;}
        if(next==12&&Done("home")&&CampaignController.Instance==null)
        {actions.Add("travel:town");FieldSceneTravel.BeginToTown();while(FieldSceneTravel.Busy||CampaignController.Instance?.Ready!=true)yield return null;}
        yield return RunTutorial(true,next);
    }
    IEnumerator RunTutorial(bool skipIntro = false, int fromScene = 4)
    {
        try
        {
            if (!skipIntro) { inIntro = true; yield return Intro(); inIntro = false; Checkpoint(4); }
            if(fromScene <= 4)
            {
                storyScene=4;
            Task("enter-smithy", "대장간에 들어가보자. [F]", A("world:Smithy"));
            yield return Say("4Entrance");
            yield return Until(() => SmithyInterior.Instance?.Inside == true && !CampaignController.Instance.Travelling);
            yield return Say("4Room");
            yield return MoveActor(SmithyInterior.Instance.entry.position + Vector3.right * 2, true);
            yield return Say("4Look");
            var craftingDoor = FindObjectsByType<SmithyStation>(FindObjectsSortMode.None).FirstOrDefault(s => s.kind == SmithyStationKind.CraftingDoor);
            if (craftingDoor != null) yield return MoveActor(craftingDoor.transform.position, true);
            yield return AutoOpenWorkshop();
            yield return Say("4Voice");
            yield return AutoCloseWorkshop();
            yield return Say("4Return");
            yield return AutoOpenWorkshop();
            yield return Say("4Meet");
            Task("tools", "도구 걸이를 클릭하여 에고 망치를 따라가자.", A(), A("Facility_ToolRack"));
            yield return Until(() => Shop?.State == ScreenState.Station && Shop.CurrentStation == Station.Tools);
            }
            if(fromScene <= 5) Checkpoint(5);
            if(fromScene <= 5)
            {
                storyScene=5;
            if(!Done("first-plank"))
            {
            OpenTutorialToolRack();
            yield return Say("5Start"); yield return Say("5Saw");
            Task("saw", "톱을 선택해보자.", A(), A("Tool_2"), "Tool_2");
            yield return Until(() => Shop?.State == ScreenState.Selecting && Shop.CurrentTool == ToolKind.Saw);
            yield return Say("5Material"); GrantOnce("starting-wood", "wood", 5);
            Task("wood", "나무 원목을 하나 선택해보자.", A("select:wood"), item: "wood");
            yield return Until(() => Shop.Inventory.Selection.Any(s => s.itemId == "wood"));
            yield return Say("5Begin");
            yield return Craft("plank", "나무 판자를 만들어보자.", checkpoint:"first-plank");
            }
            if(!Done("second-plank"))
            {
            yield return Say("5Recipe");
            Task("recipe", "양피지를 눌러 기록한 레시피를 확인하자.", A("recipes"), A("Recipes", "ScrollRecipe"), "ScrollRecipe");
            yield return Until(() => Shop.State == ScreenState.Recipes);
            yield return Say("5Record");
            Task("recipe-node", "나무 판자 레시피를 선택하자.", A(), A("RecipeVertex_plank"), "RecipeVertex_plank");
            yield return Until(() => FindObjectsByType<Button>(FindObjectsSortMode.None).Any(b => b.name == "Prepare_notion_plank_1"));
            yield return Say("5Prepare");
            Task("recipe-fill", "재료 배치를 눌러 다시 제작해보자.", A("fill:notion_plank_1"), A("Prepare_notion_plank_1"), "Prepare_notion_plank_1");
            yield return Until(() => Shop.State == ScreenState.Selecting && Shop.Inventory.Selection.Count > 0);
            yield return Craft("plank", "시작하기를 눌러 판자를 만들자.", checkpoint:"second-plank");
            }
            if(!Done("handle"))
            {
            yield return Say("5Knife");
            Task("knife", "칼을 선택해 나무 판자를 깎아 나무 자루를 만들자.", A("select:plank"), A("Tool_0", "OpenMaterials", "StationObject"), "Tool_0");
            yield return Until(() => Shop.State == ScreenState.Selecting && Shop.CurrentTool == ToolKind.Knife);
            focusName = null; focusItem = "plank";
            yield return Until(() => Shop.Inventory.Selection.Any(s => s.itemId == "plank"));
            yield return Craft("handle", "칼로 나무 자루를 만들어보자.", checkpoint:"handle");
            }
            yield return Say("5Blade");
            Task("blade", "나무 판자를 숫돌에 갈아 긴 나무 칼날을 만들자.", A("select:plank"), A("Tool_3", "OpenMaterials", "StationObject"), "Tool_3");
            yield return Until(() => Shop.State == ScreenState.Selecting && Shop.CurrentTool == ToolKind.Whetstone);
            }
            if(fromScene <= 6) Checkpoint(6);
            if(fromScene <= 6)
            {
                storyScene=6;
            if(!Done("blade"))
            {
            if (ChestCount("plank") == 0 && ChestCount("wood") == 0 && ChestCount("wood_long_blade") == 0)
            {
                // Failure fallback specified by scene 6-2, without changing normal craft success rules.
                yield return Say("6Failure"); GrantOnce("blade-fallback", "wood_long_blade", 1);
                yield return NavigateBackToStation();
            }
            else
            {
                focusName = null; focusItem = "plank";
                yield return Until(() => Shop.Inventory.Selection.Any(s => s.itemId == "plank"));
                yield return Craft("wood_long_blade", "긴 나무 칼날을 만들자.", true, "blade");
                yield return Say(ChestCount("wood_long_blade") > 0 ? "6Success" : "6Failure");
                if (ChestCount("wood_long_blade") == 0 && ChestCount("plank") == 0 && ChestCount("wood") == 0) GrantOnce("blade-fallback", "wood_long_blade", 1);
            }
            }
            Mark("blade");
            }
            if(fromScene <= 7) Checkpoint(7);
            if(fromScene <= 7)
            {
                storyScene=7;
            if(!Done("wood-sword"))
            {
            yield return Say("7Workbench");
            Task("workbench", "왼쪽 탭으로 작업대로 이동하자.", A(), A("PreviousStation"), "PreviousStation");
            yield return Until(() => Shop.CurrentStation == Station.Workbench);
            yield return Say("7Materials");
            Task("assembly-open", "책상 커버 또는 재료 선택을 누르자.", A(), A("StationObject", "OpenMaterials"), "OpenMaterials");
            yield return Until(() => Shop.State == ScreenState.Selecting);
            yield return Say("7Select");
            Task("assembly", "긴 나무 칼날과 나무 자루를 하나씩 선택하자.", A("select:wood_long_blade", "select:handle"));
            yield return Until(() => Shop.Inventory.Selection.Any(s => s.itemId == "wood_long_blade") && Shop.Inventory.Selection.Any(s => s.itemId == "handle"));
            yield return Craft("wood_sword", "첫 나무 검을 완성하자.", checkpoint:"wood-sword");
            }
            yield return AutoCloseWorkshop(); yield return Say("7Praise"); view.Sound("knock");
            CreateDoor();
            int opened = CountEvent("door");
            Task("door", "입구에서 문 열기 [F] · 누가 찾아왔는지 확인하자.", A("door"));
            yield return Until(() => CountEvent("door") > opened);
            }
            if(fromScene <= 8) Checkpoint(8);
            if(fromScene <= 8)
            {
                storyScene=8;
            view.Sound("door"); yield return view.Visitor(true); yield return Say("8Collector");
            // Only carried ordinary materials are recovered; storage, equipment and required items remain.
            RecoverCarriedMaterials();
            yield return view.Visitor(false); view.Sound("door");
            }
            if(fromScene <= 9) Checkpoint(9);
            if(fromScene <= 9)
            {
                storyScene=9;
            GrantTutorialGatheringTools(); yield return Say("9Equipment");
            Task("rack", "장비 거치대에서 나무 검과 낡은 도끼를 장착하자.", A("station:Rack", "equip:wood_sword", "equip:town_axe_1"), A("Back"));
            yield return Until(() => (Loop.InShop ? Shop.Inventory.Data : Loop.SmithData).equipment.Any(e => e.stack.itemId == "wood_sword") &&
                (Loop.InShop ? Shop.Inventory.Data : Loop.SmithData).equipment.Any(e => e.stack.itemId == "town_axe_1"));
            }
            if(fromScene <= 10) Checkpoint(10);
            if(fromScene <= 10)
            {
                storyScene=10;
            yield return AutoCloseWorkshop(); yield return Say("10Leave");
            yield return view.Fade(0, 1); CampaignController.Instance.Teleport(CampaignController.Instance.townScene.blackSmith.position + Vector3.up * 2);
            yield return view.Fade(1, 0);
            }
            if(fromScene <= 11) Checkpoint(11);
            if(fromScene <= 11)
            {
                storyScene=11;
            yield return Say("11Forest");
            Task("forest", "숲으로 가자. 경계에서 [F]", A("world:ForestGate", "travel:field"));
            yield return Until(() => !FieldSceneTravel.Busy && SceneManager.GetActiveScene().path == FieldSceneTravel.FieldScenePath && FindFirstObjectByType<FieldSceneState>()?.Ready == true);
            }
            if(fromScene <= 12) Checkpoint(12);
            if(fromScene <= 12)
            {
                storyScene=12;
            if(!Done("tree"))
            {
            int felled = CountEvent("tree"); yield return Say("12Tree");
            Task("tree", "나무 하나를 베어보자. 도구는 휠로 바꿀 수 있다.", A("gather"));
            yield return Until(() => CountEvent("tree") > felled); Mark("tree");
            }
            if(!Done("gather"))
            {
            yield return Say("12Gather");
            Task("gather", "나무 원목 15개와 가죽 2개를 구하자.", A("gather"));
            yield return Until(() => BagCount("wood") >= 15 && BagCount("raw_leather") >= 2);
            yield return view.CompleteObjective(); Mark("gather");
            }
            if(!Done("offering"))
            {
            yield return Say("12Altar");
            Task("altar", "숲의 석상에 다가가 [F]로 상호작용하자.", A("altar", "offering-ui"));
            yield return Until(() => Player.GetComponent<AssimilationOfferingUIController>()?.IsOpen == true);
            yield return Say("12Offering");
            int offered = CountEvent("offering");
            // A zero gauge needs no offering; closing acknowledges the lesson without consuming or faking an offering event.
            Task("offering", AssimilationAlreadyClear()
                ? "동화율이 이미 0이다. 재료를 바칠 필요 없이 [←]로 닫고 돌아가자."
                : "재료를 골라 석상에 제물을 바치자.", A("offering-ui", "offer"));
            yield return Until(() => (CountEvent("offering") > offered || AssimilationAlreadyClear()) &&
                Player.GetComponent<AssimilationOfferingUIController>()?.IsOpen != true); Mark("offering");
            }
            if(!Done("home"))
            {
            yield return Say("12Home");
            Task("home", "마을로 돌아가자. 동굴에는 다음에 가자.", A("return", "travel:town"));
            yield return Until(() => !FieldSceneTravel.Busy && CampaignController.Instance?.Ready == true && SceneManager.GetActiveScene().path == FieldSceneTravel.TownScenePath);
            Mark("home");
            }
            Loop.BeginNight();
            if(!Done("delivery"))
            {
            Task("delivery-entrance", "대장간 앞에서 [F]로 에고 망치의 말을 듣자.", A("world:Smithy"));
            int approach = CountEvent("delivery-entrance");
            yield return Until(() => CountEvent("delivery-entrance") > approach);
            int branches = BagCount("branch"); int target = Progress.deliveryTarget > 0 ? Progress.deliveryTarget : Mathf.Max(1, branches / 2);
            Progress.deliveryTarget = target; Loop.RequestAutosave();
            // A tree's random drops can leave no branches. One explicit tutorial branch avoids a dead end.
            if (branches == 0 && !Progress.applied.Contains("delivery-branch"))
            {
                var branch = Loop.FieldItemFor(new Blacksmith.Stack("branch", 1));
                if (!Player.GetComponent<InventorySystem>().TryAddItem(branch, 1))
                { InventoryService.Add(Loop.SmithData.bag, new Blacksmith.Stack("branch", 1)); }
                Progress.applied.Add("delivery-branch"); Loop.RequestAutosave();
            }
            yield return Say("12Delivery");
            Task("delivery", $"납품 상자에 나뭇가지 {target}개를 넣고 창을 닫자.", A("world:Delivery", "ui:납품 상자"));
            yield return Until(() => Loop.Campaign.delivery.Where(s => s.itemId == "branch").Sum(s => s.count) >= target && CampaignController.Instance.UI.IsOpen == false);
            Mark("delivery");
            }
            yield return Say("12DeliveryDone");
            Task("home-smithy", "대장간으로 들어가자. [F]", A("world:Smithy"));
            yield return Until(() => SmithyInterior.Instance?.Inside == true && !CampaignController.Instance.Travelling);
            }
            if(fromScene <= 13) Checkpoint(13);
            if(fromScene <= 13)
            {
                storyScene=13;
            yield return Say("13Sleep");
            yield return ParkHammer(true);
            int day = Loop.SmithData.day;
            Task("sleep", "침대에서 잠을 자자. [F]", A("station:Bed"), A("SleepYes", "Back"));
            yield return Until(() => Loop.SmithData.day > day && !Loop.InShop);
            Progress.tutorialCompleted = true; Progress.safeScene=14; Progress.step = "complete";
            view.SetObjective(""); view.SetSkipAvailable(false); actions.Clear(); allowedButtons.Clear(); ClearBlockers();
            Loop.RefreshEquipment(); Loop.RequestAutosave();
            }
        }
        finally
        {
            Unlock(); ExitStage();
            if (door != null) Destroy(door);
        }
    }
    IEnumerator Craft(string output, string goal, bool bladeFallback = false, string checkpoint = null)
    {
        craftCheckpoint=checkpoint;
        int crafted = CountEvent("craft:" + output);
        while (CountEvent("craft:" + output) == crafted)
        {
            int started=CountEvent("work-started");
            Task("craft-" + output, goal, A(), A("StartCraft"), "StartCraft");
            yield return Until(() => CountEvent("work-started") > started);
            Task("working-" + output, "화면의 조작 안내에 따라 가공한 뒤 완료하세요.", A(), A("FinishTool", "TimingTarget"));
            yield return Until(() => Shop.State == ScreenState.Result);
            Task("collect-" + output, "확인을 눌러 결과를 받아보자.", A(), A("Collect"), "Collect");
            yield return Until(() => Shop.State != ScreenState.Result);
            if (CountEvent("craft:" + output) > crafted) break;
            if (bladeFallback && ChestCount("plank") == 0 && ChestCount("wood") == 0) break;
            yield return view.Speak(new[] { "에고 망치: 실패했네. 재료는 남아 있으니 다시 해보자." });
            string ingredient = output == "plank" ? "wood" : output == "wood_sword" ? "wood_long_blade" : "plank";
            Task("retry", "같은 재료를 선택하고 다시 제작하자.", A("select:" + ingredient, "select:handle"),
                A("Tool_2", "Tool_0", "Tool_3", "StationObject", "OpenMaterials"));
            yield return Until(() => Shop.Crafting.CanBegin(Shop.CurrentStation, Shop.CurrentTool, out _));
        }
    }
    IEnumerator NavigateBackToStation()
    {
        Task("station-back", "도구 걸이로 돌아가자.", A(), A("Back", "CloseSelection"), "Back");
        yield return Until(() => Shop.State == ScreenState.Station);
    }
    IEnumerator AutoOpenWorkshop()
    {
        // Temporarily permit only this scripted call, while the world remains paused by the shop owner.
        actions.Add("station:CraftingDoor"); actions.Add("panel:Workshop");
        GameUIController.ExternalActivity = false;
        // Closing a modal blocks its release frame; wait before the single open request.
        while(GameUIController.BlocksGameplayInput)yield return null;
        Loop.OpenInteriorPanel(ScreenState.Workshop);
        yield return Until(() => Loop.InShop && !Loop.Transitioning);
        view.Sound("door");
    }
    IEnumerator AutoCloseWorkshop()
    {
        if (!Loop.InShop) yield break;
        if (Shop.State == ScreenState.Result) yield return Until(() => Shop.State != ScreenState.Result);
        // Revoke the finished station task before unloading its UI. Otherwise a repeated
        // interaction can reopen it between LeaveShop completion and the next dialogue.
        actions.Clear(); allowedButtons.Clear(); focusName = focusItem = null;taskId="";view.SetObjective("");
        actions.Add("shop-leave"); Loop.Travel();
        yield return Until(() => !Loop.InShop && !Loop.Transitioning);
    }
    IEnumerator Intro()
    {
        // Story scenes share SampleScene; its authored forest is entered after the fall.
        Lock(); var c = CampaignController.Instance;
        view.SetIntroBackdrop(true);
        c.Teleport(c.townScene.pawnshop.position + Vector3.right * 2 + Vector3.up * 2);
        yield return view.Speak(dialogue["1Hesitate"]);
        view.ShowPurchasePreview(Loop.Content.ArtForItem(Loop.Catalog.Item("sword")));
        yield return view.Speak(dialogue["1Purchase"]);
        bool bought = false;
        view.Purchase(Loop.Content.ArtForItem(Loop.Catalog.Item("sword")), CampaignController.Instance.Economy.GoldText+" G", () => bought = true);
        while (!bought) yield return null;
        view.Sound("money"); Loop.Campaign.gold = 0; Loop.Campaign.goldMilliRemainder=0;
        view.ShowPurchasePreview(Loop.Content.ArtForItem(Loop.Catalog.Item("sword")));
        yield return view.Speak(dialogue["1Departure"]);
        yield return view.LeavePurchasePresentation();
        yield return WalkIntroActor(c.townSpawn + Vector2.left * 3);
        yield return SlipActor(); view.ShowItem(null, false);
        // Scene1 fades total .75 + .75 seconds; SlipActor already completed the blackout.
        storyScene=2;EnterStage(); yield return PoseActor(.7f); yield return view.Blink();
        yield return view.Speak(dialogue["2"]);
        yield return view.Fade(0, 1); view.Sound("wood"); yield return view.Fade(1, 0);
        storyScene=3;view.ShowCraftedSword(Loop.Content.ArtForItem(Loop.Catalog.Item("wood_sword")));
        yield return view.Speak(dialogue["3Before"]);
        yield return view.Passer(Loop.Content.ArtForItem(Loop.Catalog.Item("wood_sword")));
        if (!Progress.applied.Contains("intro-coin")) { Loop.Campaign.gold = 1; Progress.applied.Add("intro-coin"); }
        yield return view.Speak(dialogue["3After"]);
        yield return view.Logo(); yield return view.Fade(0, 1);
        ExitStage(); c.Teleport(c.townScene.blackSmith.position + Vector3.up * 2);
        yield return view.Fade(1, 0); Unlock();
    }
    // CampaignController calls this before entering the smithy: the return tutorial
    // must deliver its outside dialogue before enabling the delivery box.
    public static bool HandleSmithyApproach()
    {
        if (!Guiding || Instance.taskId != "delivery-entrance") return false;
        Notify("delivery-entrance"); return true;
    }
    bool AssimilationAlreadyClear() => Player != null &&
        Player.TryGetComponent<PlayerAssimilate>(out var assimilation) && assimilation.CurrentAssimilation <= 0f;

    // Both the offering back button and GameUIController/Esc use this gate; the explanation must finish first.
    public static bool CanCloseOffering => !Guiding || Instance.skipping ||
        Instance.taskId != "altar" && Instance.taskId != "offering" || Instance.CountEvent("offering") > 0 ||
        Instance.taskId == "offering" && Instance.AssimilationAlreadyClear();
    // Keep the Scene1 departure timing and blackout without rotating the character sprite.
    IEnumerator SlipActor()
    {
        FreezeActor();
        for(float t=0;t<.45f;t+=Time.unscaledDeltaTime*view.Speed)yield return null;
        yield return view.Fade(0,1,.75f); ThawActor();
    }
    // Only Scene1 uses shape-aware, unscaled cinematic walking. Other movement retains its owner.
    IEnumerator WalkIntroActor(Vector2 target)
    {
        var player=Player; var movement=player!=null?player.GetComponent<PlayerMovement>():null;
        var shape=player!=null?player.GetComponent<Collider2D>():null;
        if(player==null||movement==null||shape==null)yield break;
        var settings=FindObjectsByType<StoryForestStage>(FindObjectsInactive.Include,FindObjectsSortMode.None).FirstOrDefault(s=>s.gameObject.scene==player.gameObject.scene);
        float introWalkMultiplier=settings!=null?Mathf.Clamp(settings.introWalkMultiplier,.1f,4f):3.6f;
        float introWalkTimeout=settings!=null?Mathf.Max(1,settings.introWalkTimeout):30f;
        // Keep the authored Cinemachine target, offset and confiner; only tick it during the paused walk.
        introCameraBrain=Camera.main!=null?Camera.main.GetComponent<Unity.Cinemachine.CinemachineBrain>():null;
        if(introCameraBrain!=null){introCameraIgnoreTime=introCameraBrain.IgnoreTimeScale;introCameraUpdate=introCameraBrain.UpdateMethod;
            introCameraBrain.IgnoreTimeScale=true;introCameraBrain.UpdateMethod=Unity.Cinemachine.CinemachineBrain.UpdateMethods.ManualUpdate;}
        FreezeActor(); Player.GetComponent<PlayerInputHandler>()?.ClearGameplayInput();
        introAnimator=player.GetComponent<Animator>();
        if(introAnimator!=null){introAnimatorSpeed=introAnimator.speed;introAnimatorMode=introAnimator.updateMode;
            introAnimator.enabled=true;introAnimator.updateMode=AnimatorUpdateMode.UnscaledTime;introAnimator.Play("Walk",0,0);}
        var filter=new ContactFilter2D();filter.SetLayerMask(movement.GroundLayers);filter.useTriggers=false;
        float elapsed=0;var renderers=player.GetComponentsInChildren<SpriteRenderer>();
        try
        {
            while(Mathf.Abs(player.position.x-target.x)>.03f&&elapsed<introWalkTimeout)
            {
                if(view.Speed<=0){if(introAnimator!=null)introAnimator.speed=0;yield return null;continue;}
                elapsed+=Time.unscaledDeltaTime;
                float direction=Mathf.Sign(target.x-player.position.x);
                float speed=movement.AuthoredWalkSpeed*introWalkMultiplier*view.Speed;
                foreach(var renderer in renderers)renderer.flipX=direction<0;
                if(introAnimator!=null)introAnimator.speed=introAnimatorSpeed*introWalkMultiplier*view.Speed;
                float dx=direction*Mathf.Min(Mathf.Abs(target.x-player.position.x),speed*Time.unscaledDeltaTime);
                Bounds bounds=shape.bounds;bool blocked=false;
                int count=Physics2D.BoxCast(bounds.center,(Vector2)bounds.size*.94f,0,Vector2.right*direction,filter,introHits,Mathf.Abs(dx)+.02f);
                for(int i=0;i<count;i++)if(introHits[i].collider!=null&&!introHits[i].transform.IsChildOf(player)&&introHits[i].normal.x*direction<-.2f){blocked=true;break;}
                float x=player.position.x+dx;float? floor=null;
                count=Physics2D.Raycast(new Vector2(x,bounds.min.y+.35f),Vector2.down,filter,introHits,.8f);
                for(int i=0;i<count;i++)if(introHits[i].collider!=null&&!introHits[i].transform.IsChildOf(player)&&introHits[i].normal.y>=movement.MinimumGroundNormalY){floor=introHits[i].point.y;break;}
                if(blocked||!floor.HasValue)break; // Stop safely at the last valid floor, never through a wall.
                Vector2 position=new(x,player.position.y+floor.Value-bounds.min.y+.015f);
                player.position=new Vector3(position.x,position.y,player.position.z);
                if(actorBody!=null)actorBody.position=position;
                Physics2D.SyncTransforms();introCameraBrain?.ManualUpdate();yield return null;
            }
        }
        finally{RestoreIntroAnimation();ThawActor();Player?.GetComponent<PlayerInputHandler>()?.ClearGameplayInput();}
    }
    void RestoreIntroAnimation()
    {
        if(introCameraBrain!=null){introCameraBrain.IgnoreTimeScale=introCameraIgnoreTime;introCameraBrain.UpdateMethod=introCameraUpdate;introCameraBrain=null;}
        if(introAnimator==null)return;
        introAnimator.Play("Idle",0,0);introAnimator.speed=introAnimatorSpeed;introAnimator.updateMode=introAnimatorMode;introAnimator=null;
    }
    IEnumerator MoveActor(Vector2 target, bool horizontalOnly=false)
    {
        var player = Player; if (player == null) yield break;
        FreezeActor(); Vector2 from = player.position;
        if(horizontalOnly)target.y=from.y;
        for (float t = 0; t < 2f; t += Time.unscaledDeltaTime * view.Speed)
        {
            Vector2 pos = Vector2.Lerp(from, target, Mathf.SmoothStep(0, 1, t / 2f));
            player.position = new(pos.x, pos.y, player.position.z);
            if (actorBody != null) actorBody.position = pos;
            yield return null;
        }
        if(actorBody!=null){actorBody.position=target;actorBody.linearVelocity=Vector2.zero;}
        player.position=new Vector3(target.x,target.y,player.position.z);Physics2D.SyncTransforms();
        ThawActor(); CampaignController.Instance.Teleport(target);
    }
    void FreezeActor()
    {
        var player = Player; if (player == null || actorComponents.Count > 0) return;
        foreach (var b in player.GetComponents<Behaviour>())
            if (b is PlayerMovement || b is PlayerAnimationController || b is Animator || b is PlayerInteraction)
            { actorComponents.Add((b, b.enabled)); b.enabled = false; }
        actorBody = player.GetComponent<Rigidbody2D>();
        if (actorBody != null) { actorSimulated = actorBody.simulated; actorBody.linearVelocity = Vector2.zero; actorBody.simulated = false; }
    }
    void OnCue(string cue)
    {
        if(cue=="reveal")
        {
            if(!Progress.applied.Contains("ego-hammer-revealed")){Progress.applied.Add("ego-hammer-revealed");Loop.RequestAutosave();}
            // 공개 이후 월드에 놓이는 망치도 전용 에고 스프라이트를 사용한다.
            StartCoroutine(ParkHammer(false));
        }
        else if (cue == "collector-sword") view.HoldVisitorItem(Loop.Content.ArtForItem(Loop.Catalog.Item("wood_sword")));
        else if (cue == "collector-return") view.HoldVisitorItem(null);
        else if (cue == "charge" && endingPaid && !Progress.applied.Contains("ending-charge"))
        {
            Loop.Campaign.gold -= StoryProgress.DebtGold; Progress.applied.Add("ending-charge"); view.Sound("money");
        }
        else if (stage != null && (cue == "stand" || cue == "sit"))
        {
            if (poseRoutine != null) StopCoroutine(poseRoutine);
            poseRoutine = StartCoroutine(PoseActor(cue == "sit" ? .7f : 1f));
        }
    }
    // No authored sitting animation exists; a temporary idle-sprite pose is isolated to this cinematic.
    IEnumerator PoseActor(float height)
    {
        if (posedVisual == null)
        {
            posedVisual = Player.GetComponentInChildren<SpriteRenderer>().transform;
            originalVisualScale = posedVisual.localScale;
        }
        var renderer=posedVisual.GetComponent<SpriteRenderer>();
        float feet=renderer!=null?renderer.bounds.min.y:posedVisual.position.y;
        Vector3 from = posedVisual.localScale;
        Vector3 to = new Vector3(originalVisualScale.x, originalVisualScale.y * height, originalVisualScale.z);
        for (float t=0;t<.4f;t+=Time.unscaledDeltaTime*view.Speed)
        { posedVisual.localScale=Vector3.Lerp(from,to,Mathf.SmoothStep(0,1,t/.4f));
          if(renderer!=null)posedVisual.position+=Vector3.up*(feet-renderer.bounds.min.y);
          if(actorBody!=null)actorBody.position=Player.position;Physics2D.SyncTransforms();yield return null; }
        posedVisual.localScale=to;if(renderer!=null)posedVisual.position+=Vector3.up*(feet-renderer.bounds.min.y);poseRoutine=null;
    }
    void RestoreActorPose()
    {
        if(poseRoutine!=null)StopCoroutine(poseRoutine);poseRoutine=null;
        if(posedVisual!=null){var r=posedVisual.GetComponent<SpriteRenderer>();float feet=r!=null?r.bounds.min.y:posedVisual.position.y;
            posedVisual.localScale=originalVisualScale;if(r!=null)posedVisual.position+=Vector3.up*(feet-r.bounds.min.y);}posedVisual=null;
    }
    void ThawActor()
    {
        RestoreIntroAnimation();
        RestoreActorPose();
        foreach (var pair in actorComponents) if (pair.component != null) pair.component.enabled = pair.enabled;
        actorComponents.Clear(); if (actorBody != null) actorBody.simulated = actorSimulated; actorBody = null;
    }
    void EnterStage()
    {
        // Authored SampleScene forest is reused; no terrain/player/manager is created at runtime.
        var forest=FindObjectsByType<StoryForestStage>(FindObjectsInactive.Include,FindObjectsSortMode.None)
            .FirstOrDefault(f=>f.gameObject.scene==SceneManager.GetActiveScene());
        if(forest==null)throw new InvalidOperationException("SampleScene story forest is missing");
        beforeStage=Player.position;stage=forest.gameObject;stage.SetActive(true);
        CampaignController.Instance.Teleport(forest.spawn.position);FreezeActor();forest.BeginCamera(Camera.main);
    }
    void ExitStage()
    {
        if(stage==null)return;
        stage.GetComponent<StoryForestStage>()?.EndCamera();ThawActor();stage=null;
        CampaignController.Instance?.Teleport(beforeStage);
    }

    // Use the shared serialized art only after the saved reveal; presentation never grants an item.
    IEnumerator ParkHammer(bool animate)
    {
        if (!HammerRevealed || GameArtSkin.Current == null || GameArtSkin.Current.egoHammer == null) yield break;
        var rack = FindObjectsByType<SmithyStation>(FindObjectsSortMode.None).FirstOrDefault(s => s.kind == SmithyStationKind.Rack);
        if (rack == null) yield break;
        if(restingHammer==null)restingHammer = new GameObject("Story Ego Hammer Resting", typeof(SpriteRenderer));
        SceneManager.MoveGameObjectToScene(restingHammer, rack.gameObject.scene);
        var renderer = restingHammer.GetComponent<SpriteRenderer>(); renderer.sprite = GameArtSkin.Current.egoHammer;
        // 플레이어 몸체의 실제 월드 표시 높이를 기준으로 망치를 절반 크기로 맞춘다.
        var body=Player!=null ? Player.GetComponent<SpriteRenderer>() : null;
        if(body==null && Player!=null)body=Player.GetComponentInChildren<SpriteRenderer>();
        float playerHeight=body!=null ? body.bounds.size.y : 2.22f;
        renderer.sortingOrder = 35;
        restingHammer.transform.localScale = Vector3.one * (Mathf.Max(.01f,playerHeight)*.5f / Mathf.Max(.01f,renderer.sprite.bounds.size.y));
        renderer.color = Color.white;
        Vector3 destination = rack.transform.position + new Vector3(1.2f, .8f, 0);
        Vector3 from = Player != null ? Player.position + Vector3.up : destination;
        for (float t = 0; animate && t < .9f; t += Time.unscaledDeltaTime * view.Speed)
        {
            float p = Mathf.Clamp01(t / .9f);
            restingHammer.transform.position = Vector3.Lerp(from, destination, Mathf.SmoothStep(0, 1, p));
            renderer.color = Color.white; yield return null;
        }
        restingHammer.transform.position = destination; renderer.color = Color.white;
    }
    void CreateDoor()
    {
        var interior = SmithyInterior.Instance;
        door = new GameObject("Tutorial Door [F]", typeof(BoxCollider2D), typeof(StoryTutorialDoor));
        SceneManager.MoveGameObjectToScene(door, SceneManager.GetActiveScene());
        door.layer = LayerMask.NameToLayer("Interactable");
        door.transform.position = interior.exit.position + Vector3.right * 1.5f;
        var box = door.GetComponent<BoxCollider2D>(); box.isTrigger = true; box.size = new Vector2(1.2f, 3);
    }
    public static bool UsesStoryDebt => SmithingLoop.Instance?.Campaign.story?.enabled == true;
    public static bool TryEnding()
    {
        var self = Instance; var loop = SmithingLoop.Instance;
        if (self == null || self.ending || loop == null || loop.InShop || CampaignController.Instance?.Ready != true || CampaignController.Instance.Travelling || CampaignController.Instance.UI.IsOpen ||
            self.Progress?.tutorialCompleted != true || self.Progress.endingSeen ||
            loop.SmithData.night || loop.SmithData.day < self.Progress.startingDay + 7) return false;
        self.ending = true; self.StartCoroutine(self.Ending()); return true;
    }
    IEnumerator Ending()
    {
        view.SetObjective(""); Lock(); view.Sound("knock");
        var economy = CampaignController.Instance.Economy;
        bool paid = economy.GoldMilli >= StoryProgress.DebtGold * 1000L; endingPaid = paid;
        if (SmithyInterior.Instance?.Inside == true) yield return MoveActor(SmithyInterior.Instance.exit.position);
        view.Sound("door"); yield return view.Visitor(true);
        yield return StoryEndingDialogue.Main(view, dialogue, paid);
        yield return view.Visitor(false);
        if (paid) OnCue("charge");
        else { if(Loop.Campaign.gold!=0||Loop.Campaign.goldMilliRemainder!=0)view.Sound("money"); Loop.Campaign.gold = 0; Loop.Campaign.goldMilliRemainder = 0; }
        Progress.endingSeen = true; Unlock(); Loop.RequestAutosave();
        bool saved = Loop.SaveProgress(); Lock();
        while (!saved)
        {
            // Keep the once-only ending result in memory until it can be committed.
            bool retry = false;
            view.Decision("엔딩을 저장하지 못했습니다. 기존 저장본은 유지됩니다. 다시 시도해 주세요.",
                "다시 저장", () => retry = true);
            while (!retry) yield return null;
            Unlock(); saved = Loop.SaveProgress(); Lock();
        }
        yield return view.Fade(0, 1);
        if (!paid)
        {
            CampaignController.Instance.Teleport(CampaignController.Instance.townScene.blackSmith.position + Vector3.up * 2);
            view.ShowClosure(CampaignController.Instance.townScene.blackSmith.position); yield return view.Fade(1, 0);
            yield return StoryEndingDialogue.AfterFailure(view, dialogue);
        }
        for (float t=0;t<1f;t+=Time.unscaledDeltaTime*view.Speed) yield return null;
        // No invented author credits. Replace the project-provided credit text when available.
        StoryEndingChoice choice = StoryEndingChoice.Continue;
        yield return StoryEndingDialogue.Credits(view, selected => choice = selected);
        Unlock();
        if (choice == StoryEndingChoice.MainMenu) SceneManager.LoadScene(TitleSceneController.ScenePath);
        else
        {
            // The settled, saved session stays alive; only the completed story presentation stops.
            view.CancelPresentation(); view.enabled = false; view.GetComponent<Canvas>().enabled = false;
        }
    }
    // A disabled Canvas still has active children; it must not become a tutorial input hole.
    public static bool IsVisibleControl(Component control) => control != null && control.gameObject.activeInHierarchy &&
        control.GetComponentsInParent<Canvas>(true).All(c => c.isActiveAndEnabled);
    void Update()
    {
        string path = SceneManager.GetActiveScene().path;
        if (path == TitleSceneController.ScenePath) { Destroy(gameObject); return; }
        if (view == null || Loop?.Initialized != true) return;
        // Skip owns asynchronous shop unload; do not read its destroyed selection for highlights.
        if (skipping) return;
        if (skipQueued && !SkipCommitBusy()) { skipQueued=false; SkipStory(); return; }
        if (Guiding && !ending) view.SetSkipAvailable(true);
        if (Progress?.tutorialCompleted == true)
        { ClearBlockers(); if (CampaignController.Instance?.Ready == true) TryEnding(); return; }
        if (taskId == "gather") view.SetGatherObjective(Loop.Content.ArtForItem(Loop.Catalog.Item("wood")), Loop.Content.ArtForItem(Loop.Catalog.Item("raw_leather")), BagCount("wood"), BagCount("raw_leather"));
        if (Time.unscaledTime < nextFocus) return;
        RefreshInputTargets();
    }
    public static void RefreshCraftingInput()
    {
        var self=Instance;
        if(self==null||self.view==null||self.Loop?.Initialized!=true||self.skipping||self.Progress?.tutorialCompleted!=false)return;
        self.RefreshInputTargets();
    }
    void RefreshInputTargets()
    {
        if(view==null||Loop?.Initialized!=true||skipping)return;
        nextFocus = Time.unscaledTime + .15f;
        // Release the finished dialogue surface before granting the next action's controls.
        view.SynchronizeInputNavigation();
        RectTransform target = null;
        if (!string.IsNullOrEmpty(focusName))
            target = FindObjectsByType<Button>(FindObjectsSortMode.None).FirstOrDefault(b => b.name == focusName && IsVisibleControl(b))?.transform as RectTransform;
        else if (!string.IsNullOrEmpty(focusItem))
            target = FindObjectsByType<InventorySlotView>(FindObjectsSortMode.None).FirstOrDefault(s => s.Stack?.itemId == focusItem && IsVisibleControl(s))?.transform as RectTransform;
        if (taskId == "assembly")
        {
            var slots = FindObjectsByType<InventorySlotView>(FindObjectsSortMode.None);
            var wanted = new[] { "wood_long_blade", "handle" };
            var pendingTargets = wanted.Where(id => !Shop.Inventory.Selection.Any(s => s.itemId == id))
                .Select(id => slots.FirstOrDefault(v => v.Stack?.itemId == id && IsVisibleControl(v))?.transform as RectTransform).Where(t => t != null).ToArray();
            var destination = FindObjectsByType<ItemDropTarget>(FindObjectsSortMode.None).FirstOrDefault(t => t.name == "Slots" && IsVisibleControl(t))?.transform as RectTransform;
            view.FocusTargets(pendingTargets.Concat(destination == null ? Array.Empty<RectTransform>() : new[] { destination }).ToArray());
        }
        else if (focusItem != null && target != null)
        {
            var destination = FindObjectsByType<ItemDropTarget>(FindObjectsSortMode.None).FirstOrDefault(t => t.name == "Slots" && IsVisibleControl(t))?.transform as RectTransform;
            view.FocusTargets(destination == null ? new[] { target } : new[] { target, destination });
        }
        else view.Focus(target);
        var visibleButtons=FindObjectsByType<Button>(FindObjectsSortMode.None);
        var allowedUi=visibleButtons.Where(b=>IsVisibleControl(b)&&!locked&&IsButtonAllowed(b.name)).Select(b=>b.transform).ToList();
        if(!locked&&target!=null){allowedUi.Add(target);var scroll=target.GetComponentInParent<ScrollRect>();if(scroll!=null)allowedUi.Add(scroll.transform);}
        // Gesture receivers are Images, not Buttons. Keep their existing tutorial permission
        // in the presentation allow-list as well, so active work can receive clicks/drags.
        if(!locked&&CanUseAction("craft-gesture")&&Shop?.view?.overlay!=null)
        {
            var minigame=Shop.view.overlay.Find("Minigame");
            if(minigame!=null)
                allowedUi.AddRange(minigame.GetComponentsInChildren<CraftGestureInput>(false)
                    .Where(g=>IsVisibleControl(g)).Select(g=>g.transform));
        }
        view.AllowPresentationTargets(allowedUi);
        // A transparent child captures disallowed pointer clicks without replacing authored button listeners.
        foreach (var b in visibleButtons)
        {
            if (b.transform.IsChildOf(transform) || !IsVisibleControl(b)) continue;
            bool permit = !locked && IsButtonAllowed(b.name);
            var blocker = b.transform.Find("StoryInputBlocker");
            if (!permit && blocker == null)
            {
                var go = new GameObject("StoryInputBlocker", typeof(RectTransform), typeof(Image), typeof(StoryBlockedControl), typeof(LayoutElement));
                go.transform.SetParent(b.transform, false); RuntimeUIFactory.StretchToParent((RectTransform)go.transform);
                go.GetComponent<Image>().color = Color.clear; go.GetComponent<LayoutElement>().ignoreLayout = true; blocker = go.transform;
            }
            if (blocker != null) blocker.gameObject.SetActive(!permit);
        }
    }
    bool IsButtonAllowed(string name) => allowedButtons.Contains(name) || taskId == "offering" || taskId == "delivery" || taskId == "sleep" ||
        taskId == "rack" && (name.StartsWith("Equip_", StringComparison.Ordinal) || name == "Back") ||
        (taskId == "forest" || taskId == "home") && (name.Contains("Yes") || name.Contains("No"));
    // Normal Scene9 and confirmed skips share persistent once-only grant keys.
    void GrantTutorialGatheringTools()
    {
        GrantOnce("starting-axe","town_axe_1",1);
        GrantOnce("starting-pickaxe","town_pick_1",1);
    }
    void GrantOnce(string key, string id, int count)
    {
        if (Progress.applied.Contains(key)) return;
        Grant(id, count); Progress.applied.Add(key); Loop.RequestAutosave();
    }
    void RecoverCarriedMaterials()
    {
        if (Progress.applied.Contains("collector")) return;
        var bag = Player.GetComponent<InventorySystem>();
        var protectedIds = new HashSet<string> { "wood_sword", "town_axe_1", "wood_long_blade", "handle", "golem_core", "fire_stone", "wind_stone" };
        foreach (var entry in bag.Items.ToArray())
        {
            string id = Loop.Catalog.items.FirstOrDefault(i => Loop.FieldItemFor(new Blacksmith.Stack(i.id, 1)) == entry.itemData)?.id;
            var definition = Loop.Catalog.Item(id);
            if (definition?.group == ItemGroup.Gathered && !protectedIds.Contains(id)) bag.RemoveItem(entry.itemData, entry.quantity);
        }
        Progress.applied.Add("collector"); Loop.RequestAutosave();
    }
    static void ClearBlockers()
    {
        foreach (var blocker in FindObjectsByType<StoryBlockedControl>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (blocker.name == "StoryInputBlocker") { blocker.gameObject.SetActive(false); Destroy(blocker.gameObject); }
    }
    // Scene/shop commits must complete before stopping the sequence; repeated requests coalesce.
    bool SkipCommitBusy() => Loop==null || Loop.Transitioning || FieldSceneTravel.Busy ||
        CampaignController.Instance?.Travelling==true || Shop!=null && Shop.State==ScreenState.Animating;
    void SkipStory()
    {
        if (skipping || ending || Progress?.tutorialCompleted != false) return;
        if (SkipCommitBusy()) { skipQueued=true; return; }
        skipQueued=false; reminderOwnsLock=false;skipping = true;
        // Retire the old task before the destination dialogue resumes after scene unload.
        taskId="";focusName=null;focusItem=null;
        if (tutorialRoutine != null) StopCoroutine(tutorialRoutine);
        view.CancelPresentation(); Unlock(); ExitStage(); ThawActor();
        StartCoroutine(FinishSkip(inIntro, storyScene + 1));
    }
    IEnumerator FinishSkip(bool introOnly, int nextScene)
    {
        if (introOnly)
        {
            Lock(); yield return view.Logo(); Unlock();
            CampaignController.Instance.Teleport(CampaignController.Instance.townScene.blackSmith.position + Vector3.up * 2);
            Checkpoint(4); inIntro = false; skipping = false; tutorialRoutine = StartCoroutine(RunTutorial(true)); yield break;
        }
        if (Loop.InShop && Shop != null) { Shop.CancelTutorialWork(); Loop.Travel(); }
        while (Loop.Transitioning || Loop.InShop || !Loop.Initialized) yield return null;
        GameUIController.Instance?.CloseAssimilationOffering(); CampaignController.Instance?.UI?.Close();
        actions.Clear(); allowedButtons.Clear(); ClearBlockers();
        if(nextScene>=14)
        {
            StoryProgress.CompleteForContinue(Loop.Campaign,Loop.SmithData,Loop.Catalog);
            view.SetObjective("");view.SetSkipAvailable(false);Loop.RefreshEquipment();Loop.RequestAutosave();skipping=false;yield break;
        }
        // A confirmed skip advances one story segment. Repair only essentials for its
        // destination through the existing inventory/equipment services, preserving owned stacks.
        yield return PrepareSkippedSegment(nextScene);
        Checkpoint(nextScene);skipping=false;tutorialRoutine=StartCoroutine(RunTutorial(true,nextScene));
    }
    void EnsureStoryItem(string id)
    {
        var data=Loop.InShop&&Shop!=null?Shop.Inventory.Data:Loop.SmithData;
        if(data.chest.Concat(data.bag).Any(x=>x.itemId==id&&x.count>0)||data.equipment.Any(x=>x.stack.itemId==id))return;
        Grant(id,1);
    }
    void EquipStoryItem(string id)
    {
        EnsureStoryItem(id);var data=Loop.SmithData;
        if(data.equipment.Any(x=>x.stack.itemId==id))return;
        actions.Add("equip:"+id);
        new InventoryService(data,Loop.Catalog).Equip(data.chest.Concat(data.bag).First(x=>x.itemId==id));
    }
    IEnumerator PrepareSkippedSegment(int nextScene)
    {
        if(nextScene>=5)OnCue("reveal");
        if(nextScene>=6&&nextScene<=7){EnsureStoryItem("handle");EnsureStoryItem(nextScene==6?"plank":"wood_long_blade");}
        if(nextScene>=8){EnsureStoryItem("wood_sword");if(nextScene>=9)RecoverCarriedMaterials();}
        if(nextScene>=10){GrantTutorialGatheringTools();EquipStoryItem("wood_sword");EquipStoryItem("town_axe_1");if(!Progress.applied.Contains("starting-axe"))Progress.applied.Add("starting-axe");Loop.RefreshEquipment();}
        if(nextScene<=10)
        {
            if(SmithyInterior.Instance?.Inside!=true)CampaignController.Instance.Teleport(SmithyInterior.Instance.entry.position);
            if(nextScene<=7)
            {
                yield return AutoOpenWorkshop();Shop.SetState(ScreenState.Workshop);
                OpenTutorialToolRack();
                if(nextScene==6)
                {
                    Shop.OpenTutorialTools(ToolKind.Whetstone);
                    Task("blade","숫돌에 나무 판자를 넣어 긴 나무 칼날을 만들자.",A("select:plank"),A("Tool_3","OpenMaterials","StationObject"),item:"plank");
                }
            }
        }
        else if(nextScene==11)CampaignController.Instance.Teleport(CampaignController.Instance.townScene.blackSmith.position+Vector3.up*2);
        else if(nextScene==12)
        {
            actions.Add("travel:field");if(!FieldSceneTravel.BeginToField())throw new InvalidOperationException("Tutorial field arrival failed");
            while(FieldSceneTravel.Busy||FindFirstObjectByType<FieldSceneState>()?.Ready!=true)yield return null;
        }
        else if(nextScene==13)
        {
            if(CampaignController.Instance==null){actions.Add("travel:town");if(!FieldSceneTravel.BeginToTown())throw new InvalidOperationException("Tutorial town arrival failed");while(FieldSceneTravel.Busy||CampaignController.Instance?.Ready!=true)yield return null;}
            Loop.BeginNight();CampaignController.Instance.Teleport(SmithyInterior.Instance.entry.position);
        }
        Loop.RequestAutosave();
    }
    void OnDisable() { skipQueued=false; reminderOwnsLock=false; StopAllCoroutines(); tutorialRoutine=null; if(view!=null){view.CancelPresentation();view.SetSkipAvailable(false);} ExitStage(); ThawActor(); Unlock(); ClearBlockers(); }
    void OnDestroy()
    {
        Unlock(); ExitStage(); ThawActor();
        if (restingHammer != null) Destroy(restingHammer);

        foreach (var blocker in FindObjectsByType<StoryBlockedControl>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (blocker != null && !blocker.transform.IsChildOf(transform)) Destroy(blocker.gameObject);
        if (Instance == this) Instance = null;
    }
}

public sealed class StoryTutorialDoor : MonoBehaviour, IInteractable
{
    public bool CanInteract() => StoryTutorial.Guiding && !StoryTutorial.Cinematic;
    public bool CanUseTool(ToolData tool) => true;
    public void Interact(PlayerInteraction player)
    { if (StoryTutorial.AllowAction("door")) StoryTutorial.Notify("door"); }
}

