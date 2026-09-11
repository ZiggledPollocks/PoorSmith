using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public sealed class AggMonsterController : MonoBehaviour, IDamageable, IInteractable
{
    [Header("References")]
    [SerializeField] private Transform playerTarget;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Collider2D bodyCollider;
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Health")]
    [SerializeField, Min(1)] private int maxHealth = 30;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float wanderSpeed = 1.5f;
    [SerializeField, Min(0f)] private float chaseSpeed = 3f;
    [SerializeField, Min(0.01f)] private float chaseDistance = 7f;
    [SerializeField, Min(0.1f)] private float minimumWanderTime = 1f;
    [SerializeField, Min(0.1f)] private float maximumWanderTime = 3f;

    [Header("Attack")]
    [SerializeField, Min(0.01f)] private float attackDistance = 1.4f;
    [SerializeField, Min(1)] private int attackDamage = 10;
    [SerializeField, Min(0f)] private float attackWindup = 0.35f;
    [SerializeField, Min(0f)] private float attackRecovery = 0.35f;
    [SerializeField, Min(0f)] private float attackCooldown = 1.2f;

    [Header("Death Drop")]
    [SerializeField] private ResourceData dropData;
    [SerializeField] private ItemDropSpawner itemDropSpawner;
    [SerializeField, Min(0f)] private float dropRadius = 0.75f;
    [SerializeField] private Vector2 dropOffset = new(0f, 0.35f);

    private int currentHealth;
    private float wanderDirection;
    private float nextWanderChangeTime;
    private float nextAttackTime;
    private bool isAttacking;
    private bool isDead;
    private Coroutine attackRoutine;
    private IDamageable playerDamageable;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsDead => isDead;

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        if (bodyCollider == null)
            bodyCollider = GetComponent<Collider2D>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (itemDropSpawner == null)
            itemDropSpawner = FindFirstObjectByType<ItemDropSpawner>();

        currentHealth = maxHealth;
        SelectNewWanderDirection();
    }

    private void FixedUpdate()
    {
        if (isDead)
            return;

        if (!FindPlayerTarget())
        {
            Wander();
            return;
        }

        if (playerDamageable != null && playerDamageable.IsDead)
        {
            Wander();
            return;
        }

        float distanceToPlayer = Vector2.Distance(
            rb.position,
            playerTarget.position);
        float directionToPlayer = Mathf.Sign(
            playerTarget.position.x - transform.position.x);

        if (Mathf.Abs(playerTarget.position.x - transform.position.x) > 0.01f)
            FaceDirection(directionToPlayer);

        if (isAttacking)
        {
            StopHorizontalMovement();
            return;
        }

        if (distanceToPlayer <= attackDistance)
        {
            StopHorizontalMovement();

            if (Time.time >= nextAttackTime)
                attackRoutine = StartCoroutine(AttackRoutine());

            return;
        }

        if (distanceToPlayer <= chaseDistance)
        {
            MoveHorizontally(directionToPlayer, chaseSpeed);
            return;
        }

        Wander();
    }

    public bool CanInteract()
    {
        return !isDead;
    }

    public bool CanUseTool(ToolData toolData)
    {
        return toolData != null && toolData.ToolType == ToolType.Sword;
    }

    public void Interact(PlayerInteraction interactionContext)
    {
        if (interactionContext == null ||
            !CanUseTool(interactionContext.CurrentTool))
        {
            return;
        }

        TakeDamage(interactionContext.CurrentTool.Damage);
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0 || isDead)
            return;

        currentHealth = Mathf.Max(0, currentHealth - amount);
        Debug.Log($"{name} HP: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0)
            Die();
    }

    private IEnumerator AttackRoutine()
    {
        isAttacking = true;
        StopHorizontalMovement();

        if (attackWindup > 0f)
            yield return new WaitForSeconds(attackWindup);

        if (!isDead && FindPlayerTarget())
        {
            float distanceToPlayer = Vector2.Distance(
                rb.position,
                playerTarget.position);

            if (distanceToPlayer <= attackDistance &&
                playerDamageable != null &&
                !playerDamageable.IsDead)
            {
                playerDamageable.TakeDamage(attackDamage);
            }
        }

        if (attackRecovery > 0f)
            yield return new WaitForSeconds(attackRecovery);

        nextAttackTime = Time.time + attackCooldown;
        isAttacking = false;
        attackRoutine = null;
    }

    private bool FindPlayerTarget()
    {
        if (playerTarget != null)
        {
            playerDamageable ??= playerTarget.GetComponent<IDamageable>();
            return true;
        }

        PlayerAssimilate playerAssimilate =
            FindFirstObjectByType<PlayerAssimilate>();

        if (playerAssimilate == null)
            return false;

        playerTarget = playerAssimilate.transform;
        playerDamageable = playerAssimilate;
        return true;
    }

    private void Wander()
    {
        if (Time.time >= nextWanderChangeTime)
            SelectNewWanderDirection();

        MoveHorizontally(wanderDirection, wanderSpeed);
    }

    private void SelectNewWanderDirection()
    {
        wanderDirection = Random.value < 0.5f ? -1f : 1f;
        nextWanderChangeTime = Time.time + Random.Range(
            minimumWanderTime,
            maximumWanderTime);
    }

    private void MoveHorizontally(float direction, float speed)
    {
        rb.linearVelocity = new Vector2(
            direction * speed,
            rb.linearVelocity.y);

        FaceDirection(direction);
    }

    private void StopHorizontalMovement()
    {
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    private void FaceDirection(float direction)
    {
        if (spriteRenderer == null || Mathf.Abs(direction) < 0.01f)
            return;

        spriteRenderer.flipX = direction < 0f;
    }

    private void Die()
    {
        if (isDead)
            return;

        isDead = true;

        if (attackRoutine != null)
            StopCoroutine(attackRoutine);

        StopHorizontalMovement();
        rb.simulated = false;

        if (bodyCollider != null)
            bodyCollider.enabled = false;

        SpawnDeathDrops();
        Destroy(gameObject);
    }

    private void SpawnDeathDrops()
    {
        if (dropData == null || itemDropSpawner == null)
        {
            Debug.LogWarning($"{name}: Drop Data or Item Drop Spawner is missing.");
            return;
        }

        for (int i = 0; i < dropData.DropCount; i++)
        {
            if (!dropData.TryGetDrop(i, out GameObject prefab, out int amount))
                continue;

            Vector2 randomOffset = Random.insideUnitCircle * dropRadius;
            randomOffset.y = Mathf.Abs(randomOffset.y);

            Vector3 dropPosition =
                transform.position + (Vector3)(dropOffset + randomOffset);

            itemDropSpawner.Spawn(prefab, dropPosition, amount);
        }
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        chaseDistance = Mathf.Max(0.01f, chaseDistance);
        attackDistance = Mathf.Clamp(
            attackDistance,
            0.01f,
            chaseDistance);
        maximumWanderTime = Mathf.Max(
            minimumWanderTime,
            maximumWanderTime);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, chaseDistance);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackDistance);
    }
}
