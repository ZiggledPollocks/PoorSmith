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

/// <summary>One consistent night multiplier for hostile field monsters.</summary>
public static class NightMonsterStats
{
    public const float Multiplier = 1.25f;
    public static bool IsNight => SmithingLoop.Instance?.Initialized == true &&
                                  SmithingLoop.Instance.SmithData.night;
    public static int Health(int baseHealth) => Mathf.CeilToInt(baseHealth * (IsNight ? Multiplier : 1f));
    public static int Damage(int baseDamage) => Mathf.CeilToInt(baseDamage * (IsNight ? Multiplier : 1f));
    public static float Speed(float baseSpeed) => baseSpeed * (IsNight ? Multiplier : 1f);

    // Keep the remaining health percentage when day turns into night or back.
    public static float RefreshHealth(float current, int baseMax, ref bool previousNight)
    {
        bool night = IsNight;
        if (previousNight == night) return current;
        int oldMax = Mathf.CeilToInt(baseMax * (previousNight ? Multiplier : 1f));
        previousNight = night;
        return oldMax > 0 ? Mathf.Min(Health(baseMax), current * Health(baseMax) / oldMax) : current;
    }
}
