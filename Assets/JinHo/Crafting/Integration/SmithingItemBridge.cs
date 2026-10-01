// [코드 지도] SmithingItemBridge: 필드 ItemData와 대장간 Stack의 ID·품질·수량을 변환하고 소유권을 이전한다.
// 주요 함수: Import, Export, FieldItem
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Crafting/Integration/SmithingItemBridge.cs.md

using System;
using System.Collections.Generic;
using System.Linq;
using Blacksmith;
using UnityEngine;

// Field ItemData is a runtime view of catalog identity + quality. Ownership moves,
// never copies, across the two existing inventory implementations.
public sealed class SmithingItemBridge : IDisposable
{
    readonly SmithingLoopContent content;readonly Dictionary<string,ItemData> generated=new();
    public SmithingItemBridge(SmithingLoopContent value){content=value;}
    public ItemData Resolve(string id)
    {
        var original=content.materials.FirstOrDefault(x=>x.fieldItem.ItemId==id);
        if(original!=null)return original.fieldItem;
        if(!TryStack(id,1,out var stack))return null;
        return FieldItem(stack);
    }
    public bool TryStack(string fieldId,int count,out Blacksmith.Stack stack)
    {
        stack=null;var link=content.materials.FirstOrDefault(x=>x.fieldItem.ItemId==fieldId);
        if(link!=null){stack=new(link.smithItemId,count);return true;}
        if(fieldId==null||!fieldId.StartsWith("smith:"))return false;
        int split=fieldId.LastIndexOf(':');if(split<=6||!int.TryParse(fieldId.Substring(split+1),out int q)||q<0||q>4)return false;
        string id=fieldId.Substring(6,split-6);var definition=content.catalog.Item(id);if(definition==null)return false;
        stack=new(id,count,QualityRules.AppliesTo(definition)?(Quality)q:Quality.High);return true;
    }
    public ItemData FieldItem(Blacksmith.Stack stack)
    {
        var link=content.materials.FirstOrDefault(x=>x.smithItemId==stack.itemId);
        var def=content.catalog.Item(stack.itemId);if(def==null)return null;
        var quality=QualityRules.AppliesTo(def)?stack.quality:Quality.High;
        if(link!=null&&quality==Quality.High)return link.fieldItem;
        string id="smith:"+stack.itemId+":"+(int)quality;if(generated.TryGetValue(id,out var result))return result;
        result=ScriptableObject.CreateInstance<ItemData>();
        result.ConfigureBridge(id,def.displayName+(QualityRules.AppliesTo(def)?" · "+QualityRules.Name(quality):""),def.description+(link==null?"\n운반 무게 CSV 임시값":""),link?.fieldItem.Weight??def.carryWeightKg,content.ArtForItem(def));
        generated.Add(id,result);return result;
    }
    public int Import(InventorySystem field,List<Blacksmith.Stack> target)
    {
        var owned=field.Items.Where(x=>x?.itemData!=null&&TryStack(x.itemData.ItemId,x.quantity,out _)).ToArray();
        var amounts=owned.Select(x=>new InventoryItem(x.itemData,x.quantity)).ToArray();
        var incoming=new Dictionary<string,int>(StringComparer.Ordinal);
        foreach(var item in amounts)
        {
            TryStack(item.itemData.ItemId,item.quantity,out var mapped);
            if(!InventoryRequirements.TryAdd(incoming,mapped.Key,mapped.count))return 0;
        }
        foreach(var pair in incoming)
            if(InventoryStackLedger.Count(target,pair.Key,x=>x.Key,x=>x.count)>int.MaxValue-pair.Value)
                return 0;
        if(!field.TryConsume(amounts))return 0;
        int count=0;foreach(var s in amounts){TryStack(s.itemData.ItemId,s.quantity,out var stack);InventoryService.Add(target,stack);count+=s.quantity;}return count;
    }
    public int Export(List<Blacksmith.Stack> source,InventorySystem field)
    {
        int remaining=0;foreach(var stack in source.ToArray())
        {
            var item=FieldItem(stack);
            if(item!=null&&source.Contains(stack)&&field.TryAddItem(item,stack.count))
            {
                if(!InventoryStackLedger.TryRemoveExact(source,stack,stack.count,x=>x.count,(x,n)=>x.count=n))
                    throw new InvalidOperationException("Exported item was not removed from the source bag");
            }
            else remaining+=stack.count;
        }
        return remaining;
    }
    public void Dispose(){foreach(var item in generated.Values)if(item!=null)UnityEngine.Object.Destroy(item);generated.Clear();}
}
