using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public sealed partial class StoneGolemController : MonoBehaviour, IHealthSource, IInteractable
{
    private enum AnimationState { Idle, Walk, Death }

    [Header("References")]
    [SerializeField] private Transform playerTarget;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Collider2D bodyCollider;
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Stats")]
    [SerializeField, Min(1)] private int maxHealth = 100;
    [SerializeField, Min(0f)] private float moveSpeed = 2.4f;
    [SerializeField, Min(1)] private int attackDamage = 20;
    [SerializeField, Min(1)] private int pickaxeDamage = 13;

    [Header("Behaviour")]
    [SerializeField, Min(0.1f)] private float idlePatrolRadius = 1f;
    [SerializeField, Min(0.1f)] private float detectionRange = 6f;
    [SerializeField, Min(0.1f)] private float attackRange = 6f;
    [SerializeField, Min(0f)] private float attackCooldown = 1.25f;
    [SerializeField, Min(0f)] private float criticalFrameHold = 0.5f;
    [SerializeField] private LayerMask groundLayers = 64;
    [SerializeField, Min(0.01f)] private float ledgeProbeDistance = 0.8f;
    [SerializeField, Min(0f)] private float wallProbeDistance = 0.12f;

    [Header("Animation Frames")]
    [SerializeField] private Sprite[] idleFrames;
    [SerializeField] private Sprite[] walkFrames;
    [SerializeField] private Sprite[] attackFrames;
    [SerializeField] private Sprite[] deathFrames;
    [SerializeField, Min(0.1f)] private float idleFramesPerSecond = 8f;
    [SerializeField, Min(0.1f)] private float walkFramesPerSecond = 8f;
    [SerializeField, Min(0.1f)] private float attackFramesPerSecond = 9f;
    [SerializeField, Min(0.1f)] private float deathFramesPerSecond = 7f;

    [Header("Death Drop")]
    [SerializeField] private ResourceData dropData;
    [SerializeField] private ItemDropSpawner itemDropSpawner;
    [SerializeField] private Vector2 dropOffset = new(0f, 0.35f);

    private readonly RaycastHit2D[] wallHits = new RaycastHit2D[4];
    private readonly BehaviourStateMachine stateMachine = new();
    private IBehaviourState currentState => stateMachine.Current;
    private IdlePatrolState idleState;
    private ChaseState chaseState;
    private AttackState attackState;
    private DeadState deadState;
    private ContactFilter2D groundFilter;
    private IDamageable playerDamageable;
    private Sprite[] currentFrames;
    private float currentFramesPerSecond;
    private float animationTime;
    private bool animationLoops;
    private bool manualAttackAnimation;
    private CharacterPhysics2D characterPhysics;
    private Vector3 visualBaseLocalPosition;
    private Vector3 visualBaseLocalScale;
    private float referenceVisualHeight;
    private float referenceVisualBottom;
    private float currentHealth;
    private float nextAttackTime;
    private Vector2 spawnPosition;
    private bool isProvoked;
    private bool isDead;

    public float CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsDead => isDead;
    public bool IsProvoked => isProvoked;

    private void Awake()
    {
        rb ??= GetComponent<Rigidbody2D>();
        bodyCollider ??= GetComponent<Collider2D>();
        spriteRenderer ??= GetComponentInChildren<SpriteRenderer>();
        itemDropSpawner ??= FindFirstObjectByType<ItemDropSpawner>();
        InitializeVisualNormalization();
        SpriteColliderAutoFit2D.Attach(gameObject, bodyCollider, spriteRenderer);
        characterPhysics = CharacterPhysics2D.Attach(gameObject, rb, bodyCollider);
        currentHealth = maxHealth;
        MonsterHealthBar2D.Attach(gameObject, bodyCollider, spriteRenderer);
        spawnPosition = rb.position;
        attackRange = Mathf.Max(attackRange, detectionRange);
        groundFilter = new ContactFilter2D();
        groundFilter.SetLayerMask(groundLayers);
        groundFilter.useTriggers = false;
        idleState = new IdlePatrolState(this);
        chaseState = new ChaseState(this);
        attackState = new AttackState(this);
        deadState = new DeadState(this);
    }

    private void Start() => ChangeState(idleState);
    private void Update() { if (!manualAttackAnimation) UpdateAnimation(); }
    private void FixedUpdate()
    {
        if (characterPhysics == null || !characterPhysics.IsKnockbackActive)
            stateMachine.Tick();
    }

    public bool CanInteract() => !isDead;
    public bool CanUseTool(ToolData toolData) => toolData != null && toolData.ToolType == ToolType.Pickaxe;

    public void Interact(PlayerInteraction interactionContext)
    {
        if (interactionContext == null || !CanUseTool(interactionContext.CurrentTool)) return;
        playerTarget = interactionContext.transform;
        playerDamageable = interactionContext.GetComponent<IDamageable>();
        ApplyPickaxeHit();
        if (!isDead)
            characterPhysics?.ApplyKnockbackFrom(interactionContext.transform.position);
    }

    // IDamageable 직접 호출은 무기 종류를 증명할 수 없으므로 무시한다.
    // 돌골렘 피해는 반드시 Pickaxe가 검증되는 Interact 경로로 들어온다.
    public void TakeDamage(float amount) { }

    private void ApplyPickaxeHit()
    {
        if (isDead) return;
        isProvoked = true;
        currentHealth = Mathf.Max(0, currentHealth - pickaxeDamage);
        if (currentHealth == 0) { ChangeState(deadState); return; }
        ChangeState(chaseState);
    }

    private void ChangeState(IBehaviourState nextState)
    {
        stateMachine.Change(nextState);
    }

    private bool FindLivingPlayer()
    {
        return LivingPlayerTarget.Resolve(ref playerTarget, ref playerDamageable);
    }

    private bool PlayerIsDetected()
    {
        return FindLivingPlayer()
            && Vector2.Distance(rb.position, playerTarget.position) <= detectionRange;
    }

    private bool CanMove(float direction)
    {
        if (Mathf.Abs(direction) < 0.01f) return false;
        float sign = Mathf.Sign(direction);
        Bounds bounds = bodyCollider.bounds;
        int wallHitCount = bodyCollider.Cast(
            Vector2.right * sign,
            groundFilter,
            wallHits,
            wallProbeDistance);

        for (int i = 0; i < wallHitCount; i++)
        {
            RaycastHit2D hit = wallHits[i];
            bool blocksHorizontalMovement =
                Vector2.Dot(hit.normal, Vector2.right * sign) < -0.5f;

            if (blocksHorizontalMovement) return false;
        }

        Vector2 ledgeOrigin = new(bounds.center.x + sign * (bounds.extents.x + 0.15f), bounds.min.y + 0.1f);
        RaycastHit2D ground = Physics2D.Raycast(ledgeOrigin, Vector2.down, ledgeProbeDistance, groundLayers);
        return ground.collider != null && ground.normal.y >= 0.5f;
    }

    private void Move(float direction)
    {
        direction = Mathf.Sign(direction);
        rb.linearVelocity = new Vector2(direction * moveSpeed, rb.linearVelocity.y);
        spriteRenderer.flipX = direction < 0f;
    }

    private void StopMoving() => rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

    private void PlayAnimation(AnimationState state)
    {
        manualAttackAnimation = false;
        Sprite[] frames;
        float fps;
        bool loops;
        switch (state)
        {
            case AnimationState.Walk: frames = walkFrames; fps = walkFramesPerSecond; loops = true; break;
            case AnimationState.Death: frames = deathFrames; fps = deathFramesPerSecond; loops = false; break;
            default: frames = idleFrames; fps = idleFramesPerSecond; loops = true; break;
        }
        if (currentFrames == frames) return;
        currentFrames = frames;
        currentFramesPerSecond = fps;
        animationLoops = loops;
        animationTime = 0f;
        ApplyFrame(frames, 0);
    }

    private void UpdateAnimation()
    {
        if (currentFrames == null || currentFrames.Length == 0) return;
        int index = SpriteFrameClock.Advance(ref animationTime, Time.deltaTime,
            currentFramesPerSecond, currentFrames.Length, animationLoops);
        ApplyFrame(currentFrames, index);
    }

    private void ApplyAttackFrame(int index)
    {
        manualAttackAnimation = true;
        ApplyFrame(attackFrames, index);
    }

    private void ApplyFrame(Sprite[] frames, int index)
    {
        if (frames == null || frames.Length == 0) return;
        Sprite frame = frames[Mathf.Clamp(index, 0, frames.Length - 1)];
        if (frame == null) return;

        spriteRenderer.sprite = frame;
        NormalizeVisualSize(frame);
    }

    private void InitializeVisualNormalization()
    {
        if (spriteRenderer == null) return;

        Transform visual = spriteRenderer.transform;
        visualBaseLocalPosition = visual.localPosition;
        visualBaseLocalScale = visual.localScale;

        Sprite reference = GetFirstValidSprite(walkFrames) ?? spriteRenderer.sprite;
        if (reference == null) return;

        referenceVisualHeight = reference.bounds.size.y;
        referenceVisualBottom = visualBaseLocalPosition.y
            + reference.bounds.min.y * visualBaseLocalScale.y;
    }

    private void NormalizeVisualSize(Sprite frame)
    {
        if (spriteRenderer == null
            || spriteRenderer.transform == transform
            || referenceVisualHeight <= Mathf.Epsilon
            || frame.bounds.size.y <= Mathf.Epsilon)
        {
            return;
        }

        float scaleMultiplier = referenceVisualHeight / frame.bounds.size.y;
        Transform visual = spriteRenderer.transform;
        visual.localScale = new Vector3(
            visualBaseLocalScale.x * scaleMultiplier,
            visualBaseLocalScale.y * scaleMultiplier,
            visualBaseLocalScale.z);

        Vector3 position = visualBaseLocalPosition;
        position.y = referenceVisualBottom - frame.bounds.min.y * visual.localScale.y;
        visual.localPosition = position;
    }

    private static Sprite GetFirstValidSprite(Sprite[] frames)
    {
        if (frames == null) return null;

        for (int i = 0; i < frames.Length; i++)
        {
            if (frames[i] != null) return frames[i];
        }

        return null;
    }

    private void DealCriticalFrameDamage()
    {
        if (!FindLivingPlayer()) return;
        if (Vector2.Distance(rb.position, playerTarget.position) <= attackRange)
        {
            CombatDamage.Apply(playerDamageable, attackDamage, gameObject);
            if (playerDamageable != null && !playerDamageable.IsDead)
                playerTarget.GetComponent<CharacterPhysics2D>()?.ApplyKnockbackFrom(transform.position);
        }
    }

    private float DeathDuration => deathFrames == null || deathFrames.Length == 0 ? 0.5f : deathFrames.Length / deathFramesPerSecond;

    private void SpawnCore()
    {
        if (dropData == null || itemDropSpawner == null || !dropData.TryGetDrop(0, out GameObject prefab, out _)) return;
        itemDropSpawner.Spawn(prefab, transform.position + (Vector3)dropOffset, 1);
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        pickaxeDamage = 13;
        criticalFrameHold = 0.5f;
        idlePatrolRadius = Mathf.Max(0.1f, idlePatrolRadius);
        detectionRange = Mathf.Max(0.1f, detectionRange);
        attackRange = Mathf.Max(detectionRange, attackRange);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = Color.cyan;
        Vector3 center = Application.isPlaying ? (Vector3)spawnPosition : transform.position;
        Gizmos.DrawLine(center + Vector3.left * idlePatrolRadius, center + Vector3.right * idlePatrolRadius);
    }
}
