// [코드 지도] MovementSettings: 기존 PlayerMovement 직렬화 필드의 값을 모드에 전달하는 일반·화염·상승기류 설정 객체 세 종류다. 스스로 Inspector 값을 읽지 않고 RefreshMovementSettings가 값을 복사한다.
// 주요 함수: 필드·데이터 선언
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/player/Movement/MovementSettings.cs.md

// Runtime settings are refreshed from the existing PlayerMovement Inspector fields.
// Keeping serialized fields in their original component preserves scene/prefab overrides.
public sealed class NormalMovementSettings
{
    public float WalkSpeed, RunSpeed;
    public float GroundAcceleration, AirAcceleration, GroundDeceleration, AirDeceleration;
    public float TurnAccelerationMultiplier;
    public float JumpSpeed, GravityScale, FallGravityMultiplier, JumpCutGravityMultiplier, MaxFallSpeed;
}

public sealed class FlameMovementSettings
{
    public float WalkSpeed, RunSpeed, HorizontalSpeedMultiplier, HorizontalAcceleration;
    public float RiseSpeed, RiseAcceleration, SinkSpeed, SinkAcceleration, Resistance;
}

public sealed class UpDraftMovementSettings
{
    public float WalkSpeed, RunSpeed, HorizontalSpeedMultiplier, HorizontalAcceleration;
}
