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
            if(incoming.count<=0)return;
            var existing=list.Find(x=>x.Key==incoming.Key);
            if(existing==null) list.Add(incoming.Copy(incoming.count)); else existing.count+=incoming.count;
        }
        public bool Transfer(List<Stack> from,List<Stack> to,Stack stack,int amount)
        {
            if(from==to||stack==null||amount<=0||!from.Contains(stack)||stack.count<amount)return false;
            Add(to,stack.Copy(amount)); stack.count-=amount; if(stack.count==0)from.Remove(stack);Notify();return true;
        }
        public bool Select(Stack stack,Station station)
        {
            int max=station==Station.Workbench?4:station==Station.Furnace?3:1;
            if(!Selection.Any(x=>x.itemId==stack.itemId)&&Selection.Select(x=>x.itemId).Distinct().Count()>=max)return false;
            return Transfer(Data.chest,Selection,stack,1);
        }
        public void ReturnAll() { foreach(var s in Selection)Add(Data.chest,s);Selection.Clear();Notify(); }
        public void Reorder(List<Stack> list,Stack from,Stack before)
        {
            if(from==before||!list.Contains(from)||!list.Contains(before))return;
            list.Remove(from);list.Insert(list.IndexOf(before),from);Notify();
        }
        public bool AddFuel(Stack stack)
        {
            if(stack==null||!Data.chest.Contains(stack)||Data.fuel>=50)return false;
            int value=Catalog.Item(stack.itemId).fuelValue;if(value<=0)return false;
            Data.fuel=Math.Min(50,Data.fuel+value);stack.count--;if(stack.count==0)Data.chest.Remove(stack);Notify();return true;
        }
        public bool FillRecipe(RecipeDefinition recipe,int batches)
        {
            if(recipe==null||!recipe.enabled||batches<1||batches>999)return false;
            foreach(var req in recipe.ingredients)
                if(Data.chest.Concat(Selection).Where(x=>x.itemId==req.itemId).Sum(x=>x.count)<req.count*batches)return false;
            ReturnAll();
            foreach(var req in recipe.ingredients)
            {
                int needed=req.count*batches;
                foreach(var s in Data.chest.Where(x=>x.itemId==req.itemId).ToArray())
                { int take=Math.Min(needed,s.count);Transfer(Data.chest,Selection,s,take);needed-=take;if(needed==0)break; }
            }
            Notify();return true;
        }
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
            {old.stack.count+=stack.count;source.Remove(stack);Notify();return true;}
            Unequip(slot);
            int amount=item.arrow?stack.count:1;
            Data.equipment.Add(new EquipmentEntry{slot=slot,stack=stack.Copy(amount)});
            stack.count-=amount;if(stack.count==0)source.Remove(stack);Notify();return true;
        }
        public void Unequip(string slot)
        {
            var old=Data.equipment.Find(x=>x.slot==slot);if(old==null)return;
            Add(Data.chest,old.stack);Data.equipment.Remove(old);
            if(slot=="Weapon")Unequip("Arrow");Notify();
        }
        public string EquipmentStats()
        {
            float atk=0,def=0,speed=0;
            foreach(var e in Data.equipment)
            {var d=Catalog.Item(e.stack.itemId);float m=QualityRules.Multiplier(e.stack.quality);atk+=d.attack*m;def+=d.defense*m;if(e.slot=="Weapon")speed=d.attackSpeed;}
            return $"공격력 {atk:0.##}   방어력 {def:0.##}\n공격 속도 {speed:0.##}\n"+string.Join("\n",Data.equipment.Select(e=>Catalog.Item(e.stack.itemId).specialEffect).Where(x=>!string.IsNullOrEmpty(x)));
        }
    }
}
