using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(SpriteRenderer))]
public sealed class VampireBatController : MonoBehaviour, IDamageable, IInteractable
{
    private interface IState { void Enter(); void Tick(); void Exit(); }
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

    private IState currentState;
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
    private int currentHealth;
    private float nextAttackTime;
    private bool isDead;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsDead => isDead;

    private void Awake()
    {
        rb ??= GetComponent<Rigidbody2D>();
        bodyCollider ??= GetComponent<Collider2D>();
        spriteRenderer ??= GetComponent<SpriteRenderer>();
        itemDropSpawner ??= FindFirstObjectByType<ItemDropSpawner>();
        rb.gravityScale = 0f;
        bodyCollider.isTrigger = false;
        currentHealth = maxHealth;
        idleState = new CeilingIdleState(this);
        attackState = new DiveAttackState(this);
        retreatState = new DiagonalRetreatState(this);
        returnToCeilingState = new ReturnToCeilingState(this);
        deadState = new DeadState(this);
    }

    private void Start() => ChangeState(returnToCeilingState);
    private void Update() => UpdateAnimation();
    private void FixedUpdate() => currentState?.Tick();

    public bool CanInteract() => !isDead;
    public bool CanUseTool(ToolData toolData) => toolData != null && toolData.ToolType == ToolType.Sword;

    public void Interact(PlayerInteraction interactionContext)
    {
        if (interactionContext == null || !CanUseTool(interactionContext.CurrentTool)) return;
        playerTarget = interactionContext.transform;
        playerDamageable = interactionContext.GetComponent<IDamageable>();
        TakeDamage(interactionContext.CurrentTool.Damage);
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0 || isDead) return;
        currentHealth = Mathf.Max(0, currentHealth - amount);
        if (currentHealth == 0) ChangeState(deadState);
    }

    private void ChangeState(IState nextState)
    {
        if (currentState == nextState) return;
        currentState?.Exit();
        currentState = nextState;
        currentState.Enter();
    }

    private bool FindLivingPlayer()
    {
        if (playerTarget == null)
        {
            PlayerAssimilate player = FindFirstObjectByType<PlayerAssimilate>();
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
        playerDamageable?.TakeDamage(attackDamage);
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
        animationTime += Time.deltaTime * currentFramesPerSecond;
        int index = Mathf.FloorToInt(animationTime);
        index = animationLoops ? index % currentFrames.Length : Mathf.Min(index, currentFrames.Length - 1);
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
        damageable.TakeDamage(attackDamage);
        BeginRetreat();
    }

    private sealed class CeilingIdleState : IState
    {
        private readonly VampireBatController owner;
        public CeilingIdleState(VampireBatController owner) => this.owner = owner;
        public void Enter()
        {
            owner.StopMoving();
            owner.PlayAnimation(AnimationState.Idle);
            owner.SnapToCeiling();
        }
        public void Tick()
        {
            owner.StopMoving();
            if (!owner.TryGetCeilingAnchor(out Vector2 anchor) ||
                Vector2.Distance(owner.rb.position, anchor) > owner.ceilingArrivalDistance)
            {
                owner.ChangeState(owner.returnToCeilingState);
                return;
            }

            owner.rb.position = anchor;
            if (Time.time >= owner.nextAttackTime && owner.PlayerIsDetected()) owner.ChangeState(owner.attackState);
        }
        public void Exit() { }
    }

    private sealed class DiveAttackState : IState
    {
        private readonly VampireBatController owner;
        private float endTime;
        public DiveAttackState(VampireBatController owner) => this.owner = owner;
        public void Enter() { endTime = Time.time + owner.attackDuration; owner.PlayAnimation(AnimationState.Fly); }
        public void Tick()
        {
            if (!owner.FindLivingPlayer() || Time.time >= endTime) { owner.TryDamagePlayer(); owner.BeginRetreat(); return; }
            Vector2 direction = (Vector2)owner.playerTarget.position - owner.rb.position;
            owner.Move(direction);
            if (direction.magnitude <= owner.contactDistance) { owner.TryDamagePlayer(); owner.BeginRetreat(); }
        }
        public void Exit() { }
    }

    private sealed class DiagonalRetreatState : IState
    {
        private readonly VampireBatController owner;
        private Vector2 direction;
        private float endTime;
        public DiagonalRetreatState(VampireBatController owner) => this.owner = owner;
        public void Enter()
        {
            Vector2 away = owner.FindLivingPlayer() ? owner.rb.position - (Vector2)owner.playerTarget.position : Vector2.left;
            float horizontal = Mathf.Abs(away.x) > 0.01f ? Mathf.Sign(away.x) : (Random.value < 0.5f ? -1f : 1f);
            direction = new Vector2(horizontal, Mathf.Max(0.45f, away.normalized.y + owner.retreatUpwardBias)).normalized;
            endTime = Time.time + owner.retreatDuration;
            owner.PlayAnimation(AnimationState.Fly);
        }
        public void Tick()
        {
            if (Time.time >= endTime) { owner.ChangeState(owner.returnToCeilingState); return; }
            owner.Move(direction);
        }
        public void Exit() { }
    }

    private sealed class ReturnToCeilingState : IState
    {
        private readonly VampireBatController owner;
        public ReturnToCeilingState(VampireBatController owner) => this.owner = owner;

        public void Enter() => owner.PlayAnimation(AnimationState.Fly);

        public void Tick()
        {
            if (!owner.TryGetCeilingAnchor(out Vector2 anchor))
            {
                owner.Move(Vector2.up);
                return;
            }

            Vector2 direction = anchor - owner.rb.position;
            if (direction.magnitude > owner.ceilingArrivalDistance)
            {
                owner.Move(direction);
                return;
            }

            owner.rb.position = anchor;
            owner.ChangeState(owner.idleState);
        }

        public void Exit() => owner.StopMoving();
    }

    private sealed class DeadState : IState
    {
        private readonly VampireBatController owner;
        private float destroyTime;
        public DeadState(VampireBatController owner) => this.owner = owner;
        public void Enter()
        {
            owner.isDead = true;
            owner.StopMoving();
            owner.rb.simulated = false;
            owner.bodyCollider.enabled = false;
            owner.PlayAnimation(AnimationState.Death);
            destroyTime = Time.time + owner.DeathDuration;
        }
        public void Tick()
        {
            if (Time.time < destroyTime) return;
            owner.SpawnSharpFangs();
            Destroy(owner.gameObject);
        }
        public void Exit() { }
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
