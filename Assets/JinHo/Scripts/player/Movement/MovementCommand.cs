// [코드 지도] MovementCommand: 축마다 유지·속도 지정·힘 적용을 표현하는 결과 데이터다. ResolveVelocity는 새 속도를 계산하고 Force는 힘 벡터를 계산한다. 실제 물리 변경은 PlayerMovement.ApplyMovementCommand가 한다.
// 주요 함수: Velocity, ResolveVelocity, MovementAxisCommand
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/player/Movement/MovementCommand.cs.md

using UnityEngine;

public enum MovementAxisDrive { Preserve, SetVelocity, AddForce }

/// <summary>Value means world units/second for velocity, or force units for AddForce.</summary>
public readonly struct MovementAxisCommand
{
    public readonly MovementAxisDrive Drive;
    public readonly float Value;

    public MovementAxisCommand(MovementAxisDrive drive, float value)
    {
        Drive = drive;
        Value = value;
    }
}

/// <summary>Only PlayerMovement applies this result; modes never write physics state.</summary>
public struct MovementCommand
{
    public MovementAxisCommand Horizontal;
    public MovementAxisCommand Vertical;
    public float? GravityScale;
    public bool ConsumedJump;

    public static MovementCommand Velocity(Vector2 velocity, float gravityScale, bool consumedJump = false)
    {
        return new MovementCommand
        {
            Horizontal = new MovementAxisCommand(MovementAxisDrive.SetVelocity, velocity.x),
            Vertical = new MovementAxisCommand(MovementAxisDrive.SetVelocity, velocity.y),
            GravityScale = gravityScale,
            ConsumedJump = consumedJump
        };
    }

    public Vector2 ResolveVelocity(Vector2 current)
    {
        if (Horizontal.Drive == MovementAxisDrive.SetVelocity) current.x = Horizontal.Value;
        if (Vertical.Drive == MovementAxisDrive.SetVelocity) current.y = Vertical.Value;
        return current;
    }

    public Vector2 Force => new Vector2(
        Horizontal.Drive == MovementAxisDrive.AddForce ? Horizontal.Value : 0f,
        Vertical.Drive == MovementAxisDrive.AddForce ? Vertical.Value : 0f);
}
