// [코드 지도] MovementContext: 한 물리 갱신 시점의 속도·입력·접지·점프 가능 여부·몸체 크기·시간·외부 바람을 전달하는 값 형식 스냅샷이다. 함수는 없으며 입력 소비나 물리 변경을 하지 않는다.
// 주요 함수: 필드·데이터 선언
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/player/Movement/MovementContext.cs.md

using UnityEngine;

/// <summary>A per-physics-step snapshot. Modes do not consume input themselves.</summary>
public struct MovementContext
{
    public Vector2 Velocity;
    public float HorizontalInput;
    public bool IsRunning;
    public bool JumpHeld;
    public bool CanJump;
    public bool IsGrounded;
    public float BodyCenterY;
    public float BodyHalfHeight;
    public float Time;
    public float DeltaTime;
    public float ExternalHorizontalSpeed;
}
