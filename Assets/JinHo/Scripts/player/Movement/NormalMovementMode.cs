using UnityEngine;

/// <summary>Applies the default player movement rules.</summary>
public sealed class NormalMovementMode : IMovementMode
{
    private readonly NormalMovementSettings settings;

    public NormalMovementMode(NormalMovementSettings settings) => this.settings = settings;

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
