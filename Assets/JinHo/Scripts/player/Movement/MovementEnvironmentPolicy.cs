/// <summary>Environment membership policies remain active during rolls/knockback.</summary>
public readonly struct MovementEnvironmentPolicy
{
    public readonly bool BlocksNormalJump;
    public readonly bool SuppressesFallDamage;
    public readonly float? GravityScale;

    public MovementEnvironmentPolicy(bool blocksNormalJump, bool suppressesFallDamage, float? gravityScale)
    {
        BlocksNormalJump = blocksNormalJump;
        SuppressesFallDamage = suppressesFallDamage;
        GravityScale = gravityScale;
    }

    public static MovementEnvironmentPolicy Floating => new MovementEnvironmentPolicy(true, true, 0f);
}
