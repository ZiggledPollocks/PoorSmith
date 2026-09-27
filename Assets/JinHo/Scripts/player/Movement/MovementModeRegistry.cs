using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>A registry-scoped handle; stale handles cannot remove a newer registration.</summary>
public readonly struct MovementRegistration
{
    internal readonly MovementModeRegistry Registry;
    internal readonly long Id;
    internal MovementRegistration(MovementModeRegistry registry, long id) { Registry = registry; Id = id; }
}

public sealed class MovementModeRegistry
{
    private sealed class Entry
    {
        public long Id;
        public Object Source;
        public IMovementMode Mode;
        public int Priority;
        public MovementEnvironmentPolicy Policy;
    }

    private readonly List<Entry> entries = new();
    private long nextId;

    public MovementRegistration Register(Object source, IMovementMode mode, int priority,
        MovementEnvironmentPolicy policy, out bool added)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (mode == null) throw new ArgumentNullException(nameof(mode));
        Prune();
        foreach (Entry entry in entries)
        {
            if (entry.Source == source)
            {
                added = false;
                return new MovementRegistration(this, entry.Id);
            }
        }
        var item = new Entry { Id = ++nextId, Source = source, Mode = mode, Priority = priority, Policy = policy };
        entries.Add(item);
        added = true;
        return new MovementRegistration(this, item.Id);
    }

    public bool Contains(MovementRegistration handle)
    {
        if (handle.Registry != this) return false;
        Prune();
        return entries.Exists(entry => entry.Id == handle.Id);
    }

    public bool Unregister(MovementRegistration handle)
    {
        return handle.Registry == this && entries.RemoveAll(entry => entry.Id == handle.Id) != 0;
    }

    public IMovementMode Resolve(IMovementMode fallback)
    {
        Prune();
        Entry selected = null;
        foreach (Entry entry in entries)
            if (selected == null || entry.Priority >= selected.Priority) selected = entry;
        return selected != null ? selected.Mode : fallback;
    }

    public bool HasMode<T>() where T : IMovementMode
    {
        Prune();
        return entries.Exists(entry => entry.Mode is T);
    }

    public MovementEnvironmentPolicy Policy
    {
        get
        {
            Prune();
            bool blocksJump = false, suppressesFall = false;
            Entry gravityOwner = null;
            foreach (Entry entry in entries)
            {
                blocksJump |= entry.Policy.BlocksNormalJump;
                suppressesFall |= entry.Policy.SuppressesFallDamage;
                if (entry.Policy.GravityScale.HasValue &&
                    (gravityOwner == null || entry.Priority >= gravityOwner.Priority)) gravityOwner = entry;
            }
            return new MovementEnvironmentPolicy(blocksJump, suppressesFall, gravityOwner?.Policy.GravityScale);
        }
    }

    public void Clear() => entries.Clear();

    private void Prune()
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            Object source = entries[i].Source;
            if (source == null) { entries.RemoveAt(i); continue; }
            bool active = true;
            if (source is Collider2D collider) active = collider.enabled && collider.gameObject.activeInHierarchy;
            else if (source is Behaviour behaviour) active = behaviour.isActiveAndEnabled;
            else if (source is GameObject gameObject) active = gameObject.activeInHierarchy;
            if (!active) entries.RemoveAt(i);
        }
    }
}
