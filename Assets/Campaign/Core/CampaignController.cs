// [코드 지도] CampaignController: 캠페인 플레이의 마을 상태, 상호작용, 경제 및 UI 진입을 조정한다.
// 주요 함수: Start, Interact, Teleport
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/Campaign/Core/CampaignController.cs.md

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Blacksmith;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class CampaignController : MonoBehaviour
{
    public static CampaignController Instance {get;private set;}
    static bool startAtTownSpawnOnNextScene;
    public static void StartAtTownSpawnOnNextScene()=>startAtTownSpawnOnNextScene=true;
    public CampaignRules rules;
    public TownSceneIntegration townScene;
    CampaignTransition transition;
    public bool Travelling=>transition!=null&&transition.Busy;
    public GameObject windParticleDrop;
    public Vector2 townSpawn=new(-100,3),fieldSpawn=new(5,3),windSpawn=new(243,3);
    public CampaignUI UI {get;private set;}
    public CampaignExploration Exploration {get;private set;}
    public Transform Player {get;private set;}
    public CampaignState State=>SmithingLoop.Instance.Campaign;
    public CampaignEconomy Economy=>new(State,SmithingLoop.Instance.SmithData,SmithingLoop.Instance.Catalog,rules);
    public bool InTown=>Player!=null&&(SmithyInterior.Instance?.Contains(Player.position)==true|| (townScene!=null?townScene.Contains(Player.position):Player.position.x< -50));
    public bool InWind=>Player!=null&&(townScene!=null?townScene.InWind(Player.position):Player.position.x>230);
    public bool Ready {get;private set;}
    public float MovementMultiplier=>1+(InWind?.1f:0)+(Inventory.CurrentWeight>Inventory.settingsWeight?-.1f:0)+(Player.GetComponent<CampaignCombat>()?.WindArmorBonus??0);
    public InventorySystem Inventory {get;private set;}
    PlayerAssimilate health;
    float explorationClock,saveClock;
    readonly List<ToolData> upgradedTools=new();
    int knownDay;
    bool pendingGameOver;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void ResetStatics(){Instance=null;startAtTownSpawnOnNextScene=false;}
    void Awake(){Instance=this;transition=gameObject.AddComponent<CampaignTransition>();}
    // 핵심 분기: loan.principalMilli==0 판정.
    // 상태 변경: Inventory 갱신.
    // 다음 연결: CampaignExploration.Restore(System.Collections.Generic.IEnumerable<string>) 호출.
    IEnumerator Start()
    {
        while(SmithingLoop.Instance==null||!SmithingLoop.Instance.Initialized)yield return null;
        Inventory=FindFirstObjectByType<InventorySystem>();Player=Inventory.transform;health=Player.GetComponent<PlayerAssimilate>();
        UI=GetComponent<CampaignUI>();Exploration=GetComponent<CampaignExploration>();
        State.delivery??=new();State.pawnStock??=new();State.pawnLoans??=new();State.openedChests??=new();State.usedAltars??=new();State.visitedCells??=new();State.unlockedWarps??=new();
        Exploration.Restore(State.visitedCells);knownDay=SmithingLoop.Instance.SmithData.day;
        bool migratedLoans=false;
        foreach(var loan in State.pawnLoans)
        {
            migratedLoans|=loan.principal!=0||loan.unpaidInterestCents!=0;
            if(loan.principalMilli==0)loan.principalMilli=(long)loan.principal*1000;
            if(loan.unpaidInterestMilli==0&&loan.unpaidInterestCents>0)
                loan.unpaidInterestMilli=loan.unpaidInterestCents*10;
            loan.principal=0;
            loan.unpaidInterestCents=0;
        }
        if(migratedLoans)SmithingLoop.Instance.RequestAutosave();
        bool interestNeedsCatchUp=migratedLoans||State.pawnLoans.Any(loan=>loan.lastInterestDay<knownDay);
        if(interestNeedsCatchUp)
        { Economy.AccruePawnInterest(knownDay);SmithingLoop.Instance.RequestAutosave(); }
        if(Economy.DebtDue)
        {
            if(Economy.SettleOverdueDebt()>0)SmithingLoop.Instance.RequestAutosave();
        }
        else if(interestNeedsCatchUp)
        {
            long beforeInterest=Economy.GoldMilli;
            Economy.CollectPawnInterest();
            if(Economy.GoldMilli!=beforeInterest)SmithingLoop.Instance.RequestAutosave();
        }
        bool compatible=State.hasPosition&&(State.sceneName==gameObject.scene.name||(townScene==null&&string.IsNullOrEmpty(State.sceneName)));
        var savedPosition=new Vector2(State.x,State.y);
        if(compatible&&townScene!=null&&!townScene.Contains(savedPosition)&&SmithyInterior.Instance?.Contains(savedPosition)!=true)
            compatible=false;
        bool startAtTownSpawn=startAtTownSpawnOnNextScene;
        startAtTownSpawnOnNextScene=false;
        Teleport(!startAtTownSpawn&&compatible?savedPosition:townSpawn);
        // A title-screen restart keeps the saved economy and equipment, but gives a
        // finished debt run a playable warning count for its next weekly settlement.
        if(startAtTownSpawn&&State.gameOver)
        {
            State.gameOver=false;
            State.warnings=0;
            SmithingLoop.Instance.RequestAutosave();
        }
        health.RestoreRemainingHealth(State.hasPosition ? State.health : health.MaxAssimilation);
        Player.GetComponent<LiquidCircleGaugeHUD>()?.SyncRestoredValue();
        health.Died+=OnDeath;ApplyUpgrades();Ready=true;UI.Build(this);
        if(State.gameOver)UI.ShowGameOver();
    }
    void Update()
    {
        if(!Ready||Travelling)return;
        if(pendingGameOver&&!SmithingLoop.Instance.InShop&&!UI.IsOpen)
        {pendingGameOver=false;UI.ShowGameOver();}
        if(Economy.DebtDue&&!State.gameOver)
        {if(Economy.SettleOverdueDebt()>0)Commit();if(State.gameOver)UI.ShowGameOver();}
        if(!SmithingLoop.Instance.InShop&&!GameUIController.BlocksGameplayInput)
        {
            State.playSeconds+=Time.deltaTime;explorationClock+=Time.deltaTime;
            if(explorationClock>.25f){explorationClock=0;Exploration.Reveal(MapPosition,State);}
            if(Player.position.y< -125)health.TakeDamage(health.MaxAssimilation);
        }
        saveClock+=Time.unscaledDeltaTime;
        if(saveClock>30){saveClock=0;SmithingLoop.Instance.RequestAutosave();}
    }
    public void Capture() => Capture(State);
    // The smithy is physically elsewhere in this scene but belongs to its town façade on the map.
    public Vector2 MapPosition
    {
        get
        {
            if(SmithyInterior.Instance?.Inside==true)
            {
                if(townScene?.blackSmith!=null)return townScene.blackSmith.position;
                foreach(var place in FindObjectsByType<CampaignWorldObject>(FindObjectsSortMode.None))
                    if(place.kind==CampaignObjectKind.Smithy)return place.transform.position;
            }
            return Player!=null?(Vector2)Player.position:Vector2.zero;
        }
    }
    public void Capture(CampaignState target)
    {
        if(!Ready||health.IsDead||target==null)return;
        target.sceneName=gameObject.scene.name;target.hasPosition=true;target.x=Player.position.x;target.y=Player.position.y;target.health=SmithingLoop.Instance.InShop?SmithingLoop.Instance.SmithData.hp:health.RemainingHealth;target.inTown=InTown;
    }
    public void SmithSnapshotChanged()
    {
        if(!Ready)return;
        int day=SmithingLoop.Instance.SmithData.day;
        if(day>knownDay)
        {Economy.Morning();knownDay=day;if(State.gameOver)pendingGameOver=true;}
    }
    public void EnterRegion(CampaignRegion region)
    {
        if(!Ready||SmithingLoop.Instance.InShop)return;
        if(Camera.main!=null)Camera.main.backgroundColor=region.background;
    }
    public void Teleport(Vector2 position)
    {
        if(Player==null)return;
        Vector3 before=Player.position;Player.position=new Vector3(position.x,position.y,before.z);
        if(townScene!=null&&position==townSpawn)
        {
            position=FieldSceneTravel.GroundedArrival(Player,position);
            Player.position=new Vector3(position.x,position.y,before.z);
        }
        townScene?.PrepareCamera(position);
        Unity.Cinemachine.CinemachineCore.OnTargetObjectWarped(Player,Player.position-before);
        foreach(var camera in FindObjectsByType<Unity.Cinemachine.CinemachineVirtualCameraBase>(FindObjectsSortMode.None))camera.PreviousStateIsValid=false;
        var rb=Player.GetComponent<Rigidbody2D>();if(rb!=null){rb.position=position;rb.linearVelocity=Vector2.zero;}
        Player.GetComponent<PlayerMovement>()?.ResetAfterTeleport();
        Player.GetComponent<PlayerInputHandler>()?.ClearGameplayInput();
        Physics2D.SyncTransforms();
        var cam=Camera.main;if(cam!=null)cam.transform.position=new Vector3(position.x,position.y+3,cam.transform.position.z);
    }
    public void TransitionTo(Vector2 position)
    {if(Ready&&!Travelling)transition.Begin(()=>Teleport(position),()=>SmithingLoop.Instance.RequestAutosave());}
    public void RequestReturn()
    {
        if(!Ready||Travelling||UI.IsOpen||SmithingLoop.Instance.Transitioning)return;
        UI.Confirm("마을로 돌아갈까요?","채집한 물품을 가방에 담은 채 마을로 복귀합니다.",ReturnTown);
    }
    public void ReturnTown()
    {
        if(Travelling)return;UI.Close();
        transition.Begin(()=>{SmithingLoop.Instance.StoreFieldMaterials();Teleport(townSpawn);},()=>SmithingLoop.Instance.RequestAutosave());
    }
    // 핵심 분기: !Ready||Travelling||SmithingLoop.Instance.Transitioning||UI.IsOpen||State.gameOver 판정.
    // 상태 변경: State.warpUnlocked 갱신.
    // 다음 연결: CampaignUI.Message(string, string) 호출.
    public void Interact(CampaignWorldObject obj)
    {
        if(!Ready||Travelling||SmithingLoop.Instance.Transitioning||UI.IsOpen||State.gameOver)return;
        switch(obj.kind)
        {
            case CampaignObjectKind.ForestGate:
                if(SmithingLoop.Instance.SmithData.night){UI.Message("밤에는 제작 시간입니다.","대장간 침대에서 다음 날 아침을 맞이하세요.");return;}
                if(Economy.DebtDue){UI.ShowDebt();return;}
                if(townScene!=null)
                {
                    string prompt=Inventory.Items.Count>0
                        ?"가방에 물건이 있습니다. 숲으로 이동하시겠습니까?"
                        :null;
                    if(FieldTravelPrompt.Active==null||!FieldTravelPrompt.Active.Open(FieldSceneTravel.BeginToField,prompt))
                        UI.Message("이동할 수 없습니다.","이동 확인창을 확인하세요.");
                    break;
                }
                UI.Confirm("숲으로 출발할까요?",Inventory.Items.Count>0?"가방에 물건이 들어 있습니다. 그대로 가져갑니다.":"낮 동안 자원을 모으고 돌아오세요.",()=>
                {
                    UI.Close();
                    transition.Begin(()=>{SmithingLoop.Instance.ExportCarriedBag();Teleport(fieldSpawn);},()=>SmithingLoop.Instance.RequestAutosave());
                });break;
            case CampaignObjectKind.ReturnGate:RequestReturn();break;
            case CampaignObjectKind.WindEntrance:UI.Confirm("바람 신전으로 들어갈까요?","바람 구역에서는 이동 속도가 10% 증가합니다.",()=>{UI.Close();Teleport(windSpawn);});break;
            case CampaignObjectKind.WindExit:UI.Confirm("동굴로 돌아갈까요?","깊은 동굴 입구로 이동합니다.",()=>{UI.Close();Teleport(new Vector2(199,-89));});break;
            case CampaignObjectKind.Smithy:
                if(Economy.DebtDue)UI.ShowDebt();else if(SmithyInterior.Instance!=null)SmithyInterior.Instance.Enter();else SmithingLoop.Instance.Travel();break;
            case CampaignObjectKind.EquipmentShop:UI.ShowDialogue("equipment");break;
            case CampaignObjectKind.PawnShop:UI.ShowDialogue("pawnSell");break;
            case CampaignObjectKind.FacilityShop:UI.ShowDialogue("facility");break;
            case CampaignObjectKind.Delivery:SmithingLoop.Instance.StoreFieldMaterials();UI.ShowStock("delivery");break;
            case CampaignObjectKind.Warp:
                if(!State.warpUnlocked){UI.Message("워프석이 잠겨 있습니다.","동굴의 상자에서 바람의 결정을 찾아 주세요.");return;}
                if(!State.unlockedWarps.Contains(obj.stableId))State.unlockedWarps.Add(obj.stableId);
                UI.ShowMap(true);SmithingLoop.Instance.RequestAutosave();break;
            case CampaignObjectKind.Treasure:
                if(State.openedChests.Contains(obj.stableId))return;
                State.openedChests.Add(obj.stableId);State.warpUnlocked=true;
                UI.Message("바람의 결정을 얻었습니다.","워프석을 사용할 수 있습니다. 결정은 사망해도 잃지 않습니다.");SmithingLoop.Instance.RequestAutosave();break;
        }
    }
    public void ApplyUpgrades()
    {
        Inventory.ConfigureCapacity(CampaignRules.BagCapacityKg(State.bagTier),0f);
        if(State.toolsLinked){SmithingLoop.Instance.ApplyGatheringTools();return;}
        var tools=Player.GetComponent<PlayerToolController>();
        for(int i=0;i<tools.ToolSlotCount;i++)
        {
            var tool=tools.GetToolAtSlot(i);if(tool==null||tool.IsWeapon)continue;
            int tier=tool.ToolType==ToolType.Axe?State.axeTier:State.pickTier;
            if(tool.Tier==tier)continue;
            var copy=Instantiate(tool);copy.ConfigureTier(tier);tools.SetToolSlot(i,copy);upgradedTools.Add(copy);
        }
    }
    public void BossDefeated()
    {
        bool first=!State.windBossDefeated;State.windBossDefeated=true;
        InventoryService.Add(SmithingLoop.Instance.SmithData.chest,new Blacksmith.Stack("wind_feather",first?5:7));
        State.lastReceipt=first?"바람의 파수꾼 첫 처치 · 순풍의 결정과 깃털 5개 · 마을 워프 해금":"바람의 파수꾼 처치 · 깃털 7개";
        SmithingLoop.Instance.RequestAutosave();UI.Message("바람의 파수꾼 처치",State.lastReceipt);
    }
    public void SpiritDefeated(Vector3 position)
    {
        var spawner=FindFirstObjectByType<ItemDropSpawner>();
        if(spawner!=null&&windParticleDrop!=null)spawner.Spawn(windParticleDrop,position,3);
    }
    void OnDeath()
    {
        Inventory.RestoreSnapshot(Array.Empty<InventoryItem>());
        State.gold-=Mathf.FloorToInt(State.gold*.04f);State.health=health.MaxAssimilation;SmithingLoop.Instance.SmithData.hp=health.MaxAssimilation;State.x=townSpawn.x;State.y=townSpawn.y;State.hasPosition=true;State.inTown=true;
        SmithingLoop.Instance.RequestAutosave();
    }
    public void Commit(){ApplyUpgrades();SmithingLoop.Instance.RequestAutosave();}
    void OnDestroy(){if(health!=null)health.Died-=OnDeath;foreach(var t in upgradedTools)if(t!=null)Destroy(t);if(Instance==this)Instance=null;}
}
