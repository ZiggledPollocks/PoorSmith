using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(SpriteRenderer))]
public sealed partial class MossSlimeController : MonoBehaviour, IHealthSource, IInteractable
{
    private enum AnimationState { Hop, Death }

    [Header("References")]
    [SerializeField] private Transform playerTarget;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Collider2D bodyCollider;
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Stats")]
    [SerializeField, Min(1)] private int maxHealth = 50;
    [SerializeField, Min(0f)] private float moveSpeed = 3f;
    [SerializeField, Min(1)] private int attackDamage = 10;

    [Header("Jump Behaviour")]
    [SerializeField, Min(0.1f)] private float detectionRadius = 6f;
    [SerializeField, Min(0.1f)] private float jumpForce = 5f;
    [SerializeField, Min(0.05f)] private float jumpInterval = 0.25f;
    [SerializeField, Min(0.1f)] private float wanderDirectionTime = 2f;
    [SerializeField] private LayerMask groundLayers = 64;
    [SerializeField, Min(0.01f)] private float groundProbeDistance = 0.12f;
    [SerializeField, Min(0f)] private float contactDamageCooldown = 1f;

    [Header("Animation Frames")]
    [SerializeField] private Sprite[] hopFrames;
    [SerializeField] private Sprite[] deathFrames;
    [SerializeField, Min(0.1f)] private float hopFramesPerSecond = 12f;
    [SerializeField, Min(0.1f)] private float deathFramesPerSecond = 12f;

    [Header("Death Drop")]
    [SerializeField] private ResourceData dropData;
    [SerializeField] private ItemDropSpawner itemDropSpawner;
    [SerializeField] private Vector2 dropOffset = new(0f, 0.25f);

    private readonly RaycastHit2D[] groundHits = new RaycastHit2D[4];
    private readonly BehaviourStateMachine stateMachine = new();
    private IBehaviourState currentState => stateMachine.Current;
    private WanderHopState wanderState;
    private ChaseHopState chaseState;
    private DeadState deadState;
    private ContactFilter2D groundFilter;
    private IDamageable playerDamageable;
    private Sprite[] currentFrames;
    private float currentFramesPerSecond;
    private float animationTime;
    private bool animationLoops;
    private CharacterPhysics2D characterPhysics;
    private float currentHealth;
    private float nextJumpTime;
    private float nextContactDamageTime;
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
        currentHealth = maxHealth;
        MonsterHealthBar2D.Attach(gameObject, bodyCollider, spriteRenderer);
        groundFilter = new ContactFilter2D();
        groundFilter.SetLayerMask(groundLayers);
        groundFilter.useTriggers = false;
        wanderState = new WanderHopState(this);
        chaseState = new ChaseHopState(this);
        deadState = new DeadState(this);
    }

    private void Start() => ChangeState(wanderState);
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

    private bool IsGrounded()
    {
        return bodyCollider.Cast(Vector2.down, groundFilter, groundHits, groundProbeDistance) > 0;
    }

    private void Jump(float direction)
    {
        if (!IsGrounded() || Time.time < nextJumpTime) return;
        direction = Mathf.Abs(direction) < 0.01f ? 0f : Mathf.Sign(direction);
        rb.linearVelocity = new Vector2(direction * moveSpeed, jumpForce);
        if (Mathf.Abs(direction) > 0.01f) spriteRenderer.flipX = direction < 0f;
        nextJumpTime = Time.time + jumpInterval;
    }

    private void PlayAnimation(AnimationState state)
    {
        Sprite[] frames = state == AnimationState.Death ? deathFrames : hopFrames;
        float fps = state == AnimationState.Death ? deathFramesPerSecond : hopFramesPerSecond;
        bool loops = state != AnimationState.Death;
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

    private void SpawnMucus()
    {
        if (dropData == null || itemDropSpawner == null || !dropData.TryGetDrop(0, out GameObject prefab, out _)) return;
        itemDropSpawner.Spawn(prefab, transform.position + (Vector3)dropOffset, 1);
    }

    private void OnCollisionEnter2D(Collision2D collision) => DamagePlayerOnContact(collision.collider);
    private void OnCollisionStay2D(Collision2D collision) => DamagePlayerOnContact(collision.collider);

    private void DamagePlayerOnContact(Collider2D other)
    {
        if (isDead || Time.time < nextContactDamageTime) return;
        PlayerAssimilate player = other.GetComponentInParent<PlayerAssimilate>();
        if (player == null || player.IsDead) return;
        CombatDamage.Apply(player,attackDamage,gameObject);
        if (!player.IsDead)
            player.GetComponent<CharacterPhysics2D>()?.ApplyKnockbackFrom(transform.position);
        nextContactDamageTime = Time.time + contactDamageCooldown;
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        detectionRadius = Mathf.Max(0.1f, detectionRadius);
        groundProbeDistance = Mathf.Max(0.01f, groundProbeDistance);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}
