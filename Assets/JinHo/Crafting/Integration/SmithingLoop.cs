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
    SmithingLoopContent content;
    SmithingItemBridge itemBridge;
    GatheringEquipmentBridge gathering;
    public Sprite ToolIcon(string name){var icon=gathering?.Icon(name);if(icon!=null)return icon;return name=="linked_pick"?content.pickaxeIcon:name=="linked_axe"?content.axeIcon:null;}
    public void ApplyGatheringTools()=>gathering?.Apply(progress.smith,content.catalog,Campaign);
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
    Canvas canvas;
    Button travel;
    TMP_Text travelLabel,message;
    string status="채집한 재료로 장비를 만들어 보세요.";
    string SavePath=>Path.Combine(Application.persistentDataPath,"smithing-loop-v1.json");
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
        ApplyGatheringTools();
        inventory.InventoryChanged+=InventoryChanged;
        BuildUI();ApplyEquipment();
        if(!string.IsNullOrEmpty(progress.selectedToolId))tools?.SelectToolId(progress.selectedToolId);
        if(tools!=null)tools.CurrentToolChanged+=SelectedToolChanged;
        Initialized=true;
    }
    void InventoryChanged(){if(!restoring)dirty=true;}
    void SelectedToolChanged(ToolData tool,int slot){if(!restoring)dirty=true;}
    void Update()
    {
        if(travel==null)return;
        travelLabel.text=Transitioning?"이동 중…":InteriorPanel?"대장간 실내로":InShop?(CampaignController.Instance!=null?"마을로 나가기":"필드로 재출발"):CampaignController.Instance!=null&&CampaignController.Instance.Ready?(CampaignController.Instance.InTown?"대장간 입장":"마을 귀환"):"대장간 귀환";
        travel.interactable=!Transitioning&&(InShop?shop!=null&&shop.CanLeave:health!=null&&!health.IsDead&&!GameUIController.BlocksGameplayInput);
        travel.gameObject.SetActive(InShop);
        message.text=InShop?string.Empty:status;
    }
    void LateUpdate(){if(dirty&&Automatic&&!Transitioning&&!sceneTravelPrepared&&CampaignController.Instance?.Travelling!=true)SaveProgress();}
    void BuildUI()
    {
        var root=new GameObject("LoopNavigation",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        root.transform.SetParent(transform,false);canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=1000;
        var scale=root.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1920,1080);
        var font=TMP_Settings.defaultFontAsset;
        travel=Button(root.transform,"LeaveSmithy",new Vector2(CampaignController.Instance!=null?.43f:.78f,.005f),new Vector2(CampaignController.Instance!=null?.58f:.925f,.065f),font,Travel);travelLabel=travel.GetComponentInChildren<TMP_Text>();travel.gameObject.SetActive(false);
        var label=new GameObject("LoopStatus",typeof(RectTransform),typeof(TextMeshProUGUI));label.transform.SetParent(root.transform,false);
        var rect=(RectTransform)label.transform;rect.anchorMin=new Vector2(.01f,.005f);rect.anchorMax=new Vector2(.77f,.062f);rect.offsetMin=rect.offsetMax=Vector2.zero;
        message=label.GetComponent<TMP_Text>();message.font=font;message.fontSize=19;message.color=Color.white;message.raycastTarget=false;
    }
    static Button Button(Transform parent,string name,Vector2 min,Vector2 max,TMP_FontAsset font,UnityEngine.Events.UnityAction click)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(parent,false);
        var r=(RectTransform)go.transform;r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;
        go.GetComponent<Image>().color=new Color(.13f,.20f,.19f,.96f);
        var text=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI));text.transform.SetParent(go.transform,false);
        var tr=(RectTransform)text.transform;tr.anchorMax=Vector2.one;tr.offsetMin=tr.offsetMax=Vector2.zero;
        var tmp=text.GetComponent<TMP_Text>();tmp.font=font;tmp.fontSize=22;tmp.alignment=TextAlignmentOptions.Center;tmp.raycastTarget=false;
        var button=go.GetComponent<Button>();button.targetGraphic=go.GetComponent<Image>();button.onClick.AddListener(click);return button;
    }
    public void Travel()
    {
        if(Transitioning||CampaignController.Instance?.UI?.IsOpen==true)return;
        var campaign=CampaignController.Instance;
        if(!InShop&&campaign!=null&&campaign.Ready&&!campaign.InTown){campaign.RequestReturn();return;}
        if(InShop){if(shop!=null&&shop.CanLeave)StartCoroutine(LeaveShop());}
        else if(health!=null&&!health.IsDead&&!GameUIController.BlocksGameplayInput)StartCoroutine(EnterShop());
    }
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
            {item.attack=content.baseSword.Damage;item.attackSpeed=content.baseSword.AttackSpeed;item.description+="\n수치 미정 · 기존 검의 임시 전투 수치 적용";}
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
        InShop=true;Transitioning=false;dirty=true;travel.gameObject.SetActive(true);
        if(entryRack)shop.OpenEquipmentRack();else shop.SetState(entryScreen);
        status="재료를 보관함으로 옮겼습니다. 제작·장착 후 재출발하세요. 미정 무기 수치는 기본 검 기준입니다.";
    }
    SaveData LoadShopSession(BlacksmithCatalog catalog)
    {
        itemBridge.Import(inventory,SmithyInterior.Instance?.Inside==true?progress.smith.bag:progress.smith.chest);
        progress.smith.hp=health.CurrentAssimilation;progress.smith.maxHp=health.MaxAssimilation;
        return JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(progress.smith));
    }
    void ReceiveShopSnapshot(SaveData data){progress.smith=data;CampaignController.Instance?.SmithSnapshotChanged();dirty=true;if(Automatic&&!Transitioning)SaveProgress();}
    IEnumerator LeaveShop()
    {
        Transitioning=true;shop.PrepareToLeave();
        int left=ExportCarriedBag();
        if(health!=null&&!health.IsDead)health.Assimilate(progress.smith.hp-health.CurrentAssimilation);
        RefreshEquipment();
        yield return SceneManager.UnloadSceneAsync(content.shopScene);
        shop=null;InShop=false;travel.gameObject.SetActive(false);RestoreField();Transitioning=false;dirty=true;
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
            runtimeWeapon.ConfigureCrafted(item.displayName,item.attack>0?item.attack:content.baseSword.Damage,
                item.attack>0?item.attackSpeed:content.baseSword.AttackSpeed,QualityRules.Multiplier(entry.stack.quality));
            if(combatReady)runtimeWeapon.ConfigureWeaponKind(item.bow,item.id.Contains("dagger"),item.id.Contains("hammer")?160:110);
            tools.SetToolSlot(weaponSlot,runtimeWeapon);
        }
        if(item==null&&combatReady)
        {
            var branch=content.catalog.Item("branch");
            runtimeWeapon=Instantiate(originalWeapon??content.baseSword);
            runtimeWeapon.ConfigureCatalogIdentity("branch");
            runtimeWeapon.ConfigureCrafted(branch?.displayName??"나뭇가지",branch?.attack>0?branch.attack:10,branch?.attackSpeed>0?branch.attackSpeed:2,1);
            tools.SetToolSlot(weaponSlot,runtimeWeapon);
        }
    }
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
            if(fromSceneTransfer)pendingSceneTransfer=null;
        }
        catch(Exception e){status="진행 저장을 읽지 못해 원본을 보호했습니다. 저장 중단: "+e.Message;Debug.LogWarning(status);}
        finally{restoring=false;}
    }
    void CaptureProgress(Progress target,InventorySystem fieldInventory)
    {
        target.campaign??=new CampaignState();
        CampaignController.Instance?.Capture(target.campaign);
        FieldSceneTravel.CaptureFieldHealth(target.campaign,inventory);
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
            !Application.CanStreamedLevelBeLoaded(destinationPath))return false;
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
                itemBridge.Import(stagedInventory,draft.smith.chest);
                if(health!=null&&!health.IsDead)draft.smith.hp=health.CurrentAssimilation;
                if(health!=null)draft.smith.maxHp=Mathf.RoundToInt(health.MaxAssimilation);
            }
            CaptureProgress(draft,stagedInventory);
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
    public bool SaveProgress()
    {
        if(SaveBlocked||sceneTravelPrepared||inventory==null||CampaignController.Instance?.Travelling==true)return false;
        try
        {
            CaptureProgress(progress,inventory);
            new FileTextStore(SavePath).Write(JsonUtility.ToJson(progress,true));dirty=false;return true;
        }
        catch(Exception e){dirty=false;status="저장 실패 · 이전 저장 보존: "+e.Message;Debug.LogWarning(status);return false;}
    }
    string SlotPath(int slot)=>slot==0?SavePath:Path.Combine(Application.persistentDataPath,"smithing-loop-slot-"+slot+".json");
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
        CaptureProgress(progress,inventory);
        try{new FileTextStore(SlotPath(slot)).Write(JsonUtility.ToJson(progress,true));return true;}catch(Exception e){Debug.LogWarning(e.Message);return false;}
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
    bool ValidProgress(Progress p,string json)
    {
        if(p==null||!json.Contains("\"version\"")||p.version!=1||p.smith==null||p.field==null||p.smith.chest==null||p.smith.bag==null||p.smith.equipment==null||p.smith.progress==null)return false;
        bool StackValid(Blacksmith.Stack s)=>s!=null&&s.count>0&&(int)s.quality>=0&&(int)s.quality<=4&&content.catalog.Item(s.itemId)!=null;
        if(p.field.Any(s=>s==null||s.count<=0||itemBridge.Resolve(s.id)==null))return false;
        if(p.smith.chest.Concat(p.smith.bag).Any(s=>!StackValid(s))||p.smith.equipment.Any(e=>e==null||!StackValid(e.stack)))return false;
        var c=p.campaign;
        return c==null||(c.gold>=0&&c.pendingGold>=0&&c.debtCarry>=0&&c.warnings>=0&&c.delivery!=null&&c.pawnStock!=null&&c.delivery.Concat(c.pawnStock).All(StackValid)&&!float.IsNaN(c.x)&&!float.IsNaN(c.y)&&!float.IsInfinity(c.x)&&!float.IsInfinity(c.y));
    }
    void ManualSave(){if(InShop)shop?.PrepareToLeave();if(SaveProgress())status="필드 가방·대장간 재료·레시피·장비를 저장했습니다.";}
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
