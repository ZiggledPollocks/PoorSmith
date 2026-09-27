using UnityEngine;

public sealed class FlameMovementMode : IMovementMode
{
    public const int Priority = 200;
    private readonly FlameMovementSettings settings;

    public FlameMovementMode(FlameMovementSettings settings) => this.settings = settings;

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
