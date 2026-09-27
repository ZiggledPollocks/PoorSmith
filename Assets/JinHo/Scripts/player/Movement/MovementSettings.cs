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
