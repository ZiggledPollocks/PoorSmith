// [코드 지도] StoneGolemRockProjectile: 돌 골렘이 던진 돌의 이동, 충돌, 피해와 밀림을 처리한다.
// 주요 함수: Launch, Land, Update
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/Combat/Monsters/StoneGolem/StoneGolemRockProjectile.cs.md

using UnityEngine;

/// <summary>Visual-only flying rock. Damage is tested once at the predicted ground impact.</summary>
[DisallowMultipleComponent]
public sealed class StoneGolemRockProjectile : MonoBehaviour
{
    [SerializeField] private SpriteRenderer visual;
    [SerializeField, Min(0f)] private float arcHeight = 2.4f;

    private Vector2 origin;
    private Vector2 destination;
    private Transform player;
    private IDamageable playerHealth;
    private GameObject source;
    private Vector2 sourcePosition;
    private float duration;
    private float startTime;
    private float radius;
    private float knockbackMultiplier;
    private int damage;
    private bool launched;
    private bool landed;

    public void Launch(Vector2 impact, float seconds, Transform playerTarget,
        IDamageable targetHealth, GameObject attacker, Vector2 attackerPosition,
        int impactDamage, float impactRadius, float knockback)
    {
        visual ??= GetComponentInChildren<SpriteRenderer>();
        origin = transform.position;
        destination = impact;
        player = playerTarget;
        playerHealth = targetHealth;
        source = attacker;
        sourcePosition = attackerPosition;
        damage = impactDamage;
        radius = impactRadius;
        knockbackMultiplier = knockback;
        duration = Mathf.Max(.1f, seconds);
        startTime = Time.time;
        launched = true;
    }

    private void Update()
    {
        if (!launched || landed) return;
        float t = Mathf.Clamp01((Time.time - startTime) / duration);
        Vector2 position = Vector2.Lerp(origin, destination, t);
        position.y += 4f * arcHeight * t * (1f - t);
        transform.position = position;
        if (visual != null) visual.transform.Rotate(0f, 0f, 280f * Time.deltaTime);
        if (t >= 1f) Land();
    }

    private void Land()
    {
        landed = true;
        StoneGolemImpactPulse.Spawn(destination, radius);
        if (player != null && playerHealth != null && !playerHealth.IsDead)
        {
            Collider2D collider = player.GetComponent<Collider2D>();
            if (collider != null && Vector2.Distance(destination, collider.ClosestPoint(destination)) <= radius)
            {
                CombatDamage.Apply(playerHealth, damage, source,sourcePosition);
                if (!playerHealth.IsDead)
                    player.GetComponent<CharacterPhysics2D>()?.ApplyKnockbackFrom(sourcePosition, knockbackMultiplier);
            }
        }
        Destroy(gameObject);
    }
}
