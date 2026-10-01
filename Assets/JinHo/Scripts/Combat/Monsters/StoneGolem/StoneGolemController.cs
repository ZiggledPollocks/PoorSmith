// [코드 지도] StoneGolemController: 곡괭이에만 고정 피해를 받는 지상 골렘입니다. 평상시 생성 위치 주변을 순찰하다 감지 또는 피격으로 도발되면 공격·추격을 지속합니다. 다섯 번째 공격 그림에서 피해를 주고 잠시 프레임을 유지합니다. 자식 Visual의 높이와 발 위치를 정규화하여 서로 다른 Sprite 크기를 보정합니다.
// 주요 함수: CanMove, Awake, PlayAnimation
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/Combat/Monsters/StoneGolem/StoneGolemController.cs.md

using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public sealed partial class StoneGolemController : MonoBehaviour, IHealthSource, IInteractable
{
    private enum AnimationState { Idle, Awake, Walk, Slam, Throw, Death }

    [Header("References")]
    [SerializeField] private Transform playerTarget;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Collider2D bodyCollider;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private StoneGolemRockProjectile rockProjectilePrefab;

    [Header("Stats")]
    [SerializeField, Min(1)] private int maxHealth = 300;
    [SerializeField, Min(0f)] private float moveSpeed = 2.4f;
    [SerializeField, Min(0f)] private float chaseSpeed = 3f;
    [SerializeField, Min(1)] private int slamDamage = 25;
    [SerializeField, Min(1)] private int rockDamage = 20;
    [SerializeField, Min(1)] private int pickaxeDamage = 13;

    [Header("Behaviour")]
    [SerializeField, Min(0.1f)] private float meleeRange = 3.8f;
    [SerializeField, Min(0.1f)] private float throwRange = 8.5f;
    [SerializeField, Min(0.1f)] private float returnDistance = 12f;
    [SerializeField, Min(0.1f)] private float slamImpactRadius = 3.8f;
    [SerializeField, Min(0.1f)] private float rockImpactRadius = 2.8f;
    [SerializeField, Min(0.1f)] private float fiveStepDuration = 4.5f;
    [SerializeField, Min(0.1f)] private float repeatWalkDuration = 2.1f;
    [SerializeField, Min(0f)] private float attackCooldown = 0.45f;
    [SerializeField, Min(0f)] private float slamKnockbackMultiplier = 1.25f;
    [SerializeField, Min(0f)] private float rockKnockbackMultiplier = 1f;
    [SerializeField, Min(0.1f)] private float rockFlightSeconds = 0.7f;
    [SerializeField] private LayerMask groundLayers = 64;
    [SerializeField, Min(0.01f)] private float ledgeProbeDistance = 2.5f;
    [SerializeField, Min(0f)] private float wallProbeDistance = 0.12f;

    [Header("Animation Frames")]
    [SerializeField] private Sprite[] idleFrames;
    [SerializeField] private Sprite[] awakeFrames;
    [SerializeField] private Sprite[] walkFrames;
    [SerializeField] private Sprite[] slamFrames;
    [SerializeField] private Sprite[] throwFrames;
    [SerializeField] private Sprite[] deathFrames;
    [SerializeField, Min(0.1f)] private float idleFramesPerSecond = 12.05f;
    [SerializeField, Min(0.1f)] private float awakeFramesPerSecond = 12.05f;
    [SerializeField, Min(0.1f)] private float slamFramesPerSecond = 9f;
    [SerializeField, Min(0.1f)] private float throwFramesPerSecond = 12.05f;
    [SerializeField, Min(0.1f)] private float deathFramesPerSecond = 12.05f;
    [SerializeField, Range(0, 9)] private int slamImpactFrame = 7;
    [SerializeField, Range(0, 10)] private int throwReleaseFrame = 7;

    [Header("Death Drop")]
    [SerializeField] private ResourceData dropData;
    [SerializeField] private ItemDropSpawner itemDropSpawner;
    [SerializeField] private Vector2 dropOffset;

    private readonly RaycastHit2D[] wallHits = new RaycastHit2D[4];
    private readonly List<Vector2> outboundPath = new();
    private readonly BehaviourStateMachine stateMachine = new();
    private IBehaviourState currentState => stateMachine.Current;
    private DormantState dormantState;
    private AwakingState awakingState;
    private FiveStepState fiveStepState;
    private SlamState slamState;
    private ThrowState throwState;
    private ReturnHomeState returnHomeState;
    private DeadState deadState;
    private ContactFilter2D groundFilter;
    private IDamageable playerDamageable;
    private Sprite[] currentFrames;
    private float currentFramesPerSecond;
    private float animationTime;
    private bool animationLoops;
    private CharacterPhysics2D characterPhysics;
    private Vector3 visualBaseLocalPosition;
    private Vector3 visualBaseLocalScale;
    private float referenceVisualBottom;
    private float currentHealth;
    private float nextAttackTime;
    private Vector2 spawnPosition;
    private Vector3 deathHeadDropPosition;
    private bool isProvoked;
    private bool isDead;
    private bool hasAttacked;

    public float CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsDead => isDead;
    public bool IsProvoked => isProvoked;

    // 상태 변경: rb 갱신.
    // 다음 연결: StoneGolemController.InitializeVisualAlignment() 호출.
    private void Awake()
    {
        rb ??= GetComponent<Rigidbody2D>();
        bodyCollider ??= GetComponent<Collider2D>();
        spriteRenderer ??= GetComponentInChildren<SpriteRenderer>();
        itemDropSpawner ??= FindFirstObjectByType<ItemDropSpawner>();
        InitializeVisualAlignment();
        ApplyFrame(idleFrames, 0);
        SpriteColliderAutoFit2D.Attach(gameObject, bodyCollider, spriteRenderer);
        characterPhysics = CharacterPhysics2D.Attach(gameObject, rb, bodyCollider);
        currentHealth = maxHealth;
        MonsterHealthBar2D.Attach(gameObject, bodyCollider, spriteRenderer);
        spawnPosition = rb.position;
        groundFilter = new ContactFilter2D();
        groundFilter.SetLayerMask(groundLayers);
        groundFilter.useTriggers = false;
        dormantState = new DormantState(this);
        awakingState = new AwakingState(this);
        fiveStepState = new FiveStepState(this);
        slamState = new SlamState(this);
        throwState = new ThrowState(this);
        returnHomeState = new ReturnHomeState(this);
        deadState = new DeadState(this);
    }

    private void Start() => ChangeState(dormantState);
    private void Update() => UpdateAnimation();
    private void FixedUpdate()
    {
        if (isProvoked && !isDead && stateMachine.Current != returnHomeState)
            RecordPath();
        if (characterPhysics == null || !characterPhysics.IsKnockbackActive)
            stateMachine.Tick();
    }

    private void RecordPath()
    {
        if (outboundPath.Count == 0 || Vector2.Distance(outboundPath[outboundPath.Count - 1], rb.position) >= .1f)
            outboundPath.Add(rb.position);
    }

    private bool ReturnAlongPath()
    {
        while (outboundPath.Count > 1 &&
               Vector2.Distance(rb.position, outboundPath[outboundPath.Count - 1]) <= .08f)
            outboundPath.RemoveAt(outboundPath.Count - 1);
        Vector2 destination = outboundPath.Count > 0 ? outboundPath[outboundPath.Count - 1] : spawnPosition;
        if (outboundPath.Count <= 1 && Vector2.Distance(rb.position, spawnPosition) <= .2f)
            return true;
        spriteRenderer.flipX = destination.x > rb.position.x;
        rb.MovePosition(Vector2.MoveTowards(rb.position, destination, moveSpeed * Time.fixedDeltaTime));
        return false;
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
            interactionContext.ApplyMonsterKnockback(characterPhysics);
    }

    // IDamageable 직접 호출은 무기 종류를 증명할 수 없으므로 무시한다.
    // 돌골렘 피해는 반드시 Pickaxe가 검증되는 Interact 경로로 들어온다.
    public void TakeDamage(float amount) { }

    private void ApplyPickaxeHit()
    {
        if (isDead) return;
        float before = currentHealth;
        currentHealth = Mathf.Max(0, currentHealth - pickaxeDamage);
        CampaignDamageNumber.Show(gameObject, before - currentHealth);
        if (currentHealth == 0) { ChangeState(deadState); return; }
        if (!isProvoked)
        {
            isProvoked = true;
            ChangeState(awakingState);
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

    private float PlayerDistance => FindLivingPlayer()
        ? Vector2.Distance(rb.position, playerTarget.position) : float.PositiveInfinity;

    // 핵심 분기: Mathf.Abs(direction) < 0.01f 판정.
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
            // The field stairs' effector intentionally allows side passage.
            // A shape cast alone does not account for that effector policy.
            if (hit.collider != null && hit.collider.GetComponent<FieldOneWayPlatform>() != null)
                continue;
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
        rb.linearVelocity = new Vector2(direction * chaseSpeed, rb.linearVelocity.y);
        spriteRenderer.flipX = direction > 0f;
    }

    private void StopMoving() => rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

    private void PlayAnimation(AnimationState state, float walkDuration = 0f)
    {
        Sprite[] frames;
        float fps;
        bool loops;
        switch (state)
        {
            case AnimationState.Awake: frames = awakeFrames; fps = awakeFramesPerSecond; loops = false; break;
            case AnimationState.Walk: frames = walkFrames; fps = walkFrames == null ? 1f : walkFrames.Length / (walkDuration > 0f ? walkDuration : fiveStepDuration); loops = false; break;
            case AnimationState.Slam: frames = slamFrames; fps = slamFramesPerSecond; loops = false; break;
            case AnimationState.Throw: frames = throwFrames; fps = throwFramesPerSecond; loops = false; break;
            case AnimationState.Death: frames = deathFrames; fps = deathFramesPerSecond; loops = false; break;
            default: frames = idleFrames; fps = idleFramesPerSecond; loops = true; break;
        }
        currentFrames = frames;
        currentFramesPerSecond = Mathf.Max(.1f, fps);
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

    private void ApplyFrame(Sprite[] frames, int index)
    {
        if (frames == null || frames.Length == 0) return;
        Sprite frame = frames[Mathf.Clamp(index, 0, frames.Length - 1)];
        if (frame == null) return;

        spriteRenderer.sprite = frame;
        AlignVisualFeet(frame);
    }

    // The hand side of each attack sprite follows the player's current side.
    private void FacePlayer()
    {
        if (FindLivingPlayer())
            spriteRenderer.flipX = playerTarget.position.x < rb.position.x;
    }

    private void EmitAwakeningDebris()
    {
        if (rockProjectilePrefab == null || bodyCollider == null) return;
        SpriteRenderer rockVisual = rockProjectilePrefab.GetComponentInChildren<SpriteRenderer>();
        if (rockVisual != null)
            StoneGolemAwakeningDebris.Emit(bodyCollider.bounds, rockVisual.sprite, spriteRenderer);
    }

    private void InitializeVisualAlignment()
    {
        if (spriteRenderer == null) return;

        Transform visual = spriteRenderer.transform;
        visualBaseLocalPosition = visual.localPosition;
        visualBaseLocalScale = visual.localScale;

        Sprite reference = GetFirstValidSprite(walkFrames) ?? spriteRenderer.sprite;
        if (reference == null) return;

        referenceVisualBottom = visualBaseLocalPosition.y
            + reference.bounds.min.y * visualBaseLocalScale.y;
    }

    private void AlignVisualFeet(Sprite frame)
    {
        if (spriteRenderer == null
            || spriteRenderer.transform == transform)
        {
            return;
        }

        Transform visual = spriteRenderer.transform;
        visual.localScale = visualBaseLocalScale;

        Vector3 position = visualBaseLocalPosition;
        position.y = referenceVisualBottom - frame.bounds.min.y * visualBaseLocalScale.y;
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

    private float Duration(Sprite[] frames, float fps) =>
        frames == null || frames.Length == 0 ? .5f : frames.Length / Mathf.Max(.1f, fps);

    private bool PlayerInside(Vector2 center, float radius)
    {
        if (!FindLivingPlayer()) return false;
        Collider2D collider = playerTarget.GetComponent<Collider2D>();
        return collider != null && Vector2.Distance(center, collider.ClosestPoint(center)) <= radius;
    }

    private void SlamImpact()
    {
        Vector2 center = new(bodyCollider.bounds.center.x, bodyCollider.bounds.min.y + .2f);
        StoneGolemImpactPulse.Spawn(center, slamImpactRadius);
        if (!PlayerInside(center, slamImpactRadius)) return;
        CombatDamage.Apply(playerDamageable, slamDamage, gameObject);
        if (!playerDamageable.IsDead)
            playerTarget.GetComponent<CharacterPhysics2D>()?.ApplyKnockbackFrom(rb.position, slamKnockbackMultiplier);
    }

    private void ThrowRock()
    {
        if (rockProjectilePrefab == null || !FindLivingPlayer()) return;
        Vector2 origin = bodyCollider.bounds.center + new Vector3(spriteRenderer.flipX ? -1f : 1f, .8f);
        Collider2D collider = playerTarget.GetComponent<Collider2D>();
        Vector2 target = collider != null
            ? new Vector2(playerTarget.position.x, collider.bounds.min.y)
            : (Vector2)playerTarget.position;
        RaycastHit2D ground = Physics2D.Raycast(target + Vector2.up * .5f, Vector2.down, 12f, groundLayers);
        if (ground.collider != null) target.y = ground.point.y + .05f;
        StoneGolemRockProjectile rock = Instantiate(rockProjectilePrefab, origin, Quaternion.identity);
        rock.Launch(target, rockFlightSeconds, playerTarget, playerDamageable,
            gameObject, rb.position, rockDamage, rockImpactRadius, rockKnockbackMultiplier);
    }

    private float DeathDuration => Duration(deathFrames, deathFramesPerSecond);

    // Use the upright walk frame as the head reference. Attack poses can raise their arms above the head.
    private void CaptureHeadDropPosition()
    {
        Sprite reference = GetFirstValidSprite(walkFrames) ?? spriteRenderer?.sprite;
        if (spriteRenderer == null || reference == null)
        {
            deathHeadDropPosition = bodyCollider != null ? bodyCollider.bounds.center : transform.position;
            return;
        }

        float headX = spriteRenderer.flipX ? 0.4f : 0.6f;
        Vector3 headLocalPosition = new Vector3(
            visualBaseLocalPosition.x + Mathf.Lerp(reference.bounds.min.x, reference.bounds.max.x, headX) * visualBaseLocalScale.x,
            referenceVisualBottom + reference.bounds.size.y * visualBaseLocalScale.y * 0.8f,
            visualBaseLocalPosition.z);
        deathHeadDropPosition = transform.TransformPoint(headLocalPosition);
    }

    private void SpawnCore()
    {
        if (dropData == null || itemDropSpawner == null || !dropData.TryGetDrop(0, out GameObject prefab, out _)) return;
        itemDropSpawner.Spawn(prefab, deathHeadDropPosition + (Vector3)dropOffset, 1);
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        pickaxeDamage = 13;
        throwRange = Mathf.Max(meleeRange, throwRange);
        returnDistance = Mathf.Max(throwRange, returnDistance);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, meleeRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, throwRange);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, returnDistance);
    }
}
