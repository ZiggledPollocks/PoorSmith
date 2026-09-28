using UnityEngine;

// Carries the attacker only for direct enemy hits. Environmental damage has no source.
public static class CombatDamage
{
    public static float RoundHealth(float value)=>Mathf.Round(value*10f)/10f;
    public static float CeilTenth(float value)=>(float)(System.Math.Ceiling((decimal)Mathf.Max(0,value)*10m)/10m);
    public static void Apply(IDamageable target,float amount,GameObject source)
    {
        if(target is PlayerAssimilate player)player.ReceiveDamage(amount,source);
        else target?.TakeDamage(amount);
    }
}
