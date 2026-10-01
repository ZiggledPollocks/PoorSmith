// [코드 지도] SmithingLoop: 필드·마을·대장간의 세션, 씬 이동, 장비 동기화와 통합 저장을 조정한다.
// 주요 함수: TryPrepareSceneTravel, ApplyEditorGrant, EnterShop
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Crafting/Integration/SmithingLoop.cs.md

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Blacksmith;
using SettingsMenuUI;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Owns the field/shop boundary and one atomic progress snapshot.</summary>
public sealed class SmithingLoop : MonoBehaviour
{
    [Serializable] public sealed class FieldStack { public string id; public int count; }
    sealed class SceneTransfer
    {
        public string destinationPath;
        public string progressJson;
    }
    static SceneTransfer pendingSceneTransfer;
    [Serializable] public sealed class Progress
    {
        public int version=1;
        public CampaignState campaign=new CampaignState();
        public SaveData smith=new SaveData();
        public List<FieldStack> field=new List<FieldStack>();
        public string selectedToolId;
    }
    public static SmithingLoop Instance {get;private set;}
    public bool InShop {get;private set;}
    public bool Transitioning {get;private set;}
    public bool SaveBlocked {get;private set;}
    public SaveData SmithData => progress.smith;
    public CampaignState Campaign=>progress.campaign??=new CampaignState();
    public BlacksmithCatalog Catalog=>content.catalog;
    public bool Initialized {get;private set;}
    public bool CanSnapshot=>Initialized&&!Transitioning&&!sceneTravelPrepared&&CampaignController.Instance?.Travelling!=true&&(!InShop||shop!=null&&shop.CanLeave);
    public void RequestAutosave(){dirty=true;}
    public void StoreFieldMaterials(){LoadShopSession(content.catalog);dirty=true;}
    public void RefreshEquipment(){ApplyEquipment();ApplyGatheringTools();}
#if UNITY_EDITOR
    // The editor-only grant is staged and validated before this method is called.
    public SaveData EditorGrantSource => InShop && shop?.Inventory != null ? shop.Inventory.Data : progress.smith;
    // 핵심 분기: !CanSnapshot || staged == null || !EditorGodModeSession.IsActive 판정.
    // 상태 변경: error 갱신.
    // 다음 연결: Blacksmith.InventoryService.Notify() 호출.
    public bool ApplyEditorGrant(SaveData staged, out string error)
    {
        error = null;
        if (!CanSnapshot || staged == null || !EditorGodModeSession.IsActive)
        {
            error = "게임 또는 임시 저장 세션이 준비되지 않았습니다.";
            return false;
        }
        SaveData target = EditorGrantSource;
        var oldChest = target.chest;
        var oldRecipes = target.progress;
        var oldAcquired = target.acquiredItems;
        var oldLoopData = progress.smith;
        try
        {
            target.chest = staged.chest;
            target.progress = staged.progress;
            target.acquiredItems = staged.acquiredItems;
            if (InShop)
            {
                shop.Inventory.Notify();
                shop.RefreshEditorGrant();
                shop.Persist(); // SessionWriter now writes only beneath the isolated root.
            }
            else
            {
                CampaignController.Instance?.SmithSnapshotChanged();
                RequestAutosave();
            }
            return true;
        }
        catch (Exception exception)
        {
            target.chest = oldChest;
            target.progress = oldRecipes;
            target.acquiredItems = oldAcquired;
            progress.smith = oldLoopData;
            error = exception.Message;
            return false;
        }
    }
#endif
    SmithingLoopContent content;
    SmithingItemBridge itemBridge;
    GatheringEquipmentBridge gathering;
    public Sprite ToolIcon(string name){var icon=gathering?.Icon(name);if(icon!=null)return icon;return name=="linked_sword"?content.baseSword?.Icon:name=="linked_pick"?content.pickaxeIcon:name=="linked_axe"?content.axeIcon:null;}
    public void ApplyGatheringTools()=>gathering?.Apply(progress.smith,content.catalog,Campaign,content);
    ScreenState entryScreen=ScreenState.Home;bool entryRack;
    public bool InteriorPanel=>InShop&&SmithyInterior.Instance?.Inside==true;
    public void ImportCarriedBag(){itemBridge.Import(inventory,progress.smith.bag);dirty=true;}
    public int ExportCarriedBag(){int left=itemBridge.Export(progress.smith.bag,inventory);dirty=true;return left;}
    public ItemData FieldItemFor(Blacksmith.Stack stack)=>itemBridge.FieldItem(stack);
    public void OpenInteriorPanel(ScreenState screen,bool rack=false)
    {if(InShop||Transitioning||GameUIController.BlocksGameplayInput)return;entryScreen=screen;entryRack=rack;StartCoroutine(EnterShop());}

    Progress progress=new Progress();
    InventorySystem inventory;
    PlayerAssimilate health;
    PlayerToolController tools;
    GameSettingsController settings;
    BlacksmithController shop;
    ToolData runtimeWeapon;
    BlacksmithCatalog runtimeCatalog;
    ToolData originalWeapon;
    int weaponSlot=-1;
    bool dirty, restoring, sceneTravelPrepared;
    string priorSceneTravelSave;
    bool priorSceneTravelSaveExists;
    Scene fieldScene;
    float previousTimeScale;
    readonly List<Behaviour> hidden=new List<Behaviour>();
    TMP_Text message;
    string status=string.Empty;
    string SavePath=>GameSavePaths.File("smithing-loop-v1.json");
    bool Automatic=>settings==null||settings.AutoSaveEnabled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics(){Instance=null;pendingSceneTransfer=null;BlacksmithController.SessionLoader=null;BlacksmithController.SessionWriter=null;BlacksmithController.SessionCatalog=null;GameUIController.ExternalActivity=false;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded-=SceneLoaded;
        SceneManager.sceneLoaded+=SceneLoaded;
        SceneLoaded(SceneManager.GetActiveScene(),LoadSceneMode.Single);
    }
    static void SceneLoaded(Scene scene,LoadSceneMode mode)
    {
        if(Instance!=null||mode==LoadSceneMode.Additive)return;
        var config=Resources.Load<SmithingLoopContent>("SmithingLoopContent");
        if(config==null)return;
        var inv=FindFirstObjectByType<InventorySystem>();
        if(inv==null)return;
        var root=new GameObject("SmithingLoop");SceneManager.MoveGameObjectToScene(root,scene);
        var loop=root.AddComponent<SmithingLoop>();loop.content=config;loop.inventory=inv;loop.fieldScene=scene;
    }
    IEnumerator Start()
    {
        if(Instance!=null&&Instance!=this){Destroy(gameObject);yield break;}
        Instance=this;
        yield return null; // Existing player/UI Start methods establish their own references.
        health=inventory.GetComponent<PlayerAssimilate>();tools=inventory.GetComponent<PlayerToolController>();
        settings=FindFirstObjectByType<GameSettingsController>(FindObjectsInactive.Include);
        for(int i=0;tools!=null&&i<tools.ToolSlotCount;i++)
            if(tools.GetToolAtSlot(i)?.ToolType==ToolType.Sword){weaponSlot=i;originalWeapon=tools.GetToolAtSlot(i);break;}
        itemBridge=new SmithingItemBridge(content);
        gathering=new GatheringEquipmentBridge(tools);
        LoadProgress();
        gathering.Migrate(Campaign,progress.smith,content.catalog);
        MigrateStarterSword();
        ApplyGatheringTools();
        inventory.InventoryChanged+=InventoryChanged;
        BuildUI();ApplyEquipment();
        if(!string.IsNullOrEmpty(progress.selectedToolId))tools?.SelectToolId(progress.selectedToolId);
        if(tools!=null)tools.CurrentToolChanged+=SelectedToolChanged;
        Initialized=true;
    }
    void InventoryChanged(){if(!restoring)dirty=true;}
    void MigrateStarterSword()
    {
        if(Campaign.starterSwordLinked||content.catalog.Item("starter_sword")==null)return;
        var sword=new Blacksmith.Stack("starter_sword",1);
        if(progress.smith.equipment.Any(e=>e.slot=="Weapon"))
            InventoryService.Add(progress.smith.chest,sword);
        else
            progress.smith.equipment.Add(new EquipmentEntry{slot="Weapon",stack=sword});
        progress.smith.acquiredItems??=new List<string>();
        if(!progress.smith.acquiredItems.Contains(sword.itemId))progress.smith.acquiredItems.Add(sword.itemId);
        Campaign.starterSwordLinked=true;
        dirty=true;
    }
    void SelectedToolChanged(ToolData tool,int slot){if(!restoring)dirty=true;}
    void Update()
    {
        if(message==null)return;
        message.gameObject.SetActive(!InShop&&!string.IsNullOrEmpty(status));
        if(message.gameObject.activeSelf)message.text=status;
    }
    void LateUpdate(){if(dirty&&Automatic&&!Transitioning&&!sceneTravelPrepared&&CampaignController.Instance?.Travelling!=true)SaveProgress();}
    void BuildUI()
    {
        var root=new GameObject("LoopNavigation",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        root.transform.SetParent(transform,false);var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=1000;
        var scale=root.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1920,1080);
        var font=TMP_Settings.defaultFontAsset;
        var label=new GameObject("LoopStatus",typeof(RectTransform),typeof(TextMeshProUGUI));label.transform.SetParent(root.transform,false);
        var rect=(RectTransform)label.transform;rect.anchorMin=new Vector2(.01f,.005f);rect.anchorMax=new Vector2(.77f,.062f);rect.offsetMin=rect.offsetMax=Vector2.zero;
        message=label.GetComponent<TMP_Text>();message.font=font;RuntimeUIFactory.FitText(message,19);message.color=Color.white;message.raycastTarget=false;
    }
    public void Travel()
    {
        if(Transitioning||CampaignController.Instance?.UI?.IsOpen==true)return;
        var campaign=CampaignController.Instance;
        if(!InShop&&campaign!=null&&campaign.Ready&&!campaign.InTown){campaign.RequestReturn();return;}
        if(InShop){if(shop!=null&&shop.CanLeave)StartCoroutine(LeaveShop());}
        else if(health!=null&&!health.IsDead&&!GameUIController.BlocksGameplayInput)StartCoroutine(EnterShop());
    }
    // 핵심 분기: !Application.CanStreamedLevelBeLoaded(content.shopScene) 판정.
    // 상태 변경: status 갱신.
    // 다음 연결: PlayerInputHandler.ClearGameplayInput() 호출.
    IEnumerator EnterShop()
    {
        if(!Application.CanStreamedLevelBeLoaded(content.shopScene)){status="대장간 씬이 빌드 목록에 없습니다.";yield break;}
        Transitioning=true;
        previousTimeScale=Time.timeScale;GameUIController.ExternalActivity=true;Time.timeScale=0;
        inventory.GetComponent<PlayerInputHandler>()?.ClearGameplayInput();
        foreach(var root in fieldScene.GetRootGameObjects())
        {
            if(root==gameObject)continue;
            foreach(var camera in root.GetComponentsInChildren<Camera>(true))if(camera.enabled){hidden.Add(camera);camera.enabled=false;}
            foreach(var c in root.GetComponentsInChildren<Canvas>(true))if(c.enabled){hidden.Add(c);c.enabled=false;}
            foreach(var listener in root.GetComponentsInChildren<AudioListener>(true))if(listener.enabled){hidden.Add(listener);listener.enabled=false;}
        }
        foreach(var es in FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None))
            if(es.enabled){hidden.Add(es);es.enabled=false;}
        runtimeCatalog=Instantiate(content.catalog);
        foreach(var item in runtimeCatalog.items)
        {
            if(CampaignController.Instance!=null)item.price=CampaignController.Instance.Economy.BasePrice(item);
            if(item.material==MaterialKind.Weapon&&item.attack<=0)
            {item.attack=content.baseSword.Damage;item.attackSpeed=content.baseSword.BaseAttackSpeed;item.description+="\n수치 미정 · 기존 검의 임시 전투 수치 적용";}
            if(item.material==MaterialKind.Armor&&item.defense<=0){item.defense=CampaignController.Instance!=null?CampaignController.Instance.rules.temporaryArmorDefense:0;item.description+="\n방어 수치 미정 · 임시 방어력 적용";}
            if(item.id.StartsWith("blood_"))item.specialEffect=item.material==MaterialKind.Armor?"피격 시 흡혈 방어구 1/2/3/4개: 적 피해 5/12/18/24":"공격 적중 / 방패 방어 성공: 동화율 2 회복";
            if(item.id.StartsWith("fire_")&&CampaignController.Instance!=null){var r=CampaignController.Instance.rules;item.specialEffect=$"공격·방어 성공 / 방어구 피격: 초당 {r.temporaryBurnDamage} 피해, {r.temporaryBurnSeconds}초 (임시) · 동일 효과 시간 갱신";}
            if(item.bow)item.description+="\n충전 후 발사 · 화살 소모 · 수치는 임시값";
        }
        BlacksmithController.SessionCatalog=runtimeCatalog;
        BlacksmithController.SessionLoader=LoadShopSession;BlacksmithController.SessionWriter=ReceiveShopSnapshot;
        var operation=SceneManager.LoadSceneAsync(content.shopScene,LoadSceneMode.Additive);
        if(operation==null){RestoreField();Transitioning=false;yield break;}
        yield return operation;yield return null;
        var scene=SceneManager.GetSceneByPath(content.shopScene);
        shop=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<BlacksmithController>(true)).FirstOrDefault();
        if(shop==null){yield return SceneManager.UnloadSceneAsync(scene);RestoreField();Transitioning=false;status="대장간 컨트롤러가 없어 필드로 복귀했습니다.";yield break;}
        // The field EventSystem keeps UI navigation; avoid competing EventSystems.
        foreach(var es in scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>()))es.gameObject.SetActive(false);
        foreach(var es in hidden.OfType<UnityEngine.EventSystems.EventSystem>())if(es!=null)es.enabled=true;
        InShop=true;Transitioning=false;dirty=true;
        if(entryRack)shop.OpenEquipmentRack();else if(entryScreen==ScreenState.Chest)shop.OpenStorageChest();else shop.SetState(entryScreen);
        status="재료를 가방에 유지했습니다. 제작·장착 후 재출발하세요. 미정 무기 수치는 기본 검 기준입니다.";
    }
    SaveData LoadShopSession(BlacksmithCatalog catalog)
    {
        // The field inventory and town inventory are two views of the carried bag.
        // Import to the bag regardless of whether the player entered the interior.
        itemBridge.Import(inventory,progress.smith.bag);
        progress.smith.hp=health.RemainingHealth;progress.smith.maxHp=health.MaxAssimilation;
        return JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(progress.smith));
    }
    void ReceiveShopSnapshot(SaveData data){progress.smith=data;CampaignController.Instance?.SmithSnapshotChanged();dirty=true;if(Automatic&&!Transitioning)SaveProgress();}
    IEnumerator LeaveShop()
    {
        Transitioning=true;shop.PrepareToLeave();
        int left=ExportCarriedBag();
        if(health!=null&&!health.IsDead)health.RestoreRemainingHealth(progress.smith.hp);
        RefreshEquipment();
        yield return SceneManager.UnloadSceneAsync(content.shopScene);
        shop=null;InShop=false;RestoreField();Transitioning=false;dirty=true;
        status=left>0?"재출발했습니다. 무게 초과 물품는 대장간 배낭에 보존했습니다.":"재출발했습니다. 장착한 제작 무기의 품질이 전투에 반영됩니다.";
    }
    void RestoreField()
    {
        foreach(var b in hidden)if(b!=null)b.enabled=true;hidden.Clear();
        GameUIController.ExternalActivity=false;Time.timeScale=previousTimeScale;
        BlacksmithController.SessionLoader=null;BlacksmithController.SessionWriter=null;
        BlacksmithController.SessionCatalog=null;
        if(runtimeCatalog!=null){Destroy(runtimeCatalog);runtimeCatalog=null;}
        if(inventory!=null)inventory.GetComponent<PlayerInputHandler>()?.ClearGameplayInput();
    }
    // 핵심 분기: tools==null||weaponSlot<0 판정.
    // 상태 변경: runtimeWeapon 갱신.
    // 다음 연결: Blacksmith.BlacksmithCatalog.Item(string) 호출.
    void ApplyEquipment()
    {
        if(tools==null||weaponSlot<0)return;
        bool combatReady=inventory.GetComponent<CampaignCombat>()!=null;
        var entry=progress.smith.equipment.FirstOrDefault(x=>x.slot=="Weapon");
        var item=entry==null?null:content.catalog.Item(entry.stack.itemId);
        if(runtimeWeapon!=null){tools.SetToolSlot(weaponSlot,originalWeapon);Destroy(runtimeWeapon);runtimeWeapon=null;}
        if(item!=null&&(!item.bow||combatReady))
        {
            runtimeWeapon=Instantiate(originalWeapon??content.baseSword);
            runtimeWeapon.ConfigureCatalogIdentity(item.id);
            runtimeWeapon.ConfigureIcon(content.ArtForItem(item));
            runtimeWeapon.ConfigureCrafted(item.displayName,item.attack>0?item.attack:content.baseSword.Damage,
                item.isHammerWeapon||item.attack>0?item.attackSpeed:content.baseSword.BaseAttackSpeed,QualityRules.Multiplier(entry.stack.quality));
            if(combatReady)runtimeWeapon.ConfigureWeaponKind(item.bow,item.id.Contains("dagger"),item.isHammerWeapon?160:110,item.isHammerWeapon,item.hammerAttackRateMultiplier);
            tools.SetToolSlot(weaponSlot,runtimeWeapon);
        }
        if(item==null)tools.SetToolSlot(weaponSlot,null);
    }
    // 핵심 분기: !fromSceneTransfer&&!File.Exists(SavePath) 판정.
    // 상태 변경: SaveBlocked 갱신.
    // 다음 연결: SmithingLoop.ValidProgress(SmithingLoop.Progress, string) 호출.
    void LoadProgress()
    {
        bool fromSceneTransfer=pendingSceneTransfer!=null&&pendingSceneTransfer.destinationPath==fieldScene.path;
        if(!fromSceneTransfer&&!File.Exists(SavePath))return;
        SaveBlocked=true;
        try
        {
            var json=fromSceneTransfer?pendingSceneTransfer.progressJson:File.ReadAllText(SavePath);
            var loaded=JsonUtility.FromJson<Progress>(json);
            if(!ValidProgress(loaded,json))throw new InvalidDataException("Invalid campaign save");
            if(loaded==null||!json.Contains("\"version\"")||loaded.version!=1||loaded.smith==null||loaded.field==null||loaded.smith.chest==null||loaded.smith.bag==null||loaded.smith.progress==null||loaded.smith.equipment==null)throw new InvalidDataException("Invalid loop save");
            foreach(var s in loaded.field)if(s==null||s.count<=0||itemBridge.Resolve(s.id)==null)throw new InvalidDataException("Unknown field item");
            foreach(var s in loaded.smith.chest.Concat(loaded.smith.bag))if(s==null||s.count<=0||content.catalog.Item(s.itemId)==null)throw new InvalidDataException("Unknown smith item");
            foreach(var e in loaded.smith.equipment)if(e?.stack==null||content.catalog.Item(e.stack.itemId)==null)throw new InvalidDataException("Unknown equipment");
            var restored=loaded.field.Select(s=>new InventoryItem(itemBridge.Resolve(s.id),s.count)).ToList();
            restoring=true;inventory.RestoreSnapshot(restored);progress=loaded;
            progress.smith.acquiredItems??=new List<string>();SaveBlocked=false;
            if(fromSceneTransfer){pendingSceneTransfer=null;dirty=true;}
        }
        catch(Exception e){status="진행 저장을 읽지 못해 원본을 보호했습니다. 저장 중단: "+e.Message;Debug.LogWarning(status);}
        finally{restoring=false;}
    }
    void CaptureProgress(Progress target,InventorySystem fieldInventory,bool captureFieldItems=true)
    {
        target.campaign??=new CampaignState();
        CampaignController.Instance?.Capture(target.campaign);
        FieldSceneTravel.CaptureFieldHealth(target.campaign,inventory);
        if(captureFieldItems)
            target.field=fieldInventory.Items.Where(x=>x?.itemData!=null&&x.quantity>0)
                .Select(x=>new FieldStack{id=x.itemData.ItemId,count=x.quantity}).ToList();
        target.selectedToolId=tools?.CurrentTool?.ToolId;
    }
    /// <summary>
    /// Save a prepared copy for the destination scene. The current scene's player,
    /// inventory and progress remain unchanged until it is unloaded.
    /// </summary>
    public bool TryPrepareSceneTravel(string destinationPath,bool toField)
    {
        if(!Initialized||SaveBlocked||sceneTravelPrepared||pendingSceneTransfer!=null||
            InShop||Transitioning||inventory==null||string.IsNullOrEmpty(destinationPath)||
            !Application.CanStreamedLevelBeLoaded(destinationPath)||
            (toField&&(health==null||health.IsDead)))return false;
        GameObject stagingRoot=null;
        try
        {
            // Read before writing so a failed scene-load start can restore the prior save.
            bool hadSave=File.Exists(SavePath);
            string oldSave=hadSave?File.ReadAllText(SavePath):null;
            var draft=JsonUtility.FromJson<Progress>(JsonUtility.ToJson(progress));
            stagingRoot=new GameObject("Scene Travel Inventory Snapshot");
            stagingRoot.hideFlags=HideFlags.HideAndDontSave;
            stagingRoot.SetActive(false);
            var stagedInventory=stagingRoot.AddComponent<InventorySystem>();
            float allowance=inventory.settingsWeight>0f?
                Mathf.Max(0f,inventory.MaxWeight/inventory.settingsWeight-1f):0f;
            stagedInventory.ConfigureCapacity(inventory.settingsWeight,allowance);
            stagedInventory.RestoreSnapshot(inventory.Items);
            if(toField)itemBridge.Export(draft.smith.bag,stagedInventory);
            else
            {
                // Scene travel must preserve carried items in the town bag.
                itemBridge.Import(stagedInventory,draft.smith.bag);
                if(health!=null&&!health.IsDead)draft.smith.hp=health.RemainingHealth;
                if(health!=null)draft.smith.maxHp=Mathf.RoundToInt(health.MaxAssimilation);
            }
            CaptureProgress(draft,stagedInventory);
            if(toField)
            {
                // The destination player is a new scene object: stage the town
                // player's current assimilation instead of an older save value.
                draft.campaign.health=health.RemainingHealth;
                draft.smith.hp=health.RemainingHealth;
                draft.smith.maxHp=health.MaxAssimilation;
                // A previous field save may still point into the cave. The field
                // player's Start() restores this snapshot during scene loading,
                // so stage the same forest arrival used by FieldSceneTravel.
                draft.campaign.hasPosition=true;
                draft.campaign.sceneName=Path.GetFileNameWithoutExtension(destinationPath);
                draft.campaign.x=FieldSceneTravel.FieldArrival.x;
                draft.campaign.y=FieldSceneTravel.FieldArrival.y;
                draft.campaign.inTown=false;
            }
            string json=JsonUtility.ToJson(draft,true);
            if(!ValidProgress(draft,json))throw new InvalidDataException("Prepared scene travel state is invalid");
            new FileTextStore(SavePath).Write(json);
            priorSceneTravelSaveExists=hadSave;
            priorSceneTravelSave=oldSave;
            pendingSceneTransfer=new SceneTransfer{destinationPath=destinationPath,progressJson=json};
            sceneTravelPrepared=true;
            return true;
        }
        catch(Exception e)
        {
            status="씬 이동 준비 실패 · 현재 플레이어 유지: "+e.Message;
            Debug.LogWarning(status);
            return false;
        }
        finally
        {
            if(stagingRoot!=null)Destroy(stagingRoot);
        }
    }
    // 핵심 분기: !sceneTravelPrepared 판정.
    // 상태 변경: SaveBlocked 갱신.
    // 다음 연결: FileTextStore.Write(string) 호출.
    public void CancelPreparedSceneTravel()
    {
        if(!sceneTravelPrepared)return;
        try
        {
            if(priorSceneTravelSaveExists)new FileTextStore(SavePath).Write(priorSceneTravelSave);
            else if(File.Exists(SavePath))File.Delete(SavePath);
        }
        catch(Exception e)
        {
            SaveBlocked=true;
            status="씬 이동 취소 후 이전 저장 복원 실패: "+e.Message;
            Debug.LogError(status);
        }
        finally
        {
            sceneTravelPrepared=false;
            priorSceneTravelSave=null;
            pendingSceneTransfer=null;
        }
    }
    public static void DiscardPendingSceneTravel()=>pendingSceneTransfer=null;
    bool InDedicatedField=>fieldScene.path==FieldSceneTravel.FieldScenePath;
    public bool SaveProgress(bool commitFieldItems=false)
    {
        if(SaveBlocked||sceneTravelPrepared||inventory==null||CampaignController.Instance?.Travelling==true)return false;
        try
        {
            // Field inventory changes become durable when town travel is prepared.
            // Death is the one explicit exception: its cleared bag must be saved.
            CaptureProgress(progress,inventory,!InDedicatedField||commitFieldItems);
            AutoSaveHistory.Save(GameSavePaths.Root,JsonUtility.ToJson(progress,true),progress.smith.day);
            dirty=false;return true;
        }
        catch(Exception e){dirty=false;status="저장 실패 · 이전 저장 보존: "+e.Message;Debug.LogWarning(status);return false;}
    }
    string SlotPath(int slot)=>slot==0?SavePath:GameSavePaths.File("smithing-loop-slot-"+slot+".json");
    public string SlotInfo(int slot)
    {
        if(slot<0||slot>4)return "잘못된 슬롯";
        try{if(!File.Exists(SlotPath(slot)))return "빈 슬롯";var p=JsonUtility.FromJson<Progress>(File.ReadAllText(SlotPath(slot)));if(p?.smith==null||p.version!=1)return "미지원";return $"{p.smith.day}일 · {(p.campaign?.playSeconds??0)/60:0}분";}catch{return "읽기 오류";}
    }
    public bool SaveSlot(int slot)
    {
        if(slot<0||slot>4||!CanSnapshot||SaveBlocked)return false;
        if(InShop)shop.PrepareToLeave();
        // Capture once; writing a manual slot does not overwrite the automatic slot.
        CaptureProgress(progress,inventory,!InDedicatedField);
        try
        {
            string json=JsonUtility.ToJson(progress,true);
            if(slot==0)AutoSaveHistory.Save(GameSavePaths.Root,json,progress.smith.day);
            else new FileTextStore(SlotPath(slot)).Write(json);
            return true;
        }
        catch(Exception e){Debug.LogWarning(e.Message);return false;}
    }
    public bool LoadSlot(int slot)
    {
        if(slot<0||slot>4||!CanSnapshot)return false;
        try
        {
            string json=File.ReadAllText(SlotPath(slot));var saved=JsonUtility.FromJson<Progress>(json);
            if(!ValidProgress(saved,json))return false;
            new FileTextStore(SavePath).Write(json);
            // Prevent OnQuit/LateUpdate from replacing the selected snapshot during reload.
            dirty=false;SaveBlocked=true;GameUIController.ExternalActivity=false;Time.timeScale=1;
            SceneManager.LoadScene(fieldScene.path);return true;
        }
        catch(Exception e){Debug.LogWarning(e.Message);return false;}
    }
    public static bool IsValidSave(string json)
    {
        try
        {
            var source=Resources.Load<SmithingLoopContent>("SmithingLoopContent");
            if(source?.catalog==null)return false;
            using(var bridge=new SmithingItemBridge(source))
                return ValidateProgress(JsonUtility.FromJson<Progress>(json),json,source,bridge);
        }
        catch{return false;}
    }
    bool ValidProgress(Progress p,string json)=>ValidateProgress(p,json,content,itemBridge);
    static bool ValidateProgress(Progress p,string json,SmithingLoopContent content,SmithingItemBridge itemBridge)
    {
        if(p==null||!json.Contains("\"version\"")||p.version!=1||p.smith==null||p.field==null||p.smith.chest==null||p.smith.bag==null||p.smith.equipment==null||p.smith.progress==null)return false;
        bool StackValid(Blacksmith.Stack s)=>s!=null&&s.count>0&&(int)s.quality>=0&&(int)s.quality<=4&&content.catalog.Item(s.itemId)!=null;
        if(p.field.Any(s=>s==null||s.count<=0||itemBridge.Resolve(s.id)==null))return false;
        if(p.smith.chest.Concat(p.smith.bag).Any(s=>!StackValid(s))||p.smith.equipment.Any(e=>e==null||!StackValid(e.stack)))return false;
        var c=p.campaign;
        return c==null||(c.gold>=0&&c.goldMilliRemainder>=0&&c.goldMilliRemainder<1000&&
            c.pendingGold>=0&&c.debtCarry>=0&&c.warnings>=0&&c.delivery!=null&&c.pawnStock!=null&&
            c.delivery.Concat(c.pawnStock).All(StackValid)&&
            (c.pawnLoans==null||c.pawnLoans.All(l=>l!=null&&!string.IsNullOrEmpty(l.id)&&StackValid(l.collateral)&&
                (l.principalMilli>0||l.principal>0)&&l.principal>=0&&l.principalMilli>=0&&
                l.unpaidInterestCents>=0&&l.unpaidInterestMilli>=0&&
                l.pledgedDay>=1&&l.lastInterestDay>=l.pledgedDay&&
                l.lastInterestDay<=p.smith.day)&&c.pawnLoans.Select(l=>l.id).Distinct().Count()==c.pawnLoans.Count)&&
            !float.IsNaN(c.x)&&!float.IsNaN(c.y)&&!float.IsInfinity(c.x)&&!float.IsInfinity(c.y));
    }
    void ManualSave(){if(InShop)shop?.PrepareToLeave();if(SaveProgress())status=InDedicatedField?"진행을 저장했습니다. 필드 채집 아이템은 마을 복귀 시 저장됩니다.":"필드 가방·대장간 재료·레시피·장비를 저장했습니다.";}
    void OnApplicationPause(bool paused){if(paused&&Automatic){if(InShop&&shop!=null&&shop.CanLeave)shop.PrepareToLeave();if(!InShop||shop!=null&&shop.CanLeave)SaveProgress();}}
    public void QuitCampaign(){SaveBlocked=true;dirty=false;Application.Quit();}
    void OnApplicationQuit(){if(Automatic&&(!InShop||shop!=null&&shop.CanLeave)){if(InShop)shop.PrepareToLeave();SaveProgress();}}
    void OnDestroy()
    {
        if(inventory!=null)inventory.InventoryChanged-=InventoryChanged;
        if(tools!=null)tools.CurrentToolChanged-=SelectedToolChanged;
        if(Instance==this){if(InShop||Transitioning)RestoreField();Instance=null;}
        if(runtimeWeapon!=null)Destroy(runtimeWeapon);
        itemBridge?.Dispose();gathering?.Dispose();
    }
}
