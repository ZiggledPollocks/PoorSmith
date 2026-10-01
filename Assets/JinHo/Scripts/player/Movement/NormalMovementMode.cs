// [코드 지도] NormalMovementMode: 일반 걷기·달리기의 가감속, 점프 초기 속도, 상승·하강 중 중력 배율과 낙하 속도 제한을 계산한다. Rigidbody2D를 직접 쓰지 않는다.
// 주요 함수: Calculate, NormalMovementMode
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/player/Movement/NormalMovementMode.cs.md

using UnityEngine;

/// <summary>Applies the default player movement rules.</summary>
public sealed class NormalMovementMode : IMovementMode
{
    private readonly NormalMovementSettings settings;

    public NormalMovementMode(NormalMovementSettings settings) => this.settings = settings;

    // 핵심 분기: hasInput && Mathf.Abs(context.Velocity.x) > 0.01f && Mathf.Sign(target) != Mathf.Sign(context.Velocity.x) 판정.
    // 상태 변경: acceleration 갱신.
    // 다음 연결: MovementCommand.Velocity(UnityEngine.Vector2, float, bool) 호출.
    public MovementCommand Calculate(in MovementContext context)
    {
        float input = Mathf.Clamp(context.HorizontalInput, -1f, 1f);
        float speed = context.IsRunning ? settings.RunSpeed : settings.WalkSpeed;
        float target = input * speed + context.ExternalHorizontalSpeed;
        bool hasInput = Mathf.Abs(input) > 0.01f;
        float acceleration = hasInput
            ? (context.IsGrounded ? settings.GroundAcceleration : settings.AirAcceleration)
            : (context.IsGrounded ? settings.GroundDeceleration : settings.AirDeceleration);

        if (hasInput && Mathf.Abs(context.Velocity.x) > 0.01f &&
            Mathf.Sign(target) != Mathf.Sign(context.Velocity.x))
            acceleration *= settings.TurnAccelerationMultiplier;

        float x = Mathf.MoveTowards(context.Velocity.x, target, acceleration * context.DeltaTime);
        float y = context.CanJump ? settings.JumpSpeed : context.Velocity.y;
        float gravity = settings.GravityScale;
        if (y < 0f)
            gravity *= settings.FallGravityMultiplier;
        else if (y > 0f && !context.JumpHeld)
            gravity *= settings.JumpCutGravityMultiplier;

        y = Mathf.Max(y, -settings.MaxFallSpeed);
        return MovementCommand.Velocity(new Vector2(x, y), gravity, context.CanJump);
    }
}
