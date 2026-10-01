// [코드 지도] CombatDamage: 공격자와 피격자의 공통 피해 전달 경로를 제공한다.
// 주요 함수: Apply, RoundHealth, CeilTenth
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/Campaign/Combat/CombatDamage.cs.md

using UnityEngine;

// Carries the attacker only for direct enemy hits. Environmental damage has no source.
public static class CombatDamage
{
    public static float RoundHealth(float value)=>Mathf.Round(value*10f)/10f;
    public static float CeilTenth(float value)=>(float)(System.Math.Ceiling((decimal)Mathf.Max(0,value)*10m)/10m);
    public static void Apply(IDamageable target,float amount,GameObject source,Vector2? attackOrigin=null,Vector2? incomingDirection=null)
    {
        if(target is PlayerAssimilate player)player.ReceiveDamage(amount,source,attackOrigin,incomingDirection);
        else target?.TakeDamage(amount);
    }
}
