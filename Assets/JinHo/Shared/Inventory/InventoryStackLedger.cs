// [코드 지도] InventoryStackLedger: 인벤토리 스택의 추가·제거·수량 계산을 공통 규칙으로 처리한다.
// 주요 함수: TryRemove, TryTransfer, TryAdd
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Shared/Inventory/InventoryStackLedger.cs.md

using System;
using System.Collections.Generic;

/// <summary>
/// Shared, serialization-independent stack mutations for the field bag and smithy containers.
/// Callers supply their existing stack type and identity; no save schema changes are required.
/// </summary>
public static class InventoryStackLedger
{
    public static long Count<TStack, TKey>(IEnumerable<TStack> stacks, TKey key,
        Func<TStack, TKey> identity, Func<TStack, int> quantity)
    {
        if (stacks == null || identity == null || quantity == null) return 0;
        long total = 0;
        foreach (var stack in stacks)
            if (stack != null && EqualityComparer<TKey>.Default.Equals(identity(stack), key) && quantity(stack) > 0)
                total += quantity(stack);
        return total;
    }

    public static bool TryAdd<TStack, TKey>(List<TStack> stacks, TKey key, int amount,
        Func<TStack, TKey> identity, Func<TStack, int> quantity, Action<TStack, int> setQuantity,
        Func<TKey, int, TStack> create) where TStack : class
    {
        if (stacks == null || key == null || amount <= 0 || identity == null || quantity == null ||
            setQuantity == null || create == null) return false;
        var found = stacks.Find(s => s != null && EqualityComparer<TKey>.Default.Equals(identity(s), key));
        if (found == null)
        {
            var added = create(key, amount);
            if (added == null || quantity(added) != amount) return false;
            stacks.Add(added);
            return true;
        }
        int current = quantity(found);
        if (current < 0 || current > int.MaxValue - amount) return false;
        setQuantity(found, current + amount);
        return true;
    }

    public static bool TryRemove<TStack, TKey>(List<TStack> stacks, TKey key, int amount,
        Func<TStack, TKey> identity, Func<TStack, int> quantity, Action<TStack, int> setQuantity)
        where TStack : class
    {
        if (stacks == null || key == null || amount <= 0 || identity == null || quantity == null ||
            setQuantity == null || Count(stacks, key, identity, quantity) < amount) return false;
        int remaining = amount;
        for (int i = 0; i < stacks.Count && remaining > 0;)
        {
            var stack = stacks[i];
            if (stack == null || !EqualityComparer<TKey>.Default.Equals(identity(stack), key)) { i++; continue; }
            int count = quantity(stack);
            if (count <= 0) { i++; continue; }
            int taken = Math.Min(count, remaining);
            setQuantity(stack, count - taken);
            remaining -= taken;
            if (count == taken) stacks.RemoveAt(i); else i++;
        }
        return true;
    }

    public static bool TryRemoveExact<TStack>(List<TStack> stacks, TStack stack, int amount,
        Func<TStack, int> quantity, Action<TStack, int> setQuantity) where TStack : class
    {
        if (stacks == null || stack == null || amount <= 0 || quantity == null || setQuantity == null ||
            !stacks.Contains(stack) || quantity(stack) < amount) return false;
        int left = quantity(stack) - amount;
        setQuantity(stack, left);
        if (left == 0) stacks.Remove(stack);
        return true;
    }

    public static bool TryTransfer<TStack, TKey>(List<TStack> source, List<TStack> target,
        TStack stack, int amount, Func<TStack, TKey> identity, Func<TStack, int> quantity,
        Action<TStack, int> setQuantity, Func<TStack, int, TStack> copy) where TStack : class
    {
        if (source == null || target == null || ReferenceEquals(source, target) || stack == null ||
            amount <= 0 || identity == null || quantity == null || setQuantity == null || copy == null ||
            !source.Contains(stack) || quantity(stack) < amount) return false;
        var key = identity(stack);
        if (key == null) return false;
        var destination = target.Find(s => s != null && EqualityComparer<TKey>.Default.Equals(identity(s), key));
        if (destination != null && (quantity(destination) < 0 || quantity(destination) > int.MaxValue - amount)) return false;
        if (destination == null)
        {
            var newStack = copy(stack, amount);
            if (newStack == null || quantity(newStack) != amount) return false;
            target.Add(newStack);
        }
        else setQuantity(destination, quantity(destination) + amount);
        return TryRemoveExact(source, stack, amount, quantity, setQuantity);
    }

    public static bool TryConsume<TStack, TKey>(List<TStack> stacks, IReadOnlyDictionary<TKey, int> requirements,
        Func<TStack, TKey> identity, Func<TStack, int> quantity, Action<TStack, int> setQuantity)
        where TStack : class
    {
        if (stacks == null || requirements == null || identity == null || quantity == null || setQuantity == null)
            return false;
        foreach (var pair in requirements)
            if (pair.Key == null || pair.Value <= 0 || Count(stacks, pair.Key, identity, quantity) < pair.Value)
                return false;
        foreach (var pair in requirements)
            if (!TryRemove(stacks, pair.Key, pair.Value, identity, quantity, setQuantity))
                throw new InvalidOperationException("Prevalidated inventory consumption failed.");
        return true;
    }

    public static List<TStack> CopySnapshot<TStack>(IReadOnlyList<TStack> snapshot,
        Func<TStack, bool> valid, Func<TStack, TStack> copy)
    {
        if (snapshot == null || valid == null || copy == null) throw new ArgumentException("Invalid inventory snapshot");
        var result = new List<TStack>(snapshot.Count);
        foreach (var stack in snapshot)
        {
            if (!valid(stack)) throw new ArgumentException("Invalid inventory snapshot");
            result.Add(copy(stack));
        }
        return result;
    }

    public static bool Reorder<TStack>(List<TStack> stacks, TStack from, TStack before)
    {
        if (stacks == null || ReferenceEquals(from, before) || !stacks.Contains(from) || !stacks.Contains(before))
            return false;
        stacks.Remove(from);
        stacks.Insert(stacks.IndexOf(before), from);
        return true;
    }

    public static float TotalWeight<TStack>(IEnumerable<TStack> stacks, Func<TStack, float> weight)
    {
        if (stacks == null || weight == null) return 0;
        float total = 0;
        foreach (var stack in stacks) if (stack != null) total += weight(stack);
        return total;
    }

    public static bool FitsWeight(float current, float unitWeight, int amount, float capacity)
    {
        if (amount <= 0 || unitWeight < 0 || capacity < 0) return false;
        double after = (double)current + (double)unitWeight * amount;
        return !double.IsNaN(after) && after <= capacity + Math.Max(0.0001, Math.Abs(capacity) * 0.000001);
    }
}
