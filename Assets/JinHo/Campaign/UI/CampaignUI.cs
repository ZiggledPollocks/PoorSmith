// [코드 지도] CampaignUI: 캠페인의 일시정지·저장·지도 등 모달 화면을 조정한다.
// 주요 함수: ShowMap, Build, Update
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/Campaign/UI/CampaignUI.cs.md

using System;
using System.Linq;
using Blacksmith;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class CampaignUI : MonoBehaviour
{
    GameUIController.Activity pauseActivity;
    public BlacksmithView view;
    public Button rowPrefab;
    public Sprite mapPlayer,mapWarp,mapAltar,mapEntrance;
    public Sprite tradeBag;
    CampaignController owner;
    CampaignTradeUI trade;
    public TownDialogue Npc {get;private set;}
    TownStorageUI storage;
    RectTransform hud,modal,body;
    string activeLayoutName;
    TMP_Text hudText,details;
    float previousTime;
    bool previousExternal;
    string stockMode;
    Blacksmith.Stack selected;
    int quantity=1;
    Action dismiss;
    bool debtCollectorActive;
    TMP_Text dialogue;float dialogueClock;
    RectTransform mapPlayerMarker;
    Bounds mapWorldBounds;
    bool showingMap;
    float nextTextContainment;
    public bool IsOpen=>modal!=null&&modal.gameObject.activeSelf;
    public void Build(CampaignController c)
    {
        owner=c;trade=gameObject.AddComponent<CampaignTradeUI>();trade.Initialize(c,this);
        PlayerInfoUI.Attach(c.Player.gameObject,view.font,view.sprites);
        Npc=gameObject.AddComponent<TownDialogue>();Npc.Initialize(c,this);
        storage=gameObject.AddComponent<TownStorageUI>();storage.Initialize(c,this);
        var go=new GameObject("CampaignCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));go.transform.SetParent(transform,false);
        var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=1100;
        var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);
        hud=view.Full("CampaignHUD",go.transform);
        hudText=view.Text("DateGoldDebt",hud,"",23,new(.02f,.92f),new(.74f,.99f));
        view.Button("MapButton",hud,"지도",new(.75f,.93f),new(.82f,.995f),()=>{if(!GameUIController.BlocksGameplayInput)ShowMap(false);});
        gameObject.AddComponent<TownHud>().Initialize(c,this,go.transform);
        modal=view.Full("CampaignModal",go.transform);modal.gameObject.SetActive(false);
    }
    void Update()
    {
        if(owner==null||!owner.Ready)return;
        if(dialogue!=null){dialogueClock+=Time.unscaledDeltaTime*30;dialogue.maxVisibleCharacters=ReleaseInput.Pressed("DialogueAdvance")?int.MaxValue:Mathf.Max(dialogue.maxVisibleCharacters,(int)dialogueClock);}
        var s=SmithingLoop.Instance.SmithData;
        hudText.text=$"{owner.Economy.GoldText} G\n{(owner.rules.provisional?"미정 가격·장비 수치: 임시 밸런스 적용":"")}";
        var combat=owner.Player.GetComponent<CampaignCombat>();
        if(combat!=null)hudText.text+=$"    화살 {combat.ArrowCount}";
        hud.gameObject.SetActive(!StoryTutorial.HideGameplayHud&&!owner.InTown&&!SmithingLoop.Instance.InShop&&!GameUIController.BlocksGameplayInput);
        if(showingMap&&IsOpen)
            ExplorationMapLayout.MoveMarker(mapPlayerMarker,owner.MapPosition,mapWorldBounds);
        // Campaign modals consume E while they block GameUIController. Only the
        // town inventory treats that input as a close request.
        bool inventoryToggle = IsOpen &&
            owner.Player.GetComponent<PlayerInputHandler>()?.ConsumeInventoryToggleInput() == true;
        if(inventoryToggle && activeLayoutName == "TownInventory")
        {
            Close();
            return;
        }
        if(IsOpen&&GameUIController.CancelPressed&&!owner.State.gameOver)GameUIController.HandleEscape();
    }
    void LateUpdate()
    {
        if (IsOpen && body != null)
            ScreenLayoutTemplate.Apply(activeLayoutName, body, true);
        if (IsOpen && Time.unscaledTime >= nextTextContainment)
        {
            nextTextContainment = Time.unscaledTime + .15f;
            TownFieldUiStyle.ContainText(modal);
            TownFieldUiStyle.PresentGameplay(modal);
        }
    }

    static string LayoutFor(string title)
    {
        if (title == "마을 인벤토리") return "TownInventory";
        if (title == "납품 상자") return "DeliveryBox";
        if (title.StartsWith("장비 상점 · 구매", StringComparison.Ordinal)) return "WeaponShopBuy";
        if (title.StartsWith("장비 상점 · 판매", StringComparison.Ordinal)) return "WeaponShopSell";
        if (title.StartsWith("전당포", StringComparison.Ordinal)) return "PawnShop";
        if (title == "시설 상점") return "FacilityShop";
        if (title.Contains("지도")) return "CampaignMap";
        if (title.Contains("저장") || title.Contains("불러오")) return "CampaignSaves";
        return "CampaignModal";
    }
    public RectTransform Open(string title, bool showBack = true)
    {
        using var measurement=ReleaseDiagnostics.Measure("UI_TOWN_PANEL");

        if(!IsOpen) GameSound.UI(title.Contains("지도")?"Map":title.Contains("상자")||title.Contains("보관")||title.Contains("납품")?"Chest":"Popup",this);
        activeLayoutName = LayoutFor(title);
        showingMap=false;mapPlayerMarker=null;
        if(!IsOpen)pauseActivity=GameUIController.Acquire(this,Close,true,true,500);
        dialogue=null;dismiss=null;modal.gameObject.SetActive(true);UIKeyboardSurface.Bind(this,modal,"Campaign/"+activeLayoutName,Close,500);BlacksmithView.Clear(modal);
        var dim=modal.gameObject.GetComponent<Image>()??modal.gameObject.AddComponent<Image>();dim.color=new Color(0,0,0,.8f);dim.raycastTarget=true;
        bool saveScreen=activeLayoutName=="CampaignSaves";
        body=view.Panel("Paper",modal,
            saveScreen?Vector2.zero:new Vector2(.10f,.10f),
            saveScreen?Vector2.one:new Vector2(.90f,.89f),!saveScreen);
        if (!saveScreen) TownFieldUiStyle.Frame(body.GetComponent<Image>());
        if(saveScreen)
        {
            body.GetComponent<Image>().color=new Color(.14f,.13f,.11f,1f);
            var outline=body.GetComponent<Outline>();
            if(outline!=null)outline.enabled=false;
        }
        view.Text("Title",body,title,36,new(.04f,.86f),new(.86f,.97f),
            saveScreen?Color.white:BlacksmithView.Ink);
        if(showBack)
        {
            var back=view.Button("Close",body,"뒤로",new(.86f,.88f),new(.97f,.97f),Close);
            if(saveScreen)StyleMenuButton(back,true);
        }
        return body;
    }
    // Copy the in-game main menu's button colors and typography onto save controls.
    void StyleMenuButton(Button button,bool selected=false)
    {
        if(button==null)return;
        var template=GameUIController.Instance?.MenuButtonTemplate;
        if(template!=null)
        {
            button.image.sprite=template.image.sprite;
            button.image.type=template.image.type;
            button.colors=template.colors;
        }
        button.image.color=selected?new Color(.79f,.63f,.37f,1f):
            template!=null?template.image.color:new Color(.40f,.32f,.23f,1f);
        var text=button.GetComponentInChildren<TMP_Text>(true);
        if(text==null)return;
        var source=template!=null?template.GetComponentInChildren<TMP_Text>(true):null;
        if(source!=null&&source.font!=null)text.font=source.font;
        RuntimeUIFactory.FitText(text, Mathf.Min(text.fontSize, 30f));
        text.color=selected?new Color(.11f,.09f,.07f):new Color(.95f,.89f,.77f);
        text.alignment=TextAlignmentOptions.Center;
    }
    public void Close()
    {
        if(!IsOpen)return;
        if(debtCollectorActive)return;
        if(owner!=null&&owner.State.gameOver)return;
        if(Npc!=null&&Npc.HandleClose())return;
        modal.gameObject.SetActive(false);pauseActivity?.Dispose();pauseActivity=null;
        showingMap=false;mapPlayerMarker=null;
        owner?.Player.GetComponent<PlayerInputHandler>()?.ClearGameplayInput();
        var cancelled=dismiss;dismiss=null;cancelled?.Invoke();
    }
    public void Message(string title,string text)
    {var p=Open(title);var message=view.Text("Message",p,text,28,new(.08f,.25f),new(.92f,.80f),
        activeLayoutName=="CampaignSaves"?Color.white:BlacksmithView.Ink);
        if (activeLayoutName != "CampaignSaves")
            TownFieldUiStyle.FrameBehind(message, new Color(.91f,.84f,.70f,.96f));
        if(title.Contains("바람의 결정") && GameArtSkin.Current != null)
            view.Image("WindCrystalReward",p,null,Color.white,new(.43f,.77f),new(.57f,.91f),true).sprite=GameArtSkin.Current.windCrystal;}
    public void Confirm(string title,string text,Action yes,Action no=null)
    {
        var p=Open(title);bool saveScreen=activeLayoutName=="CampaignSaves";
        var question=view.Text("Question",p,text,28,new(.08f,.34f),new(.92f,.78f),
            saveScreen?Color.white:BlacksmithView.Ink);
        if (!saveScreen) TownFieldUiStyle.FrameBehind(question, new Color(.91f,.84f,.70f,.96f));
        dismiss=no;
        var confirm=view.Button("ConfirmYes",p,"확인",new(.18f,.10f),new(.45f,.25f),()=>{dismiss=null;yes();});
        var cancel=view.Button("ConfirmNo",p,"취소",new(.55f,.10f),new(.82f,.25f),Close);
        if(saveScreen){StyleMenuButton(confirm);StyleMenuButton(cancel);}
    }
    public void ShowQuit()
    {
        var p=Open("게임을 종료할까요?");
        view.Text("QuitWarning",p,"저장하지 않고 종료하면 마지막 저장 이후의 진행을 잃습니다.",28,new(.08f,.45f),new(.92f,.8f),BlacksmithView.Ink);
        view.Button("SaveAndQuit",p,"저장 후 종료",new(.08f,.18f),new(.47f,.35f),()=>{if(SmithingLoop.Instance.SaveSlot(0))SmithingLoop.Instance.QuitCampaign();else SaveExitFailed();});
        view.Button("QuitWithoutSave",p,"저장 없이 종료",new(.53f,.18f),new(.92f,.35f),()=>
            Confirm("저장 없이 종료할까요?","마지막 저장 이후의 진행을 잃습니다.",
                ()=>SmithingLoop.Instance.QuitCampaign(),ShowQuit));
    }
    void SaveExitFailed()
    {
        ReleaseDecisionUI.For(gameObject).Show("저장하지 못했습니다. 기존 저장은 보존했습니다.","다시 저장","저장 없이 종료",
            ()=>{if(SmithingLoop.Instance.SaveSlot(0))SmithingLoop.Instance.QuitCampaign();else SaveExitFailed();},
            ()=>Confirm("저장 없이 종료할까요?","마지막 저장 이후 진행은 보존되지 않습니다.",()=>SmithingLoop.Instance.QuitCampaign(),ShowQuit),onEscape:()=>{});
    }
    Button Row(Transform parent,string label,Action click)
    {
        var row=Instantiate(rowPrefab,parent);row.name=label;row.gameObject.SetActive(true);
        row.GetComponentInChildren<TMP_Text>().text=label;row.onClick.RemoveAllListeners();row.onClick.AddListener(()=>click());
        StyleMenuButton(row);
        GameArtSkin.Button(row);
        return row;
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
    {Message("주간 청구 대기","대장간 출구에서 빚을 정산해야 숲으로 갈 수 있습니다.");}
    RectTransform DebtCollectorPanel()
    {
        var panel=Open("빚쟁이",false);
        activeLayoutName="DebtCollector";
        panel.anchorMin=new Vector2(.12f,.08f);
        panel.anchorMax=new Vector2(.88f,.45f);
        panel.anchoredPosition=Vector2.zero;
        panel.sizeDelta=Vector2.zero;
        panel.GetComponent<Image>().color=new Color(.15f,.14f,.13f,.98f);
        var title=panel.Find("Title")?.GetComponent<TMP_Text>();
        if(title!=null)title.color=BlacksmithView.Cream;
        return panel;
    }
    public void ShowDebtCollector()
    {
        if(IsOpen||owner==null||!owner.Economy.WeeklyCollectorNeeded)return;
        var panel=DebtCollectorPanel();
        debtCollectorActive=true;
        view.Text("CollectorSpeech",panel,
            $"돈은 준비됐겠지?\n주간 청구 후 잔액 {CampaignEconomy.FormatMilli(owner.Economy.WeeklyBalanceAfterChargeMilli)} G",
            32,new(.06f,.30f),new(.70f,.79f),BlacksmithView.Cream);
        StyleMenuButton(view.Button("CollectorYes",panel,"넵",new(.73f,.57f),new(.95f,.79f),()=>ShowDebtCollectorReply(true)));
        StyleMenuButton(view.Button("CollectorNo",panel,"아니요...",new(.73f,.30f),new(.95f,.52f),()=>ShowDebtCollectorReply(false)));
    }
    void ShowDebtCollectorReply(bool agreed)
    {
        var panel=DebtCollectorPanel();
        view.Text("CollectorReply",panel,agreed?"좋아. 이번 주 빚을 정산하자.":"털면 1골드 하나 안 나오나. 확인해 보지.",
            32,new(.06f,.28f),new(.72f,.78f),BlacksmithView.Cream);
        StyleMenuButton(view.Button("CollectorContinue",panel,"계속",new(.73f,.34f),new(.95f,.58f),ResolveDebtCollector));
    }
    void ResolveDebtCollector()
    {
        int settled=owner.Economy.SettleOverdueDebt();
        if(settled>0)owner.Commit();
        var panel=DebtCollectorPanel();
        view.Text("CollectorResult",panel,owner.State.lastReceipt,27,
            new(.06f,.27f),new(.92f,.79f),BlacksmithView.Cream);
        StyleMenuButton(view.Button("CollectorDone",panel,"확인",new(.73f,.08f),new(.95f,.25f),()=>
        {
            debtCollectorActive=false;
            if(owner.State.gameOver)ShowGameOver();else Close();
        }));
    }
    public void ShowGameOver(){Message("게임 오버","빚 경고가 3회 누적되었습니다. 이전 저장 슬롯을 불러올 수 있습니다.");view.Button("RecoverySaves",body,"저장 슬롯",new(.3f,.13f),new(.7f,.27f),()=>ShowSaveTab(true));}
    public void ShowSaves()=>ShowSaveTab(false);
    void ShowSaveTab(bool loading)
    {
        if(owner==null||!owner.Ready||!SmithingLoop.Instance.CanSnapshot)return;
        var p=Open("저장 / 불러오기");
        var saveTab=view.Button("SaveTab",p,"저장",new(.15f,.72f),new(.47f,.83f),()=>ShowSaveTab(false));
        var loadTab=view.Button("LoadTab",p,"불러오기",new(.53f,.72f),new(.85f,.83f),()=>ShowSaveTab(true));
        StyleMenuButton(saveTab,!loading);
        StyleMenuButton(loadTab,loading);
        var list=view.Scroll(p,"Slots",new(.08f,.12f),new(.92f,.69f));
        for(int i=0;i<5;i++)
        {
            int slot=i;string info=SmithingLoop.Instance.SlotInfo(slot);
            var row=Row(list,$"{(slot==0?"자동 저장":"저장 "+slot)} · {info}",()=>ShowSlot(slot,info,loading));
            if(loading)row.interactable=info!="빈 슬롯"&&info!="미지원"&&info!="읽기 오류";
        }
        foreach(var image in list.parent.parent.GetComponentsInChildren<Image>(true))
        {
            if(image.name=="Scrollbar")image.color=new Color(.24f,.21f,.17f);
            else if(image.name=="Handle")image.color=new Color(.79f,.63f,.37f);
        }
    }
    void ShowSlot(int slot,string info,bool loading)
    {
        var p=Open((loading?"불러오기 · ":"저장 · ")+(slot==0?"자동 저장":"저장 "+slot));
        var back=p.Find("Close")?.GetComponent<Button>();
        if(back!=null){back.onClick.RemoveAllListeners();back.onClick.AddListener(()=>ShowSaveTab(loading));}
        view.Text("SlotInfo",p,info,30,new(.1f,.47f),new(.9f,.70f),Color.white,TextAlignmentOptions.Center);
        if(loading)
        {
            var action=view.Button("ReadSlot",p,"불러오기",new(.30f,.21f),new(.70f,.37f),()=>
                Confirm("불러올까요?","현재 저장하지 않은 진행은 교체됩니다.",()=>
                {Close();if(!SmithingLoop.Instance.LoadSlot(slot))Message("불러오기 실패","없는 슬롯 또는 손상·미지원 저장입니다. 파일은 보존했습니다.");}));
            StyleMenuButton(action);
        }
        else
        {
            var action=view.Button("WriteSlot",p,"저장 / 덮어쓰기",new(.30f,.21f),new(.70f,.37f),()=>
                Confirm("이 슬롯에 저장할까요?","기존 슬롯이 있으면 백업 후 교체합니다.",()=>
                {bool ok=SmithingLoop.Instance.SaveSlot(slot);Message("저장",ok?"저장했습니다.":"저장 실패 · 기존 파일 보존");}));
            StyleMenuButton(action);
        }
    }
    // 핵심 분기: !owner.Exploration.IsExplored(landmark.transform.position) 판정.
    // 상태 변경: map.gameObject.AddComponent<Image>().color 갱신.
    // 다음 연결: CampaignUI.Open(string) 호출.
    public void ShowMap(bool warp)
    {
        var p=Open(warp?"워프석 지도":owner.InTown?"마을 지도":"탐험 지도");
        var map=view.Rect("MapArea",p,new(.06f,.16f),new(.94f,.83f));
        var mapImage=map.gameObject.AddComponent<Image>();
        mapImage.color=new Color(.14f,.13f,.11f,.96f);
        TownFieldUiStyle.Frame(mapImage);
        map=UIKeyboardPanRegion.WrapMap(map);
        bool townSideView=owner.townScene!=null&&owner.InTown&&!warp;
        mapWorldBounds=townSideView
            ?ExplorationMapLayout.TownSideBounds(owner.townScene)
            :owner.townScene!=null&&owner.InTown
            ?owner.townScene.TownBounds
            :new Bounds(new Vector3(90f,-12.5f,0f),new Vector3(460f,205f,1f));
        if(townSideView)
        {
            map.gameObject.AddComponent<RectMask2D>();
            ExplorationMapLayout.DrawTownSideView(map,owner.Exploration,mapWorldBounds);
        }
        else
        {
            ExplorationMapLayout.DrawVisited(map,owner.State.visitedCells,owner.Exploration.cellSize,
                mapWorldBounds,new Color(.45f,.62f,.45f,.88f),new Color(.56f,.50f,.40f,.90f),0f,-10f);
            ExplorationMapLayout.DrawTerrain(map,owner.Exploration,mapWorldBounds,
                new Color(.26f,.24f,.20f,.85f));
        }
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
        var legend=view.Text("Legend",p,"현재 위치 · 발견한 시설 · 석상 · 입구  |  방문한 구역만 표시",19,
            new(.05f,.04f),warp?new(.71f,.12f):new(.75f,.12f),BlacksmithView.Ink);
        TownFieldUiStyle.FrameBehind(legend, new Color(.91f,.84f,.70f,.96f));
        if(warp)view.Button("HomeWarp",p,owner.State.windBossDefeated?"마을로":"마을 워프 미해금",new(.74f,.03f),new(.97f,.12f),()=>{if(owner.State.windBossDefeated)Confirm("마을로 워프할까요?","채집물을 보관함으로 옮깁니다.",owner.ReturnTown);}).interactable=owner.State.windBossDefeated;
    }
    void OnDisable(){pauseActivity?.Dispose();pauseActivity=null;}
    void OnDestroy(){pauseActivity?.Dispose();pauseActivity=null;}
}

// Shared text frames for town and gathering interaction screens.
public static class TownFieldUiStyle
{
    static readonly Color Border = new(.69f, .52f, .31f, .86f);
    static readonly Color GameplayCream=new(.95f,.89f,.77f,1);
    static readonly Color GameplayFace=new(.30f,.215f,.155f,1);
    static readonly Vector3[] WindowCorners=new Vector3[4];
    public static bool IsGameplayScreen(string name) => name switch
    {
        "FieldHUD" or "ToolHUD" or "AssimilationHUD" or "FieldBag" or "FieldEquipment" or
        "PlayerInfo" or "TownInventory" or "CampaignModal" or "CampaignMap" or "CampaignSaves" or
        "Offering" or "DeathScreen" or "SettingsMenu" or "QuickInteractionPrompt" or
        "ReleaseDecision" or "DeliveryBox" or "WeaponShopBuy" or "WeaponShopSell" or
        "PawnShop" or "FacilityShop" or "KeyboardFooterDefault" => true,
        _ => false
    };

    // 필드 화면만 공유하는 표현 규칙이다. 값·콜백·아이템 정렬·제작 화면은 소유하지 않는다.
    public static void PresentGameplay(Transform root)
    {
        if(root==null)return;
        var skin=GameArtSkin.Current;if(skin==null)return;
        foreach(var image in root.GetComponentsInChildren<UnityEngine.UI.Image>(true))
        {
            if(image==null||image.color.a<.1f||image.preserveAspect||image.type==UnityEngine.UI.Image.Type.Filled)continue;
            string n=image.name;
            bool slot=n.Contains("Slot")||n.StartsWith("Equip_",StringComparison.Ordinal)||n.StartsWith("Tool_",StringComparison.Ordinal);
            if(slot)continue;
            bool wood=image.sprite==skin.window||image.sprite==skin.panel||image.sprite==skin.inset||image.sprite==skin.tooltip||
                image.sprite==skin.button||image.sprite==skin.hover||image.sprite==skin.pressed||image.sprite==skin.disabled||
                image.sprite==skin.tab||image.sprite==skin.selectedTab||image.sprite==skin.input;
            if(!wood)
            {
                // 기존 대화창의 겹친 밝은 장식선은 얇은 황동색으로 낮춘다.
                if(image.sprite==null&&(n.EndsWith("Bottom",StringComparison.Ordinal)||n.EndsWith("Top",StringComparison.Ordinal)||
                    n.EndsWith("Left",StringComparison.Ordinal)||n.EndsWith("Right",StringComparison.Ordinal)))
                    image.color=new Color(.48f,.36f,.22f,image.color.a);
                continue; // 고유 지도·아이템·캐릭터·게이지·컷신 원본은 보존한다.
            }
            var button=image.GetComponent<UnityEngine.UI.Button>();
            if(button!=null)
            {
                bool selected=n.Contains("Selected")||image.sprite==skin.selectedTab;
                image.color=selected?new Color(.43f,.32f,.19f,image.color.a):new Color(GameplayFace.r,GameplayFace.g,GameplayFace.b,image.color.a);
                button.transition=UnityEngine.UI.Selectable.Transition.ColorTint;
                image.overrideSprite=null;
                var colors=button.colors;
                colors.normalColor=Color.white;colors.highlightedColor=new Color(1.18f,1.12f,1.02f,1);
                colors.selectedColor=colors.highlightedColor;colors.pressedColor=new Color(.80f,.74f,.66f,1);
                colors.disabledColor=new Color(.62f,.60f,.57f,1);colors.fadeDuration=.07f;button.colors=colors;
            }
            else if(image.GetComponent<UnityEngine.UI.Selectable>()==null)
            {
                bool inset=image.sprite==skin.inset||image.sprite==skin.input;
                var tint=inset?new Color(.24f,.205f,.17f,image.color.a):new Color(.34f,.28f,.22f,image.color.a);
                image.color=tint;
            }
            var outline=image.GetComponent<UnityEngine.UI.Outline>();
            if(outline!=null && button==null){outline.effectDistance=new Vector2(1,-1);outline.effectColor=new Color(.55f,.43f,.27f,.55f);}
        }
        foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if(text==null)continue;
            // 경고·품질·선택 등의 의미 있는 색은 유지하고, 짙은 잉크만 크림색으로 바꾼다.
            var c=text.color;
            if(Mathf.Max(c.r,c.g,c.b)<.5f || Mathf.Max(c.r,c.g,c.b)-Mathf.Min(c.r,c.g,c.b)<.22f || text.GetComponentInParent<UnityEngine.UI.Button>()!=null)
                text.color=new Color(GameplayCream.r,GameplayCream.g,GameplayCream.b,c.a);
            FullTextLayout.Bind(text);
        }
    }

    // 프레임만 화면의 안전 영역으로 옮긴다. 스크롤 내용과 월드 좌표 UI는 제외한다.
    public static void ClampWindow(RectTransform panel)
    {
        if(panel==null||!panel.gameObject.activeInHierarchy)return;
        var canvas=panel.GetComponentInParent<Canvas>();
        if(canvas==null||canvas.renderMode==RenderMode.WorldSpace)return;
        var screen=canvas.rootCanvas.transform as RectTransform;if(screen==null||panel==screen)return;
        var safe=Screen.safeArea;
        float width=Mathf.Max(1,Screen.width),height=Mathf.Max(1,Screen.height);
        float pad=12;
        var bounds=UnityEngine.Rect.MinMaxRect(screen.rect.xMin+screen.rect.width*safe.xMin/width+pad,
            screen.rect.yMin+screen.rect.height*safe.yMin/height+pad,
            screen.rect.xMin+screen.rect.width*safe.xMax/width-pad,
            screen.rect.yMin+screen.rect.height*safe.yMax/height-pad);
        panel.GetWorldCorners(WindowCorners);
        Vector2 lo=screen.InverseTransformPoint(WindowCorners[0]),hi=screen.InverseTransformPoint(WindowCorners[2]);
        if(hi.x-lo.x>bounds.width)panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,bounds.width);
        if(hi.y-lo.y>bounds.height)panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,bounds.height);
        panel.GetWorldCorners(WindowCorners);
        lo=screen.InverseTransformPoint(WindowCorners[0]);hi=screen.InverseTransformPoint(WindowCorners[2]);
        float dx=lo.x<bounds.xMin?bounds.xMin-lo.x:hi.x>bounds.xMax?bounds.xMax-hi.x:0;
        float dy=lo.y<bounds.yMin?bounds.yMin-lo.y:hi.y>bounds.yMax?bounds.yMax-hi.y:0;
        if(dx!=0||dy!=0)panel.position+=screen.TransformVector(new Vector3(dx,dy,0));
    }


    public static void Frame(Image image)
    {
        if (image == null) return;
        GameArtSkin.Panel(image);
        var outline = image.GetComponent<Outline>() ?? image.gameObject.AddComponent<Outline>();
        outline.effectColor = Border;
        outline.effectDistance = new Vector2(2f, -2f);
        outline.useGraphicAlpha = true;
    }

    public static void FrameBehind(TMP_Text text, Color fill)
    {
        if (text == null) return;
        var source = text.rectTransform;
        var frame = new GameObject(text.name + "Frame", typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)frame.transform;
        rect.SetParent(source.parent, false);
        rect.anchorMin = source.anchorMin;
        rect.anchorMax = source.anchorMax;
        rect.pivot = source.pivot;
        float padding=GameArtSkin.Current!=null?GameArtSkin.Current.textFramePadding:8;
        rect.offsetMin = source.offsetMin - Vector2.one*padding;
        rect.offsetMax = source.offsetMax + Vector2.one*padding;
        rect.SetSiblingIndex(source.GetSiblingIndex());
        var image = frame.GetComponent<Image>();
        image.color = fill;
        image.raycastTarget = false;
        Frame(image);
        // Compact text frames reserve their actual sliced border outside the glyph area.
        if(GameArtSkin.Current!=null){image.sprite=GameArtSkin.Current.tooltip;image.type=Image.Type.Sliced;image.pixelsPerUnitMultiplier=GameArtSkin.Current.textFramePixelsPerUnit;}
        var frameStyle=frame.AddComponent<UIElementTemplate>();frameStyle.appearance=UIElementTemplate.Appearance.PrefabOverrides;
        frameStyle.placement=false;frameStyle.image=true;frameStyle.imageColor=false;frameStyle.selectable=frameStyle.typography=frameStyle.layout=false;
        frame.AddComponent<TextFrameFadeSync>().Bind(text, image);
        text.overflowMode = TextOverflowModes.Overflow; FullTextLayout.Bind(text);
    }

    public static void ContainText(Transform root)
    {
        if (root == null) return;
        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            FullTextLayout.Bind(text);
        }
    }
}

// The frame is a sibling so it can sit behind the text; mirror the text's fade
// rather than leaving an opaque rectangle after the message disappears.
public sealed class TextFrameFadeSync : MonoBehaviour
{
    TMP_Text source;
    Image frame;
    float baseAlpha;

    public void Bind(TMP_Text text, Image image)
    {
        source = text;
        frame = image;
        baseAlpha = image.color.a;
        Sync();
    }

    void LateUpdate() => Sync();

    void Sync()
    {
        if (source == null)
        {
            gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
            return;
        }
        if (frame == null) return;
        // Layout guides may move the text after creation. Its decorative sibling must follow it,
        // unless a frame explicitly owns its placement in an authored template.
        var placement=frame.GetComponent<UIElementTemplate>();
        if(placement==null||!placement.placement){
            var from=source.rectTransform;var to=frame.rectTransform;
            to.anchorMin=from.anchorMin;to.anchorMax=from.anchorMax;to.pivot=from.pivot;
            float padding=GameArtSkin.Current!=null?GameArtSkin.Current.textFramePadding:8;
            to.offsetMin=from.offsetMin-Vector2.one*padding;to.offsetMax=from.offsetMax+Vector2.one*padding;
        }
        // CrossFadeAlpha changes CanvasRenderer alpha; manual fades change color.a.
        // Min avoids applying the same alpha twice when both report the same value.
        float alpha = string.IsNullOrWhiteSpace(source.text)?0:Mathf.Clamp01(Mathf.Min(source.color.a, source.canvasRenderer.GetAlpha()));
        var color = frame.color;
        float target = baseAlpha * alpha;
        if (Mathf.Abs(color.a - target) < .001f) return;
        color.a = target;
        frame.color = color;
    }
}
