using System;
using UnityEngine;

public class PlayerAssimilate : MonoBehaviour, IDamageable
{
    [SerializeField] private int maxAssimilation = 100;
    [SerializeField] private int currentAssimilation = 10;

    public int CurrentAssimilation => currentAssimilation;
    public int MaxAssimilation => maxAssimilation;
    public bool IsDead => currentAssimilation <= 0;

    public event Action<int, int> AssimilationChanged;
    public event Action Died;

    public void Assimilate(int amount)
    {
        int previousAssimilation = currentAssimilation;
        currentAssimilation = Mathf.Clamp(currentAssimilation + amount, 0, maxAssimilation);

        if (currentAssimilation != previousAssimilation)
            AssimilationChanged?.Invoke(currentAssimilation, maxAssimilation);

        Debug.Log($"동화율: {currentAssimilation} / {maxAssimilation}");
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0 || IsDead)
            return;

        Assimilate(-amount);

        if (!IsDead)
            return;

        Debug.Log("Player died because assimilation reached zero.");
        Died?.Invoke();
    }

    private void OnValidate()
    {
        maxAssimilation = Mathf.Max(1, maxAssimilation);
        currentAssimilation = Mathf.Clamp(currentAssimilation, 0, maxAssimilation);
    }
}
