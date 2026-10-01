// [코드 지도] MovementZoneContacts: 여러 플레이어 충돌체가 같은 구역과 겹쳐도 플레이어당 환경 등록 하나를 유지한다. 마지막 유효 접촉이 사라질 때 등록을 해제한다.
// 주요 함수: Prune, Stay, MovementZoneContacts
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/player/Movement/MovementZoneContacts.cs.md

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One environment registration per player, even with multiple player colliders.</summary>
public sealed class MovementZoneContacts
{
    private readonly Behaviour source;
    private readonly Func<PlayerMovement, IMovementMode> createMode;
    private readonly int priority;
    private readonly MovementEnvironmentPolicy policy;
    private readonly Dictionary<Collider2D, PlayerMovement> contacts = new();
    private readonly Dictionary<PlayerMovement, MovementRegistration> registrations = new();
    private readonly List<Collider2D> staleContacts = new();
    private readonly List<PlayerMovement> stalePlayers = new();

    public MovementZoneContacts(Behaviour source, Func<PlayerMovement, IMovementMode> createMode,
        int priority, MovementEnvironmentPolicy policy)
    {
        this.source = source;
        this.createMode = createMode;
        this.priority = priority;
        this.policy = policy;
    }

    public void Stay(Collider2D collider)
    {
        Prune();
        if (source == null || !source.isActiveAndEnabled || collider == null || !collider.enabled) return;
        PlayerMovement player = collider.attachedRigidbody != null
            ? collider.attachedRigidbody.GetComponent<PlayerMovement>() : null;
        if (player == null) player = collider.GetComponentInParent<PlayerMovement>();
        if (player == null || !player.isActiveAndEnabled) return;
        contacts[collider] = player;
        if (!registrations.TryGetValue(player, out MovementRegistration handle) ||
            !player.IsEnvironmentRegistered(handle))
            registrations[player] = player.RegisterEnvironmentMode(source, createMode(player), priority, policy);
    }

    public void Exit(Collider2D collider)
    {
        contacts.Remove(collider);
        Prune();
    }

    public void Prune()
    {
        staleContacts.Clear();
        foreach (var pair in contacts)
            if (pair.Key == null || !pair.Key.enabled || !pair.Key.gameObject.activeInHierarchy ||
                pair.Value == null || !pair.Value.isActiveAndEnabled) staleContacts.Add(pair.Key);
        foreach (Collider2D collider in staleContacts) contacts.Remove(collider);

        stalePlayers.Clear();
        foreach (var pair in registrations)
            if (pair.Key == null || !contacts.ContainsValue(pair.Key)) stalePlayers.Add(pair.Key);
        foreach (PlayerMovement player in stalePlayers)
        {
            if (player != null) player.UnregisterEnvironmentMode(registrations[player]);
            registrations.Remove(player);
        }
    }

    public void Clear()
    {
        foreach (var pair in registrations)
            if (pair.Key != null) pair.Key.UnregisterEnvironmentMode(pair.Value);
        registrations.Clear();
        contacts.Clear();
    }
}
