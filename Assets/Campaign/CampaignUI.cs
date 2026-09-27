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
    CampaignController owner;
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
    public bool IsOpen=>modal!=null&&modal.gameObject.activeSelf;
    public void Build(CampaignController c)
    {
        owner=c;
        var go=new GameObject("CampaignCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));go.transform.SetParent(transform,false);
        var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=1100;
        var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);
        hud=view.Full("CampaignHUD",go.transform);
        hudText=view.Text("DateGoldDebt",hud,"",23,new(.02f,.92f),new(.74f,.99f));
        view.Button("MapButton",hud,"지도",new(.75f,.93f),new(.82f,.995f),()=>{if(!GameUIController.BlocksGameplayInput)ShowMap(false);});
        view.Button("SavesButton",hud,"저장 / 불러오기",new(.83f,.93f),new(.985f,.995f),ShowSaves);
        menuLoad=view.Button("MainMenuSaves",go.transform,"저장 / 불러오기",new(.83f,.93f),new(.985f,.995f),ShowSaves);
        modal=view.Full("CampaignModal",go.transform);modal.gameObject.SetActive(false);
    }
    void Update()
    {
        if(owner==null||!owner.Ready)return;
        if(dialogue!=null){dialogueClock+=Time.unscaledDeltaTime*30;dialogue.maxVisibleCharacters=Keyboard.current?.spaceKey.wasPressedThisFrame==true?int.MaxValue:Mathf.Max(dialogue.maxVisibleCharacters,(int)dialogueClock);}
        shownGold=Mathf.MoveTowards(shownGold,owner.State.gold,Time.unscaledDeltaTime*Mathf.Max(40,Mathf.Abs(owner.State.gold-shownGold)*6));
        var s=SmithingLoop.Instance.SmithData;
        hudText.text=$"{s.day}일 · {(s.night?"밤 / 제작":"낮 / 채집")}    {Mathf.RoundToInt(shownGold)} G    빚 D-{Mathf.Max(0,owner.State.lastDebtDay+7-s.day)}   경고 {owner.State.warnings}/3\n{(owner.rules.provisional?"미정 가격·장비 수치: 임시 밸런스 적용":"")}";
        var combat=owner.Player.GetComponent<CampaignCombat>();
        if(combat!=null)hudText.text+=$"    화살 {combat.ArrowCount}";
        hud.gameObject.SetActive(!SmithingLoop.Instance.InShop&&!GameUIController.BlocksGameplayInput);
        menuLoad.gameObject.SetActive(GameUIController.Instance!=null&&GameUIController.Instance.MainMenuVisible&&!IsOpen);
        if(IsOpen&&Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame&&!owner.State.gameOver)Close();
    }
    RectTransform Open(string title)
    {
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
        modal.gameObject.SetActive(false);Time.timeScale=previousTime;GameUIController.ExternalActivity=previousExternal;
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
        var p=Open(kind=="facility"?"시설 상점":"장비 상점");var list=view.Scroll(p,"Goods",new(.04f,.18f),new(.5f,.83f));
        details=view.Text("Description",p,"물건을 선택하세요.\n가격과 일부 효과는 임시값입니다.",26,new(.55f,.33f),new(.95f,.82f),BlacksmithView.Ink);
        string[] keys=kind=="facility"?new[]{"facility"}:new[]{"axe","pick","bag","arrows"};
        foreach(var key in keys)
        {
            string label=key=="axe"?"도끼 업그레이드":key=="pick"?"곡괭이 업그레이드":key=="bag"?"배낭 업그레이드":key=="arrows"?"화살 10개":"대장간 연료 보급";
            Row(list,label,()=>SelectShop(key,label));
        }
        view.Text("TownNote",p,"도구는 기존 슬롯에 즉시 적용 · 배낭은 용량 증가 · 임시값",20,new(.04f,.03f),new(.95f,.13f),BlacksmithView.Ink);
    }
    public void ShowDialogue(string kind)
    {
        var p=Open(kind=="pawnSell"?"전당포 주인":kind=="facility"?"시설 상인":"장비 상인");
        dialogue=view.Text("NpcDialogue",p,kind=="pawnSell"?"물건을 팔러 오셨나요? 판매한 물건은 재구매할 수도 있습니다.":kind=="facility"?"대장간을 위한 연료가 필요하신가요? 시설 효과와 가격은 임시 설정입니다.":"더 좋은 도구와 넓은 배낭이 있으면 깊은 동굴도 탐험할 수 있지요.",30,new(.08f,.4f),new(.92f,.8f),BlacksmithView.Ink);dialogue.maxVisibleCharacters=0;dialogueClock=0;
        view.Text("SkipHint",p,"Space: 대화 바로 표시",19,new(.08f,.28f),new(.92f,.38f),BlacksmithView.Ink);
        view.Button("BrowseGoods",p,"거래하기",new(.18f,.10f),new(.45f,.25f),()=>{if(kind=="pawnSell"){SmithingLoop.Instance.StoreFieldMaterials();ShowStock(kind);}else ShowShop(kind);});
        view.Button("LeaveNpc",p,"다음에 올게요",new(.55f,.10f),new(.82f,.25f),Close);
    }
    void SelectShop(string key,string label)
    {
        int tier=key=="axe"?owner.State.axeTier:key=="pick"?owner.State.pickTier:key=="bag"?owner.State.bagTier:owner.State.facilityTier;
        int price=key=="arrows"?owner.rules.arrowPrice*10:key=="bag"?owner.rules.bagUpgradePrice*tier:key=="facility"?owner.rules.facilityUpgradePrice*tier:owner.rules.toolUpgradePrice*tier;
        details.text=$"{label}\n현재 {tier}단계\n가격 {price}G (임시)\n"+(key=="bag"?$"용량 +{owner.rules.capacityPerTier}kg":key=="arrows"?"보관함에 화살 10개 지급":key=="facility"?"구매 시 연료를 50으로 보충 (임시 효과)":"최대 3티어. 상위 자원을 정상 채집합니다.");
        var old=body.Find("Purchase");if(old!=null){old.gameObject.SetActive(false);Destroy(old.gameObject);}
        var b=view.Button("Purchase",body,key!="arrows"&&tier>=3?"보유함":"구매하기",new(.57f,.13f),new(.94f,.27f),()=>
        {
            bool ok;
            if(key=="arrows")
            {
                var arrow=SmithingLoop.Instance.Catalog.items.FirstOrDefault(i=>i.arrow);
                ok=arrow!=null&&owner.State.gold>=price;
                if(ok){owner.State.gold-=price;InventoryService.Add(SmithingLoop.Instance.SmithData.chest,new Blacksmith.Stack(arrow.id,10));}
            }
            else {ok=owner.Economy.BuyUpgrade(key);if(ok&&key=="facility")SmithingLoop.Instance.SmithData.fuel=50;}
            if(ok){owner.Commit();ShowShop(key=="facility"?"facility":"equipment");}else details.text+="\n구매 불가: 재화 부족 또는 이미 보유";
        });
        b.interactable=key=="arrows"||tier<3;
    }
    public void ShowStock(string mode)
    {
        stockMode=mode;selected=null;quantity=1;var p=Open(mode=="pawnBuy"?"전당포 · 재구매":mode=="pawnSell"?"전당포 · 판매":"납품 상자");
        var list=view.Scroll(p,"Stock",new(.04f,.22f),new(.50f,.82f));
        var items=mode=="pawnBuy"?owner.State.pawnStock:mode=="withdraw"?owner.State.delivery:SmithingLoop.Instance.SmithData.chest;
        details=view.Text("ItemDetails",p,"아이템을 선택하세요.",25,new(.55f,.38f),new(.96f,.80f),BlacksmithView.Ink);
        foreach(var stack in items.ToArray())
        {
            var captured=stack;var def=SmithingLoop.Instance.Catalog.Item(stack.itemId);
            Row(list,$"{def.displayName} ×{stack.count}",()=>{selected=captured;quantity=1;RefreshItem();});
        }
        view.Button("Minus",p,"−",new(.55f,.28f),new(.65f,.37f),()=>{quantity=Mathf.Max(1,quantity-1);RefreshItem();});
        view.Button("Plus",p,"+",new(.69f,.28f),new(.79f,.37f),()=>{if(selected!=null)quantity=Mathf.Min(selected.count,quantity+1);RefreshItem();});
        view.Button("All",p,"전체",new(.83f,.28f),new(.95f,.37f),()=>{if(selected!=null)quantity=selected.count;RefreshItem();});
        view.Button("Trade",p,mode=="pawnBuy"?"구매":mode=="pawnSell"?"판매":mode=="withdraw"?"회수":"납품",new(.55f,.14f),new(.95f,.25f),()=>
        {
            bool ok=mode=="pawnBuy"?owner.Economy.PawnBuy(selected,quantity):mode=="pawnSell"?owner.Economy.PawnSell(selected,quantity):mode=="withdraw"?owner.Economy.Withdraw(selected,quantity):owner.Economy.Deposit(selected,quantity);
            if(ok){owner.Commit();ShowStock(mode);}else details.text+="\n거래 불가 · 재화/수량을 확인하세요.";
        });
        view.Button("SwitchStock",p,mode.StartsWith("pawn")?"판매 ↔ 재구매":"납품 ↔ 회수",new(.04f,.06f),new(.33f,.17f),()=>ShowStock(mode=="pawnBuy"?"pawnSell":mode=="pawnSell"?"pawnBuy":mode=="withdraw"?"delivery":"withdraw"));
        if(!mode.StartsWith("pawn"))view.Button("CollectProceeds",p,$"대금 {owner.State.pendingGold}G 받기",new(.35f,.06f),new(.68f,.17f),()=>{owner.Economy.Collect();owner.Commit();ShowStock(mode);});
    }
    void RefreshItem()
    {
        if(selected==null)return;var def=SmithingLoop.Instance.Catalog.Item(selected.itemId);int price=owner.Economy.Price(selected);if(stockMode=="pawnSell")price/=2;
        details.text=$"{def.displayName}\n{def.description}\n보유 {selected.count} · 선택 {quantity}\n단가 {price}G · 합계 {(long)price*quantity}G\n{(stockMode.StartsWith("pawn")?"판매는 기준가 50% 내림 / 재구매 100%":"다음 날 아침에 판매 후 대금을 직접 수령")}";
    }
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
        var p=Open(warp?"워프석 지도":"탐험 지도");var map=view.Rect("MapArea",p,new(.06f,.16f),new(.94f,.83f));
        const float xmin=-140,xmax=320,ymin=-115,ymax=55;
        Vector2 Project(Vector2 pos)=>new((pos.x-xmin)/(xmax-xmin),(pos.y-ymin)/(ymax-ymin));
        foreach(string cell in owner.State.visitedCells)
        {
            var xy=cell.Split(',');if(xy.Length!=2||!int.TryParse(xy[0],out int x)||!int.TryParse(xy[1],out int y))continue;
            Vector2 min=Project(new Vector2(x*8,y*8)),max=Project(new Vector2(x*8+8,y*8+8));
            var image=view.Image("Explored",map,null,new Color(.65f,.48f,.20f,.35f),min,max);
        }
        foreach(var tiles in FindObjectsByType<UnityEngine.Tilemaps.Tilemap>(FindObjectsSortMode.None))
        foreach(var cell in tiles.cellBounds.allPositionsWithin)
        {
            if(cell.x%3!=0||cell.y%3!=0||!tiles.HasTile(cell))continue;
            Vector2 world=tiles.CellToWorld(cell);if(!owner.Exploration.IsExplored(world))continue;
            Vector2 min=Project(world),max=Project(world+new Vector2(3,3));
            view.Image("Terrain",map,null,new Color(.25f,.20f,.12f,.85f),min,max);
        }
        foreach(var landmark in FindObjectsByType<CampaignWorldObject>(FindObjectsSortMode.None))
        {
            if(!owner.Exploration.IsExplored(landmark.transform.position))continue;
            bool isWarp=landmark.kind==CampaignObjectKind.Warp;
            if(warp&&!isWarp)continue;
            if(!isWarp&&landmark.kind!=CampaignObjectKind.Treasure&&landmark.kind!=CampaignObjectKind.ForestGate&&landmark.kind!=CampaignObjectKind.ReturnGate&&landmark.kind!=CampaignObjectKind.WindEntrance)continue;
            var pos=Project(landmark.transform.position);var captured=landmark;
            var button=view.Button("Marker_"+landmark.stableId,map,"",pos-new Vector2(.012f,.02f),pos+new Vector2(.012f,.02f),()=>
            {
                if(!warp)return;
                Confirm("워프 이동 확인",captured.displayName+"으로 이동할까요?",()=>{Close();owner.Teleport((Vector2)captured.transform.position+Vector2.up);owner.Commit();});
            });
            button.image.sprite=isWarp?mapWarp:mapEntrance;button.image.color=Color.white;button.image.preserveAspect=true;
            button.interactable=!warp||owner.State.unlockedWarps.Contains(landmark.stableId);
        }
        foreach(var altar in FindObjectsByType<AssissZone>(FindObjectsSortMode.None))
        {if(warp||!owner.Exploration.IsExplored(altar.transform.position))continue;var pos=Project(altar.transform.position);var im=view.Image("Altar",map,null,Color.white,pos-new Vector2(.01f,.02f),pos+new Vector2(.01f,.02f),true);im.sprite=mapAltar;}
        var point=Project(owner.Player.position);var marker=view.Image("PlayerPosition",map,null,Color.white,point-new Vector2(.008f,.02f),point+new Vector2(.008f,.02f),true);marker.sprite=mapPlayer;
        view.Text("Legend",p,"현재 위치 · 워프석 · 석상 · 입구  |  방문한 구역만 표시",19,new(.05f,.04f),new(.75f,.12f),BlacksmithView.Ink);
        if(warp)view.Button("HomeWarp",p,owner.State.windBossDefeated?"마을로":"마을 워프 미해금",new(.74f,.03f),new(.97f,.12f),()=>{if(owner.State.windBossDefeated)Confirm("마을로 워프할까요?","채집물을 보관함으로 옮깁니다.",owner.ReturnTown);}).interactable=owner.State.windBossDefeated;
    }
    void OnDestroy(){if(IsOpen){Time.timeScale=previousTime;GameUIController.ExternalActivity=previousExternal;}}
}
