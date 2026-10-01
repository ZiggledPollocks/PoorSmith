// [코드 지도] CampaignCombat: 플레이어의 캠페인 무기 공격을 시작하고 대상·피해·발사체를 연결한다.
// 주요 함수: SwingTool, OnReceivedHit, Fire
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/Campaign/Combat/CampaignCombat.cs.md

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
    [Header("Bow Draw")]
    [SerializeField, Min(0.1f)] float minimumArrowSpeed=12f;
    [SerializeField, Min(0.1f)] float maximumArrowSpeed=24f;
    float nextSwing,charge;bool charging;
    CameraLookAhead bowCamera;
    float lastMoveFacingX=1f;
    public float WindArmorBonus=>SmithingLoop.Instance==null||Rules==null?0:SmithingLoop.Instance.SmithData.equipment.Count(x=>x.stack.itemId.StartsWith("wind_")&&SmithingLoop.Instance.Catalog.Item(x.stack.itemId)?.material==MaterialKind.Armor)*Rules.temporaryWindArmorBonus;
    public string WeaponAlloy=>SmithingLoop.Instance?.SmithData.equipment.FirstOrDefault(x=>x.slot=="Weapon")?.stack.itemId??"";
    public bool IsGuarding=>SmithingLoop.Instance?.SmithData.equipment.Any(x=>x.slot=="Shield")==true&&Mouse.current?.rightButton.isPressed==true;
    public float GuardFacingX
    {
        get
        {
            float moveX=input?.MoveInput.x??0f;
            if(Mathf.Abs(moveX)>.01f)lastMoveFacingX=Mathf.Sign(moveX);
            return lastMoveFacingX;
        }
    }
    public bool IsGuardingAgainst(GameObject attacker,Vector2? attackOrigin=null,Vector2? incomingDirection=null)
    {
        if(!IsGuarding)return false;
        float towardAttack;
        if(incomingDirection.HasValue&&Mathf.Abs(incomingDirection.Value.x)>.01f)
            towardAttack=-incomingDirection.Value.x;
        else if(attackOrigin.HasValue)towardAttack=attackOrigin.Value.x-transform.position.x;
        else if(attacker!=null)towardAttack=attacker.transform.position.x-transform.position.x;
        else return false;
        return Mathf.Abs(towardAttack)>.05f&&Mathf.Sign(towardAttack)==GuardFacingX;
    }
    public void OnDealtDamage(GameObject target,string weaponAtLaunch=null)
    {
        string id=weaponAtLaunch??WeaponAlloy;
        if(id.StartsWith("blood_"))HealBlood();
        if(id.StartsWith("fire_"))Burn(target);
    }
    void HealBlood(){var health=GetComponent<PlayerAssimilate>();if(health!=null&&!health.IsDead)health.Assimilate(-2);}
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
    void Awake(){interaction=GetComponent<PlayerInteraction>();input=GetComponent<PlayerInputHandler>();tools=GetComponent<PlayerToolController>();lastMoveFacingX=GetComponent<SpriteRenderer>()?.flipX==true?-1f:1f;}
    public bool TryBeginToolAction(ToolData tool)
    {
        if(tool==null||Rules==null||SmithingLoop.Instance?.Initialized!=true)return false;
        if(tool.ToolType==ToolType.Bow)
        {
            if(ArrowCount<=0)return true;
            charging=true;charge=0;
            bowCamera ??= FindFirstObjectByType<CameraLookAhead>();
            return true;
        }
        if(tool.ToolType!=ToolType.Axe&&tool.ToolType!=ToolType.Pickaxe)return false;
        SwingTool(tool);return true;
    }
    void Update()
    {
        float moveX=input?.MoveInput.x??0f;
        if(Mathf.Abs(moveX)>.01f)lastMoveFacingX=Mathf.Sign(moveX);
        if(GameUIController.BlocksGameplayInput){CancelBowDraw();return;}
        var tool=tools.CurrentTool;if(tool==null){CancelBowDraw();return;}
        if(input.IsLeftClickHeld&&(tool.ToolType==ToolType.Axe||tool.ToolType==ToolType.Pickaxe))SwingTool(tool);
        if(!charging)return;
        if(tool.ToolType!=ToolType.Bow){CancelBowDraw();return;}
        if(!input.IsLeftClickHeld)
        {
            float draw=charge;
            CancelBowDraw();
            Fire(draw);
            return;
        }
        charge=Mathf.Min(charge+Time.deltaTime,Rules.bowChargeSeconds/ToolData.GlobalAttackRateMultiplier);
        if(bowCamera!=null&&Camera.main!=null&&Mouse.current!=null)
            bowCamera.SetBowAim(Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue()));
    }
    void OnDisable()=>CancelBowDraw();
    void CancelBowDraw(){charging=false;charge=0;bowCamera?.ClearBowAim();}
    Vector2 Aim()
    {var cam=Camera.main;return cam!=null&&Mouse.current!=null?((Vector2)cam.ScreenToWorldPoint(Mouse.current.position.ReadValue())-(Vector2)transform.position).normalized:Vector2.right;}
    // 핵심 분기: Time.time<nextSwing 판정.
    // 상태 변경: nextSwing 갱신.
    // 다음 연결: CampaignCombat.Aim() 호출.
    public void SwingTool(ToolData tool)
    {
        if (Time.time < nextSwing || Camera.main == null || Mouse.current == null) return;
        Vector2 pointer = Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        IInteractable chosen = null;
        GameObject wrongResource = null;
        float nearest = float.PositiveInfinity;
        foreach (var hit in Physics2D.OverlapPointAll(pointer))
        {
            var target = hit.GetComponentInParent<IInteractable>();
            if (target == null || !target.CanInteract()) continue;
            if (hit.GetComponentInParent<IResourceProvider>() == null &&
                !(tool.ToolType == ToolType.Pickaxe && target is StoneGolemController)) continue;
            float distance = Vector2.Distance(hit.ClosestPoint(transform.position), transform.position);
            if (distance > tool.Reach || distance >= nearest) continue;
            nearest = distance;
            if (target.CanUseTool(tool)) chosen = target;
            else if (target is Component resource) wrongResource = resource.gameObject;
        }
        if (chosen == null)
        {
            if (wrongResource != null)
            {
                nextSwing = Time.time + .45f/ToolData.GlobalAttackRateMultiplier;
                ResourceToolFeedback2D.PlayWrongTool(gameObject, wrongResource);
            }
            return;
        }
        nextSwing = Time.time + .45f/ToolData.GlobalAttackRateMultiplier;
        Vector2 direction = (pointer - (Vector2)transform.position).normalized;
        GetComponent<PlayerAnimationController>()?.PlayToolUse(direction,tool.AttackAnimationMultiplier);
        chosen.Interact(interaction);
    }
    public bool Fire()=>Fire(charge);
    bool Fire(float drawSeconds)
    {
        var loop=SmithingLoop.Instance;
        if(loop==null||!loop.Initialized||tools?.CurrentTool?.ToolType!=ToolType.Bow)return false;
        var ammo=loop.SmithData.equipment.FirstOrDefault(x=>x.slot=="Arrow");
        if(ammo==null||ammo.stack.count<=0)return false;
        var ammoDef=loop.Catalog.Item(ammo.stack.itemId);
        var recoveredItem=loop.FieldItemFor(ammo.stack.Copy(1));
        if(ammoDef==null||recoveredItem==null)return false;
        float arrowDamage=ammoDef!=null&&ammoDef.attack>0
            ? ammoDef.attack*QualityRules.Multiplier(ammo.stack.quality)
            : tools.CurrentTool.Damage;
        float fullDraw=Mathf.Max(.01f,Rules.bowChargeSeconds/ToolData.GlobalAttackRateMultiplier);
        float speed=Mathf.Lerp(minimumArrowSpeed,Mathf.Max(minimumArrowSpeed,maximumArrowSpeed),
            Mathf.Clamp01(drawSeconds/fullDraw));
        ammo.stack.count--;if(ammo.stack.count==0)loop.SmithData.equipment.Remove(ammo);
        Vector2 direction=Aim();var go=new GameObject("Crafted Arrow",typeof(SpriteRenderer),typeof(CampaignArrow));
        GetComponent<PlayerAnimationController>()?.PlayToolUse(direction,tools.CurrentTool.AttackAnimationMultiplier);
        go.transform.position=(Vector2)transform.position+direction*.6f;go.transform.right=direction;
        go.GetComponent<SpriteRenderer>().sprite=arrowSprite;go.transform.localScale=Vector3.one*.45f;
        go.GetComponent<CampaignArrow>().Initialize(direction,speed,arrowDamage,gameObject,recoveredItem,ammo.stack.Copy(1),
            ammo.stack.itemId.StartsWith("blood_")||ammo.stack.itemId.StartsWith("fire_")?ammo.stack.itemId:WeaponAlloy);
        loop.RequestAutosave();return true;
    }

    public bool TryReloadRecoveredArrow(Blacksmith.Stack arrow,int amount)
    {
        var loop=SmithingLoop.Instance;
        if(loop==null||!loop.Initialized||arrow==null||amount<=0)return false;
        var weapon=loop.SmithData.equipment.FirstOrDefault(x=>x.slot=="Weapon");
        if(loop.Catalog.Item(weapon?.stack.itemId)?.bow!=true)return false;
        var equipped=loop.SmithData.equipment.FirstOrDefault(x=>x.slot=="Arrow");
        if(equipped!=null&&equipped.stack.Key!=arrow.Key)return false;
        if(equipped!=null)
        {
            if(equipped.stack.count>int.MaxValue-amount)return false;
            equipped.stack.count+=amount;
        }
        else loop.SmithData.equipment.Add(new EquipmentEntry{slot="Arrow",stack=arrow.Copy(amount)});
        loop.RequestAutosave();
        return true;
    }
    public float Mitigate(float damage)=>Mitigate(damage,false);
    public float Mitigate(float damage,bool forwardGuard)
    {
        if(GetComponent<PlayerMovement>()?.IsRolling==true)return 0;
        var loop=SmithingLoop.Instance;var rules=Rules;if(loop==null||rules==null)return damage;
        float defense=0;
        foreach(var entry in loop.SmithData.equipment)
        {
            var def=loop.Catalog.Item(entry.stack.itemId);if(def==null)continue;
            if(def.material==MaterialKind.Armor)defense+=(def.defense>0?def.defense:rules.temporaryArmorDefense)*QualityRules.Multiplier(entry.stack.quality);
        }
        float applied=Mathf.Max(0,damage-defense*.5f);
        if(forwardGuard)
            applied*=rules.forwardShieldDamageMultiplier>0f?rules.forwardShieldDamageMultiplier:.30f;
        return CombatDamage.CeilTenth(applied);
    }
    public static float GuardedDamage(float damage,ItemDefinition shield,float now,ref float nextFullBlock)
    {
        // Legacy entry point: a guard is directional and calculated before mitigation.
        // Authored cooldown/reduction fields are retained for data compatibility, not generic full blocks.
        return shield==null?damage:damage*.30f;
    }
}
