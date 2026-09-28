using System.Collections.Generic;
using UnityEngine;

// Equal profiles refresh duration without delaying ticks; different profiles coexist.
public sealed class CampaignBurn : MonoBehaviour
{
    sealed class Flame { public float damage,duration,remaining,untilTick=1; }
    readonly List<Flame> flames=new();IHealthSource health;
    public int ActiveProfiles=>flames.Count;
    public static void Apply(GameObject target,float damage,float seconds)
    {
        if(target==null||damage<=0||seconds<=0||float.IsNaN(damage)||float.IsNaN(seconds)||float.IsInfinity(damage)||float.IsInfinity(seconds))return;
        var health=target.GetComponentInParent<IHealthSource>();
        if(health==null||health.IsDead||!(health is Component component))return;
        var burn=component.GetComponent<CampaignBurn>()??component.gameObject.AddComponent<CampaignBurn>();burn.health=health;
        var same=burn.flames.Find(f=>f.damage==damage&&f.duration==seconds);
        if(same!=null)same.remaining=seconds;
        else burn.flames.Add(new Flame{damage=damage,duration=seconds,remaining=seconds});
    }
    void Update()=>Tick(Time.deltaTime);
    public void Tick(float elapsed)
    {
        if(health==null||health.IsDead){flames.Clear();return;}
        if(elapsed<=0)return;
        for(int i=flames.Count-1;i>=0;i--)
        {
            var f=flames[i];float step=Mathf.Min(elapsed,f.remaining);f.remaining-=step;f.untilTick-=step;
            while(f.untilTick<=0.00001f&&!health.IsDead){health.TakeDamage(f.damage);f.untilTick+=1;}
            if(f.remaining<=0)flames.RemoveAt(i);
        }
        if(health.IsDead)flames.Clear();
    }
    void OnDisable()=>flames.Clear();
}
