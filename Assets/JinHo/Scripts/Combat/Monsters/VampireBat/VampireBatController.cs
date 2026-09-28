using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(SpriteRenderer))]
public sealed partial class VampireBatController : MonoBehaviour, IHealthSource, IInteractable
{
    private enum AnimationState { Idle, Fly, Death }

    [Header("References")]
    [SerializeField] private Transform playerTarget;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Collider2D bodyCollider;
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Stats")]
    [SerializeField, Min(1)] private int maxHealth = 38;
    [SerializeField, Min(0f)] private float moveSpeed = 7f;
    [SerializeField, Min(1)] private int attackDamage = 7;

    [Header("Behaviour")]
    [SerializeField, Min(0.1f)] private float detectionRadius = 7f;
    [SerializeField, Min(0.1f)] private float attackDuration = 1.1f;
    [SerializeField, Min(0.1f)] private float retreatDuration = 0.8f;
    [SerializeField, Min(0f)] private float retreatUpwardBias = 0.8f;
    [SerializeField, Min(0f)] private float attackCooldown = 2f;
    [SerializeField, Min(0.01f)] private float contactDistance = 0.8f;

    [Header("Ceiling")]
    [SerializeField] private LayerMask groundLayers = 1 << 6;
    [SerializeField, Min(0.1f)] private float ceilingSearchDistance = 12f;
    [SerializeField, Range(0f, 1f)] private float minimumCeilingNormal = 0.5f;
    [SerializeField, Min(0f)] private float ceilingClearance = 0.02f;
    [SerializeField, Min(0.01f)] private float ceilingArrivalDistance = 0.08f;

    [Header("Animation Frames")]
    [SerializeField] private Sprite[] idleFrames;
    [SerializeField] private Sprite[] flyFrames;
    [SerializeField] private Sprite[] deathFrames;
    [SerializeField, Min(0.1f)] private float idleFramesPerSecond = 7f;
    [SerializeField, Min(0.1f)] private float flyFramesPerSecond = 12f;
    [SerializeField, Min(0.1f)] private float deathFramesPerSecond = 9f;

    [Header("Death Drop")]
    [SerializeField] private ResourceData dropData;
    [SerializeField] private ItemDropSpawner itemDropSpawner;
    [SerializeField, Min(0f)] private float dropRadius = 0.65f;
    [SerializeField] private Vector2 dropOffset = new(0f, 0.2f);

    private readonly BehaviourStateMachine stateMachine = new();
    private IBehaviourState currentState => stateMachine.Current;
    private CeilingIdleState idleState;
    private DiveAttackState attackState;
    private DiagonalRetreatState retreatState;
    private ReturnToCeilingState returnToCeilingState;
    private DeadState deadState;
    private IDamageable playerDamageable;
    private Sprite[] currentFrames;
    private float currentFramesPerSecond;
    private float animationTime;
    private bool animationLoops;
    private CharacterPhysics2D characterPhysics;
    private float currentHealth;
    private float nextAttackTime;
    private bool isDead;

    public float CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsDead => isDead;

    private void Awake()
    {
        rb ??= GetComponent<Rigidbody2D>();
        bodyCollider ??= GetComponent<Collider2D>();
        spriteRenderer ??= GetComponent<SpriteRenderer>();
        SpriteColliderAutoFit2D.Attach(gameObject, bodyCollider, spriteRenderer);
        characterPhysics = CharacterPhysics2D.Attach(gameObject, rb, bodyCollider);
        itemDropSpawner ??= FindFirstObjectByType<ItemDropSpawner>();
        rb.gravityScale = 0f;
        bodyCollider.isTrigger = false;
        currentHealth = maxHealth;
        MonsterHealthBar2D.Attach(gameObject, bodyCollider, spriteRenderer);
        idleState = new CeilingIdleState(this);
        attackState = new DiveAttackState(this);
        retreatState = new DiagonalRetreatState(this);
        returnToCeilingState = new ReturnToCeilingState(this);
        deadState = new DeadState(this);
    }

    private void Start() => ChangeState(returnToCeilingState);
    private void Update() => UpdateAnimation();
    private void FixedUpdate()
    {
        if (characterPhysics == null || !characterPhysics.IsKnockbackActive)
            stateMachine.Tick();
    }

    public bool CanInteract() => !isDead;
    public bool CanUseTool(ToolData toolData) => toolData != null && toolData.ToolType == ToolType.Sword;

    public void Interact(PlayerInteraction interactionContext)
    {
        if (interactionContext == null || !CanUseTool(interactionContext.CurrentTool)) return;
        playerTarget = interactionContext.transform;
        playerDamageable = interactionContext.GetComponent<IDamageable>();
        TakeDamage(interactionContext.CurrentTool.Damage);
        if (!isDead)
            characterPhysics?.ApplyKnockbackFrom(interactionContext.transform.position);
    }

    public void TakeDamage(float amount)
    {
        if (amount <= 0 || isDead) return;
        float before=currentHealth;
        currentHealth = Mathf.Max(0, CombatDamage.RoundHealth(currentHealth - amount));
        CampaignDamageNumber.Show(gameObject,before-currentHealth);
        if (currentHealth == 0) ChangeState(deadState);
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
        return FindLivingPlayer() && Vector2.Distance(rb.position, playerTarget.position) <= detectionRadius;
    }

    private void Move(Vector2 direction)
    {
        direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.zero;
        rb.linearVelocity = direction * moveSpeed;
        if (Mathf.Abs(direction.x) > 0.01f) spriteRenderer.flipX = direction.x < 0f;
    }

    private void StopMoving() => rb.linearVelocity = Vector2.zero;

    private bool TryGetCeilingAnchor(out Vector2 anchor)
    {
        RaycastHit2D[] hits = Physics2D.RaycastAll(rb.position, Vector2.up, ceilingSearchDistance, groundLayers);
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit2D hit = hits[i];
            if (hit.collider == null || hit.collider == bodyCollider || hit.normal.y > -minimumCeilingNormal) continue;

            float spriteTopOffset = Mathf.Max(0.01f, spriteRenderer.bounds.max.y - rb.position.y);
            anchor = hit.point + hit.normal * (spriteTopOffset + ceilingClearance);
            return true;
        }

        anchor = rb.position;
        return false;
    }

    private void SnapToCeiling()
    {
        if (TryGetCeilingAnchor(out Vector2 anchor)) rb.position = anchor;
    }

    private void TryDamagePlayer()
    {
        if (!FindLivingPlayer()) return;
        if (Vector2.Distance(rb.position, playerTarget.position) > contactDistance) return;
        CombatDamage.Apply(playerDamageable,attackDamage,gameObject);
        if (playerDamageable != null && !playerDamageable.IsDead)
            playerTarget.GetComponent<CharacterPhysics2D>()?.ApplyKnockbackFrom(transform.position);
    }

    private void BeginRetreat()
    {
        nextAttackTime = Time.time + attackCooldown;
        ChangeState(retreatState);
    }

    private void PlayAnimation(AnimationState state)
    {
        Sprite[] frames;
        float fps;
        bool loops;
        switch (state)
        {
            case AnimationState.Fly: frames = flyFrames; fps = flyFramesPerSecond; loops = true; break;
            case AnimationState.Death: frames = deathFrames; fps = deathFramesPerSecond; loops = false; break;
            default: frames = idleFrames; fps = idleFramesPerSecond; loops = true; break;
        }
        if (currentFrames == frames) return;
        currentFrames = frames;
        currentFramesPerSecond = fps;
        animationLoops = loops;
        animationTime = 0f;
        ApplyFrame(0);
    }

    private void UpdateAnimation()
    {
        if (currentFrames == null || currentFrames.Length == 0) return;
        int index = SpriteFrameClock.Advance(ref animationTime, Time.deltaTime,
            currentFramesPerSecond, currentFrames.Length, animationLoops);
        ApplyFrame(index);
    }

    private void ApplyFrame(int index)
    {
        if (currentFrames == null || currentFrames.Length == 0) return;
        Sprite frame = currentFrames[Mathf.Clamp(index, 0, currentFrames.Length - 1)];
        if (frame != null) spriteRenderer.sprite = frame;
    }

    private float DeathDuration => deathFrames == null || deathFrames.Length == 0 ? 0.5f : deathFrames.Length / deathFramesPerSecond;

    private void SpawnSharpFangs()
    {
        if (dropData == null || itemDropSpawner == null || !dropData.TryGetDrop(0, out GameObject prefab, out _)) return;
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            Vector2 offset = dropOffset + new Vector2(side * dropRadius, 0.15f);
            itemDropSpawner.Spawn(prefab, transform.position + (Vector3)offset, 1);
        }
    }

    private void OnTriggerEnter2D(Collider2D other) => HandlePlayerContact(other);
    private void OnCollisionEnter2D(Collision2D collision) => HandlePlayerContact(collision.collider);

    private void HandlePlayerContact(Collider2D other)
    {
        if (isDead || Time.time < nextAttackTime) return;
        IDamageable damageable = other.GetComponentInParent<IDamageable>();
        if (damageable == null || damageable.IsDead || other.GetComponentInParent<PlayerAssimilate>() == null) return;
        CombatDamage.Apply(damageable,attackDamage,gameObject);
        if (!damageable.IsDead)
            other.GetComponentInParent<CharacterPhysics2D>()?.ApplyKnockbackFrom(transform.position);
        BeginRetreat();
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        attackCooldown = Mathf.Max(2f, attackCooldown);
        contactDistance = Mathf.Min(contactDistance, detectionRadius);
        ceilingSearchDistance = Mathf.Max(0.1f, ceilingSearchDistance);
        minimumCeilingNormal = Mathf.Clamp01(minimumCeilingNormal);
        ceilingClearance = Mathf.Max(0f, ceilingClearance);
        ceilingArrivalDistance = Mathf.Max(0.01f, ceilingArrivalDistance);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, contactDistance);
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * ceilingSearchDistance);
    }
}
