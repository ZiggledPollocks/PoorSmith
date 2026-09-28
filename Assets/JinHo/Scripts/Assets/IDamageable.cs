public interface IDamageable
{
    bool IsDead { get; }
    void TakeDamage(float amount);
}

/// <summary>
/// Read-only health data used by world-space UI such as monster health bars.
/// </summary>
public interface IHealthSource : IDamageable
{
    float CurrentHealth { get; }
    int MaxHealth { get; }
}
