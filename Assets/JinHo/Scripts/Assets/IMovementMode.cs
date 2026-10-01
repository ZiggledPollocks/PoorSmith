// [코드 지도] IMovementMode: 물리 객체를 직접 변경하지 않고 MovementContext를 받아 MovementCommand를 계산하는 이동 전략 계약이다.
// 주요 함수: Calculate
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/Assets/IMovementMode.cs.md

/// <summary>Calculates movement without mutating the player's Rigidbody2D.</summary>
public interface IMovementMode
{
    MovementCommand Calculate(in MovementContext context);
}
