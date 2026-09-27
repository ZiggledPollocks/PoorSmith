using System.Collections.Generic;
using UnityEngine;

/// <summary>Adapts the existing Flame layer to the shared environment registry.</summary>
public sealed class FlameContactSensor
{
    private sealed class Contact
    {
        public int Count;
        public MovementRegistration Registration;
    }
    private readonly PlayerMovement player;
    private readonly IMovementMode mode;
    private readonly Dictionary<Collider2D, Contact> contacts = new();
    private readonly List<Collider2D> stale = new();
    public LayerMask Layers { get; set; }

    public FlameContactSensor(PlayerMovement player, IMovementMode mode)
    {
        this.player = player;
        this.mode = mode;
    }

    public void Enter(Collider2D other)
    {
        if (!Accepts(other)) return;
        if (contacts.TryGetValue(other, out Contact contact)) { contact.Count++; return; }
        Add(other);
    }

    public void Stay(Collider2D other)
    {
        if (Accepts(other) && !contacts.ContainsKey(other)) Add(other);
    }

    public void Exit(Collider2D other)
    {
        if (!contacts.TryGetValue(other, out Contact contact)) return;
        if (--contact.Count > 0) return;
        player.UnregisterEnvironmentMode(contact.Registration);
        contacts.Remove(other);
    }

    public void Prune()
    {
        stale.Clear();
        foreach (var pair in contacts)
            if (!Accepts(pair.Key)) stale.Add(pair.Key);
        foreach (Collider2D collider in stale)
        {
            player.UnregisterEnvironmentMode(contacts[collider].Registration);
            contacts.Remove(collider);
        }
    }

    public void Clear() => contacts.Clear();

    private bool Accepts(Collider2D other) => other != null && other.enabled &&
        other.gameObject.activeInHierarchy && (Layers.value & (1 << other.gameObject.layer)) != 0;

    private void Add(Collider2D other)
    {
        contacts.Add(other, new Contact
        {
            Count = 1,
            Registration = player.RegisterEnvironmentMode(other, mode,
                FlameMovementMode.Priority, MovementEnvironmentPolicy.Floating)
        });
    }
}
