// [코드 지도] CampaignState: 금화, 빚, 전당포, 납품 및 발견 상태를 보관하고 거래 규칙을 실행한다.
// 주요 함수: Pledge, Morning, PayDebt
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/Campaign/Core/CampaignState.cs.md

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Blacksmith;
using UnityEngine;

// Stored inside SmithingLoop.Progress, so transfers and economy commit together.
[Serializable]
public sealed class CampaignState
{
    public int gold, goldMilliRemainder, pendingGold, debtCarry, warnings, lastDebtDay=1, bagTier=1, axeTier=1, pickTier=1, facilityTier=1;
    public bool toolsLinked, starterSwordLinked;
    public bool gameOver, hasPosition, inTown=true, windBossDefeated, warpUnlocked;
    public float x=-100, y=3, playSeconds;
    public float health=100;
    public string sceneName="";
    public List<Blacksmith.Stack> delivery=new(), pawnStock=new();
    // pawnStock is legacy buyback inventory; new collateral is kept in separate loan records.
    public List<PawnLoan> pawnLoans=new();
    public List<string> openedChests=new(), usedAltars=new(), visitedCells=new(), unlockedWarps=new();
    public string lastReceipt="";
}

[Serializable]
public sealed class PawnLoan
{
    public string id;
    public Blacksmith.Stack collateral;
    public int principal;
    public long unpaidInterestCents;
    // The older integer/cents fields above are read when migrating a loan saved by an earlier build.
    public long principalMilli, unpaidInterestMilli;
    public int pledgedDay, lastInterestDay;
}

public sealed class CampaignEconomy
{
    readonly CampaignState state;readonly SaveData smith;readonly BlacksmithCatalog catalog;readonly CampaignRules rules;
    const long MilliPerGold=1000;
    const long MaxGoldMilli=(long)int.MaxValue*MilliPerGold+999;
    public long GoldMilli=>(long)state.gold*MilliPerGold+state.goldMilliRemainder;
    public static string FormatMilli(long value)=>(value/1000m).ToString("0.###",CultureInfo.InvariantCulture);
    public string GoldText=>FormatMilli(GoldMilli);
    bool CanCreditMilli(long value)=>value>=0&&value<=MaxGoldMilli-GoldMilli;
    void SetGoldMilli(long value){state.gold=(int)(value/MilliPerGold);state.goldMilliRemainder=(int)(value%MilliPerGold);}
    void CreditMilli(long value)=>SetGoldMilli(GoldMilli+value);
    void DebitMilli(long value)=>SetGoldMilli(GoldMilli-value);
    public CampaignEconomy(CampaignState state,SaveData smith,BlacksmithCatalog catalog,CampaignRules rules)
    {this.state=state;this.smith=smith;this.catalog=catalog;this.rules=rules;}
    public int BasePrice(ItemDefinition item)
    {
        if(item==null)return 0;
        // Explicit Notion prices are imported from items.csv into the catalog.
        if(item.price>0)return item.price;
        return item.arrow?rules.arrowPrice:item.group==ItemGroup.Equipment?rules.temporaryEquipmentPrice:item.group==ItemGroup.Crafted?rules.temporaryCraftedPrice:rules.temporaryOtherPrice;
    }
    public int Price(Blacksmith.Stack s)
    {
        var item=catalog.Item(s.itemId);
        return Mathf.Max(0,Mathf.RoundToInt(BasePrice(item)*QualityRules.Multiplier(item,s.quality)*(QualityRules.AppliesTo(item)&&s.quality==Quality.Master?2:1)));
    }
    // The six town axes/pickaxes use the special tool purchase path.
    public static bool IsTradeTool(ItemDefinition item)=>item!=null&&
        item.id.StartsWith("town_",StringComparison.Ordinal)&&!string.IsNullOrEmpty(item.toolKind);
    // Shop resale also accepts crafted weapons; armor and other goods stay with the pawn shop.
    public static bool IsShopSaleTool(ItemDefinition item)=>IsTradeTool(item)||
        item!=null&&item.material==MaterialKind.Weapon&&item.group==ItemGroup.Equipment;
    public int NextDebtDay=>state.lastDebtDay+7;
    public int BagUpgradePrice(int level)=>CampaignRules.BagPrice(level);
    public int RecipeMastery(string itemId)
    {
        var item=catalog.Item(itemId);
        if(item==null)return 0;
        bool equipment=item.material==MaterialKind.Weapon||item.material==MaterialKind.Armor;
        return catalog.recipes.Where(r=>r.enabled&&r.outputId==itemId)
            .Select(r=>smith.progress?.FirstOrDefault(p=>p.id==r.id)?.crafts??0)
            .Select(crafts=>CraftingService.MasteryLevel(crafts,equipment)).DefaultIfEmpty(0).Max();
    }
    public bool RecipeDiscovered(string itemId)=>catalog.recipes.Any(r=>r.enabled&&r.outputId==itemId&&
        (smith.progress?.Any(p=>p.id==r.id&&p.crafts>0)??false));
    public bool CanBuyCatalogItem(string itemId)
    {
        var item=catalog.Item(itemId);
        return item!=null&&item.id!="golem_core"&&item.price>0&&RecipeDiscovered(itemId);
    }
    static bool CanAdd(List<Blacksmith.Stack> target,Blacksmith.Stack stack,int count)=>
        count>0&&target.Where(s=>s.Key==stack.Key).Sum(s=>(long)s.count)+count<=int.MaxValue;
    public bool BuyCatalogItem(string itemId)
    {
        if(!CanBuyCatalogItem(itemId))return false;
        var item=catalog.Item(itemId);
        var stack=new Blacksmith.Stack(itemId,1);
        if(state.gold<item.price||!CanAdd(smith.bag,stack,1))return false;
        InventoryService.Add(smith.bag,stack);
        state.gold-=item.price;
        new InventoryService(smith,catalog).Notify();
        return true;
    }
    bool Locate(string source,Blacksmith.Stack stack,int count,out List<Blacksmith.Stack> pool,out EquipmentEntry equipped)
    {
        pool=source=="bag"?smith.bag:source=="chest"?smith.chest:null;
        equipped=source!=null&&source.StartsWith("equip:",StringComparison.Ordinal)
            ?smith.equipment.FirstOrDefault(e=>e.slot==source.Substring(6)&&ReferenceEquals(e.stack,stack)):null;
        return stack!=null&&count>0&&stack.count>=count&&
            (pool!=null&&pool.Contains(stack)||equipped!=null);
    }
    bool RemoveTradeSource(List<Blacksmith.Stack> pool,EquipmentEntry equipped,Blacksmith.Stack stack,int count)
    {
        if(equipped?.slot=="Weapon")
        {
            var arrow=smith.equipment.FirstOrDefault(e=>e.slot=="Arrow");
            if(arrow!=null&&!CanAdd(smith.chest,arrow.stack,arrow.stack.count))return false;
            if(arrow!=null){InventoryService.Add(smith.chest,arrow.stack);smith.equipment.Remove(arrow);}
        }
        stack.count-=count;
        if(stack.count==0){if(pool!=null)pool.Remove(stack);else smith.equipment.Remove(equipped);}
        return true;
    }
    public bool Pledge(string source,Blacksmith.Stack stack,int count,out PawnLoan loan)
    {
        loan=null;
        if(stack==null||catalog.Item(stack.itemId)==null||
            IsShopSaleTool(catalog.Item(stack.itemId)))return false;
        if(!Locate(source,stack,count,out var pool,out var equipped))return false;
        long assessed=(long)Price(stack)*count;
        if(assessed<=0||assessed>MaxGoldMilli/500)return false;
        long principalMilli=assessed*500;
        if(!CanCreditMilli(principalMilli))return false;
        var collateral=stack.Copy(count);
        if(!RemoveTradeSource(pool,equipped,stack,count))return false;
        loan=new PawnLoan{id=Guid.NewGuid().ToString("N"),collateral=collateral,
            principalMilli=principalMilli,
            pledgedDay=smith.day,lastInterestDay=smith.day};
        state.pawnLoans??=new List<PawnLoan>();
        state.pawnLoans.Add(loan);
        CreditMilli(principalMilli);
        new InventoryService(smith,catalog).Notify();
        return true;
    }
    public bool SellToShop(string source,Blacksmith.Stack stack,int count,out int proceeds)
    {
        proceeds=0;
        if((source!="bag"&&source!="chest")||stack==null||stack.itemId=="golem_core"||catalog.Item(stack.itemId)==null||
            !IsShopSaleTool(catalog.Item(stack.itemId))||
            !Locate(source,stack,count,out var pool,out var equipped))return false;
        long assessed=(long)Price(stack)*count;
        if(assessed<=0||assessed>MaxGoldMilli/500)return false;
        long valueMilli=assessed*500;
        if(!CanCreditMilli(valueMilli))return false;
        if(!RemoveTradeSource(pool,equipped,stack,count))return false;
        CreditMilli(valueMilli);proceeds=(int)(valueMilli/1000);
        new InventoryService(smith,catalog).Notify();
        return true;
    }
    public long RepaymentMilli(PawnLoan loan)=>loan==null?0:
        loan.unpaidInterestMilli>long.MaxValue-loan.principalMilli
            ?long.MaxValue:loan.principalMilli+loan.unpaidInterestMilli;
    public int RepaymentGold(PawnLoan loan)=>loan==null||RepaymentMilli(loan)>int.MaxValue*1000L
        ?int.MaxValue:(int)((RepaymentMilli(loan)+999)/1000);
    public bool Repay(PawnLoan loan)
    {
        if(loan==null||state.pawnLoans==null||!state.pawnLoans.Contains(loan)||loan.collateral==null)return false;
        long due=RepaymentMilli(loan);
        if(due<=0||due>GoldMilli||
            !CanAdd(smith.chest,loan.collateral,loan.collateral.count))return false;
        InventoryService.Add(smith.chest,loan.collateral.Copy(loan.collateral.count));
        state.pawnLoans.Remove(loan);DebitMilli(due);
        new InventoryService(smith,catalog).Notify();
        return true;
    }
    public void AccruePawnInterest(int day)
    {
        foreach(var loan in state.pawnLoans??Enumerable.Empty<PawnLoan>())
        {
            if(loan==null||day<=loan.lastInterestDay)continue;
            long perDay=loan.principalMilli/20;
            long days=day-loan.lastInterestDay;
            long room=long.MaxValue-loan.unpaidInterestMilli;
            loan.unpaidInterestMilli+=days>room/perDay?room:perDay*days;
            loan.lastInterestDay=day;
        }
    }
    public void CollectPawnInterest()
    {
        if(DebtDue)return;
        foreach(var loan in state.pawnLoans??Enumerable.Empty<PawnLoan>())
        {
            if(loan==null||GoldMilli<=0)continue;
            long payment=Math.Min(GoldMilli,loan.unpaidInterestMilli);
            DebitMilli(payment);
            loan.unpaidInterestMilli-=payment;
        }
    }
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
        if(stack==null||count<=0||!state.pawnStock.Contains(stack)||stack.count<count||
            !CanAdd(smith.chest,stack,count))return false;
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
        if(catalog.Item(id)==null||cost<0||state.gold<cost||
            !CanAdd(smith.bag,new Blacksmith.Stack(id,1),1))return false;
        state.gold-=cost;InventoryService.Add(smith.bag,new Blacksmith.Stack(id,1));
        new InventoryService(smith,catalog).Notify();return true;
    }
    public bool BuyUpgrade(string kind)
    {
        if(kind=="axe"||kind=="pick")return BuyTool(kind,(kind=="axe"?state.axeTier:state.pickTier)+1);
        int tier=kind=="bag"?state.bagTier:kind=="axe"?state.axeTier:kind=="pick"?state.pickTier:kind=="facility"?state.facilityTier:0;
        if(tier<=0||tier>=3)return false;
        int cost=kind=="bag"?BagUpgradePrice(tier+1):(kind=="facility"?rules.facilityUpgradePrice:rules.toolUpgradePrice)*tier;
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
        AccruePawnInterest(smith.day);
        long interestBefore=(state.pawnLoans??Enumerable.Empty<PawnLoan>()).Sum(l=>l.unpaidInterestMilli);
        string debtReceipt="";
        if(DebtDue)
        {
            int settled=SettleOverdueDebt();
            if(settled>0)debtReceipt=$"{settled}주분 · {state.lastReceipt}";
        }
        else CollectPawnInterest();
        long interestAfter=(state.pawnLoans??Enumerable.Empty<PawnLoan>()).Sum(l=>l.unpaidInterestMilli);
        state.lastReceipt=$"{smith.day}일 아침 · 납품 {proceeds}G (상자에서 수령)"+
            (string.IsNullOrEmpty(debtReceipt)?"":"\n"+debtReceipt)+
            (interestBefore>0?$"\n전당포 이자 자동 납부 {FormatMilli(interestBefore-interestAfter)}G · 미납 {FormatMilli(interestAfter)}G":"");
    }
    public bool DebtDue=>smith.day>=state.lastDebtDay+7;
    public int SettleOverdueDebt()
    {
        int settled=0;
        var random=new System.Random();
        while(DebtDue&&!state.gameOver&&PayDebt(random))settled++;
        return settled;
    }
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
        state.gameOver=state.warnings>=3;state.lastDebtDay+=7;
        state.lastReceipt=$"빚 정산: 현금 {cash}G · 압류 {seized}개 · 이월 {state.debtCarry}G · 경고 {state.warnings}/3";
        CollectPawnInterest();
        return true;
    }
}
