using System.Collections;
using System.Linq;
using Blacksmith;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Presentation only. Inventory ownership and transactions remain in CampaignEconomy.
public sealed class CampaignTradeUI : MonoBehaviour
{
    CampaignController owner;CampaignUI ui;BlacksmithView view;
    RectTransform panel,information;TMP_Text detail;Button selectedRow;
    string filter="all",mode,receipt="",selectedKey="";int page,quantity;Blacksmith.Stack selected;
    public void Initialize(CampaignController c,CampaignUI u){owner=c;ui=u;view=u.view;}
    RectTransform Open(string title)
    {
        panel=ui.Open(title);panel.GetComponent<Image>().sprite=null;panel.GetComponent<Image>().color=BlacksmithView.Dark;
        panel.Find("Title").GetComponent<TMP_Text>().color=BlacksmithView.Cream;
        foreach(var roll in new[]{"TopRoll","BottomRoll"})if(panel.Find(roll)!=null)panel.Find(roll).gameObject.SetActive(false);
        view.Text("TradeGold",panel,$"보유 {owner.State.gold} G",25,new(.60f,.86f),new(.85f,.97f),BlacksmithView.Cream);
        selectedRow=null;return panel;
    }
    Button Row(Transform parent,string name,string label,Sprite icon,System.Action selected)
    {
        var b=Instantiate(ui.rowPrefab,parent);b.name=name;b.gameObject.SetActive(true);
        var text=b.GetComponentInChildren<TMP_Text>();text.text=label;text.fontSize=23;
        var r=text.rectTransform;r.anchorMin=new(.2f,0);r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;
        var image=view.Image("ItemIcon",b.transform,null,Color.white,new(.015f,.12f),new(.18f,.88f),true);image.sprite=icon;
        b.onClick.RemoveAllListeners();b.onClick.AddListener(()=>
        {SelectRow(b);selected();});
        var outline=b.gameObject.AddComponent<Outline>();outline.effectColor=BlacksmithView.Gold;outline.effectDistance=new Vector2(3,-3);outline.enabled=false;
        if(name==selectedKey)SelectRow(b);
        return b;
    }
    void SelectRow(Button b)
    {
        if(selectedRow!=null){selectedRow.image.color=Color.white;foreach(var o in selectedRow.GetComponents<Outline>())o.enabled=false;}
        selectedRow=b;selectedKey=b.name;b.image.color=BlacksmithView.Gold;foreach(var o in b.GetComponents<Outline>())o.enabled=true;
    }
    void Purchased(string message){receipt=message;ui.RecordTrade();owner.Commit();}
    public void ResetShop(){filter="all";receipt=selectedKey="";}
    Sprite ToolIcon(string key)
    {
        if(key=="bag")return ui.tradeBag!=null?ui.tradeBag:view.Art("bag_leather");if(key=="arrows")return view.Art("arrow");
        var tools=owner.Player.GetComponent<PlayerToolController>();
        for(int i=0;i<tools.ToolSlotCount;i++){var t=tools.GetToolAtSlot(i);if(t!=null&&t.Icon!=null&&t.ToolType==(key=="axe"?ToolType.Axe:ToolType.Pickaxe))return t.Icon;}
        return SmithingLoop.Instance.ToolIcon(key=="axe"?"linked_axe":"linked_pick");
    }
    int Tier(string key)=>key=="bag"?owner.State.bagTier:key=="axe"?owner.State.axeTier:owner.State.pickTier;
    int Cost(string key)=>key=="arrows"?owner.rules.arrowPrice*10:(key=="bag"?owner.rules.bagUpgradePrice:owner.rules.toolUpgradePrice)*Tier(key);
    string Label(string key)=>key=="axe"?"도끼 업그레이드":key=="pick"?"곡괭이 업그레이드":key=="bag"?"배낭 업그레이드":"화살 10개";
    public void ShowShop()
    {
        Open("장비 상점");
        view.Text("ShopFeedback",panel,receipt,21,new(.17f,.025f),new(.91f,.12f),BlacksmithView.Cream);
        string[] categories={"all","pick","axe","bag","arrows"};string[] labels={"전체","곡괭이","도끼","배낭","화살"};
        for(int i=0;i<categories.Length;i++){string key=categories[i];float y=.73f-i*.13f;var b=view.Button("Filter_"+key,panel,labels[i],new(.02f,y),new(.14f,y+.1f),()=>{filter=key;selectedKey="";ShowShop();});b.image.color=filter==key?BlacksmithView.Gold:new Color(.22f,.21f,.18f);}
        var list=view.Scroll(panel,"ShopGoods",new(.16f,.13f),new(.53f,.83f));
        information=view.Panel("ShopInformation",panel,new(.57f,.15f),new(.95f,.8f),true);information.gameObject.SetActive(false);
        foreach(string key in new[]{"pick","axe","bag","arrows"})
        {
            if(filter!="all"&&filter!=key)continue;
            if(key=="bag")
            {
                for(int t=1;t<=3;t++)
                {
                    int tier=t;bool has=Tier("bag")>=tier;
                    string id=tier==Mathf.Min(3,Tier("bag")+1)?"Goods_bag":"Goods_bag_"+tier;
                    Row(list,id,$"배낭 {tier}단계\n"+(has?"보유함":$"{owner.rules.bagUpgradePrice*(tier-1)} G · 임시"),ToolIcon(key),()=>SelectBag(tier));
                }
                continue;
            }
            if(key=="pick"||key=="axe")
            {
                for(int t=1;t<=3;t++){int tier=t;var item=SmithingLoop.Instance.Catalog.Item($"town_{key}_{tier}");string label=item?.displayName??(key=="pick"?"곡괭이":"도끼")+$" T{tier}";string provisional=item==null||item.buyPrice<=0?" · 임시":"";Row(list,"Goods_"+key+"_"+tier,$"{label}\n{owner.Economy.ToolPrice(key,tier)} G{provisional}",ToolIcon(key),()=>SelectTool(key,tier));}
                continue;
            }
            bool owned=key!="arrows"&&Tier(key)>=3;
            Row(list,"Goods_"+key,Label(key)+"\n"+(owned?"보유함":Cost(key)+" G · 임시"),ToolIcon(key),()=>SelectShop(key));
        }
    }
    void SelectTool(string key,int tier)
    {
        BlacksmithView.Clear(information);information.gameObject.SetActive(true);
        string id=$"town_{key}_{tier}";var def=SmithingLoop.Instance.Catalog.Item(id);
        int cost=owner.Economy.ToolPrice(key,tier);
        var icon=view.Image("SelectedIcon",information,null,Color.white,new(.3f,.71f),new(.7f,.94f),true);icon.sprite=ToolIcon(key);
        view.Text("Name",information,def.displayName,29,new(.05f,.55f),new(.95f,.7f),BlacksmithView.Ink,TextAlignmentOptions.Center);
        string description=string.IsNullOrWhiteSpace(def.description)?"":def.description+"\n";
        detail=view.Text("Description",information,$"{description}티어 {tier} · 가방에 지급\n대장간 장비 거치대에서 장착\n{cost} G"+(def.buyPrice>0?"":" (임시)"),22,new(.08f,.27f),new(.92f,.53f),BlacksmithView.Ink);
        Button purchase=null;purchase=view.Button("Purchase",information,"구매하기",new(.2f,.08f),new(.8f,.22f),()=>
        {
            if(!owner.Economy.BuyTool(key,tier)){detail.text="구매 불가: 금화 또는 수량을 확인하세요.";StartCoroutine(Flash(purchase));return;}
            Purchased($"{def.displayName} 구매 완료 · 가방에 지급 · −{cost} G");ShowShop();SelectTool(key,tier);
        });
    }
    void SelectBag(int tier)
    {
        BlacksmithView.Clear(information);information.gameObject.SetActive(true);
        var icon=view.Image("SelectedIcon",information,null,Color.white,new(.3f,.71f),new(.7f,.94f),true);icon.sprite=ToolIcon("bag");
        bool owned=Tier("bag")>=tier,next=tier==Tier("bag")+1;
        view.Text("Name",information,$"배낭 {tier}단계",29,new(.05f,.55f),new(.95f,.7f),BlacksmithView.Ink,TextAlignmentOptions.Center);
        detail=view.Text("Description",information,$"기본 용량 +{owner.rules.capacityPerTier*(tier-1)}kg\n"+(owned?"이미 보유한 단계입니다.":$"{owner.rules.bagUpgradePrice*(tier-1)} G (임시)"+(next?"":"\n이전 단계를 먼저 구매하세요.")),24,new(.08f,.27f),new(.92f,.53f),BlacksmithView.Ink);
        Button purchase=null;purchase=view.Button("Purchase",information,owned?"보유함":"구매하기",new(.2f,.08f),new(.8f,.22f),()=>
        {
            if(tier!=Tier("bag")+1)return;
            if(owner.Economy.BuyUpgrade("bag")){Purchased($"배낭 {tier}단계 구매 완료 · 용량 +{owner.rules.capacityPerTier}kg");ShowShop();SelectBag(tier);}
            else{detail.text="구매 불가: 금화가 부족합니다.";StartCoroutine(Flash(purchase));}
        });purchase.interactable=!owned&&next;
    }
    void SelectShop(string key)
    {
        BlacksmithView.Clear(information);information.gameObject.SetActive(true);
        var icon=view.Image("SelectedIcon",information,null,Color.white,new(.3f,.71f),new(.7f,.94f),true);icon.sprite=ToolIcon(key);
        bool owned=key!="arrows"&&Tier(key)>=3;
        view.Text("Name",information,Label(key),29,new(.05f,.55f),new(.95f,.7f),BlacksmithView.Ink,TextAlignmentOptions.Center);
        string effect=key=="bag"?$"용량 +{owner.rules.capacityPerTier}kg":key=="arrows"?"화살 10개를 보관함에 지급":"도구 슬롯에 상위 티어 즉시 적용";
        detail=view.Text("Description",information,effect+"\n"+(owned?"이미 보유한 최종 단계입니다.":$"{Cost(key)} G (임시)"),24,new(.08f,.27f),new(.92f,.53f),BlacksmithView.Ink);
        Button purchase=null;purchase=view.Button("Purchase",information,owned?"보유함":"구매하기",new(.2f,.08f),new(.8f,.22f),()=>
        {
            bool ok;
            if(key=="arrows")
            {var arrow=SmithingLoop.Instance.Catalog.items.FirstOrDefault(x=>x.arrow);int cost=Cost(key);ok=arrow!=null&&owner.State.gold>=cost;if(ok){owner.State.gold-=cost;InventoryService.Add(SmithingLoop.Instance.SmithData.chest,new Blacksmith.Stack(arrow.id,10));}}
            else ok=owner.Economy.BuyUpgrade(key);
            if(ok){Purchased(Label(key)+" 구매 완료");ShowShop();SelectShop(key);}else{detail.text="구매 불가: 금화가 부족합니다.";StartCoroutine(Flash(purchase));}
        });purchase.interactable=!owned;
    }
    IEnumerator Flash(Button b)
    {for(int i=0;i<4&&b!=null;i++){b.image.color=i%2==0?new Color(1,.2f,.2f):Color.white;yield return new WaitForSecondsRealtime(.12f);}if(b!=null)b.image.color=Color.white;}
    public void ShowPawn(string requestedMode,int requestedPage=0)
    {
        mode=requestedMode;selected=null;page=requestedPage;Open(mode=="pawnBuy"?"전당포 · 구매":"전당포 · 판매");
        view.Button("SwitchStock",panel,mode=="pawnBuy"?"판매 탭 →":"← 구매 탭",new(.02f,.04f),new(.2f,.14f),()=>ShowPawn(mode=="pawnBuy"?"pawnSell":"pawnBuy"));
        var items=mode=="pawnBuy"?owner.State.pawnStock:SmithingLoop.Instance.SmithData.chest;
        if(mode=="pawnBuy")
        {
            var list=view.Scroll(panel,"PawnStock",new(.05f,.2f),new(.5f,.82f));
            information=view.Panel("PawnInformation",panel,new(.55f,.2f),new(.94f,.8f),true);information.gameObject.SetActive(false);
            foreach(var s in items.ToArray()){var def=SmithingLoop.Instance.Catalog.Item(s.itemId);Row(list,"Stock_"+s.itemId,def.displayName+$" ×{s.count}\n{owner.Economy.Price(s)} G",view.Art(def.sprite),()=>SelectPawn(s,-1));}
            if(items.Count==0)view.Text("EmptyStock",panel,"재구매할 물품이 없습니다.",25,new(.06f,.35f),new(.48f,.65f));
        }
        else
        {
            var bag=view.Image("Bag",panel,"bag_leather",Color.white,new(.29f,.13f),new(.71f,.84f),true);if(ui.tradeBag!=null)bag.sprite=ui.tradeBag;
            var grid=view.Rect("PawnGrid",panel,new(.365f,.19f),new(.635f,.60f));
            page=Mathf.Clamp(page,0,Mathf.Max(0,(items.Count-1)/25));
            for(int i=0;i<25;i++)
            {
                int cell=i,index=page*25+i;var b=Instantiate(ui.rowPrefab,grid);b.name="PawnSlot_"+i;b.gameObject.SetActive(true);
                Destroy(b.GetComponent<LayoutElement>());var rect=(RectTransform)b.transform;int col=i%5,row=i/5;
                rect.anchorMin=new(col/5f,1-(row+1)/5f);rect.anchorMax=new((col+1)/5f,1-row/5f);rect.offsetMin=new(3,3);rect.offsetMax=new(-3,-3);
                b.image.sprite=view.Art("bag_slot");var colors=b.colors;colors.disabledColor=Color.white;b.colors=colors;b.GetComponentInChildren<TMP_Text>().text="";b.onClick.RemoveAllListeners();b.interactable=index<items.Count;
                if(index>=items.Count)continue;var stack=items[index];var def=SmithingLoop.Instance.Catalog.Item(stack.itemId);
                view.Image("Icon",b.transform,def.sprite,Color.white,new(.1f,.15f),new(.9f,.95f),true);
                view.Text("Count",b.transform,stack.count.ToString(),18,new(.3f,0),Vector2.one,null,TextAlignmentOptions.BottomRight);
                b.onClick.AddListener(()=>{if(selectedRow!=null)selectedRow.image.color=Color.white;selectedRow=b;b.image.color=BlacksmithView.Gold;SelectPawn(stack,cell%5);});
            }
            information=view.Panel("PawnInformation",panel,new(.02f,.3f),new(.3f,.8f),true);information.gameObject.SetActive(false);
            view.Button("PreviousPage",panel,"◀",new(.34f,.04f),new(.42f,.12f),()=>ShowPawn(mode,page-1)).interactable=page>0;
            view.Text("Page",panel,$"{page+1} / {Mathf.Max(1,(items.Count+24)/25)}",20,new(.43f,.04f),new(.57f,.12f));
            view.Button("NextPage",panel,"▶",new(.58f,.04f),new(.66f,.12f),()=>ShowPawn(mode,page+1)).interactable=(page+1)*25<items.Count;
        }
    }
    void SelectPawn(Blacksmith.Stack stack,int column)
    {
        selected=stack;quantity=1;
        if(column>=0){information.anchorMin=new(column<3?.02f:.70f,.28f);information.anchorMax=new(column<3?.30f:.98f,.81f);}
        BlacksmithView.Clear(information);information.gameObject.SetActive(true);information.SetAsLastSibling();
        var def=SmithingLoop.Instance.Catalog.Item(stack.itemId);view.Image("ItemIcon",information,def.sprite,Color.white,new(.33f,.73f),new(.67f,.96f),true);
        detail=view.Text("PawnDetails",information,"",22,new(.05f,.37f),new(.95f,.73f),BlacksmithView.Ink);
        view.Button("Minus",information,"−",new(.05f,.22f),new(.25f,.35f),()=>{quantity=Mathf.Max(1,quantity-1);Refresh();});
        view.Button("Plus",information,"+",new(.28f,.22f),new(.48f,.35f),()=>{quantity=Mathf.Min(selected.count,quantity+1);Refresh();});
        view.Button("All",information,"전체",new(.51f,.22f),new(.95f,.35f),()=>{quantity=selected.count;Refresh();});
        view.Button("Trade",information,mode=="pawnBuy"?"구매하기":"판매하기",new(.13f,.05f),new(.87f,.19f),()=>
        {bool ok=mode=="pawnBuy"?owner.Economy.PawnBuy(selected,quantity):owner.Economy.PawnSell(selected,quantity);if(ok){ui.RecordTrade();owner.Commit();ShowPawn(mode,page);}else detail.text+="\n거래 불가: 금화·수량 확인";});Refresh();
    }
    void Refresh()
    {var def=SmithingLoop.Instance.Catalog.Item(selected.itemId);int price=owner.Economy.Price(selected);if(mode=="pawnSell")price/=2;detail.text=$"{def.displayName}\n보유 {selected.count} · 선택 {quantity}\n단가 {price} G\n합계 {(long)price*quantity} G";}
}
