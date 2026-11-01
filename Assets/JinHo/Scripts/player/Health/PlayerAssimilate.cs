// [코드 지도] PlayerAssimilate: 플레이어 동화율을 수치 상태로 관리하며 IDamageable도 구현한다. 피해를 받으면 동화율이 감소하고0이면 사망 이벤트를 낸다. 회복/제물 조정과 피해 경로는 같은 수치 변경을 공유하지만 이벤트는 다르다.
// 주요 함수: ReceiveDamage, Assimilate, Awake
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/player/Health/PlayerAssimilate.cs.md

using System;
using UnityEngine;

/// <summary>Owns player health, damage intake and assimilation changes.</summary>
public class PlayerAssimilate : MonoBehaviour, IDamageable
{
    [SerializeField] private int maxAssimilation = 100;
    [SerializeField] private float currentAssimilation;

    public float CurrentAssimilation => currentAssimilation;
    public int MaxAssimilation => maxAssimilation;
    public float RemainingHealth => maxAssimilation - currentAssimilation;
    public bool IsDead => currentAssimilation >= maxAssimilation;

    public event Action<float, int> AssimilationChanged;
    public event Action<float> Damaged;
    public event Action Died;

    private void Awake()
    {
        // Older scenes serialize the former health-like value (80 or 100).
        // The campaign restores its saved remaining health after initialization.
        currentAssimilation = 0f;
    }

    public void RestoreRemainingHealth(float remainingHealth)
    {
        if (float.IsNaN(remainingHealth) || float.IsInfinity(remainingHealth)) return;
        float restored = maxAssimilation - Mathf.Clamp(remainingHealth, 1f, maxAssimilation);
        Assimilate(restored - currentAssimilation);
    }

    public void Assimilate(float amount)
    {
        if (float.IsNaN(amount) || float.IsInfinity(amount)) return;
        float previousAssimilation = currentAssimilation;
        currentAssimilation = Mathf.Clamp(CombatDamage.RoundHealth(currentAssimilation + amount), 0, maxAssimilation);

        if (currentAssimilation != previousAssimilation)
            AssimilationChanged?.Invoke(currentAssimilation, maxAssimilation);

        if (previousAssimilation < maxAssimilation && IsDead) Died?.Invoke();
        Debug.Log($"동화율: {currentAssimilation} / {maxAssimilation}");
    }

    public void TakeDamage(float amount) => ReceiveDamage(amount, null);
    public void ReceiveDamage(float amount, GameObject attacker,Vector2? attackOrigin=null,Vector2? incomingDirection=null)
    {
        if (amount <= 0 || float.IsNaN(amount) || float.IsInfinity(amount) || IsDead) return;
        var combat = GetComponent<CampaignCombat>();
        if (GetComponent<PlayerMovement>()?.IsRolling == true) return;
        bool guarded = combat != null && combat.IsGuardingAgainst(attacker,attackOrigin,incomingDirection);
        float applied = combat != null ? combat.Mitigate(amount,guarded) : CombatDamage.CeilTenth(amount);
        float before = currentAssimilation;
        Assimilate(applied);
        float lost = CombatDamage.RoundHealth(currentAssimilation - before);
        if (lost > 0) { Damaged?.Invoke(lost); CampaignDamageNumber.Show(gameObject, lost); }
        combat?.OnReceivedHit(attacker, guarded);
    }

    private void OnValidate()
    {
        maxAssimilation = Mathf.Max(1, maxAssimilation);
        currentAssimilation = Mathf.Clamp(currentAssimilation, 0, maxAssimilation);
    }
}
