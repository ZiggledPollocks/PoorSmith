using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public sealed class PeMonsterController : MonoBehaviour, IDamageable, IInteractable
{
    [Header("References")]
    [SerializeField] private Transform playerTarget;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Collider2D bodyCollider;
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Health")]
    [SerializeField, Min(1)] private int maxHealth = 20;

    [Header("Wander")]
    [SerializeField, Min(0f)] private float wanderSpeed = 1.2f;
    [SerializeField, Min(0.1f)] private float minimumWanderTime = 1f;
    [SerializeField, Min(0.1f)] private float maximumWanderTime = 3f;

    [Header("Flee When Hit")]
    [SerializeField, Min(0f)] private float fleeSpeed = 5f;
    [SerializeField, Min(0f)] private float fleeDuration = 2f;

    [Header("Death Drop")]
    [SerializeField] private ResourceData dropData;
    [SerializeField] private ItemDropSpawner itemDropSpawner;
    [SerializeField, Min(0f)] private float dropRadius = 0.75f;
    [SerializeField] private Vector2 dropOffset = new(0f, 0.35f);

    private int currentHealth;
    private float wanderDirection;
    private float nextWanderChangeTime;
    private float fleeDirection;
    private float fleeEndTime;
    private bool isDead;

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

        if (Time.time < fleeEndTime)
        {
            UpdateFleeDirection();
            MoveHorizontally(fleeDirection, fleeSpeed);
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

        playerTarget = interactionContext.transform;
        TakeDamage(interactionContext.CurrentTool.Damage);
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0 || isDead)
            return;

        currentHealth = Mathf.Max(0, currentHealth - amount);
        Debug.Log($"{name} HP: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0)
        {
            Die();
            return;
        }

        BeginFleeing();
    }

    private void BeginFleeing()
    {
        FindPlayerTarget();
        UpdateFleeDirection();
        fleeEndTime = Time.time + fleeDuration;
    }

    private void UpdateFleeDirection()
    {
        if (FindPlayerTarget())
        {
            float horizontalDifference =
                transform.position.x - playerTarget.position.x;

            if (Mathf.Abs(horizontalDifference) > 0.01f)
                fleeDirection = Mathf.Sign(horizontalDifference);
        }

        if (Mathf.Abs(fleeDirection) < 0.01f)
        {
            fleeDirection = Mathf.Abs(wanderDirection) > 0.01f
                ? -Mathf.Sign(wanderDirection)
                : (Random.value < 0.5f ? -1f : 1f);
        }
    }

    private bool FindPlayerTarget()
    {
        if (playerTarget != null)
            return true;

        PlayerAssimilate player = FindFirstObjectByType<PlayerAssimilate>();

        if (player == null)
            return false;

        playerTarget = player.transform;
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
        rb.linearVelocity = Vector2.zero;
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
        maximumWanderTime = Mathf.Max(
            minimumWanderTime,
            maximumWanderTime);
    }
}
