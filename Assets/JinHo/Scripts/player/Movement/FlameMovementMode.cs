// [코드 지도] FlameMovementMode: 화염 안의 감속된 수평 이동과 점프 유지에 따른 상승·키 해제에 따른 침강을 계산한다. 중력 0인 속도 명령을 반환하며 우선순위는 200이다.
// 주요 함수: Calculate, FlameMovementMode
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/player/Movement/FlameMovementMode.cs.md

using UnityEngine;

public sealed class FlameMovementMode : IMovementMode
{
    public const int Priority = 200;
    private readonly FlameMovementSettings settings;

    public FlameMovementMode(FlameMovementSettings settings) => this.settings = settings;

    // 핵심 분기: context.JumpHeld 판정.
    // 상태 변경: y 갱신.
    // 다음 연결: MovementCommand.Velocity(UnityEngine.Vector2, float, bool) 호출.
    public MovementCommand Calculate(in MovementContext context)
    {
        float speed = context.IsRunning ? settings.RunSpeed : settings.WalkSpeed;
        float target = Mathf.Clamp(context.HorizontalInput, -1f, 1f) * speed *
            settings.HorizontalSpeedMultiplier + context.ExternalHorizontalSpeed;
        float x = Mathf.MoveTowards(context.Velocity.x, target,
            settings.HorizontalAcceleration * context.DeltaTime);
        float y = context.Velocity.y;
        if (context.JumpHeld)
        {
            y = Mathf.MoveTowards(y, settings.RiseSpeed, settings.RiseAcceleration * context.DeltaTime);
        }
        else
        {
            // Preserve the existing immediate removal of upward speed on release.
            y = Mathf.Min(y, 0f);
            float acceleration = y < -settings.SinkSpeed ? settings.Resistance : settings.SinkAcceleration;
            y = Mathf.MoveTowards(y, -settings.SinkSpeed, acceleration * context.DeltaTime);
        }
        return MovementCommand.Velocity(new Vector2(x, y), 0f);
    }
}
