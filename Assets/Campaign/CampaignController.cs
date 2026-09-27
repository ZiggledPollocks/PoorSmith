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
    public CampaignRules rules;
    public GameObject windParticleDrop;
    public Vector2 townSpawn=new(-100,3),fieldSpawn=new(5,3),windSpawn=new(243,3);
    public CampaignUI UI {get;private set;}
    public CampaignExploration Exploration {get;private set;}
    public Transform Player {get;private set;}
    public CampaignState State=>SmithingLoop.Instance.Campaign;
    public CampaignEconomy Economy=>new(State,SmithingLoop.Instance.SmithData,SmithingLoop.Instance.Catalog,rules);
    public bool InTown=>Player!=null&&Player.position.x< -50;
    public bool InWind=>Player!=null&&Player.position.x>230;
    public bool Ready {get;private set;}
    public float MovementMultiplier=>1+(InWind?.1f:0)+(Inventory.CurrentWeight>Inventory.settingsWeight?-.1f:0)+(Player.GetComponent<CampaignCombat>()?.WindArmorBonus??0);
    public InventorySystem Inventory {get;private set;}
    PlayerAssimilate health;
    float explorationClock,saveClock;
    readonly List<ToolData> upgradedTools=new();
    int knownDay;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void ResetStatics(){Instance=null;}
    void Awake(){Instance=this;}
    IEnumerator Start()
    {
        while(SmithingLoop.Instance==null||!SmithingLoop.Instance.Initialized)yield return null;
        Inventory=FindFirstObjectByType<InventorySystem>();Player=Inventory.transform;health=Player.GetComponent<PlayerAssimilate>();
        UI=GetComponent<CampaignUI>();Exploration=GetComponent<CampaignExploration>();
        State.delivery??=new();State.pawnStock??=new();State.openedChests??=new();State.usedAltars??=new();State.visitedCells??=new();State.unlockedWarps??=new();
        Exploration.Restore(State.visitedCells);knownDay=SmithingLoop.Instance.SmithData.day;
        Teleport(State.hasPosition?new Vector2(State.x,State.y):townSpawn);
        health.Assimilate((State.hasPosition?Mathf.Max(1,State.health):health.MaxAssimilation)-health.CurrentAssimilation);
        health.Died+=OnDeath;ApplyUpgrades();Ready=true;UI.Build(this);
        if(State.gameOver)UI.ShowGameOver();
    }
    void Update()
    {
        if(!Ready)return;
        if(InTown&&!SmithingLoop.Instance.InShop&&!UI.IsOpen&&!GameUIController.BlocksGameplayInput&&Economy.DebtDue)UI.ShowDebt();
        if(!SmithingLoop.Instance.InShop&&!GameUIController.BlocksGameplayInput)
        {
            State.playSeconds+=Time.deltaTime;explorationClock+=Time.deltaTime;
            if(explorationClock>.25f){explorationClock=0;Exploration.Reveal(Player.position,State);}
            if(Player.position.y< -125)health.TakeDamage(health.MaxAssimilation);
        }
        saveClock+=Time.unscaledDeltaTime;
        if(saveClock>30){saveClock=0;SmithingLoop.Instance.RequestAutosave();}
    }
    public void Capture()
    {
        if(!Ready||health.IsDead)return;
        State.hasPosition=true;State.x=Player.position.x;State.y=Player.position.y;State.health=SmithingLoop.Instance.InShop?SmithingLoop.Instance.SmithData.hp:health.CurrentAssimilation;State.inTown=InTown;
    }
    public void SmithSnapshotChanged()
    {
        if(!Ready)return;
        int day=SmithingLoop.Instance.SmithData.day;
        if(day>knownDay){Economy.Morning();knownDay=day;}
    }
    public void EnterRegion(CampaignRegion region)
    {
        if(!Ready||SmithingLoop.Instance.InShop)return;
        if(Camera.main!=null)Camera.main.backgroundColor=region.background;
    }
    public void Teleport(Vector2 position)
    {
        if(Player==null)return;
        Player.position=position;var rb=Player.GetComponent<Rigidbody2D>();if(rb!=null){rb.position=position;rb.linearVelocity=Vector2.zero;}
        Player.GetComponent<PlayerInputHandler>()?.ClearGameplayInput();
        Physics2D.SyncTransforms();
        var cam=Camera.main;if(cam!=null)cam.transform.position=new Vector3(position.x,position.y+3,cam.transform.position.z);
    }
    public void RequestReturn()
    {
        if(!Ready||UI.IsOpen||SmithingLoop.Instance.Transitioning)return;
        UI.Confirm("마을로 돌아갈까요?","채집한 재료는 보관함에 넣고 마을로 복귀합니다.",ReturnTown);
    }
    public void ReturnTown()
    {
        UI.Close();SmithingLoop.Instance.StoreFieldMaterials();Teleport(townSpawn);SmithingLoop.Instance.RequestAutosave();
    }
    public void Interact(CampaignWorldObject obj)
    {
        if(!Ready||UI.IsOpen||State.gameOver)return;
        switch(obj.kind)
        {
            case CampaignObjectKind.ForestGate:
                if(SmithingLoop.Instance.SmithData.night){UI.Message("밤에는 제작 시간입니다.","대장간 침대에서 다음 날 아침을 맞이하세요.");return;}
                if(Economy.DebtDue){UI.ShowDebt();return;}
                UI.Confirm("숲으로 출발할까요?",Inventory.Items.Count>0?"가방에 물건이 들어 있습니다. 그대로 가져갑니다.":"낮 동안 자원을 모으고 돌아오세요.",()=>{UI.Close();Teleport(fieldSpawn);SmithingLoop.Instance.RequestAutosave();});break;
            case CampaignObjectKind.ReturnGate:RequestReturn();break;
            case CampaignObjectKind.WindEntrance:UI.Confirm("바람 신전으로 들어갈까요?","바람 구역에서는 이동 속도가 10% 증가합니다.",()=>{UI.Close();Teleport(windSpawn);});break;
            case CampaignObjectKind.WindExit:UI.Confirm("동굴로 돌아갈까요?","깊은 동굴 입구로 이동합니다.",()=>{UI.Close();Teleport(new Vector2(199,-89));});break;
            case CampaignObjectKind.Smithy:
                if(Economy.DebtDue)UI.ShowDebt();else SmithingLoop.Instance.Travel();break;
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
        Inventory.ConfigureCapacity(rules.bagBaseCapacity+(State.bagTier-1)*rules.capacityPerTier,.05f);
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
        State.gold-=Mathf.FloorToInt(State.gold*.04f);State.health=health.MaxAssimilation;State.x=townSpawn.x;State.y=townSpawn.y;State.hasPosition=true;State.inTown=true;
        SmithingLoop.Instance.RequestAutosave();StartCoroutine(DeathReturn());
    }
    IEnumerator DeathReturn()
    {
        yield return new WaitForSecondsRealtime(2.5f);
        UI.Confirm("마을에서 회복할까요?","채집 가방을 잃고 치료비로 보유 금액의 4%를 지불했습니다. 보상 결정은 유지됩니다.",()=>
        {
            UI.Close();health.Assimilate(health.MaxAssimilation);Teleport(townSpawn);GameUIController.Instance?.Play();SmithingLoop.Instance.RequestAutosave();
        });
    }
    public void Commit(){ApplyUpgrades();SmithingLoop.Instance.RequestAutosave();}
    void OnDestroy(){if(health!=null)health.Died-=OnDeath;foreach(var t in upgradedTools)if(t!=null)Destroy(t);if(Instance==this)Instance=null;}
}
