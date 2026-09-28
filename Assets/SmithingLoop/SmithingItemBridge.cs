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
        string id=fieldId.Substring(6,split-6);if(content.catalog.Item(id)==null)return false;
        stack=new(id,count,(Quality)q);return true;
    }
    public ItemData FieldItem(Blacksmith.Stack stack)
    {
        var link=content.materials.FirstOrDefault(x=>x.smithItemId==stack.itemId);
        if(link!=null&&stack.quality==Quality.High)return link.fieldItem;
        var def=content.catalog.Item(stack.itemId);if(def==null)return null;
        string id="smith:"+stack.Key;if(generated.TryGetValue(id,out var result))return result;
        result=ScriptableObject.CreateInstance<ItemData>();
        result.ConfigureBridge(id,def.displayName+" · "+QualityRules.Name(stack.quality),def.description+(link==null?"\n운반 무게 1kg · 임시":""),link?.fieldItem.Weight??1,content.Art(def.sprite));
        generated.Add(id,result);return result;
    }
    public int Import(InventorySystem field,List<Blacksmith.Stack> target)
    {
        var owned=field.Items.Where(x=>x?.itemData!=null&&TryStack(x.itemData.ItemId,x.quantity,out _)).ToArray();
        var amounts=owned.Select(x=>new InventoryItem(x.itemData,x.quantity)).ToArray();
        if(!field.TryConsume(amounts))return 0;
        int count=0;foreach(var s in amounts){TryStack(s.itemData.ItemId,s.quantity,out var stack);InventoryService.Add(target,stack);count+=s.quantity;}return count;
    }
    public int Export(List<Blacksmith.Stack> source,InventorySystem field)
    {
        int remaining=0;foreach(var stack in source.ToArray())
        {var item=FieldItem(stack);if(item!=null&&field.TryAddItem(item,stack.count))source.Remove(stack);else remaining+=stack.count;}return remaining;
    }
    public void Dispose(){foreach(var item in generated.Values)if(item!=null)UnityEngine.Object.Destroy(item);generated.Clear();}
}
