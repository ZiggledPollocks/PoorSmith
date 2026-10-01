// [코드 지도] InventoryService: 대장간 상자·가방·선택 재료·장착 슬롯의 소유권과 수량을 관리한다.
// 주요 함수: Equip, FillRecipe, EquipmentStats
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Crafting/Scripts/Domain/InventoryService.cs.md

using System;
using System.Collections.Generic;
using System.Linq;

namespace Blacksmith
{
    // Ordered stacks are the shared storage model. Removing a full stack and returning
    // it appends it, matching the planning document's chest ordering requirement.
    public sealed class InventoryService
    {
        public readonly SaveData Data;
        public readonly BlacksmithCatalog Catalog;
        public readonly List<Stack> Selection = new List<Stack>();
        public event Action Changed;
        public InventoryService(SaveData data, BlacksmithCatalog catalog) { Data=data; Catalog=catalog; RecordAcquired(); }
        public void RecordAcquired()
        {
            Data.acquiredItems ??= new List<string>();
            foreach(var stack in Data.chest.Concat(Data.bag).Concat(Selection))
                if(stack.count>0&&!Data.acquiredItems.Contains(stack.itemId))Data.acquiredItems.Add(stack.itemId);
        }
        public void Notify() { RecordAcquired(); Changed?.Invoke(); }
        public static void Add(List<Stack> list, Stack incoming)
        {
            if(incoming==null||incoming.count<=0)return;
            if(!InventoryStackLedger.TryAdd(list,incoming.Key,incoming.count,
                x=>x.Key,x=>x.count,(x,n)=>x.count=n,
                (_,n)=>incoming.Copy(n)))throw new OverflowException("Inventory stack count overflow");
        }
        public bool Transfer(List<Stack> from,List<Stack> to,Stack stack,int amount)
        {
            if(!InventoryStackLedger.TryTransfer(from,to,stack,amount,
                x=>x.Key,x=>x.count,(x,n)=>x.count=n,(x,n)=>x.Copy(n)))return false;
            Notify();return true;
        }
        public bool Select(Stack stack,Station station)
        {
            if(stack==null||!Data.chest.Contains(stack)||stack.count<=0)
                return false;
            int max=station==Station.Workbench?4:station==Station.Furnace?3:1;
            if(!Selection.Any(x=>x.itemId==stack.itemId)&&Selection.Select(x=>x.itemId).Distinct().Count()>=max)
                return false;
            if(station==Station.Workbench && Selection.Count>0)
            {
                // A partial combination is valid in any insertion order if one
                // enabled workbench recipe contains every selected item ID.
                var ids=Selection.Select(x=>x.itemId).Append(stack.itemId).Distinct().ToArray();
                if(!Catalog.recipes.Any(r=>r.enabled&&r.station==Station.Workbench&&
                    ids.All(id=>r.ingredients.Any(req=>req.itemId==id))))
                    return false;
            }
            return Transfer(Data.chest,Selection,stack,1);
        }
        public void ReturnAll()
        {
            foreach(var group in Selection.GroupBy(x=>x.Key))
                if(group.Sum(x=>(long)x.count) > int.MaxValue -
                   InventoryStackLedger.Count(Data.chest,group.Key,x=>x.Key,x=>x.count))
                    throw new OverflowException("Cannot return selected materials to chest");
            foreach(var stack in Selection.ToArray())
                if(!InventoryStackLedger.TryTransfer(Selection,Data.chest,stack,stack.count,
                    x=>x.Key,x=>x.count,(x,n)=>x.count=n,(x,n)=>x.Copy(n)))
                    throw new OverflowException("Cannot return selected materials to chest");
            Notify();
        }
        // Consume complete workbench batches and return every unused selected
        // material to its original storage pool, preserving stack quality.
        public void ConsumeSelectedBatches(RecipeDefinition recipe, int batches)
        {
            if (recipe == null || batches < 1)
                throw new ArgumentException("A completed recipe batch is required.");
            var required = new Dictionary<string, int>();
            foreach (var ingredient in recipe.ingredients)
            {
                if (!InventoryRequirements.TryScale(ingredient.count, batches, out int amount) ||
                    !InventoryRequirements.TryAdd(required, ingredient.itemId, amount))
                    throw new OverflowException("Recipe material count overflow");
            }

            // Validate the full transfer before changing the live selection.
            var remaining = Selection.Select(stack => stack.Copy(stack.count)).ToList();
            if (!InventoryStackLedger.TryConsume(remaining, required,
                stack => stack.itemId, stack => stack.count, (stack, count) => stack.count = count))
                throw new InvalidOperationException("Selected materials changed before crafting finished.");
            foreach (var group in remaining.GroupBy(stack => stack.Key))
                if (group.Sum(stack => (long)stack.count) > int.MaxValue -
                    InventoryStackLedger.Count(Data.chest, group.Key, stack => stack.Key, stack => stack.count))
                    throw new OverflowException("Cannot return unused materials to chest");

            if (!InventoryStackLedger.TryConsume(Selection, required,
                stack => stack.itemId, stack => stack.count, (stack, count) => stack.count = count))
                throw new InvalidOperationException("Prevalidated material consumption failed.");
            ReturnAll();
        }
        public void Reorder(List<Stack> list,Stack from,Stack before)
        {
            if(InventoryStackLedger.Reorder(list,from,before))Notify();
        }
        public bool AddFuel(Stack stack)
        {
            if(stack==null||!Data.chest.Contains(stack)||Data.fuel>=50)return false;
            int value=Catalog.Item(stack.itemId).fuelValue;if(value<=0)return false;
            if(!InventoryStackLedger.TryRemoveExact(Data.chest,stack,1,x=>x.count,(x,n)=>x.count=n))return false;
            Data.fuel=Math.Min(50,Data.fuel+value);Notify();return true;
        }
        // 핵심 분기: recipe==null||!recipe.enabled||batches<1||batches>999 판정.
        // 상태 변경: needed 갱신.
        // 다음 연결: InventoryRequirements.TryScale(int, int, out int) 호출.
        public bool FillRecipe(RecipeDefinition recipe,int batches)
        {
            if(recipe==null||!recipe.enabled||batches<1||batches>999)return false;
            var required=new Dictionary<string,int>();
            foreach(var req in recipe.ingredients)
            {
                if(req==null||!InventoryRequirements.TryScale(req.count,batches,out int amount)||
                   !InventoryRequirements.TryAdd(required,req.itemId,amount))return false;
            }
            if(!InventoryRequirements.HasEnough(required,id=>
                InventoryStackLedger.Count(Data.chest,id,x=>x.itemId,x=>x.count)+
                InventoryStackLedger.Count(Selection,id,x=>x.itemId,x=>x.count)))return false;
            ReturnAll();
            foreach(var req in recipe.ingredients)
            {
                int needed=req.count*batches;
                foreach(var s in Data.chest.Where(x=>x.itemId==req.itemId).ToArray())
                { int take=Math.Min(needed,s.count);Transfer(Data.chest,Selection,s,take);needed-=take;if(needed==0)break; }
            }
            Notify();return true;
        }
        // 핵심 분기: stack==null 판정.
        // 상태 변경: x.count 갱신.
        // 다음 연결: Blacksmith.BlacksmithCatalog.Item(string) 호출.
        public bool Equip(Stack stack)
        {
            if(stack==null)return false;
            var source=Data.chest.Contains(stack)?Data.chest:Data.bag.Contains(stack)?Data.bag:null;
            if(source==null)return false;
            var item=Catalog.Item(stack.itemId);var slot=item.equipmentSlot;
            if(string.IsNullOrEmpty(slot))return false;
            var weapon=Data.equipment.Find(x=>x.slot=="Weapon");
            var weaponDef=weapon==null?null:Catalog.Item(weapon.stack.itemId);
            if(slot=="Shield"&&weaponDef!=null&&weaponDef.twoHanded)return false;
            if(slot=="Arrow"&&(weaponDef==null||!weaponDef.bow))return false;
            if(slot=="Weapon")
            { if(item.twoHanded)Unequip("Shield"); if(!item.bow)Unequip("Arrow"); }
            var old=Data.equipment.Find(x=>x.slot==slot);
            if(slot=="Arrow"&&old!=null&&old.stack.Key==stack.Key)
            {
                var equipped=new List<Stack>{old.stack};
                if(!InventoryStackLedger.TryTransfer(source,equipped,stack,stack.count,
                    x=>x.Key,x=>x.count,(x,n)=>x.count=n,(x,n)=>x.Copy(n)))return false;
                Notify();return true;
            }
            Unequip(slot);
            int amount=item.arrow?stack.count:1;
            var equippedStack=stack.Copy(amount);
            if(!InventoryStackLedger.TryRemoveExact(source,stack,amount,x=>x.count,(x,n)=>x.count=n))return false;
            Data.equipment.Add(new EquipmentEntry{slot=slot,stack=equippedStack});Notify();return true;
        }
        public void Unequip(string slot)
        {
            var old=Data.equipment.Find(x=>x.slot==slot);if(old==null)return;
            Add(Data.chest,old.stack);Data.equipment.Remove(old);
            if(slot=="Weapon")Unequip("Arrow");Notify();
        }
        public string EquipmentStats()
        {
            float atk=0,armor=0,speed=0;
            foreach(var e in Data.equipment)
            {
                var d=Catalog.Item(e.stack.itemId);
                if(d==null)continue;
                float m=QualityRules.Multiplier(e.stack.quality);
                if(e.slot=="Weapon") { atk=d.attack*m; speed=d.EffectiveAttackSpeed; }
                if(d.material==MaterialKind.Armor)armor+=d.defense*m;
            }
            var effects=Data.equipment.Select(e=>Catalog.Item(e.stack.itemId)?.specialEffect)
                .Where(x=>!string.IsNullOrEmpty(x)).Distinct().ToArray();
            return $"장착 무기 공격력 {atk:0.##}   방어구 방어력 {armor:0.##}\n"+
                $"무기 공격 속도 {speed:0.##}/초 · 방어력의 50%만 피격 피해에서 감소\n"+
                (effects.Length==0?"특수 효과 없음":string.Join(" / ",effects));
        }
    }
}
