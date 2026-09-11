public interface IDamageable
{
    bool IsDead { get; }
    void TakeDamage(int amount);
}
