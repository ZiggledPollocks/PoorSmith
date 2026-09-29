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
