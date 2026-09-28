using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(SpriteRenderer))]
public sealed class WindSpiritController : MonoBehaviour, IHealthSource, IInteractable
{
    private enum BehaviourState { Idle, CombatHold, Retreat, Dead }
    private enum AnimationState { Idle, Move, Death }

    [Header("References")]
    [SerializeField] private Transform playerTarget;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Collider2D bodyCollider;
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Stats")]
    [SerializeField, Min(1)] private int maxHealth = 49;
    [Tooltip("플레이어 걷기 속도 5의 110%입니다.")]
    [SerializeField, Min(0f)] private float moveSpeed = 5.5f;
    [SerializeField, Min(1)] private int attackDamage = 17;

    [Header("Behaviour")]
    [SerializeField, Min(0.1f)] private float idleWanderRadius = 2f;
    [SerializeField, Min(0.1f)] private float detectionRadius = 9f;
    [Tooltip("인식 최대 거리보다 조금 안쪽에서 공격을 준비합니다.")]
    [SerializeField, Min(0.1f)] private float retreatDistance = 8f;
    [SerializeField, Min(0.1f)] private float retreatTimeout = 2f;
    [SerializeField, Min(0.1f)] private float attackCooldown = 2.2f;
    [SerializeField, Min(0.1f)] private float wanderTargetInterval = 1.4f;
    [SerializeField, Min(0.01f)] private float wanderArrivalDistance = 0.15f;

    [Header("Wind Orb")]
    [SerializeField, Min(0.01f)] private float projectileInitialSpeed = 12f;
    [SerializeField, Min(0.01f)] private float projectileDeceleration = 8f;
    [SerializeField, Min(0f)] private float projectileSpawnDistance = 0.8f;
    [SerializeField, Min(0.05f)] private float projectileRadius = 0.32f;

    [Header("Animation Frames")]
    [SerializeField] private Sprite[] idleFrames;
    [SerializeField] private Sprite[] moveFrames;
    [SerializeField] private Sprite[] deathFrames;
    [SerializeField, Min(0.1f)] private float idleFramesPerSecond = 7f;
    [SerializeField, Min(0.1f)] private float moveFramesPerSecond = 10f;
    [SerializeField, Min(0.1f)] private float deathFramesPerSecond = 8f;

    [Header("Death Drop")]
    [SerializeField] private ResourceData dropData;
    [SerializeField] private ItemDropSpawner itemDropSpawner;
    [SerializeField, Min(1)] private int coalDropCount = 2;
    [SerializeField, Min(0f)] private float dropRadius = 0.45f;

    private BehaviourState state;
    private IDamageable playerDamageable;
    private CharacterPhysics2D characterPhysics;
    private Vector2 idleCenter;
    private Vector2 wanderTarget;
    private float nextWanderTargetTime;
    private float nextAttackTime;
    private float retreatEndsAt;
    private float currentHealth;
    private bool isDead;
    private bool droppedItems;
    private float destroyAt;

    private Sprite[] currentFrames;
    private float currentFramesPerSecond;
    private float animationTime;
    private bool animationLoops;

    public float CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsDead => isDead;

    private void Awake()
    {
        rb ??= GetComponent<Rigidbody2D>();
        bodyCollider ??= GetComponent<Collider2D>();
        spriteRenderer ??= GetComponent<SpriteRenderer>();
        itemDropSpawner ??= FindFirstObjectByType<ItemDropSpawner>();

        rb.gravityScale = 0f;
        rb.constraints |= RigidbodyConstraints2D.FreezeRotation;
        bodyCollider.isTrigger = true;
        SpriteColliderAutoFit2D.Attach(gameObject, bodyCollider, spriteRenderer);
        characterPhysics = CharacterPhysics2D.Attach(gameObject, rb, bodyCollider);
        MonsterHealthBar2D.Attach(gameObject, bodyCollider, spriteRenderer);

        currentHealth = maxHealth;
        idleCenter = rb.position;
        ChooseWanderTarget();
        SetState(BehaviourState.Idle);
    }

    private void Update()
    {
        UpdateAnimation();
        if (!isDead || Time.time < destroyAt)
            return;

        SpawnCoal();
        Destroy(gameObject);
    }

    private void FixedUpdate()
    {
        if (isDead || (characterPhysics != null && characterPhysics.IsKnockbackActive))
            return;

        switch (state)
        {
            case BehaviourState.Retreat:
                TickRetreat();
                break;
            case BehaviourState.CombatHold:
                TickCombatHold();
                break;
            default:
                TickIdle();
                break;
        }
    }

    public bool CanInteract() => !isDead;
    public bool CanUseTool(ToolData toolData) =>
        toolData != null && toolData.ToolType == ToolType.Sword;

    public void Interact(PlayerInteraction interactionContext)
    {
        if (interactionContext == null || !CanUseTool(interactionContext.CurrentTool))
            return;

        playerTarget = interactionContext.transform;
        playerDamageable = interactionContext.GetComponent<IDamageable>();
        TakeDamage(interactionContext.CurrentTool.Damage);
        if (!isDead)
            characterPhysics?.ApplyKnockbackFrom(interactionContext.transform.position);
    }

    public void TakeDamage(float amount)
    {
        if (amount <= 0 || isDead)
            return;

        float before=currentHealth;
        currentHealth = Mathf.Max(0, CombatDamage.RoundHealth(currentHealth - amount));
        CampaignDamageNumber.Show(gameObject,before-currentHealth);
        Debug.Log($"{name} HP: {currentHealth}/{maxHealth}");
        if (currentHealth <= 0)
            Die();
    }

    private void TickIdle()
    {
        if (PlayerIsDetected())
        {
            SetState(BehaviourState.CombatHold);
            TickCombatHold();
            return;
        }

        if (Time.time >= nextWanderTargetTime ||
            Vector2.Distance(rb.position, wanderTarget) <= wanderArrivalDistance)
        {
            ChooseWanderTarget();
        }

        MoveTowards(wanderTarget, moveSpeed);
    }

    private void TickCombatHold()
    {
        if (!PlayerIsDetected())
        {
            StopMoving();
            SetState(BehaviourState.Idle);
            return;
        }

        Face(playerTarget.position.x - transform.position.x);
        StopMoving();
        if (Time.time < nextAttackTime)
            return;

        FireWindOrb();
        nextAttackTime = Time.time + attackCooldown;
        retreatEndsAt = Time.time + retreatTimeout;
        SetState(BehaviourState.Retreat);
    }

    private void TickRetreat()
    {
        if (!FindLivingPlayer())
        {
            StopMoving();
            SetState(BehaviourState.Idle);
            return;
        }

        Vector2 away = rb.position - (Vector2)playerTarget.position;
        float distance = away.magnitude;
        if (distance >= retreatDistance || Time.time >= retreatEndsAt)
        {
            StopMoving();
            SetState(distance <= detectionRadius
                ? BehaviourState.CombatHold
                : BehaviourState.Idle);
            return;
        }

        if (away.sqrMagnitude < 0.001f)
            away = Random.value < 0.5f ? Vector2.left : Vector2.right;
        // The spirit is spawned above a ground surface; do not retreat through it.
        away.y = Mathf.Max(away.y, -0.1f);
        Move(away.normalized, moveSpeed);
    }

    private bool FindLivingPlayer()
    {
        return LivingPlayerTarget.Resolve(ref playerTarget, ref playerDamageable);
    }

    private bool PlayerIsDetected() =>
        FindLivingPlayer() &&
        Vector2.Distance(rb.position, playerTarget.position) <= detectionRadius;

    private void ChooseWanderTarget()
    {
        Vector2 offset = Random.insideUnitCircle * idleWanderRadius;
        offset.y = Mathf.Abs(offset.y) * 0.65f;
        wanderTarget = idleCenter + offset;
        nextWanderTargetTime = Time.time + wanderTargetInterval;
    }

    private void MoveTowards(Vector2 target, float speed)
    {
        Vector2 direction = target - rb.position;
        if (direction.sqrMagnitude <= wanderArrivalDistance * wanderArrivalDistance)
        {
            StopMoving();
            return;
        }
        Move(direction.normalized, speed);
    }

    private void Move(Vector2 direction, float speed)
    {
        rb.linearVelocity = direction * speed;
        Face(direction.x);
    }

    private void StopMoving() => rb.linearVelocity = Vector2.zero;

    private void Face(float horizontalDirection)
    {
        if (Mathf.Abs(horizontalDirection) > 0.01f)
            spriteRenderer.flipX = horizontalDirection < 0f;
    }

    private void FireWindOrb()
    {
        if (!FindLivingPlayer())
            return;

        Vector2 direction = ((Vector2)playerTarget.position - rb.position).normalized;
        if (direction.sqrMagnitude < 0.001f)
            direction = Vector2.right;

        Vector2 spawnPosition = rb.position + direction * projectileSpawnDistance;
        WindSpiritProjectile.Spawn(
            spawnPosition,
            direction,
            projectileInitialSpeed,
            projectileDeceleration,
            projectileRadius,
            attackDamage,
            gameObject);
    }

    private void SetState(BehaviourState nextState)
    {
        state = nextState;
        PlayAnimation(nextState switch
        {
            BehaviourState.Idle => AnimationState.Idle,
            BehaviourState.Dead => AnimationState.Death,
            _ => AnimationState.Move
        });
    }

    private void PlayAnimation(AnimationState animationState)
    {
        Sprite[] frames;
        float framesPerSecond;
        bool loops;
        switch (animationState)
        {
            case AnimationState.Death:
                frames = deathFrames;
                framesPerSecond = deathFramesPerSecond;
                loops = false;
                break;
            case AnimationState.Move:
                frames = moveFrames;
                framesPerSecond = moveFramesPerSecond;
                loops = true;
                break;
            default:
                frames = idleFrames;
                framesPerSecond = idleFramesPerSecond;
                loops = true;
                break;
        }

        if (currentFrames == frames)
            return;
        currentFrames = frames;
        currentFramesPerSecond = framesPerSecond;
        animationLoops = loops;
        animationTime = 0f;
        ApplyFrame(0);
    }

    private void UpdateAnimation()
    {
        if (currentFrames == null || currentFrames.Length == 0)
            return;
        int frameIndex = SpriteFrameClock.Advance(ref animationTime, Time.deltaTime,
            currentFramesPerSecond, currentFrames.Length, animationLoops);
        ApplyFrame(frameIndex);
    }

    private void ApplyFrame(int frameIndex)
    {
        if (currentFrames == null || currentFrames.Length == 0)
            return;
        Sprite frame = currentFrames[Mathf.Clamp(frameIndex, 0, currentFrames.Length - 1)];
        if (frame != null)
            spriteRenderer.sprite = frame;
    }

    private float DeathDuration => deathFrames == null || deathFrames.Length == 0
        ? 0.5f
        : deathFrames.Length / Mathf.Max(0.1f, deathFramesPerSecond);

    private void Die()
    {
        if (isDead)
            return;
        isDead = true;
        StopMoving();
        rb.simulated = false;
        bodyCollider.enabled = false;
        SetState(BehaviourState.Dead);
        destroyAt = Time.time + DeathDuration;
    }

    private void SpawnCoal()
    {
        if (droppedItems)
            return;
        droppedItems = true;
        if(CampaignController.Instance?.Ready==true){CampaignController.Instance.SpiritDefeated(transform.position);return;}
        if (dropData == null || itemDropSpawner == null ||
            !dropData.TryGetDrop(0, out GameObject coalPrefab, out _))
            return;

        for (int i = 0; i < coalDropCount; i++)
        {
            float angle = Mathf.PI * 2f * i / coalDropCount;
            Vector2 offset = new(Mathf.Cos(angle), Mathf.Abs(Mathf.Sin(angle)) + 0.2f);
            itemDropSpawner.Spawn(coalPrefab,
                transform.position + (Vector3)(offset * dropRadius), 1);
        }
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        moveSpeed = Mathf.Max(0f, moveSpeed);
        attackDamage = Mathf.Max(1, attackDamage);
        detectionRadius = Mathf.Max(0.1f, detectionRadius);
        retreatDistance = Mathf.Clamp(retreatDistance, 0.1f, detectionRadius - 0.01f);
        projectileInitialSpeed = Mathf.Max(0.01f, projectileInitialSpeed);
        projectileDeceleration = Mathf.Max(0.01f, projectileDeceleration);
        coalDropCount = Mathf.Max(1, coalDropCount);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(Application.isPlaying ? (Vector3)idleCenter : transform.position,
            idleWanderRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, retreatDistance);
    }
}
