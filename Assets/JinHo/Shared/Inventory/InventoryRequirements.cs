using System;
using System.Collections.Generic;

/// <summary>Common quantity validation for field inventory and smithy recipes.</summary>
public static class InventoryRequirements
{
    public static bool TryAdd<TKey>(Dictionary<TKey, int> totals, TKey key, int amount)
    {
        if (totals == null || key == null || amount <= 0)
            return false;

        int previous = totals.TryGetValue(key, out int current) ? current : 0;
        if (previous > int.MaxValue - amount)
            return false;

        totals[key] = previous + amount;
        return true;
    }

    public static bool TryScale(int amount, int batches, out int total)
    {
        total = 0;
        if (amount <= 0 || batches <= 0 || amount > int.MaxValue / batches)
            return false;

        total = amount * batches;
        return true;
    }

    public static bool HasEnough<TKey>(Dictionary<TKey, int> totals, Func<TKey, long> available)
    {
        if (totals == null || available == null)
            return false;

        foreach (var requirement in totals)
            if (available(requirement.Key) < requirement.Value)
                return false;

        return true;
    }
}
