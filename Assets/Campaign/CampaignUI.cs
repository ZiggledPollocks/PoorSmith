using System;
using System.Linq;
using Blacksmith;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class CampaignUI : MonoBehaviour
{
    public BlacksmithView view;
    public Button rowPrefab;
    public Sprite mapPlayer,mapWarp,mapAltar,mapEntrance;
    public Sprite tradeBag;
    CampaignController owner;
    CampaignTradeUI trade;
    public TownDialogue Npc {get;private set;}
    TownStorageUI storage;
    RectTransform hud,modal,body;
    Button menuLoad;
    TMP_Text hudText,details;
    float previousTime,shownGold;
    bool previousExternal;
    string stockMode;
    Blacksmith.Stack selected;
    int quantity=1;
    Action dismiss;
    TMP_Text dialogue;float dialogueClock;
    RectTransform mapPlayerMarker;
    Bounds mapWorldBounds;
    bool showingMap;
    public bool IsOpen=>modal!=null&&modal.gameObject.activeSelf;
    public void Build(CampaignController c)
    {
        owner=c;trade=gameObject.AddComponent<CampaignTradeUI>();trade.Initialize(c,this);
        Npc=gameObject.AddComponent<TownDialogue>();Npc.Initialize(c,this);
        storage=gameObject.AddComponent<TownStorageUI>();storage.Initialize(c,this);
        var go=new GameObject("CampaignCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));go.transform.SetParent(transform,false);
        var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=1100;
        var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);
        hud=view.Full("CampaignHUD",go.transform);
        hudText=view.Text("DateGoldDebt",hud,"",23,new(.02f,.92f),new(.74f,.99f));
        view.Button("MapButton",hud,"지도",new(.75f,.93f),new(.82f,.995f),()=>{if(!GameUIController.BlocksGameplayInput)ShowMap(false);});
        menuLoad=view.Button("MainMenuSaves",go.transform,"저장 / 불러오기",new(.83f,.93f),new(.985f,.995f),ShowSaves);
        gameObject.AddComponent<TownHud>().Initialize(c,this,go.transform);
        modal=view.Full("CampaignModal",go.transform);modal.gameObject.SetActive(false);
    }
    void Update()
    {
        if(owner==null||!owner.Ready)return;
        if(dialogue!=null){dialogueClock+=Time.unscaledDeltaTime*30;dialogue.maxVisibleCharacters=Keyboard.current?.spaceKey.wasPressedThisFrame==true?int.MaxValue:Mathf.Max(dialogue.maxVisibleCharacters,(int)dialogueClock);}
        shownGold=Mathf.MoveTowards(shownGold,owner.State.gold,Time.unscaledDeltaTime*Mathf.Max(40,Mathf.Abs(owner.State.gold-shownGold)*6));
        var s=SmithingLoop.Instance.SmithData;
        hudText.text=$"{Mathf.RoundToInt(shownGold)} G\n{(owner.rules.provisional?"미정 가격·장비 수치: 임시 밸런스 적용":"")}";
        var combat=owner.Player.GetComponent<CampaignCombat>();
        if(combat!=null)hudText.text+=$"    화살 {combat.ArrowCount}";
        hud.gameObject.SetActive(!owner.InTown&&!SmithingLoop.Instance.InShop&&!GameUIController.BlocksGameplayInput);
        menuLoad.gameObject.SetActive(GameUIController.Instance!=null&&(GameUIController.Instance.MainMenuVisible||GameUIController.Instance.SettingsVisible)&&!IsOpen&&!SmithingLoop.Instance.InShop);
        if(showingMap&&IsOpen)
            ExplorationMapLayout.MoveMarker(mapPlayerMarker,owner.MapPosition,mapWorldBounds);
        if(IsOpen&&Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame&&!owner.State.gameOver)Close();
    }
    public RectTransform Open(string title)
    {
        showingMap=false;mapPlayerMarker=null;
        if(!IsOpen){previousTime=Time.timeScale;previousExternal=GameUIController.ExternalActivity;}
        dialogue=null;dismiss=null;Time.timeScale=0;GameUIController.ExternalActivity=true;modal.gameObject.SetActive(true);BlacksmithView.Clear(modal);
        var dim=modal.gameObject.GetComponent<Image>()??modal.gameObject.AddComponent<Image>();dim.color=new Color(0,0,0,.8f);dim.raycastTarget=true;
        body=view.Panel("Paper",modal,new(.10f,.10f),new(.90f,.89f),true);
        view.Text("Title",body,title,36,new(.04f,.86f),new(.86f,.97f),BlacksmithView.Ink);
        view.Button("Close",body,"뒤로",new(.86f,.88f),new(.97f,.97f),Close);
        return body;
    }
    public void Close()
    {
        if(!IsOpen)return;
        if(owner!=null&&owner.State.gameOver)return;
        if(Npc!=null&&Npc.HandleClose())return;
        modal.gameObject.SetActive(false);Time.timeScale=previousTime;GameUIController.ExternalActivity=previousExternal;
        showingMap=false;mapPlayerMarker=null;
        owner?.Player.GetComponent<PlayerInputHandler>()?.ClearGameplayInput();
        var cancelled=dismiss;dismiss=null;cancelled?.Invoke();
    }
    public void Message(string title,string text)
    {var p=Open(title);view.Text("Message",p,text,28,new(.08f,.25f),new(.92f,.80f),BlacksmithView.Ink);}
    public void Confirm(string title,string text,Action yes,Action no=null)
    {
        var p=Open(title);view.Text("Question",p,text,28,new(.08f,.34f),new(.92f,.78f),BlacksmithView.Ink);
        dismiss=no;view.Button("ConfirmYes",p,"확인",new(.18f,.10f),new(.45f,.25f),()=>{dismiss=null;yes();});
        view.Button("ConfirmNo",p,"취소",new(.55f,.10f),new(.82f,.25f),Close);
    }
    public void ShowQuit()
    {
        var p=Open("게임을 종료할까요?");
        view.Text("QuitWarning",p,"저장하지 않고 종료하면 마지막 저장 이후의 진행을 잃습니다.",28,new(.08f,.45f),new(.92f,.8f),BlacksmithView.Ink);
        view.Button("SaveAndQuit",p,"저장 후 종료",new(.08f,.18f),new(.47f,.35f),()=>{if(SmithingLoop.Instance.SaveSlot(0))SmithingLoop.Instance.QuitCampaign();else Message("저장 실패","기존 저장을 보존했습니다. 종료하지 않았습니다.");});
        view.Button("QuitWithoutSave",p,"저장 없이 종료",new(.53f,.18f),new(.92f,.35f),()=>SmithingLoop.Instance.QuitCampaign());
    }
    Button Row(Transform parent,string label,Action click)
    {
        var row=Instantiate(rowPrefab,parent);row.name=label;row.gameObject.SetActive(true);
        row.GetComponentInChildren<TMP_Text>().text=label;row.onClick.RemoveAllListeners();row.onClick.AddListener(()=>click());return row;
    }
    public void ShowShop(string kind)
    {
        if(kind!="facility"){trade.ShowShop();return;}
        var p=Open("시설 상점");view.Text("ComingSoon",p,"상품 준비 중\n시설 상품과 효과는 기획 확정 후 제공됩니다.",30,new(.1f,.25f),new(.9f,.8f),BlacksmithView.Ink);
    }
    public void ShowDialogue(string kind)=>Npc.Begin(kind);
    public void RecordTrade()=>Npc.RecordTrade();
    public void ShowTownInventory(){SmithingLoop.Instance.ImportCarriedBag();storage.Show(false);}
    public void ShowStock(string mode)
    {if(mode.StartsWith("pawn"))trade.ShowPawn(mode);else storage.Show(true);}
    public void ShowDebt()
    {Action settle=()=>{owner.Economy.PayDebt(new System.Random());owner.Commit();if(owner.State.gameOver)ShowGameOver();else Message("정산 결과",owner.State.lastReceipt);};Confirm("빚쟁이가 찾아왔습니다",$"이번 주 청구: {owner.rules.weeklyDebt+owner.State.debtCarry}G (기본 금액 임시)\n부족하면 보관함·가방·납품 물품을 무작위로 압류합니다. 경고 3회면 게임 오버입니다. 거절해도 정산됩니다.",settle,settle);}
    public void ShowGameOver(){Message("게임 오버","빚 경고가 3회 누적되었습니다. 이전 저장 슬롯을 불러올 수 있습니다.");view.Button("RecoverySaves",body,"저장 슬롯",new(.3f,.13f),new(.7f,.27f),ShowSaves);}
    public void ShowSaves()
    {
        if(owner==null||!owner.Ready||!SmithingLoop.Instance.CanSnapshot)return;
        var p=Open("저장 / 불러오기");var list=view.Scroll(p,"Slots",new(.04f,.19f),new(.95f,.82f));
        for(int i=0;i<5;i++)
        {
            int slot=i;string info=SmithingLoop.Instance.SlotInfo(slot);
            Row(list,$"{(slot==0?"자동 저장":"저장 "+slot)} · {info}",()=>ShowSlot(slot,info));
        }
    }
    void ShowSlot(int slot,string info)
    {
        var p=Open(slot==0?"자동 저장":"저장 "+slot);view.Text("SlotInfo",p,info,30,new(.1f,.5f),new(.9f,.8f),BlacksmithView.Ink);
        view.Button("WriteSlot",p,"저장 / 덮어쓰기",new(.12f,.2f),new(.46f,.37f),()=>Confirm("이 슬롯에 저장할까요?","기존 슬롯이 있으면 백업 후 교체합니다.",()=>{bool ok=SmithingLoop.Instance.SaveSlot(slot);Message("저장",ok?"저장했습니다.":"저장 실패 · 기존 파일 보존");}));
        view.Button("ReadSlot",p,"불러오기",new(.54f,.2f),new(.88f,.37f),()=>Confirm("불러올까요?","현재 저장하지 않은 진행은 교체됩니다.",()=>{Close();if(!SmithingLoop.Instance.LoadSlot(slot))Message("불러오기 실패","없는 슬롯 또는 손상·미지원 저장입니다. 파일은 보존했습니다.");}));
    }
    public void ShowMap(bool warp)
    {
        var p=Open(warp?"워프석 지도":owner.InTown?"마을 지도":"탐험 지도");
        var map=view.Rect("MapArea",p,new(.06f,.16f),new(.94f,.83f));
        map.gameObject.AddComponent<Image>().color=new Color(.10f,.13f,.14f,.96f);
        mapWorldBounds=owner.townScene!=null&&owner.InTown
            ?owner.townScene.TownBounds
            :new Bounds(new Vector3(90f,-12.5f,0f),new Vector3(460f,205f,1f));
        ExplorationMapLayout.DrawVisited(map,owner.State.visitedCells,owner.Exploration.cellSize,
            mapWorldBounds,new Color(.45f,.62f,.45f,.88f),new Color(.56f,.50f,.40f,.90f),0f,-10f);
        ExplorationMapLayout.DrawTerrain(map,owner.Exploration,mapWorldBounds,
            new Color(.26f,.24f,.20f,.85f));
        foreach(var landmark in FindObjectsByType<CampaignWorldObject>(FindObjectsSortMode.None))
        {
            if(!owner.Exploration.IsExplored(landmark.transform.position))continue;
            bool isWarp=landmark.kind==CampaignObjectKind.Warp;
            if(warp&&!isWarp)continue;
            if(!ExplorationMapLayout.Contains(mapWorldBounds,landmark.transform.position))continue;
            if(!warp)
            {
                string glyph=landmark.kind switch
                {
                    CampaignObjectKind.Smithy=>"대",
                    CampaignObjectKind.EquipmentShop=>"상",
                    CampaignObjectKind.PawnShop=>"전",
                    CampaignObjectKind.FacilityShop=>"시",
                    CampaignObjectKind.Warp=>"워",
                    CampaignObjectKind.WindEntrance=>"입",
                    _=>"문"
                };
                Sprite sprite=isWarp?mapWarp:landmark.kind==CampaignObjectKind.Smithy?null:mapEntrance;
                ExplorationMapLayout.Marker(map,"Marker_"+landmark.stableId,landmark.transform.position,
                    mapWorldBounds,glyph,new Color(.88f,.68f,.32f),view.font,sprite);
                continue;
            }
            var pos=ExplorationMapLayout.Project(landmark.transform.position,mapWorldBounds);var captured=landmark;
            var button=view.Button("Marker_"+landmark.stableId,map,"",pos-new Vector2(.012f,.02f),pos+new Vector2(.012f,.02f),()=>
            {
                Confirm("워프 이동 확인",captured.displayName+"으로 이동할까요?",()=>{Close();owner.Teleport((Vector2)captured.transform.position+Vector2.up);owner.Commit();},()=>ShowMap(true));
            });
            button.image.sprite=isWarp?mapWarp:mapEntrance;button.image.color=Color.white;button.image.preserveAspect=true;button.gameObject.AddComponent<UiHoverOutline>();
            button.interactable=owner.State.unlockedWarps.Contains(landmark.stableId);
        }
        foreach(var altar in FindObjectsByType<AssissZone>(FindObjectsSortMode.None))
        {
            if(warp||!owner.Exploration.IsExplored(altar.transform.position))continue;
            ExplorationMapLayout.Marker(map,"Altar",altar.transform.position,mapWorldBounds,
                "석",new Color(.70f,.88f,.81f),view.font,mapAltar);
        }
        mapPlayerMarker=ExplorationMapLayout.Marker(map,"PlayerPosition",owner.MapPosition,
            mapWorldBounds,"나",new Color(.99f,.82f,.30f),view.font,mapPlayer);
        if(mapPlayerMarker!=null)mapPlayerMarker.SetAsLastSibling();
        showingMap=true;
        view.Text("Legend",p,"현재 위치 · 발견한 시설 · 석상 · 입구  |  방문한 구역만 표시",19,new(.05f,.04f),new(.75f,.12f),BlacksmithView.Ink);
        if(warp)view.Button("HomeWarp",p,owner.State.windBossDefeated?"마을로":"마을 워프 미해금",new(.74f,.03f),new(.97f,.12f),()=>{if(owner.State.windBossDefeated)Confirm("마을로 워프할까요?","채집물을 보관함으로 옮깁니다.",owner.ReturnTown);}).interactable=owner.State.windBossDefeated;
    }
    void OnDestroy(){if(IsOpen){Time.timeScale=previousTime;GameUIController.ExternalActivity=previousExternal;}}
}
