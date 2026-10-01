// [코드 지도] IBehaviourState: 상태 진입 Enter, 반복 실행 Tick, 이탈 Exit의 공통 계약이다. 몬스터별 조건과 행동은 구현 상태에 있고, 호출 순서는 BehaviourStateMachine이 관리한다.
// 주요 함수: Enter, Tick, Exit
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/Assets/IBehaviourState.cs.md

/// <summary>Defines the lifecycle of a monster behaviour state.</summary>
public interface IBehaviourState
{
    void Enter();
    void Tick();
    void Exit();
}
