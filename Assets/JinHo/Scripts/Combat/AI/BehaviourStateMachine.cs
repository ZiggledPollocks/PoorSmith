using System;

/// <summary>One owner per machine. Same-instance transitions are no-ops.</summary>
public sealed class BehaviourStateMachine
{
    public IBehaviourState Current { get; private set; }

    public void Change(IBehaviourState next)
    {
        if (ReferenceEquals(Current, next)) return;
        if (next == null) throw new ArgumentNullException(nameof(next));
        Current?.Exit();
        Current = next;
        Current.Enter();
    }

    public void Tick() => Current?.Tick();
}
