// [코드 지도] InventoryRequirements: 아이템 이동·보관에 필요한 무게와 슬롯 조건을 판정한다.
// 주요 함수: TryAdd, HasEnough, TryScale
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Shared/Inventory/InventoryRequirements.cs.md

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
