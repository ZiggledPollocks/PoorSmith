// [코드 지도] BehaviourStateMachine: 현재 상태 하나를 보관하고 다른 상태로 바꿀 때 이전 Exit → 대입 → 새 Enter를 실행한다. 같은 인스턴스로 전환하면 아무 일도 하지 않는다.
// 주요 함수: Change, Tick
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/Combat/AI/BehaviourStateMachine.cs.md

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
