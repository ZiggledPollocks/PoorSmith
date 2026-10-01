// [코드 지도] UpDraftMovementMode: 상승기류 안에서 수평 목표 속도와 UpDraftZone이 계산한 수직 목표 속도에 접근하는 명령을 만든다. 우선순위는 100이고 유효하지 않은 구역이면 default 명령을 반환한다.
// 주요 함수: Calculate, UpDraftMovementMode
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/player/Movement/UpDraftMovementMode.cs.md

using UnityEngine;

/// <summary>Applies movement rules while the player is in an updraft.</summary>
public sealed class UpDraftMovementMode : IMovementMode
{
    public const int Priority = 100;
    private readonly UpDraftZone zone;
    private readonly UpDraftMovementSettings settings;

    public UpDraftMovementMode(UpDraftZone zone, UpDraftMovementSettings settings)
    {
        this.zone = zone;
        this.settings = settings;
    }

    public MovementCommand Calculate(in MovementContext context)
    {
        if (zone == null || !zone.isActiveAndEnabled) return default;
        float speed = context.IsRunning ? settings.RunSpeed : settings.WalkSpeed;
        float target = Mathf.Clamp(context.HorizontalInput, -1f, 1f) * speed *
            settings.HorizontalSpeedMultiplier + context.ExternalHorizontalSpeed;
        float x = Mathf.MoveTowards(context.Velocity.x, target,
            settings.HorizontalAcceleration * context.DeltaTime);
        float desiredY = zone.GetDesiredVerticalSpeed(
            context.BodyCenterY, context.BodyHalfHeight, context.Time);
        float y = Mathf.MoveTowards(context.Velocity.y, desiredY, zone.RiseAcceleration * context.DeltaTime);
        return MovementCommand.Velocity(new Vector2(x, y), 0f);
    }
}
