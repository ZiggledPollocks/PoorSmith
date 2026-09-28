using UnityEngine;

public sealed class CampaignArrow : MonoBehaviour
{
    Vector2 direction;float damage;GameObject owner;float life;string alloy;
    public void Initialize(Vector2 aim,float amount,GameObject source,string attackAlloy=null){direction=aim.normalized;damage=amount;owner=source;alloy=attackAlloy??source.GetComponent<CampaignCombat>()?.WeaponAlloy??"";}
    void Update()
    {
        float distance=18*Time.deltaTime;life+=Time.deltaTime;
        foreach(var hit in Physics2D.RaycastAll(transform.position,direction,distance))
        {
            if(owner==null){Destroy(gameObject);return;}
            if(hit.collider.transform.IsChildOf(owner.transform))continue;
            var target=hit.collider.GetComponentInParent<IDamageable>();
            if(target!=null){float before=(target as IHealthSource)?.CurrentHealth??0;target.TakeDamage(damage);if(target is IHealthSource health&&health.CurrentHealth<before)owner.GetComponent<CampaignCombat>()?.OnDealtDamage(hit.collider.gameObject,alloy);CombatHitFeedback2D.Play(hit.collider.gameObject,hit.point);Destroy(gameObject);return;}
            if(hit.collider.isTrigger)continue;
            int layer=hit.collider.gameObject.layer;if(layer==LayerMask.NameToLayer("Ground")||layer==LayerMask.NameToLayer("Wall")){Destroy(gameObject);return;}
        }
        transform.position+=(Vector3)(direction*distance);if(life>4)Destroy(gameObject);
    }
}
