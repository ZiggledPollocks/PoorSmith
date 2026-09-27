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
    [Serializable] public sealed class Progress
    {
        public int version=1;
        public CampaignState campaign=new CampaignState();
        public SaveData smith=new SaveData();
        public List<FieldStack> field=new List<FieldStack>();
    }
    public static SmithingLoop Instance {get;private set;}
    public bool InShop {get;private set;}
    public bool Transitioning {get;private set;}
    public bool SaveBlocked {get;private set;}
    public SaveData SmithData => progress.smith;
    public CampaignState Campaign=>progress.campaign??=new CampaignState();
    public BlacksmithCatalog Catalog=>content.catalog;
    public bool Initialized {get;private set;}
    public bool CanSnapshot=>Initialized&&!Transitioning&&(!InShop||shop!=null&&shop.CanLeave);
    public void RequestAutosave(){dirty=true;}
    public void StoreFieldMaterials(){LoadShopSession(content.catalog);dirty=true;}
    public void RefreshEquipment()=>ApplyEquipment();
    SmithingLoopContent content;
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
    bool dirty, restoring;
    Scene fieldScene;
    float previousTimeScale;
    readonly List<Behaviour> hidden=new List<Behaviour>();
    Canvas canvas;
    Button travel,save;
    TMP_Text travelLabel,message;
    string status="채집한 재료로 장비를 만들어 보세요.";
    string SavePath=>Path.Combine(Application.persistentDataPath,"smithing-loop-v1.json");
    bool Automatic=>settings==null||settings.AutoSaveEnabled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics(){Instance=null;BlacksmithController.SessionLoader=null;BlacksmithController.SessionWriter=null;BlacksmithController.SessionCatalog=null;GameUIController.ExternalActivity=false;}
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
        LoadProgress();
        inventory.InventoryChanged+=InventoryChanged;
        BuildUI();ApplyEquipment();Initialized=true;
    }
    void InventoryChanged(){if(!restoring)dirty=true;}
    void Update()
    {
        if(travel==null)return;
        travelLabel.text=Transitioning?"이동 중…":InShop?(CampaignController.Instance!=null?"마을로 나가기":"필드로 재출발"):CampaignController.Instance!=null&&CampaignController.Instance.Ready?(CampaignController.Instance.InTown?"대장간 입장":"마을 귀환"):"대장간 귀환";
        travel.interactable=!Transitioning&&(InShop?shop!=null&&shop.CanLeave:health!=null&&!health.IsDead&&!GameUIController.BlocksGameplayInput);
        save.interactable=!Transitioning&&!SaveBlocked&&(!InShop||shop!=null&&shop.CanLeave);
        message.text=InShop?string.Empty:status;
    }
    void LateUpdate(){if(dirty&&Automatic&&!Transitioning)SaveProgress();}
    void BuildUI()
    {
        var root=new GameObject("LoopNavigation",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        root.transform.SetParent(transform,false);canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=1000;
        var scale=root.GetComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1920,1080);
        var font=Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault(x=>x.name.Contains("Nanum")||x.name.Contains("Blacksmith"))??TMP_Settings.defaultFontAsset;
        travel=Button(root.transform,"Travel",new Vector2(CampaignController.Instance!=null?.43f:.78f,.005f),new Vector2(CampaignController.Instance!=null?.58f:.925f,.065f),font,Travel);travelLabel=travel.GetComponentInChildren<TMP_Text>();
        save=Button(root.transform,"Save",new Vector2(CampaignController.Instance!=null?.585f:.927f,.005f),new Vector2(CampaignController.Instance!=null?.655f:.995f,.065f),font,ManualSave);save.GetComponentInChildren<TMP_Text>().text="저장";
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
        status="재료를 보관함으로 옮겼습니다. 제작·장착 후 재출발하세요. 미정 무기 수치는 기본 검 기준입니다.";
    }
    SaveData LoadShopSession(BlacksmithCatalog catalog)
    {
        var costs=new List<InventoryItem>();
        foreach(var stack in inventory.Items)
        {
            var link=content.materials.FirstOrDefault(x=>x.fieldItem==stack.itemData);
            if(link==null||catalog.Item(link.smithItemId)==null)continue;
            costs.Add(new InventoryItem(stack.itemData,stack.quantity));
        }
        if(costs.Count>0&&inventory.TryConsume(costs))
            foreach(var stack in costs)
                InventoryService.Add(progress.smith.chest,new Blacksmith.Stack(content.materials.First(x=>x.fieldItem==stack.itemData).smithItemId,stack.quantity));
        progress.smith.hp=health.CurrentAssimilation;progress.smith.maxHp=health.MaxAssimilation;
        return JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(progress.smith));
    }
    void ReceiveShopSnapshot(SaveData data){progress.smith=data;CampaignController.Instance?.SmithSnapshotChanged();dirty=true;if(Automatic&&!Transitioning)SaveProgress();}
    IEnumerator LeaveShop()
    {
        Transitioning=true;shop.PrepareToLeave();
        // Only mapped gathered materials travel back; crafted equipment remains in the shared rack.
        int left=0;
        foreach(var stack in progress.smith.bag.ToArray())
        {
            var link=content.materials.FirstOrDefault(x=>x.smithItemId==stack.itemId);
            if(link==null){left+=stack.count;continue;}
            if(inventory.TryAddItem(link.fieldItem,stack.count))progress.smith.bag.Remove(stack);else left+=stack.count;
        }
        if(health!=null&&!health.IsDead)health.Assimilate(progress.smith.hp-health.CurrentAssimilation);
        ApplyEquipment();
        yield return SceneManager.UnloadSceneAsync(content.shopScene);
        shop=null;InShop=false;RestoreField();Transitioning=false;dirty=true;
        status=left>0?"재출발했습니다. 미지원 물품 또는 무게 초과 재료는 대장간 배낭에 보존했습니다.":"재출발했습니다. 장착한 제작 무기의 품질이 전투에 반영됩니다.";
    }
    void RestoreField()
    {
        foreach(var b in hidden)if(b!=null)b.enabled=true;hidden.Clear();
        GameUIController.ExternalActivity=false;Time.timeScale=previousTimeScale;
        BlacksmithController.SessionLoader=null;BlacksmithController.SessionWriter=null;
        BlacksmithController.SessionCatalog=null;
        if(runtimeCatalog!=null){Destroy(runtimeCatalog);runtimeCatalog=null;}
        inventory?.GetComponent<PlayerInputHandler>()?.ClearGameplayInput();
    }
    void ApplyEquipment()
    {
        if(tools==null||weaponSlot<0)return;
        var entry=progress.smith.equipment.FirstOrDefault(x=>x.slot=="Weapon");
        var item=entry==null?null:content.catalog.Item(entry.stack.itemId);
        if(runtimeWeapon!=null){tools.SetToolSlot(weaponSlot,originalWeapon);Destroy(runtimeWeapon);runtimeWeapon=null;}
        if(item!=null&&(!item.bow||CampaignController.Instance!=null))
        {
            runtimeWeapon=Instantiate(originalWeapon??content.baseSword);
            runtimeWeapon.ConfigureCrafted(item.displayName,item.attack>0?item.attack:content.baseSword.Damage,
                item.attack>0?item.attackSpeed:content.baseSword.AttackSpeed,QualityRules.Multiplier(entry.stack.quality));
            if(CampaignController.Instance!=null)runtimeWeapon.ConfigureWeaponKind(item.bow,item.id.Contains("dagger"),item.id.Contains("hammer")?160:110);
            tools.SetToolSlot(weaponSlot,runtimeWeapon);
        }
        if(item==null&&CampaignController.Instance!=null)
        {
            runtimeWeapon=Instantiate(originalWeapon??content.baseSword);runtimeWeapon.ConfigureCrafted("나뭇가지 (임시 수치)",10,2,1);tools.SetToolSlot(weaponSlot,runtimeWeapon);
        }
    }
    void LoadProgress()
    {
        if(!File.Exists(SavePath))return;
        SaveBlocked=true;
        try
        {
            var json=File.ReadAllText(SavePath);var loaded=JsonUtility.FromJson<Progress>(json);
            if(!ValidProgress(loaded,json))throw new InvalidDataException("Invalid campaign save");
            if(loaded==null||!json.Contains("\"version\"")||loaded.version!=1||loaded.smith==null||loaded.field==null||loaded.smith.chest==null||loaded.smith.bag==null||loaded.smith.progress==null||loaded.smith.equipment==null)throw new InvalidDataException("Invalid loop save");
            foreach(var s in loaded.field)if(s==null||s.count<=0||!content.materials.Any(x=>x.fieldItem.ItemId==s.id))throw new InvalidDataException("Unknown field item");
            foreach(var s in loaded.smith.chest.Concat(loaded.smith.bag))if(s==null||s.count<=0||content.catalog.Item(s.itemId)==null)throw new InvalidDataException("Unknown smith item");
            foreach(var e in loaded.smith.equipment)if(e?.stack==null||content.catalog.Item(e.stack.itemId)==null)throw new InvalidDataException("Unknown equipment");
            var restored=loaded.field.Select(s=>new InventoryItem(content.materials.First(x=>x.fieldItem.ItemId==s.id).fieldItem,s.count)).ToList();
            restoring=true;inventory.RestoreSnapshot(restored);progress=loaded;
            progress.smith.acquiredItems??=new List<string>();SaveBlocked=false;
        }
        catch(Exception e){status="진행 저장을 읽지 못해 원본을 보호했습니다. 저장 중단: "+e.Message;Debug.LogWarning(status);}
        finally{restoring=false;}
    }
    public bool SaveProgress()
    {
        if(SaveBlocked||inventory==null)return false;
        try
        {
            CampaignController.Instance?.Capture();
            progress.field=inventory.Items.Where(x=>x?.itemData!=null&&x.quantity>0).Select(x=>new FieldStack{id=x.itemData.ItemId,count=x.quantity}).ToList();
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
        CampaignController.Instance?.Capture();
        progress.field=inventory.Items.Where(x=>x?.itemData!=null&&x.quantity>0).Select(x=>new FieldStack{id=x.itemData.ItemId,count=x.quantity}).ToList();
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
        if(p.field.Any(s=>s==null||s.count<=0||!content.materials.Any(m=>m.fieldItem.ItemId==s.id)))return false;
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
        if(Instance==this){if(InShop||Transitioning)RestoreField();Instance=null;}
        if(runtimeWeapon!=null)Destroy(runtimeWeapon);
    }
}
