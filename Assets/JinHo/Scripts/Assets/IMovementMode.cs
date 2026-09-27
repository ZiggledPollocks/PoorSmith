/// <summary>Calculates movement without mutating the player's Rigidbody2D.</summary>
public interface IMovementMode
{
    MovementCommand Calculate(in MovementContext context);
}
