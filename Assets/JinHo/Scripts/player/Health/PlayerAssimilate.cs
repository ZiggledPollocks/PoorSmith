using System;
using UnityEngine;

public class PlayerAssimilate : MonoBehaviour, IDamageable
{
    [SerializeField] private int maxAssimilation = 100;
    [SerializeField] private float currentAssimilation = 10;

    public float CurrentAssimilation => currentAssimilation;
    public int MaxAssimilation => maxAssimilation;
    public bool IsDead => currentAssimilation <= 0;

    public event Action<float, int> AssimilationChanged;
    public event Action<float> Damaged;
    public event Action Died;

    public void Assimilate(float amount)
    {
        if(float.IsNaN(amount)||float.IsInfinity(amount))return;
        float previousAssimilation = currentAssimilation;
        currentAssimilation = Mathf.Clamp(CombatDamage.RoundHealth(currentAssimilation + amount), 0, maxAssimilation);

        if (currentAssimilation != previousAssimilation)
            AssimilationChanged?.Invoke(currentAssimilation, maxAssimilation);

        if (previousAssimilation > 0 && IsDead) Died?.Invoke();
        Debug.Log($"동화율: {currentAssimilation} / {maxAssimilation}");
    }

    public void TakeDamage(float amount)=>ReceiveDamage(amount,null);
    public void ReceiveDamage(float amount,GameObject attacker)
    {
        if(amount<=0||float.IsNaN(amount)||float.IsInfinity(amount)||IsDead)return;
        var combat=GetComponent<CampaignCombat>();
        if(GetComponent<PlayerMovement>()?.IsRolling==true)return;
        bool guarded=combat!=null&&combat.IsGuarding;
        float applied=combat!=null?combat.Mitigate(amount):CombatDamage.CeilTenth(amount);
        float before=currentAssimilation;
        Assimilate(-applied);
        float lost=CombatDamage.RoundHealth(before-currentAssimilation);
        if(lost>0){Damaged?.Invoke(lost);CampaignDamageNumber.Show(gameObject,lost);}
        combat?.OnReceivedHit(attacker,guarded);
    }

    private void OnValidate()
    {
        maxAssimilation = Mathf.Max(1, maxAssimilation);
        currentAssimilation = Mathf.Clamp(currentAssimilation, 0, maxAssimilation);
    }
}
