using System.Collections.Generic;
using System.Linq;
using Blacksmith;
using UnityEngine;

public sealed class GatheringEquipmentBridge : System.IDisposable
{
    readonly PlayerToolController tools;readonly Dictionary<string,(int slot,ToolData original)> slots=new();
    readonly Dictionary<string,ToolData> runtime=new();
    public GatheringEquipmentBridge(PlayerToolController value)
    {
        tools=value;for(int i=0;i<tools.ToolSlotCount;i++)
        {var tool=tools.GetToolAtSlot(i);if(tool==null||tool.IsWeapon)continue;slots[tool.ToolType==ToolType.Axe?"Axe":"Pickaxe"]=(i,tool);}
    }
    public Sprite Icon(string name)
    {string slot=name=="linked_pick"?"Pickaxe":name=="linked_axe"?"Axe":null;return slot!=null&&slots.TryGetValue(slot,out var value)?value.original.Icon:null;}
    public void Migrate(CampaignState campaign,SaveData data,BlacksmithCatalog catalog)
    {
        if(campaign.toolsLinked)return;
        foreach(var pair in slots)
        {
            if(data.equipment.Any(e=>e.slot==pair.Key))continue;
            string kind=pair.Key=="Axe"?"axe":"pick";int tier=kind=="axe"?campaign.axeTier:campaign.pickTier;
            string id=$"town_{kind}_{Mathf.Clamp(tier,1,3)}";
            if(catalog.Item(id)==null)return;
            data.equipment.Add(new EquipmentEntry{slot=pair.Key,stack=new Blacksmith.Stack(id,1)});
        }
        campaign.toolsLinked=true;
    }
    public void Apply(SaveData data,BlacksmithCatalog catalog,CampaignState campaign)
    {
        string selectedId=tools.CurrentTool?.ToolId;
        foreach(var pair in slots)
        {
            var entry=data.equipment.FirstOrDefault(e=>e.slot==pair.Key);var def=entry==null?null:catalog.Item(entry.stack.itemId);
            if(def!=null&&runtime.TryGetValue(pair.Key,out var existing)&&existing!=null&&
               existing.ToolId==def.id&&existing.Tier==def.toolTier)
            {
                if(pair.Key=="Axe")campaign.axeTier=def.toolTier;else campaign.pickTier=def.toolTier;
                continue;
            }
            if(runtime.TryGetValue(pair.Key,out var previous)){tools.SetToolSlot(pair.Value.slot,null);Object.Destroy(previous);runtime.Remove(pair.Key);}
            if(def==null||def.toolTier<=0){tools.SetToolSlot(pair.Value.slot,null);continue;}
            var copy=Object.Instantiate(pair.Value.original);copy.ConfigureCatalogIdentity(def.id);copy.ConfigureTier(def.toolTier);copy.ConfigureCrafted(def.displayName,pair.Value.original.Damage,pair.Value.original.AttackSpeed,1);
            runtime[pair.Key]=copy;tools.SetToolSlot(pair.Value.slot,copy);
            if(pair.Key=="Axe")campaign.axeTier=def.toolTier;else campaign.pickTier=def.toolTier;
        }
        if(!string.IsNullOrEmpty(selectedId)&&tools.CurrentTool?.ToolId!=selectedId)
            tools.SelectToolId(selectedId);
    }
    public void Dispose(){foreach(var v in runtime.Values)if(v!=null)Object.Destroy(v);runtime.Clear();}
}
