using UnityEngine;

public static class LivingPlayerTarget
{
    public static bool Resolve(ref Transform playerTarget, ref IDamageable playerDamageable)
    {
        if (playerTarget == null)
        {
            PlayerAssimilate player = Object.FindFirstObjectByType<PlayerAssimilate>();
            if (player == null) return false;
            playerTarget = player.transform;
            playerDamageable = player;
        }
        else
        {
            playerDamageable ??= playerTarget.GetComponent<IDamageable>();
        }
        return playerDamageable == null || !playerDamageable.IsDead;
    }
}
