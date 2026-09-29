using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(SpriteRenderer))]
/// <summary>Owns deer monster health, movement and state transitions.</summary>
public sealed partial class PeMonsterController : MonoBehaviour, IHealthSource, IInteractable
{
    private enum AnimationState { Idle, Walk, Run, Death }

    [Header("References")]
    [SerializeField] private Transform playerTarget;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Collider2D bodyCollider;
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Stats")]
    [SerializeField, Min(1)] private int maxHealth = 50;
    [SerializeField, Min(0f)] private float walkSpeed = 4.5f;
    [SerializeField, Min(0f)] private float fleeSpeed = 7.2f;
    [SerializeField, Min(0.1f)] private float fleeDuration = 2f;

    [Header("Behaviour")]
    [SerializeField, Min(0.1f)] private float minimumIdleTime = 1f;
    [SerializeField, Min(0.1f)] private float maximumIdleTime = 3f;
    [SerializeField, Min(0.1f)] private float minimumPatrolTime = 2f;
    [SerializeField, Min(0.1f)] private float maximumPatrolTime = 5f;
    [SerializeField, Range(0f, 1f)] private float idleChance = 0.4f;

    [Header("Navigation")]
    [SerializeField] private LayerMask groundLayers = 64;
    [SerializeField] private LayerMask entranceLayers = 128;
    [SerializeField, Min(0.01f)] private float ledgeProbeDistance = 0.8f;
    [SerializeField, Min(0f)] private float ledgeProbeForward = 0.2f;
    [SerializeField, Min(0f)] private float wallProbeDistance = 0.12f;
    [SerializeField, Min(0f)] private float entranceProbeRadius = 0.25f;

    [Header("Animation Frames")]
    [SerializeField] private Sprite[] idleFrames;
    [SerializeField] private Sprite[] walkFrames;
    [SerializeField] private Sprite[] runFrames;
    [SerializeField] private Sprite[] deathFrames;
    [SerializeField, Min(0.1f)] private float walkFramesPerSecond = 7f;
    [SerializeField, Min(0.1f)] private float runFramesPerSecond = 10f;
    [SerializeField, Min(0.1f)] private float deathFramesPerSecond = 7f;

    [Header("Death Drop")]
    [SerializeField] private ResourceData dropData;
    [SerializeField] private ItemDropSpawner itemDropSpawner;
    [SerializeField, Min(0f)] private float dropRadius = 0.75f;
    [SerializeField] private Vector2 dropOffset = new(0f, 0.35f);

    private readonly RaycastHit2D[] wallHits = new RaycastHit2D[4];
    private readonly BehaviourStateMachine stateMachine = new();
    private IBehaviourState currentState => stateMachine.Current;
    private IdleState idleState;
    private PatrolState patrolState;
    private FleeState fleeState;
    private DeadState deadState;
    private ContactFilter2D groundFilter;
    private Sprite[] currentFrames;
    private float currentFramesPerSecond;
    private float animationTime;
    private bool animationLoops;
    private CharacterPhysics2D characterPhysics;
    private float currentHealth;
    private bool isDead;
    private float visualGroundY;
    private float nextPlayerCollisionRefreshTime;

    public float CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsDead => isDead;

    private void Awake()
    {
        rb ??= GetComponent<Rigidbody2D>();
        bodyCollider ??= GetComponent<Collider2D>();
        spriteRenderer ??= GetComponent<SpriteRenderer>();
        EnsureDedicatedVisualRenderer();
        InitializeVisualGrounding();
        SpriteColliderAutoFit2D.Attach(gameObject, bodyCollider, spriteRenderer);
        characterPhysics = CharacterPhysics2D.Attach(gameObject, rb, bodyCollider);
        itemDropSpawner ??= FindFirstObjectByType<ItemDropSpawner>();
        currentHealth = maxHealth;
        MonsterHealthBar2D.Attach(gameObject, bodyCollider, spriteRenderer);
        groundFilter = new ContactFilter2D();
        groundFilter.SetLayerMask(groundLayers);
        groundFilter.useTriggers = false;
        idleState = new IdleState(this);
        patrolState = new PatrolState(this);
        fleeState = new FleeState(this);
        deadState = new DeadState(this);
    }

    private void Start()
    {
        RefreshPlayerCollisionIgnores();
        SelectRoamingState();
    }

    private void Update() => UpdateAnimation();
    private void FixedUpdate()
    {
        if (!isDead && Time.unscaledTime >= nextPlayerCollisionRefreshTime)
            RefreshPlayerCollisionIgnores();

        if (characterPhysics == null || !characterPhysics.IsKnockbackActive)
            stateMachine.Tick();
    }
    public bool CanInteract() => !isDead;
    public bool CanUseTool(ToolData toolData) => toolData != null && toolData.ToolType == ToolType.Sword;

    public void Interact(PlayerInteraction interactionContext)
    {
        if (interactionContext == null || !CanUseTool(interactionContext.CurrentTool)) return;
        playerTarget = interactionContext.transform;
        TakeDamage(interactionContext.CurrentTool.Damage);
        if (!isDead)
            characterPhysics?.ApplyKnockbackFrom(interactionContext.transform.position);
    }

    public void TakeDamage(float amount)
    {
        if (amount <= 0 || isDead) return;
        float before = currentHealth;
        currentHealth = Mathf.Max(0, CombatDamage.RoundHealth(currentHealth - amount));
        CampaignDamageNumber.Show(gameObject, before - currentHealth);
        if (currentHealth == 0) { ChangeState(deadState); return; }
        FindPlayerTarget();
        ChangeState(fleeState);
    }

    private void ChangeState(IBehaviourState nextState)
    {
        stateMachine.Change(nextState);
    }

    private void SelectRoamingState() => ChangeState(Random.value < idleChance ? idleState : patrolState);

    private bool FindPlayerTarget()
    {
        if (playerTarget != null) return true;
        PlayerAssimilate player = FindFirstObjectByType<PlayerAssimilate>();
        if (player == null) return false;
        playerTarget = player.transform;
        return true;
    }

    private void RefreshPlayerCollisionIgnores()
    {
        nextPlayerCollisionRefreshTime = Time.unscaledTime + 1f;
        if (bodyCollider == null)
            return;

        PlayerAssimilate[] players = FindObjectsByType<PlayerAssimilate>(FindObjectsSortMode.None);
        foreach (PlayerAssimilate player in players)
        {
            Collider2D[] playerColliders = player.GetComponentsInChildren<Collider2D>(true);
            foreach (Collider2D playerCollider in playerColliders)
            {
                if (playerCollider == null || playerCollider.isTrigger)
                    continue;

                if (!Physics2D.GetIgnoreCollision(bodyCollider, playerCollider))
                    Physics2D.IgnoreCollision(bodyCollider, playerCollider, true);
            }
        }
    }

    private float GetFleeDirection()
    {
        if (FindPlayerTarget())
        {
            float difference = transform.position.x - playerTarget.position.x;
            if (Mathf.Abs(difference) > 0.01f) return Mathf.Sign(difference);
        }
        return Random.value < 0.5f ? -1f : 1f;
    }

    private bool CanMove(float direction)
    {
        if (Mathf.Abs(direction) < 0.01f) return false;
        Bounds bounds = bodyCollider.bounds;
        float sign = Mathf.Sign(direction);
        Vector2 frontCenter = new(bounds.center.x + sign * (bounds.extents.x + wallProbeDistance), bounds.center.y);
        if (bodyCollider.Cast(Vector2.right * sign, groundFilter, wallHits, wallProbeDistance) > 0) return false;
        Collider2D entrance = Physics2D.OverlapCircle(frontCenter, entranceProbeRadius, entranceLayers);
        if (entrance != null && entrance.GetComponentInParent<CaveEntranceInteractable>() != null) return false;
        Vector2 ledgeOrigin = new(bounds.center.x + sign * (bounds.extents.x + ledgeProbeForward), bounds.min.y + 0.15f);
        RaycastHit2D ground = Physics2D.Raycast(ledgeOrigin, Vector2.down, ledgeProbeDistance, groundLayers);
        return ground.collider != null && ground.normal.y >= 0.5f;
    }

    private void Move(float direction, float speed)
    {
        rb.linearVelocity = new Vector2(direction * speed, rb.linearVelocity.y);
        if (Mathf.Abs(direction) > 0.01f) spriteRenderer.flipX = direction < 0f;
    }

    private void StopMoving() => rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

    private void PlayAnimation(AnimationState state)
    {
        Sprite[] frames; float fps; bool loops;
        switch (state)
        {
            case AnimationState.Walk: frames = walkFrames; fps = walkFramesPerSecond; loops = true; break;
            case AnimationState.Run: frames = runFrames; fps = runFramesPerSecond; loops = true; break;
            case AnimationState.Death: frames = deathFrames; fps = deathFramesPerSecond; loops = false; break;
            default: frames = idleFrames; fps = 1f; loops = true; break;
        }
        if (currentFrames == frames) return;
        currentFrames = frames;
        currentFramesPerSecond = fps;
        animationLoops = loops;
        animationTime = 0f;
        ApplyAnimationFrame(0);
    }

    private void UpdateAnimation()
    {
        if (currentFrames == null || currentFrames.Length == 0) return;
        int index = SpriteFrameClock.Advance(ref animationTime, Time.deltaTime,
            currentFramesPerSecond, currentFrames.Length, animationLoops);
        ApplyAnimationFrame(index);
    }

    private void ApplyAnimationFrame(int index)
    {
        if (currentFrames == null || currentFrames.Length == 0) return;
        Sprite frame = currentFrames[Mathf.Clamp(index, 0, currentFrames.Length - 1)];
        if (frame == null) return;
        spriteRenderer.sprite = frame;
        KeepVisualGrounded(frame);
    }

    private void EnsureDedicatedVisualRenderer()
    {
        if (spriteRenderer == null || spriteRenderer.transform != transform)
            return;

        SpriteRenderer source = spriteRenderer;
        bool wasEnabled = source.enabled;
        GameObject visualObject = new("Visual");
        visualObject.layer = gameObject.layer;
        visualObject.transform.SetParent(transform, false);

        SpriteRenderer visual = visualObject.AddComponent<SpriteRenderer>();
        visual.sprite = source.sprite;
        visual.color = source.color;
        visual.flipX = source.flipX;
        visual.flipY = source.flipY;
        visual.sharedMaterial = source.sharedMaterial;
        visual.sortingLayerID = source.sortingLayerID;
        visual.sortingOrder = source.sortingOrder;
        visual.maskInteraction = source.maskInteraction;
        visual.spriteSortPoint = source.spriteSortPoint;
        visual.drawMode = source.drawMode;
        visual.size = source.size;
        visual.enabled = wasEnabled;

        source.enabled = false;
        spriteRenderer = visual;
    }

    private void InitializeVisualGrounding()
    {
        if (spriteRenderer == null || spriteRenderer.sprite == null)
            return;

        Transform visual = spriteRenderer.transform;
        visualGroundY = visual.localPosition.y
            + spriteRenderer.sprite.bounds.min.y * visual.localScale.y;
    }

    private void KeepVisualGrounded(Sprite frame)
    {
        if (spriteRenderer == null || spriteRenderer.transform == transform)
            return;

        Transform visual = spriteRenderer.transform;
        Vector3 localPosition = visual.localPosition;
        localPosition.y = visualGroundY - frame.bounds.min.y * visual.localScale.y;
        visual.localPosition = localPosition;
    }

    private float DeathAnimationDuration => deathFrames == null || deathFrames.Length == 0 ? 0.5f : deathFrames.Length / deathFramesPerSecond;

    private void SpawnDeathDrops()
    {
        if (dropData == null || itemDropSpawner == null) return;
        for (int entry = 0; entry < dropData.DropCount; entry++)
        {
            if (!dropData.TryGetDrop(entry, out GameObject prefab, out int amount)) continue;
            for (int item = 0; item < amount; item++)
            {
                float ratio = amount == 1 ? 0.5f : item / (float)(amount - 1);
                float angle = Mathf.Lerp(30f, 150f, ratio) * Mathf.Deg2Rad;
                Vector2 separation = new(Mathf.Cos(angle), Mathf.Sin(angle));
                itemDropSpawner.Spawn(prefab, transform.position + (Vector3)(dropOffset + separation * dropRadius), 1);
            }
        }
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        maximumIdleTime = Mathf.Max(minimumIdleTime, maximumIdleTime);
        maximumPatrolTime = Mathf.Max(minimumPatrolTime, maximumPatrolTime);
    }
}
