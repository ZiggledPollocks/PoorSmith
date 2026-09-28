using System;
using System.Collections.Generic;
using System.Linq;
using Blacksmith;
using UnityEngine;

// Stored inside SmithingLoop.Progress, so transfers and economy commit together.
[Serializable]
public sealed class CampaignState
{
    public int gold, pendingGold, debtCarry, warnings, lastDebtDay=1, bagTier=1, axeTier=1, pickTier=1, facilityTier=1;
    public bool toolsLinked;
    public bool gameOver, hasPosition, inTown=true, windBossDefeated, warpUnlocked;
    public float x=-100, y=3, playSeconds;
    public float health=100;
    public string sceneName="";
    public List<Blacksmith.Stack> delivery=new(), pawnStock=new();
    public List<string> openedChests=new(), usedAltars=new(), visitedCells=new(), unlockedWarps=new();
    public string lastReceipt="";
}

public sealed class CampaignEconomy
{
    readonly CampaignState state;readonly SaveData smith;readonly BlacksmithCatalog catalog;readonly CampaignRules rules;
    public CampaignEconomy(CampaignState state,SaveData smith,BlacksmithCatalog catalog,CampaignRules rules)
    {this.state=state;this.smith=smith;this.catalog=catalog;this.rules=rules;}
    public int BasePrice(ItemDefinition item)
    {
        if(item==null)return 0;
        // Explicit Notion prices are imported from items.csv into the catalog.
        if(item.price>0)return item.price;
        return item.arrow?rules.arrowPrice:item.group==ItemGroup.Equipment?rules.temporaryEquipmentPrice:item.group==ItemGroup.Crafted?rules.temporaryCraftedPrice:rules.temporaryOtherPrice;
    }
    public int Price(Blacksmith.Stack s)=>Mathf.Max(0,Mathf.RoundToInt(BasePrice(catalog.Item(s.itemId))*QualityRules.Multiplier(s.quality)*(s.quality==Quality.Master?2:1)));
    static bool Move(List<Blacksmith.Stack> from,List<Blacksmith.Stack> to,Blacksmith.Stack stack,int count)
    {
        if(stack==null||count<=0||!from.Contains(stack)||stack.count<count)return false;
        InventoryService.Add(to,stack.Copy(count));stack.count-=count;if(stack.count==0)from.Remove(stack);return true;
    }
    public bool Deposit(Blacksmith.Stack stack,int count)=>Move(smith.chest,state.delivery,stack,count);
    public bool Withdraw(Blacksmith.Stack stack,int count)=>Move(state.delivery,smith.chest,stack,count);
    public bool PawnSell(Blacksmith.Stack stack,int count)
    {
        if(stack==null||count<=0||!smith.chest.Contains(stack)||stack.count<count)return false;
        long earned=(long)(Price(stack)/2)*count;if(earned>int.MaxValue-state.gold)return false;
        if(!Move(smith.chest,state.pawnStock,stack,count))return false;state.gold+=(int)earned;return true;
    }
    public bool PawnBuy(Blacksmith.Stack stack,int count)
    {
        if(stack==null||count<=0||!state.pawnStock.Contains(stack)||stack.count<count)return false;
        long cost=(long)Price(stack)*count;if(cost>state.gold)return false;
        if(!Move(state.pawnStock,smith.chest,stack,count))return false;state.gold-=(int)cost;return true;
    }
    public bool Collect()
    {
        if(state.pendingGold<=0||state.pendingGold>int.MaxValue-state.gold)return false;
        state.gold+=state.pendingGold;state.pendingGold=0;return true;
    }
    public int ToolPrice(string kind,int tier)
    {
        var item=catalog.Item($"town_{kind}_{tier}");
        return item!=null&&item.buyPrice>0?item.buyPrice:rules.toolUpgradePrice*Mathf.Max(1,tier-1);
    }
    public bool BuyTool(string kind,int tier)
    {
        if((kind!="axe"&&kind!="pick")||tier<1||tier>3)return false;
        string id=$"town_{kind}_{tier}";int cost=ToolPrice(kind,tier);
        var prior=smith.bag.FirstOrDefault(s=>s.itemId==id&&s.quality==Quality.High);
        if(catalog.Item(id)==null||cost<0||state.gold<cost||prior?.count==int.MaxValue)return false;
        state.gold-=cost;InventoryService.Add(smith.bag,new Blacksmith.Stack(id,1));return true;
    }
    public bool BuyUpgrade(string kind)
    {
        if(kind=="axe"||kind=="pick")return BuyTool(kind,(kind=="axe"?state.axeTier:state.pickTier)+1);
        int tier=kind=="bag"?state.bagTier:kind=="axe"?state.axeTier:kind=="pick"?state.pickTier:kind=="facility"?state.facilityTier:0;
        if(tier<=0||tier>=3)return false;
        int cost=(kind=="bag"?rules.bagUpgradePrice:kind=="facility"?rules.facilityUpgradePrice:rules.toolUpgradePrice)*tier;
        if(state.gold<cost)return false;
        state.gold-=cost;
        switch(kind){case "bag":state.bagTier++;break;case "axe":state.axeTier++;break;case "pick":state.pickTier++;break;case "facility":state.facilityTier++;break;}
        return true;
    }
    // Call exactly once when the authoritative smith day advances to morning.
    public void Morning()
    {
        long proceeds=state.delivery.Sum(s=>(long)Price(s)*s.count);
        if(proceeds<=int.MaxValue-state.pendingGold){state.pendingGold+=(int)proceeds;state.delivery.Clear();}
        state.usedAltars.Clear();
        state.lastReceipt=$"{smith.day}일 아침 · 납품 {proceeds}G (상자에서 수령)";
    }
    public bool DebtDue=>smith.day>=state.lastDebtDay+7;
    public bool PayDebt(System.Random random)
    {
        if(!DebtDue||state.gameOver)return false;
        int due=checked(rules.weeklyDebt+state.debtCarry),cash=Math.Min(state.gold,due);state.gold-=cash;due-=cash;
        int seized=0;
        while(due>0)
        {
            var pools=new[]{smith.chest,smith.bag,state.delivery};
            var choices=pools.SelectMany(p=>p.Where(s=>s.count>0&&Price(s)>0).Select(s=>(pool:p,stack:s))).ToList();
            if(choices.Count==0)break;
            var item=choices[random.Next(choices.Count)];due-=Price(item.stack);item.stack.count--;seized++;
            if(item.stack.count==0)item.pool.Remove(item.stack);
        }
        state.debtCarry=Math.Max(0,due);if(due>0)state.warnings++;
        state.gameOver=state.warnings>=3;state.lastDebtDay=smith.day;
        state.lastReceipt=$"빚 정산: 현금 {cash}G · 압류 {seized}개 · 이월 {state.debtCarry}G · 경고 {state.warnings}/3";
        return true;
    }
}
