using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(SpriteRenderer))]
public sealed class StoneGolemController : MonoBehaviour, IDamageable, IInteractable
{
    private interface IState { void Enter(); void Tick(); void Exit(); }
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
    [SerializeField, Min(0.1f)] private float attackRange = 1.6f;
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
    private IState currentState;
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
    private int currentHealth;
    private float nextAttackTime;
    private Vector2 spawnPosition;
    private bool isProvoked;
    private bool isDead;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsDead => isDead;
    public bool IsProvoked => isProvoked;

    private void Awake()
    {
        rb ??= GetComponent<Rigidbody2D>();
        bodyCollider ??= GetComponent<Collider2D>();
        spriteRenderer ??= GetComponent<SpriteRenderer>();
        itemDropSpawner ??= FindFirstObjectByType<ItemDropSpawner>();
        currentHealth = maxHealth;
        spawnPosition = rb.position;
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
    private void FixedUpdate() => currentState?.Tick();

    public bool CanInteract() => !isDead;
    public bool CanUseTool(ToolData toolData) => toolData != null && toolData.ToolType == ToolType.Pickaxe;

    public void Interact(PlayerInteraction interactionContext)
    {
        if (interactionContext == null || !CanUseTool(interactionContext.CurrentTool)) return;
        playerTarget = interactionContext.transform;
        playerDamageable = interactionContext.GetComponent<IDamageable>();
        ApplyPickaxeHit();
    }

    // IDamageable 직접 호출은 무기 종류를 증명할 수 없으므로 무시한다.
    // 돌골렘 피해는 반드시 Pickaxe가 검증되는 Interact 경로로 들어온다.
    public void TakeDamage(int amount) { }

    private void ApplyPickaxeHit()
    {
        if (isDead) return;
        isProvoked = true;
        currentHealth = Mathf.Max(0, currentHealth - pickaxeDamage);
        if (currentHealth == 0) { ChangeState(deadState); return; }
        ChangeState(chaseState);
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

    private bool CanMove(float direction)
    {
        if (Mathf.Abs(direction) < 0.01f) return false;
        float sign = Mathf.Sign(direction);
        Bounds bounds = bodyCollider.bounds;
        if (bodyCollider.Cast(Vector2.right * sign, groundFilter, wallHits, wallProbeDistance) > 0) return false;
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
        animationTime += Time.deltaTime * currentFramesPerSecond;
        int index = Mathf.FloorToInt(animationTime);
        index = animationLoops ? index % currentFrames.Length : Mathf.Min(index, currentFrames.Length - 1);
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
        if (frame != null) spriteRenderer.sprite = frame;
    }

    private void DealCriticalFrameDamage()
    {
        if (!FindLivingPlayer()) return;
        if (Vector2.Distance(rb.position, playerTarget.position) <= attackRange)
            playerDamageable?.TakeDamage(attackDamage);
    }

    private float DeathDuration => deathFrames == null || deathFrames.Length == 0 ? 0.5f : deathFrames.Length / deathFramesPerSecond;

    private void SpawnCore()
    {
        if (dropData == null || itemDropSpawner == null || !dropData.TryGetDrop(0, out GameObject prefab, out _)) return;
        itemDropSpawner.Spawn(prefab, transform.position + (Vector3)dropOffset, 1);
    }

    private sealed class IdlePatrolState : IState
    {
        private readonly StoneGolemController owner;
        private float direction;
        private float nextTurnTime;
        public IdlePatrolState(StoneGolemController owner) => this.owner = owner;
        public void Enter()
        {
            direction = Random.value < 0.5f ? -1f : 1f;
            nextTurnTime = Time.time + Random.Range(0.8f, 1.8f);
            owner.PlayAnimation(AnimationState.Idle);
        }
        public void Tick()
        {
            if (owner.isProvoked) { owner.ChangeState(owner.chaseState); return; }
            float offset = owner.rb.position.x - owner.spawnPosition.x;
            if (Mathf.Abs(offset) >= owner.idlePatrolRadius) direction = -Mathf.Sign(offset);
            else if (Time.time >= nextTurnTime)
            {
                direction *= -1f;
                nextTurnTime = Time.time + Random.Range(0.8f, 1.8f);
            }
            if (!owner.CanMove(direction)) { owner.StopMoving(); return; }
            owner.Move(direction);
        }
        public void Exit() { }
    }

    private sealed class ChaseState : IState
    {
        private readonly StoneGolemController owner;
        public ChaseState(StoneGolemController owner) => this.owner = owner;
        public void Enter() => owner.PlayAnimation(AnimationState.Walk);
        public void Tick()
        {
            if (!owner.FindLivingPlayer()) { owner.StopMoving(); return; }
            float delta = owner.playerTarget.position.x - owner.transform.position.x;
            if (Mathf.Abs(delta) <= owner.attackRange && Time.time >= owner.nextAttackTime)
            {
                owner.ChangeState(owner.attackState);
                return;
            }
            if (!owner.CanMove(delta)) { owner.StopMoving(); return; }
            owner.Move(delta);
        }
        public void Exit() { }
    }

    private sealed class AttackState : IState
    {
        private const int CriticalFrameIndex = 4;
        private readonly StoneGolemController owner;
        private int frameIndex;
        private float nextFrameTime;
        private float holdEndTime;
        private bool holdingCriticalFrame;
        public AttackState(StoneGolemController owner) => this.owner = owner;
        public void Enter()
        {
            owner.StopMoving();
            frameIndex = 0;
            holdingCriticalFrame = false;
            owner.ApplyAttackFrame(frameIndex);
            nextFrameTime = Time.time + 1f / owner.attackFramesPerSecond;
        }
        public void Tick()
        {
            owner.StopMoving();
            if (holdingCriticalFrame)
            {
                if (Time.time < holdEndTime) return;
                holdingCriticalFrame = false;
                frameIndex = CriticalFrameIndex + 1;
                owner.ApplyAttackFrame(frameIndex);
                nextFrameTime = Time.time + 1f / owner.attackFramesPerSecond;
                return;
            }
            if (Time.time < nextFrameTime) return;
            frameIndex++;
            if (owner.attackFrames == null || frameIndex >= owner.attackFrames.Length)
            {
                owner.nextAttackTime = Time.time + owner.attackCooldown;
                owner.ChangeState(owner.chaseState);
                return;
            }
            owner.ApplyAttackFrame(frameIndex);
            if (frameIndex == CriticalFrameIndex)
            {
                owner.DealCriticalFrameDamage();
                holdingCriticalFrame = true;
                holdEndTime = Time.time + owner.criticalFrameHold;
            }
            else
            {
                nextFrameTime = Time.time + 1f / owner.attackFramesPerSecond;
            }
        }
        public void Exit() => owner.manualAttackAnimation = false;
    }

    private sealed class DeadState : IState
    {
        private readonly StoneGolemController owner;
        private float destroyTime;
        public DeadState(StoneGolemController owner) => this.owner = owner;
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
            owner.SpawnCore();
            Destroy(owner.gameObject);
        }
        public void Exit() { }
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        pickaxeDamage = 13;
        criticalFrameHold = 0.5f;
        idlePatrolRadius = Mathf.Max(0.1f, idlePatrolRadius);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = Color.cyan;
        Vector3 center = Application.isPlaying ? (Vector3)spawnPosition : transform.position;
        Gizmos.DrawLine(center + Vector3.left * idlePatrolRadius, center + Vector3.right * idlePatrolRadius);
    }
}
