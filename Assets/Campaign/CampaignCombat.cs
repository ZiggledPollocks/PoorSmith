using System.Collections.Generic;
using System.Linq;
using Blacksmith;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class CampaignCombat : MonoBehaviour
{
    public Sprite arrowSprite;
    public CampaignRules fieldRules;
    CampaignRules Rules=>CampaignController.Instance?.rules??fieldRules;
    PlayerInteraction interaction;PlayerInputHandler input;PlayerToolController tools;
    float nextSwing,charge,nextFullBlock;bool charging;
    public float WindArmorBonus=>SmithingLoop.Instance==null||Rules==null?0:SmithingLoop.Instance.SmithData.equipment.Count(x=>x.stack.itemId.StartsWith("wind_")&&SmithingLoop.Instance.Catalog.Item(x.stack.itemId)?.material==MaterialKind.Armor)*Rules.temporaryWindArmorBonus;
    public string WeaponAlloy=>SmithingLoop.Instance?.SmithData.equipment.FirstOrDefault(x=>x.slot=="Weapon")?.stack.itemId??"";
    public bool IsGuarding=>SmithingLoop.Instance?.SmithData.equipment.Any(x=>x.slot=="Shield")==true&&Mouse.current?.rightButton.isPressed==true;
    public void OnDealtDamage(GameObject target,string weaponAtLaunch=null)
    {
        string id=weaponAtLaunch??WeaponAlloy;
        if(id.StartsWith("blood_"))HealBlood();
        if(id.StartsWith("fire_"))Burn(target);
    }
    void HealBlood(){var health=GetComponent<PlayerAssimilate>();if(health!=null&&!health.IsDead)health.Assimilate(2);}
    void Burn(GameObject target)
    {
        var rules=Rules;
        if(target!=null&&rules!=null)CampaignBurn.Apply(target,rules.temporaryBurnDamage,rules.temporaryBurnSeconds);
    }
    public void OnReceivedHit(GameObject attacker,bool guarded)
    {
        var loop=SmithingLoop.Instance;if(loop==null||attacker==null)return;
        var enemy=attacker.GetComponentInParent<IHealthSource>();if(enemy==null||enemy.IsDead)return;
        var root=(enemy as Component)?.gameObject;if(root==null)return;
        var equipment=loop.SmithData.equipment;
        if(guarded)
        {
            string shield=equipment.FirstOrDefault(x=>x.slot=="Shield")?.stack.itemId??"";
            if(shield.StartsWith("blood_"))HealBlood();
            if(shield.StartsWith("fire_"))Burn(root);
        }
        int blood=equipment.Count(x=>x.slot!="Shield"&&x.stack.itemId.StartsWith("blood_")&&loop.Catalog.Item(x.stack.itemId)?.material==MaterialKind.Armor);
        if(blood>0)enemy.TakeDamage(new[]{0,5,12,18,24}[Mathf.Min(4,blood)]);
        if(equipment.Any(x=>x.slot!="Shield"&&x.stack.itemId.StartsWith("fire_")&&loop.Catalog.Item(x.stack.itemId)?.material==MaterialKind.Armor))Burn(root);
    }
    public int ArrowCount=>SmithingLoop.Instance?.SmithData.equipment.FirstOrDefault(x=>x.slot=="Arrow")?.stack.count??0;
    void Awake(){interaction=GetComponent<PlayerInteraction>();input=GetComponent<PlayerInputHandler>();tools=GetComponent<PlayerToolController>();}
    public bool TryBeginToolAction(ToolData tool)
    {
        if(tool==null||Rules==null||SmithingLoop.Instance?.Initialized!=true)return false;
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
        if(charge>=Rules.bowChargeSeconds){Fire();charge=0;}
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
        var ammoDef=loop.Catalog.Item(ammo.stack.itemId);
        float arrowDamage=ammoDef!=null&&ammoDef.attack>0
            ? ammoDef.attack*QualityRules.Multiplier(ammo.stack.quality)
            : tools.CurrentTool.Damage;
        ammo.stack.count--;if(ammo.stack.count==0)loop.SmithData.equipment.Remove(ammo);
        Vector2 direction=Aim();var go=new GameObject("Crafted Arrow",typeof(SpriteRenderer),typeof(CampaignArrow));
        go.transform.position=(Vector2)transform.position+direction*.6f;go.transform.right=direction;
        go.GetComponent<SpriteRenderer>().sprite=arrowSprite;go.transform.localScale=Vector3.one*.45f;
        go.GetComponent<CampaignArrow>().Initialize(direction,arrowDamage,gameObject,ammo.stack.itemId.StartsWith("blood_")||ammo.stack.itemId.StartsWith("fire_")?ammo.stack.itemId:WeaponAlloy);
        loop.RequestAutosave();if(ArrowCount==0)FallbackBranch();return true;
    }
    void FallbackBranch()
    {
        charging=false;var loop=SmithingLoop.Instance;var weapon=loop.SmithData.equipment.FirstOrDefault(x=>x.slot=="Weapon");
        if(weapon!=null){InventoryService.Add(loop.SmithData.chest,weapon.stack);loop.SmithData.equipment.Remove(weapon);}
        loop.RefreshEquipment();loop.RequestAutosave();
    }
    public float Mitigate(float damage)
    {
        if(GetComponent<PlayerMovement>()?.IsRolling==true)return 0;
        var loop=SmithingLoop.Instance;var rules=Rules;if(loop==null||rules==null)return damage;
        float defense=0;ItemDefinition shield=null;
        foreach(var entry in loop.SmithData.equipment)
        {
            var def=loop.Catalog.Item(entry.stack.itemId);if(def==null)continue;
            if(def.material==MaterialKind.Armor)defense+=(def.defense>0?def.defense:rules.temporaryArmorDefense)*QualityRules.Multiplier(entry.stack.quality);
            if(entry.slot=="Shield")shield=def;
        }
        float applied=Mathf.Max(0,damage-defense*.5f);
        if(shield!=null&&Mouse.current?.rightButton.isPressed==true)
            applied=GuardedDamage(applied,shield,Time.time,ref nextFullBlock);
        return CombatDamage.CeilTenth(applied);
    }
    public static float GuardedDamage(float damage,ItemDefinition shield,float now,ref float nextFullBlock)
    {
        if(shield==null||shield.shieldCooldownSeconds<=0)return damage*.5f; // Unspecified shields retain the existing provisional reduction.
        if(now>=nextFullBlock){nextFullBlock=now+shield.shieldCooldownSeconds;return 0;}
        return damage*(1-shield.shieldCooldownReduction);
    }
}
