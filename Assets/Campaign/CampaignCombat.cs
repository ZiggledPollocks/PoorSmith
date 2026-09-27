using System.Collections.Generic;
using System.Linq;
using Blacksmith;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class CampaignCombat : MonoBehaviour
{
    public Sprite arrowSprite;
    PlayerInteraction interaction;PlayerInputHandler input;PlayerToolController tools;
    float nextSwing,charge;bool charging;
    public float WindArmorBonus=>SmithingLoop.Instance==null?0:SmithingLoop.Instance.SmithData.equipment.Count(x=>x.stack.itemId.StartsWith("wind_")&&SmithingLoop.Instance.Catalog.Item(x.stack.itemId)?.material==MaterialKind.Armor)*CampaignController.Instance.rules.temporaryWindArmorBonus;
    public void OnDealtDamage()
    {
        var weapon=SmithingLoop.Instance?.SmithData.equipment.FirstOrDefault(x=>x.slot=="Weapon");
        if(weapon?.stack.itemId.StartsWith("blood_")==true)GetComponent<PlayerAssimilate>().Assimilate(Mathf.CeilToInt(CampaignController.Instance.rules.temporaryVampireHeal));
    }
    public int ArrowCount=>SmithingLoop.Instance?.SmithData.equipment.FirstOrDefault(x=>x.slot=="Arrow")?.stack.count??0;
    void Awake(){interaction=GetComponent<PlayerInteraction>();input=GetComponent<PlayerInputHandler>();tools=GetComponent<PlayerToolController>();}
    public bool TryBeginToolAction(ToolData tool)
    {
        if(tool==null||CampaignController.Instance==null)return false;
        if(tool.ToolType==ToolType.Bow){charging=true;charge=0;return true;}
        if(tool.ToolType!=ToolType.Axe&&tool.ToolType!=ToolType.Pickaxe)return false;
        SwingTool(tool);return true;
    }
    void Update()
    {
        if(GameUIController.BlocksGameplayInput){charging=false;charge=0;return;}
        var tool=tools.CurrentTool;if(tool==null)return;
        if(input.IsLeftClickHeld&&(tool.ToolType==ToolType.Axe||tool.ToolType==ToolType.Pickaxe))SwingTool(tool);
        if(!charging)return;
        if(!input.IsLeftClickHeld||tool.ToolType!=ToolType.Bow){charging=false;charge=0;return;}
        charge+=Time.deltaTime;
        if(charge>=CampaignController.Instance.rules.bowChargeSeconds){Fire();charge=0;}
    }
    Vector2 Aim()
    {var cam=Camera.main;return cam!=null&&Mouse.current!=null?((Vector2)cam.ScreenToWorldPoint(Mouse.current.position.ReadValue())-(Vector2)transform.position).normalized:Vector2.right;}
    public void SwingTool(ToolData tool)
    {
        if(Time.time<nextSwing)return;nextSwing=Time.time+.45f;
        Vector2 direction=Aim();GetComponent<PlayerAnimationController>()?.PlayToolUse(direction);
        var attacked=new HashSet<IInteractable>();
        foreach(var hit in Physics2D.OverlapCircleAll(transform.position,tool.Reach))
        {
            var target=hit.GetComponentInParent<IInteractable>();if(target==null||attacked.Contains(target)||!target.CanInteract()||!target.CanUseTool(tool))continue;
            if(hit.GetComponentInParent<IResourceProvider>()==null)continue;
            Vector2 offset=hit.ClosestPoint(transform.position)-(Vector2)transform.position;
            if(offset.sqrMagnitude>.04f&&Vector2.Dot(direction,offset.normalized)<.25f)continue;
            attacked.Add(target);target.Interact(interaction);
        }
    }
    public bool Fire()
    {
        var loop=SmithingLoop.Instance;var ammo=loop.SmithData.equipment.FirstOrDefault(x=>x.slot=="Arrow");
        if(ammo==null||ammo.stack.count<=0){FallbackBranch();return false;}
        ammo.stack.count--;if(ammo.stack.count==0)loop.SmithData.equipment.Remove(ammo);
        Vector2 direction=Aim();var go=new GameObject("Crafted Arrow",typeof(SpriteRenderer),typeof(CampaignArrow));
        go.transform.position=(Vector2)transform.position+direction*.6f;go.transform.right=direction;
        go.GetComponent<SpriteRenderer>().sprite=arrowSprite;go.transform.localScale=Vector3.one*.45f;
        go.GetComponent<CampaignArrow>().Initialize(direction,tools.CurrentTool.Damage,gameObject);
        loop.RequestAutosave();if(ArrowCount==0)FallbackBranch();return true;
    }
    void FallbackBranch()
    {
        charging=false;var loop=SmithingLoop.Instance;var weapon=loop.SmithData.equipment.FirstOrDefault(x=>x.slot=="Weapon");
        if(weapon!=null){InventoryService.Add(loop.SmithData.chest,weapon.stack);loop.SmithData.equipment.Remove(weapon);}
        loop.RefreshEquipment();loop.RequestAutosave();
    }
    public int Mitigate(int damage)
    {
        if(GetComponent<PlayerMovement>()?.IsRolling==true)return 0;
        var loop=SmithingLoop.Instance;var campaign=CampaignController.Instance;if(loop==null||campaign==null)return damage;
        float defense=0;bool shield=false;
        foreach(var entry in loop.SmithData.equipment)
        {
            var def=loop.Catalog.Item(entry.stack.itemId);if(def==null)continue;
            if(def.material==MaterialKind.Armor)defense+=(def.defense>0?def.defense:campaign.rules.temporaryArmorDefense)*QualityRules.Multiplier(entry.stack.quality);
            if(entry.slot=="Shield")shield=true;
        }
        float applied=Mathf.Max(0,damage-defense*.5f);
        // Provisional shield mitigation: 50%; existing integer health rounds up once.
        if(shield&&Mouse.current?.rightButton.isPressed==true)applied*=.5f;
        return Mathf.CeilToInt(applied);
    }
}
