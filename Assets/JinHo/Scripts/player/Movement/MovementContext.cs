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
