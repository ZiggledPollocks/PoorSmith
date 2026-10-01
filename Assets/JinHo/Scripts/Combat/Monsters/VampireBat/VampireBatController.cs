// [코드 지도] VampireBatController: 천장에 붙어 대기하다 플레이어에게 급강하하는 박쥐입니다. 공격 후 대각선 위쪽으로 후퇴하고 천장을 다시 찾습니다. 거리 기반 타격과 실제 접촉 타격을 모두 제공합니다. 다섯 상태 객체와 Sprite 배열 재생으로 행동·표시를 분리합니다.
// 주요 함수: SafeRetreatSpeed, MoveDuringRetreat, Awake
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/Combat/Monsters/VampireBat/VampireBatController.cs.md

using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(SpriteRenderer))]
/// <summary>Owns vampire bat health, movement and state transitions.</summary>
public sealed partial class VampireBatController : MonoBehaviour, IHealthSource, IInteractable
{
    private enum AnimationState { Idle, Fly, Attack, Damage, Death }
    private const float RetreatVisibleGap = 0.2f;
    private const float RetreatMinimumClearance = 0.3f;
    private const float RetreatBrakingDistance = 0.55f;

    [Header("References")]
    [SerializeField] private Transform playerTarget;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Collider2D bodyCollider;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private SpriteRenderer attackEffectRenderer;

    [Header("Stats")]
    [SerializeField, Min(1)] private int maxHealth = 38;
    [SerializeField, Min(0f)] private float moveSpeed = 7f;
    [SerializeField, Min(1)] private int attackDamage = 7;

    [Header("Behaviour")]
    [SerializeField, Min(0.1f)] private float detectionRadius = 9f;
    [SerializeField, Min(0.1f)] private float attackDuration = 1.1f;
    [SerializeField, Min(0f)] private float retreatUpwardBias = 0.8f;
    [SerializeField, Min(0f)] private float attackCooldown = 2f;
    [SerializeField, Min(0f)] private float counterattackWindow = 0.85f;
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
    [SerializeField] private Sprite[] attackFrames;
    [SerializeField] private Sprite[] damageFrames;
    [SerializeField] private Sprite[] deathFrames;
    [SerializeField] private Sprite[] attackEffectFrames;
    [SerializeField, Min(0.1f)] private float idleFramesPerSecond = 7f;
    [SerializeField, Min(0.1f)] private float flyFramesPerSecond = 12f;
    [SerializeField, Min(0.1f)] private float attackFramesPerSecond = 12f;
    [SerializeField, Min(0.1f)] private float damageFramesPerSecond = 12f;
    [SerializeField, Min(0.1f)] private float deathFramesPerSecond = 9f;

    [Header("Death Drop")]
    [SerializeField] private ResourceData dropData;
    [SerializeField] private ItemDropSpawner itemDropSpawner;
    [SerializeField, Min(0f)] private float dropRadius = 0.65f;
    [SerializeField] private Vector2 dropOffset = new(0f, 0.2f);

    private readonly BehaviourStateMachine stateMachine = new();
    private readonly RaycastHit2D[] retreatObstacleHits = new RaycastHit2D[16];
    private ContactFilter2D retreatTerrainFilter;
    private IBehaviourState currentState => stateMachine.Current;
    private CeilingIdleState idleState;
    private DiveAttackState attackState;
    private CounterattackWindowState counterattackState;
    private DiagonalRetreatState retreatState;
    private ReturnToCeilingState returnToCeilingState;
    private DeadState deadState;
    private IDamageable playerDamageable;
    private Sprite[] currentFrames;
    private float currentFramesPerSecond;
    private float animationTime;
    private bool animationLoops;
    private float attackEffectTime;
    private bool attackEffectPlaying;
    private Vector2 attackEffectWorldPosition;
    private float damageAnimationEndTime;
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
        retreatTerrainFilter.SetLayerMask(groundLayers);
        retreatTerrainFilter.useTriggers = false;
        currentHealth = maxHealth;
        MonsterHealthBar2D.Attach(gameObject, bodyCollider, spriteRenderer);
        idleState = new CeilingIdleState(this);
        attackState = new DiveAttackState(this);
        counterattackState = new CounterattackWindowState(this);
        retreatState = new DiagonalRetreatState(this);
        returnToCeilingState = new ReturnToCeilingState(this);
        deadState = new DeadState(this);
    }

    private void Start() => ChangeState(returnToCeilingState);
    private void Update()
    {
        UpdateAnimation();
        UpdateAttackEffect();
        if (!isDead && damageAnimationEndTime > 0f && Time.time >= damageAnimationEndTime)
        {
            damageAnimationEndTime = 0f;
            PlayAnimation(currentState == attackState ? AnimationState.Attack :
                currentState == idleState ? AnimationState.Idle : AnimationState.Fly);
        }
    }
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
            interactionContext.ApplyMonsterKnockback(characterPhysics);
    }

    public void TakeDamage(float amount)
    {
        if (amount <= 0 || isDead) return;
        float before = currentHealth;
        currentHealth = Mathf.Max(0, CombatDamage.RoundHealth(currentHealth - amount));
        CampaignDamageNumber.Show(gameObject, before - currentHealth);
        if (currentHealth == 0) ChangeState(deadState);
        else if (damageFrames != null && damageFrames.Length > 0)
        {
            PlayAnimation(AnimationState.Damage);
            damageAnimationEndTime = Time.time + damageFrames.Length / damageFramesPerSecond;
        }
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

    // 핵심 분기: direction.sqrMagnitude <= 0.001f 판정.
    // 상태 변경: safeVelocity 갱신.
    // 다음 연결: VampireBatController.StopMoving() 호출.
    private void MoveDuringRetreat(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.001f) { StopMoving(); return; }

        Vector2 desiredVelocity = direction.normalized * moveSpeed;
        float stepTime = Mathf.Max(Time.fixedDeltaTime, 0.001f);
        Vector2 safeVelocity = new(
            Mathf.Sign(desiredVelocity.x) * SafeRetreatSpeed(Vector2.right * Mathf.Sign(desiredVelocity.x),
                Mathf.Abs(desiredVelocity.x), stepTime),
            Mathf.Sign(desiredVelocity.y) * SafeRetreatSpeed(Vector2.up * Mathf.Sign(desiredVelocity.y),
                Mathf.Abs(desiredVelocity.y), stepTime));

        // Two individually clear axes can still cut across the corner of a tile.
        if (safeVelocity.sqrMagnitude > 0.0001f)
            safeVelocity = safeVelocity.normalized *
                SafeRetreatSpeed(safeVelocity.normalized, safeVelocity.magnitude, stepTime);

        rb.linearVelocity = safeVelocity;
        if (Mathf.Abs(safeVelocity.x) > 0.01f)
            spriteRenderer.flipX = safeVelocity.x < 0f;
    }

    // 핵심 분기: desiredSpeed <= 0.001f 판정.
    // 상태 변경: overhang 갱신.
    private float SafeRetreatSpeed(Vector2 castDirection, float desiredSpeed, float stepTime)
    {
        if (desiredSpeed <= 0.001f) return 0f;

        Bounds body = bodyCollider.bounds;
        Bounds art = spriteRenderer.bounds;
        float overhang = 0f;
        if (castDirection.x > 0f) overhang = Mathf.Max(overhang, art.max.x - body.max.x);
        else if (castDirection.x < 0f) overhang = Mathf.Max(overhang, body.min.x - art.min.x);
        if (castDirection.y > 0f) overhang = Mathf.Max(overhang, art.max.y - body.max.y);
        else if (castDirection.y < 0f) overhang = Mathf.Max(overhang, body.min.y - art.min.y);
        float clearance = Mathf.Max(RetreatMinimumClearance, overhang + RetreatVisibleGap);
        float brakingDistance = Mathf.Max(RetreatBrakingDistance, moveSpeed * stepTime * 3f);
        float lookAhead = desiredSpeed * stepTime + clearance + brakingDistance;
        int count = bodyCollider.Cast(castDirection, retreatTerrainFilter, retreatObstacleHits, lookAhead);
        float nearest = float.PositiveInfinity;
        for (int index = 0; index < count; index++)
        {
            RaycastHit2D hit = retreatObstacleHits[index];
            if (hit.collider == null || hit.collider == bodyCollider || hit.collider.isTrigger) continue;
            nearest = Mathf.Min(nearest, hit.distance);
        }

        if (float.IsPositiveInfinity(nearest)) return desiredSpeed;
        float available = Mathf.Max(0f, nearest - clearance);
        return Mathf.Min(desiredSpeed * Mathf.Clamp01(available / brakingDistance), available / stepTime);
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

    private bool TryDamagePlayer()
    {
        if (!FindLivingPlayer()) return false;
        if (Vector2.Distance(rb.position, playerTarget.position) > contactDistance) return false;
        CombatDamage.Apply(playerDamageable, attackDamage, gameObject);
        PlayAttackEffect(AttackContactPoint(playerTarget.GetComponent<Collider2D>()));
        if (playerDamageable != null && !playerDamageable.IsDead)
            playerTarget.GetComponent<CharacterPhysics2D>()?.ApplyKnockbackFrom(transform.position);
        return true;
    }

    private void BeginCounterattackWindow()
    {
        nextAttackTime = Time.time + attackCooldown;
        ChangeState(counterattackState);
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
            case AnimationState.Attack: frames = attackFrames; fps = attackFramesPerSecond; loops = true; break;
            case AnimationState.Damage: frames = damageFrames; fps = damageFramesPerSecond; loops = false; break;
            case AnimationState.Death: frames = deathFrames; fps = deathFramesPerSecond; loops = false; break;
            default: frames = idleFrames; fps = idleFramesPerSecond; loops = true; break;
        }
        if (currentFrames == frames && state != AnimationState.Damage) return;
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

    private Vector2 AttackContactPoint(Collider2D targetCollider)
    {
        if (bodyCollider == null || targetCollider == null)
            return ((Vector2)transform.position +
                (targetCollider != null ? (Vector2)targetCollider.transform.position :
                    playerTarget != null ? (Vector2)playerTarget.position : (Vector2)transform.position)) * .5f;
        Vector2 batPoint = bodyCollider.ClosestPoint(targetCollider.bounds.center);
        Vector2 playerPoint = targetCollider.ClosestPoint(bodyCollider.bounds.center);
        return (batPoint + playerPoint) * .5f;
    }

    private void PlayAttackEffect(Vector2 contactPoint)
    {
        if (attackEffectRenderer == null ||
            attackEffectFrames == null || attackEffectFrames.Length == 0) return;
        attackEffectWorldPosition = contactPoint;
        attackEffectTime = 0f;
        attackEffectPlaying = true;
        attackEffectRenderer.flipX = spriteRenderer.flipX;
        attackEffectRenderer.sprite = attackEffectFrames[0];
        PositionAttackEffect();
        attackEffectRenderer.enabled = true;
    }

    private void PositionAttackEffect()
    {
        attackEffectRenderer.transform.position = new Vector3(attackEffectWorldPosition.x,
            attackEffectWorldPosition.y,
            attackEffectRenderer.transform.position.z);
    }

    private void StopAttackEffect()
    {
        attackEffectPlaying = false;
        if (attackEffectRenderer != null) attackEffectRenderer.enabled = false;
    }

    private void UpdateAttackEffect()
    {
        if (!attackEffectPlaying || attackEffectRenderer == null) return;
        PositionAttackEffect();
        attackEffectTime += Time.deltaTime;
        int frameIndex = Mathf.FloorToInt(attackEffectTime * attackFramesPerSecond);
        if (frameIndex >= attackEffectFrames.Length)
        {
            StopAttackEffect();
            return;
        }

        attackEffectRenderer.sprite = attackEffectFrames[frameIndex];
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
    private void OnCollisionEnter2D(Collision2D collision) => HandlePlayerContact(collision.collider,
        collision.contactCount > 0 ? collision.GetContact(0).point : (Vector2?)null);

    private void HandlePlayerContact(Collider2D other, Vector2? collisionPoint = null)
    {
        if (isDead || Time.time < nextAttackTime) return;
        IDamageable damageable = other.GetComponentInParent<IDamageable>();
        PlayerAssimilate player = other.GetComponentInParent<PlayerAssimilate>();
        if (damageable == null || damageable.IsDead || player == null) return;
        Vector2 contactPoint = collisionPoint ?? AttackContactPoint(player.GetComponent<Collider2D>() ?? other);
        CombatDamage.Apply(damageable, attackDamage, gameObject);
        PlayAttackEffect(contactPoint);
        if (!damageable.IsDead)
            other.GetComponentInParent<CharacterPhysics2D>()?.ApplyKnockbackFrom(transform.position);
        BeginCounterattackWindow();
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        attackCooldown = Mathf.Max(2f, attackCooldown);
        counterattackWindow = Mathf.Max(0f, counterattackWindow);
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
