public interface IDamageable
{
    bool IsDead { get; }
    void TakeDamage(int amount);
}

/// <summary>
/// Read-only health data used by world-space UI such as monster health bars.
/// </summary>
public interface IHealthSource : IDamageable
{
    int CurrentHealth { get; }
    int MaxHealth { get; }
}
