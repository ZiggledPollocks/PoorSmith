using System;
using UnityEngine;

public class PlayerAssimilate : MonoBehaviour
{
    [SerializeField] private int maxAssimilation = 100;
    [SerializeField] private int currentAssimilation = 10;

    public int CurrentAssimilation => currentAssimilation;
    public int MaxAssimilation => maxAssimilation;
    public event Action<int, int> AssimilationChanged;

    public void Assimilate(int amount)
    {
        int previousAssimilation = currentAssimilation;
        currentAssimilation = Mathf.Clamp(currentAssimilation + amount, 0, maxAssimilation);

        if (currentAssimilation != previousAssimilation)
            AssimilationChanged?.Invoke(currentAssimilation, maxAssimilation);

        Debug.Log($"동화율: {currentAssimilation} / {maxAssimilation}");
    }

    private void OnValidate()
    {
        maxAssimilation = Mathf.Max(1, maxAssimilation);
        currentAssimilation = Mathf.Clamp(currentAssimilation, 0, maxAssimilation);
    }
}
