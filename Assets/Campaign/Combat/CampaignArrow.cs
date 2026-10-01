// [코드 지도] CampaignArrow: 발사된 화살의 이동, 충돌, 피해와 지면에 박힌 상태를 처리한다.
// 주요 함수: Update, Initialize
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/Campaign/Combat/CampaignArrow.cs.md

using UnityEngine;

public sealed class CampaignArrow : MonoBehaviour
{
    Vector2 velocity;float damage;GameObject owner;float life;string alloy;ItemData recoveredItem;
    Blacksmith.Stack recoveredStack;
    public void Initialize(Vector2 aim,float speed,float amount,GameObject source,ItemData item,
        Blacksmith.Stack stack,string attackAlloy=null)
    {
        velocity=aim.normalized*speed;
        damage=amount;
        owner=source;
        recoveredItem=item;
        recoveredStack=stack;
        alloy=attackAlloy??source.GetComponent<CampaignCombat>()?.WeaponAlloy??"";
    }
    void Update()
    {
        if(owner==null){Destroy(gameObject);return;}
        float delta=Time.deltaTime;
        Vector2 gravity=Physics2D.gravity;
        Vector2 step=velocity*delta+gravity*(.5f*delta*delta);
        life+=delta;
        float distance=step.magnitude;
        foreach(var hit in Physics2D.RaycastAll(transform.position,step.normalized,distance))
        {
            if(hit.collider.transform.IsChildOf(owner.transform))continue;
            var target=hit.collider.GetComponentInParent<IDamageable>();
            if(target!=null)
            {
                float before=(target as IHealthSource)?.CurrentHealth??0;
                target.TakeDamage(damage);
                if(target is IHealthSource health&&health.CurrentHealth<before)
                    owner.GetComponent<CampaignCombat>()?.OnDealtDamage(hit.collider.gameObject,alloy);
                CombatHitFeedback2D.Play(hit.collider.gameObject,hit.point);
                SpawnRecovery(hit.point);
                Destroy(gameObject);
                return;
            }
            if(hit.collider.isTrigger)continue;
            int layer=hit.collider.gameObject.layer;
            if(layer==LayerMask.NameToLayer("Ground")||layer==LayerMask.NameToLayer("Wall"))
            {
                SpawnRecovery(hit.point);
                Destroy(gameObject);
                return;
            }
        }
        transform.position+=(Vector3)step;
        velocity+=gravity*delta;
        if(velocity.sqrMagnitude>.01f)transform.right=velocity.normalized;
        if(life>6f)Destroy(gameObject);
    }

    void SpawnRecovery(Vector2 impact)
    {
        if(recoveredItem==null)return;
        var drop=new GameObject("Recoverable Arrow",typeof(SpriteRenderer),typeof(Rigidbody2D),typeof(CircleCollider2D));
        drop.transform.position=impact+Vector2.up*.12f;
        drop.transform.localScale=transform.localScale;
        int dropLayer=LayerMask.NameToLayer("ItemDrop");
        if(dropLayer>=0)drop.layer=dropLayer;
        var renderer=drop.GetComponent<SpriteRenderer>();
        renderer.sprite=GetComponent<SpriteRenderer>()?.sprite;
        renderer.sortingOrder=GetComponent<SpriteRenderer>()?.sortingOrder??0;
        var body=drop.GetComponent<Rigidbody2D>();
        body.gravityScale=1f;
        body.constraints=RigidbodyConstraints2D.FreezeRotation;
        body.linearVelocity=new Vector2(velocity.x*.15f,Mathf.Min(velocity.y,0f));
        drop.GetComponent<CircleCollider2D>().radius=.22f;
        var pickup=drop.AddComponent<ItemDropInteractable>();
        pickup.ConfigureItemData(recoveredItem);
        var combat=owner.GetComponent<CampaignCombat>();
        if(combat!=null)
            pickup.ConfigureAlternatePickup((_,count)=>combat!=null&&combat.TryReloadRecoveredArrow(recoveredStack,count));
        pickup.Initialize(1);
    }
}
