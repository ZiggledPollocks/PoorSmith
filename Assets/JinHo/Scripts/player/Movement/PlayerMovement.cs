// [코드 지도] PlayerMovement: 입력·접지·환경 정보를 MovementContext로 모아 IMovementMode.Calculate에 전달하고 반환된 MovementCommand를 Rigidbody2D에 적용한다. 구르기, 점프 버퍼, 낙하 피해, 외부 바람과 환경 등록의 조정자다.
// 주요 함수: UpdateFieldStairAscent, Awake, FixedUpdate
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/player/Movement/PlayerMovement.cs.md

using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerInputHandler inputHandler;
    [SerializeField] private PlayerAnimationController animationController;
    [SerializeField] private RuntimeAnimatorController warriorAnimatorController;

    [Header("Horizontal Movement (World Units / Second)")]
    [Tooltip("Maximum horizontal speed while walking.")]
    [SerializeField, Min(0f)] private float walkSpeed = 5.5f;
    [Tooltip("Maximum horizontal speed while running.")]
    [SerializeField, Min(0f)] private float runSpeed = 8.25f;
    [Tooltip("How quickly the player reaches the target speed while grounded.")]
    [SerializeField, Min(0.01f)] private float groundAcceleration = 45f;
    [Tooltip("How quickly the player stops after releasing movement while grounded.")]
    [SerializeField, Min(0.01f)] private float groundDeceleration = 60f;
    [Tooltip("Horizontal control strength while airborne.")]
    [SerializeField, Min(0.01f)] private float airAcceleration = 25f;
    [Tooltip("How quickly horizontal air movement slows without input.")]
    [SerializeField, Min(0.01f)] private float airDeceleration = 10f;
    [Tooltip("Extra acceleration applied when reversing direction.")]
    [SerializeField, Min(1f)] private float turnAccelerationMultiplier = 2f;

    [Header("Roll")]
    [SerializeField, Min(0.01f)] private float rollSpeed = 12.5f;
    [SerializeField, Min(0.01f)] private float rollDuration = 0.3f;
    [SerializeField, Min(0f)] private float rollCooldown = 0.4f;
    [SerializeField] private bool requireGroundedForRoll = true;

    [Header("Jump And Gravity")]
    [Tooltip("Initial upward velocity of a jump.")]
    [FormerlySerializedAs("jumpForce")]
    [SerializeField, Min(0f)] private float jumpSpeed = 11.5f;
    [Tooltip("Normal Rigidbody2D gravity scale used outside special movement zones.")]
    [SerializeField, Min(0.01f)] private float baseGravityScale = 2.2f;
    [SerializeField, Min(0.01f)] private float coyoteTime = 0.12f;
    [SerializeField, Min(0.01f)] private float jumpBufferTime = 0.12f;
    [SerializeField, Min(1f)] private float fallGravityMultiplier = 1.65f;
    [SerializeField, Min(1f)] private float jumpCutGravityMultiplier = 2.2f;
    [SerializeField, Min(0.1f)] private float maxFallSpeed = 20f;

    [Header("Fall Damage")]
    [SerializeField] private bool enableFallDamage = true;
    [Tooltip("기본 안전 낙하 높이(월드 단위). 실제 안전 높이는 이 값에 2를 더합니다.")]
    [SerializeField, Min(0f)] private float safeFallHeight = 6f;
    private const float AdditionalSafeFallHeight = 2f;
    [Tooltip("안전 높이를 초과한 1 월드 단위당 피해. 소수점은 버립니다.")]
    [SerializeField, Min(0f)] private float damagePerFallUnit = 2f;

    [Header("Ground Detection")]
    [SerializeField] private Collider2D bodyCollider;
    [SerializeField] private PhysicsMaterial2D frictionlessMaterial;
    [SerializeField, Min(0.01f)] private float groundCheckDistance = 0.1f;
    [SerializeField, Range(0f, 1f)] private float minGroundNormalY = 0.65f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField, Min(0f)] private float groundProbeSkin = 0.02f;
    [SerializeField, Range(0f, 0.49f)] private float groundProbeHorizontalInset = 0.1f;

    [Header("Field Stair Ascent")]
    [SerializeField, Min(0f)] private float maximumStairRise = 1.05f;
    [SerializeField, Min(0f)] private float stairLookAheadTime = 0.16f;

    [Header("Flame Movement")]
    [Tooltip("Flame 오브젝트에 사용하는 Trigger 레이어")]
    [SerializeField] private LayerMask flameLayer;
    [SerializeField, Range(0.1f, 1f)] private float flameHorizontalSpeedMultiplier = 0.65f;
    [SerializeField, Min(0.01f)] private float flameHorizontalAcceleration = 20f;
    [SerializeField, Min(0.01f)] private float flameRiseSpeed = 4f;
    [SerializeField, Min(0.01f)] private float flameRiseAcceleration = 12f;
    [SerializeField, Min(0.01f)] private float flameSinkSpeed = 1.5f;
    [SerializeField, Min(0.01f)] private float flameSinkAcceleration = 6f;
    [SerializeField, Min(0.01f)] private float flameResistance = 25f;

    [Header("Up Draft Movement")]
    [SerializeField, Range(0.1f, 1f)] private float upDraftHorizontalSpeedMultiplier = 0.8f;
    [SerializeField, Min(0.01f)] private float upDraftHorizontalAcceleration = 24f;

    private readonly RaycastHit2D[] groundHits = new RaycastHit2D[8];
    private readonly RaycastHit2D[] stairHits = new RaycastHit2D[16];
    private readonly ContactPoint2D[] groundContacts = new ContactPoint2D[8];

    private Rigidbody2D rb;
    private PlayerAssimilate playerHealth;
    private float fallPeakY;
    private bool trackingFallHeight;
    private CharacterPhysics2D characterPhysics;
    private SpriteRenderer facingRenderer;
    private ContactFilter2D groundContactFilter;
    private float defaultGravityScale;
    private float coyoteTimeCounter;
    private float jumpBufferCounter;
    private bool jumpAscending;
    private readonly MovementModeRegistry movementModes = new();
    private readonly NormalMovementSettings normalSettings = new();
    private readonly FlameMovementSettings flameSettings = new();
    private readonly UpDraftMovementSettings upDraftSettings = new();
    private NormalMovementMode normalMode;
    private FlameContactSensor flameContacts;
    private MovementEnvironmentPolicy environmentPolicy;
    private Vector2 rollDirection;
    private float rollEndTime;
    private float nextRollTime;
    private bool isRolling;
    private Object externalWindSource;
    private float externalWindHorizontalSpeed;
    private FieldOneWayPlatform stairTarget;
    private float stairTargetY;
    private float stairStartX;
    private float stairDirection;
    private FieldPlatformDropController platformDropController;

    public bool IsInFlame => movementModes.HasMode<FlameMovementMode>();
    public bool IsInUpDraft => movementModes.HasMode<UpDraftMovementMode>();
    public bool IsRolling => isRolling;
    public Vector2 RollDirection => rollDirection;
    public bool IsGroundedForAnimation => rb != null && IsGrounded();
    public bool IsMovedOnlyByExternalWind =>
        externalWindSource != null &&
        (inputHandler == null || Mathf.Abs(inputHandler.MoveInput.x) <= 0.01f);

    /// <summary>
    /// Adds a sustained horizontal world velocity to normal player movement.
    /// Player input remains available, so moving against the wind resists it.
    /// </summary>
    public void SetExternalHorizontalWind(Object source, float horizontalSpeed)
    {
        if (source == null)
            return;

        externalWindSource = source;
        externalWindHorizontalSpeed = horizontalSpeed;
    }

    public void ClearExternalHorizontalWind(Object source)
    {
        if (source == null || externalWindSource != source)
            return;

        externalWindSource = null;
        externalWindHorizontalSpeed = 0f;
    }

    // 핵심 분기: flameLayer.value == 0 판정.
    // 상태 변경: rb 갱신.
    // 다음 연결: PlayerAnimationController.Configure(UnityEngine.RuntimeAnimatorController, PlayerMovement, PlayerInputHandler) 호출.
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        platformDropController = GetComponent<FieldPlatformDropController>();
        playerHealth = GetComponent<PlayerAssimilate>();
        defaultGravityScale = baseGravityScale;
        rb.gravityScale = defaultGravityScale;

        if (flameLayer.value == 0)
        {
            flameLayer = LayerMask.GetMask("Flame");
        }

        if (bodyCollider == null)
        {
            bodyCollider = GetComponent<Collider2D>();
        }

        if (inputHandler == null)
        {
            inputHandler =
                GetComponent<PlayerInputHandler>();
        }

        if (animationController == null)
        {
            animationController = GetComponent<PlayerAnimationController>();
        }

        if (animationController == null)
        {
            Debug.LogError("PlayerAnimationController must be authored on the player before Play.", this);
            enabled = false;
            return;
        }

        facingRenderer = animationController.GetComponent<SpriteRenderer>();

        animationController.Configure(warriorAnimatorController, this, inputHandler);
        SpriteColliderAutoFit2D.Attach(
            gameObject,
            bodyCollider,
            GetComponentInChildren<SpriteRenderer>());
        characterPhysics = CharacterPhysics2D.Attach(gameObject, rb, bodyCollider);

        if (frictionlessMaterial != null)
        {
            bodyCollider.sharedMaterial = frictionlessMaterial;
        }

        if (groundLayer.value == 0)
        {
            groundLayer = LayerMask.GetMask("Ground");
        }

        groundContactFilter = new ContactFilter2D();
        groundContactFilter.SetLayerMask(groundLayer);
        groundContactFilter.useTriggers = false;
        RefreshMovementSettings();
        normalMode = new NormalMovementMode(normalSettings);
        flameContacts = new FlameContactSensor(this, new FlameMovementMode(flameSettings)) { Layers = flameLayer };
    }

    private void Update()
    {
        RefreshMovementSettings();
        RefreshEnvironmentPolicy();
        UpdateJumpTimers();
        UpdateRollInput();
    }

    private void Start()
    {
        RefreshGroundTilemapColliders();
    }

    // 핵심 분기: !tilemapCollider.enabled || !tilemapCollider.gameObject.activeInHierarchy || !IsInLayerMask(tilemapCollider.g… 판정.
    // 상태 변경: tilemapCollider.enabled 갱신.
    // 다음 연결: PlayerMovement.IsInLayerMask(int, UnityEngine.LayerMask) 호출.
    private void RefreshGroundTilemapColliders()
    {
        TilemapCollider2D[] tilemapColliders =
            FindObjectsByType<TilemapCollider2D>(FindObjectsSortMode.None);

        foreach (TilemapCollider2D tilemapCollider in tilemapColliders)
        {
            if (!tilemapCollider.enabled
                || !tilemapCollider.gameObject.activeInHierarchy
                || !IsInLayerMask(tilemapCollider.gameObject.layer, groundLayer))
            {
                continue;
            }

            tilemapCollider.enabled = false;
            tilemapCollider.enabled = true;
            tilemapCollider.ProcessTilemapChanges();
        }

        Physics2D.SyncTransforms();
    }

    // 핵심 분기: characterPhysics != null && characterPhysics.IsKnockbackActive 판정.
    // 상태 변경: isRolling 갱신.
    // 다음 연결: PlayerMovement.RefreshMovementSettings() 호출.
    private void FixedUpdate()
    {
        RefreshMovementSettings();
        flameContacts?.Prune();
        RefreshEnvironmentPolicy();
        TrackFallHeight();

        if (characterPhysics != null && characterPhysics.IsKnockbackActive)
        {
            ClearStairAscent();
            isRolling = false;
            return;
        }

        if (isRolling)
        {
            ClearStairAscent();
            if (Time.time < rollEndTime)
            {
                HandleRollMovement();
                return;
            }

            FinishRoll();
        }

        IMovementMode mode = movementModes.Resolve(normalMode);
        MovementContext context = new MovementContext
        {
            Velocity = rb.linearVelocity,
            HorizontalInput = inputHandler.MoveInput.x,
            IsRunning = inputHandler.IsRunning,
            JumpHeld = inputHandler.JumpHeld,
            CanJump = !environmentPolicy.BlocksNormalJump && jumpBufferCounter > 0f && coyoteTimeCounter > 0f,
            IsGrounded = IsGrounded(),
            BodyCenterY = bodyCollider != null ? bodyCollider.bounds.center.y : rb.position.y,
            BodyHalfHeight = bodyCollider != null ? bodyCollider.bounds.extents.y : 0f,
            Time = Time.time,
            DeltaTime = Time.fixedDeltaTime,
            ExternalHorizontalSpeed = GetExternalHorizontalWindSpeed()
        };
        MovementCommand command = mode.Calculate(context);
        ApplyMovementCommand(command);
        UpdateFieldStairAscent(mode, context, command);
        if (mode == normalMode && !command.ConsumedJump &&
            Mathf.Abs(context.HorizontalInput) <= 0.01f &&
            Mathf.Abs(context.ExternalHorizontalSpeed) <= 0.01f &&
            TryGetFieldSlopeNormal(out Vector2 slopeNormal))
            HoldPositionOnFieldSlope(slopeNormal);
    }

    // 핵심 분기: inputHandler == null 판정.
    // 상태 변경: rollDirection 갱신.
    // 다음 연결: PlayerInputHandler.ConsumeRollInput(out UnityEngine.Vector2, out bool) 호출.
    private void UpdateRollInput()
    {
        if (inputHandler == null)
            return;

        if (GameUIController.BlocksGameplayInput)
        {
            inputHandler.ConsumeRollInput(out _, out _);
            if (isRolling)
                FinishRoll();
            return;
        }

        if (!inputHandler.ConsumeRollInput(out _, out _))
            return;

        if (isRolling || Time.time < nextRollTime)
            return;

        /* if (requireGroundedForRoll && !IsGrounded())
            return; */

        float directionX = GetRollDirectionX();
        rollDirection = new Vector2(directionX, 0f);
        isRolling = true;
        rollEndTime = Time.time + rollDuration;
        nextRollTime = rollEndTime + rollCooldown;
        coyoteTimeCounter = 0f;
        jumpBufferCounter = 0f;
        inputHandler.ConsumeJumpInput();
    }

    private float GetRollDirectionX()
    {
        float directionX = inputHandler.MoveInput.x;
        if (Mathf.Abs(directionX) > 0.01f)
            return Mathf.Sign(directionX);

        return facingRenderer != null && facingRenderer.flipX ? -1f : 1f;
    }

    private void HandleRollMovement()
    {
        rb.linearVelocity = new Vector2(
            rollDirection.x * rollSpeed,
            rb.linearVelocity.y);
    }

    private void FinishRoll()
    {
        isRolling = false;
        float exitSpeed = Mathf.Min(Mathf.Abs(rb.linearVelocity.x), runSpeed);
        rb.linearVelocity = new Vector2(
            Mathf.Sign(rb.linearVelocity.x) * exitSpeed,
            rb.linearVelocity.y);
    }

    private void ApplyMovementCommand(MovementCommand command)
    {
        if (command.GravityScale.HasValue) rb.gravityScale = command.GravityScale.Value;
        rb.linearVelocity = command.ResolveVelocity(rb.linearVelocity);
        Vector2 force = command.Force;
        if (force != Vector2.zero) rb.AddForce(force, ForceMode2D.Force);
        if (command.ConsumedJump)
        {
            jumpAscending = true;
            coyoteTimeCounter = 0f;
            jumpBufferCounter = 0f;
        }
    }

    private bool TryGetFieldSlopeNormal(out Vector2 normal)
    {
        normal = Vector2.up;
        int count = bodyCollider.GetContacts(groundContactFilter, groundContacts);
        for (int i = 0; i < count; i++)
        {
            ContactPoint2D contact = groundContacts[i];
            Collider2D surface = contact.collider == bodyCollider
                ? contact.otherCollider : contact.collider;
            if (surface == null || surface.GetComponent<FieldOneWayPlatform>() == null ||
                !IsWalkableGroundNormal(contact.normal) ||
                Mathf.Abs(contact.normal.x) < 0.05f)
                continue;
            normal = contact.normal;
            return true;
        }
        return false;
    }

    private void HoldPositionOnFieldSlope(Vector2 normal)
    {
        // The player's frictionless material otherwise lets gravity move it
        // tangentially down a marked slope even with no movement input.
        Vector2 tangent = new Vector2(normal.y, -normal.x);
        rb.linearVelocity -= tangent * Vector2.Dot(rb.linearVelocity, tangent);
        Vector2 gravity = Physics2D.gravity * rb.gravityScale;
        Vector2 slopeGravity = gravity - normal * Vector2.Dot(gravity, normal);
        rb.AddForce(-slopeGravity * rb.mass, ForceMode2D.Force);
    }

    // 핵심 분기: !canWalk 판정.
    // 상태 변경: nearestRise 갱신.
    // 다음 연결: PlayerMovement.ClearStairAscent() 호출.
    private void UpdateFieldStairAscent(IMovementMode mode, MovementContext context,
        MovementCommand command)
    {
        float xSpeed = rb.linearVelocity.x;
        bool canWalk = platformDropController != null && mode == normalMode
            && !command.ConsumedJump && !jumpAscending
            && !GameUIController.BlocksGameplayInput
            && !platformDropController.IsDropping
            && Mathf.Abs(context.HorizontalInput) > 0.01f
            && Mathf.Abs(xSpeed) > 0.1f
            && Mathf.Sign(context.HorizontalInput) == Mathf.Sign(xSpeed);
        if (!canWalk)
        {
            ClearStairAscent();
            return;
        }

        Bounds feet = bodyCollider.bounds;
        float direction = Mathf.Sign(xSpeed);
        if (stairTarget != null && (direction != stairDirection ||
            Mathf.Abs(rb.position.x - stairStartX) > 2.5f ||
            !stairTarget.isActiveAndEnabled))
            ClearStairAscent();

        if (stairTarget == null && context.IsGrounded)
        {
            float lookAhead = Mathf.Max(0.65f,
                Mathf.Abs(xSpeed) * stairLookAheadTime);
            // A single far probe skips one-cell treads at running speed.
            // Sample near to far, taking the first reachable upper surface.
            for (int probe = 1; probe <= 3 && stairTarget == null; probe++)
            {
                float probeX = feet.center.x + direction *
                    (feet.extents.x + lookAhead * probe / 3f);
                Vector2 origin = new(probeX, feet.min.y + maximumStairRise + 0.12f);
                int count = Physics2D.Raycast(origin, Vector2.down, groundContactFilter,
                    stairHits, maximumStairRise + 0.35f);
                float nearestRise = float.PositiveInfinity;
                for (int i = 0; i < count; i++)
                {
                    RaycastHit2D hit = stairHits[i];
                    if (hit.collider == null || hit.collider == bodyCollider ||
                        hit.normal.y < minGroundNormalY)
                        continue;
                    FieldOneWayPlatform candidate = hit.collider.GetComponent<FieldOneWayPlatform>();
                    if (candidate == null || candidate.GetComponentInParent<FieldStairChain>() == null)
                        continue;
                    float rise = hit.point.y - feet.min.y;
                    if (rise <= 0.12f || rise > maximumStairRise || rise >= nearestRise)
                        continue;
                    nearestRise = rise;
                    stairTarget = candidate;
                    stairTargetY = hit.point.y;
                    stairStartX = rb.position.x;
                    stairDirection = direction;
                }
            }
        }

        if (stairTarget == null)
            return;

        float remainingRise = stairTargetY - feet.min.y;
        if (remainingRise <= -0.15f)
        {
            // Arrive just above the one-way top, then let physics settle onto
            // it. Carrying the ascent velocity onward looks like a jump.
            rb.linearVelocity = new Vector2(xSpeed,
                Mathf.Min(rb.linearVelocity.y, 0f));
            ClearStairAscent();
            return;
        }

        // Start before the player's front reaches the next tread. Keep the
        // velocity only until the feet clear its upper face; no position snap.
        float ascentSpeed = Mathf.Min(jumpSpeed * 0.8f,
            Mathf.Max(6.5f, Mathf.Abs(xSpeed) * remainingRise /
                Mathf.Max(0.35f, Mathf.Abs(xSpeed) * stairLookAheadTime)));
        rb.linearVelocity = new Vector2(xSpeed,
            Mathf.Max(rb.linearVelocity.y, ascentSpeed));
    }

    private void ClearStairAscent()
    {
        stairTarget = null;
    }

    // Compatibility bridge: old Inspector fields remain the source of truth, including Play Mode edits.
    private void RefreshMovementSettings()
    {
        var field = GetComponent<FieldSceneState>();
        float multiplier = CampaignController.Instance != null && CampaignController.Instance.Ready
            ? CampaignController.Instance.MovementMultiplier
            : field != null && field.Ready ? field.MovementMultiplier : 1f;
        normalSettings.WalkSpeed = flameSettings.WalkSpeed = upDraftSettings.WalkSpeed = walkSpeed * multiplier;
        normalSettings.RunSpeed = flameSettings.RunSpeed = upDraftSettings.RunSpeed = runSpeed * multiplier;
        normalSettings.GroundAcceleration = groundAcceleration;
        normalSettings.AirAcceleration = airAcceleration;
        normalSettings.GroundDeceleration = groundDeceleration;
        normalSettings.AirDeceleration = airDeceleration;
        normalSettings.TurnAccelerationMultiplier = turnAccelerationMultiplier;
        normalSettings.JumpSpeed = jumpSpeed;
        normalSettings.GravityScale = defaultGravityScale;
        normalSettings.FallGravityMultiplier = fallGravityMultiplier;
        normalSettings.JumpCutGravityMultiplier = jumpCutGravityMultiplier;
        normalSettings.MaxFallSpeed = maxFallSpeed;
        flameSettings.HorizontalSpeedMultiplier = flameHorizontalSpeedMultiplier;
        flameSettings.HorizontalAcceleration = flameHorizontalAcceleration;
        flameSettings.RiseSpeed = flameRiseSpeed;
        flameSettings.RiseAcceleration = flameRiseAcceleration;
        flameSettings.SinkSpeed = flameSinkSpeed;
        flameSettings.SinkAcceleration = flameSinkAcceleration;
        flameSettings.Resistance = flameResistance;
        upDraftSettings.HorizontalSpeedMultiplier = upDraftHorizontalSpeedMultiplier;
        upDraftSettings.HorizontalAcceleration = upDraftHorizontalAcceleration;
        if (flameContacts != null) flameContacts.Layers = flameLayer;
    }

    public UpDraftMovementSettings UpDraftSettings => upDraftSettings;

    private float GetExternalHorizontalWindSpeed()
    {
        if (externalWindSource != null)
            return externalWindHorizontalSpeed;

        externalWindHorizontalSpeed = 0f;
        return 0f;
    }

    // 핵심 분기: GameUIController.BlocksGameplayInput 판정.
    // 상태 변경: coyoteTimeCounter 갱신.
    // 다음 연결: PlayerInputHandler.ConsumeJumpInput() 호출.
    private void UpdateJumpTimers()
    {
        if (GameUIController.BlocksGameplayInput)
        {
            coyoteTimeCounter = 0f;
            jumpBufferCounter = 0f;
            inputHandler.ConsumeJumpInput();
            return;
        }
        if (environmentPolicy.BlocksNormalJump)
        {
            coyoteTimeCounter = 0f;
            jumpBufferCounter = 0f;

            if (inputHandler.JumpPressed)
                inputHandler.ConsumeJumpInput();

            return;
        }

        if (jumpAscending && rb.linearVelocity.y <= 0f)
            jumpAscending = false;
        bool isGrounded = IsGrounded();

        // Running uphill has positive Y velocity while the feet still touch
        // the slope. Only an actual jump ascent must suppress this refresh.
        if (isGrounded && !jumpAscending)
            coyoteTimeCounter = coyoteTime;
        else
            coyoteTimeCounter = Mathf.Max(0f, coyoteTimeCounter - Time.deltaTime);

        if (inputHandler.JumpPressed)
        {
            jumpBufferCounter = jumpBufferTime;
            inputHandler.ConsumeJumpInput();
        }
        else
        {
            jumpBufferCounter = Mathf.Max(0f, jumpBufferCounter - Time.deltaTime);
        }
    }

    public void ResetAfterTeleport()
    {
        ClearStairAscent();
        trackingFallHeight = false;
        isRolling = false; rollDirection = Vector2.zero;
        jumpBufferCounter = 0; coyoteTimeCounter = 0;
        jumpAscending = false;
        externalWindSource = null; externalWindHorizontalSpeed = 0;
    }

    private void OnDisable()
    {
        ClearStairAscent();
        // Portal transitions disable movement; never carry fall damage across them.
        trackingFallHeight = false;
        isRolling = false;
        rollDirection = Vector2.zero;
        jumpAscending = false;
        flameContacts?.Clear();
        movementModes.Clear();
        environmentPolicy = default;

        if (rb != null)
            rb.gravityScale = defaultGravityScale;
    }

    public MovementRegistration RegisterEnvironmentMode(Object source, IMovementMode mode,
        int priority, MovementEnvironmentPolicy policy)
    {
        if (!isActiveAndEnabled) return default;
        MovementRegistration registration = movementModes.Register(source, mode, priority, policy, out bool added);
        if (added)
        {
            if (policy.SuppressesFallDamage) trackingFallHeight = false;
            if (policy.BlocksNormalJump)
            {
                coyoteTimeCounter = 0f;
                jumpBufferCounter = 0f;
            }
        }
        RefreshEnvironmentPolicy();
        return registration;
    }

    public bool IsEnvironmentRegistered(MovementRegistration registration) => movementModes.Contains(registration);

    public void UnregisterEnvironmentMode(MovementRegistration registration)
    {
        movementModes.Unregister(registration);
        RefreshEnvironmentPolicy();
    }

    private void RefreshEnvironmentPolicy()
    {
        MovementEnvironmentPolicy next = movementModes.Policy;
        if (rb != null && next.GravityScale != environmentPolicy.GravityScale)
            rb.gravityScale = next.GravityScale ?? defaultGravityScale;
        environmentPolicy = next;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isActiveAndEnabled) flameContacts?.Enter(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (isActiveAndEnabled) flameContacts?.Stay(other);
    }

    private void OnTriggerExit2D(Collider2D other) => flameContacts?.Exit(other);

    private void TrackFallHeight()
    {
        if (!enableFallDamage || movementModes.Policy.SuppressesFallDamage)
        {
            trackingFallHeight = false;
            return;
        }

        if (!trackingFallHeight)
        {
            fallPeakY = rb.position.y;
            trackingFallHeight = true;
        }
        else
        {
            fallPeakY = Mathf.Max(fallPeakY, rb.position.y);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryApplyFallDamage(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        // Landing can happen while already touching a wall of the same tilemap.
        TryApplyFallDamage(collision);
    }

    // 핵심 분기: !isActiveAndEnabled || !enableFallDamage || !trackingFallHeight || movementModes.Policy.SuppressesFallDamage … 판정.
    // 상태 변경: trackingFallHeight 갱신.
    // 다음 연결: PlayerMovement.IsInLayerMask(int, UnityEngine.LayerMask) 호출.
    private void TryApplyFallDamage(Collision2D collision)
    {
        if (!isActiveAndEnabled || !enableFallDamage || !trackingFallHeight
            || movementModes.Policy.SuppressesFallDamage || playerHealth == null || playerHealth.IsDead
            || !IsInLayerMask(collision.gameObject.layer, groundLayer))
            return;

        for (int i = 0; i < collision.contactCount; i++)
        {
            if (!IsWalkableGroundNormal(collision.GetContact(i).normal))
                continue;

            float fallHeight = Mathf.Max(0f, fallPeakY - rb.position.y);
            // Consume before notifying listeners, so multiple ground colliders cannot
            // apply damage twice during the same physics step. Walls/ceilings do not count.
            trackingFallHeight = false;
            float excessHeight = Mathf.Max(0f, fallHeight - (Mathf.Max(0f, safeFallHeight) + AdditionalSafeFallHeight));
            int damage = Mathf.FloorToInt(excessHeight * Mathf.Max(0f, damagePerFallUnit));
            if (damage > 0)
                playerHealth.TakeDamage(damage);
            return;
        }
    }

    private static bool IsInLayerMask(int layer, LayerMask layerMask)
    {
        return (layerMask.value & (1 << layer)) != 0;
    }

    // 핵심 분기: bodyCollider == null || !bodyCollider.enabled 판정.
    // 다음 연결: PlayerMovement.IsWalkableGroundNormal(UnityEngine.Vector2) 호출.
    private bool IsGrounded()
    {
        if (bodyCollider == null || !bodyCollider.enabled)
            return false;

        int contactCount = bodyCollider.GetContacts(
            groundContactFilter,
            groundContacts
        );

        for (int i = 0; i < contactCount; i++)
        {
            if (IsWalkableGroundNormal(groundContacts[i].normal))
                return true;
        }

        int hitCount = bodyCollider.Cast(
            Vector2.down,
            groundContactFilter,
            groundHits,
            groundCheckDistance
        );

        for (int i = 0; i < hitCount; i++)
        {
            if (IsWalkableGroundNormal(groundHits[i].normal))
                return true;
        }

        return HasGroundBelowFoot();
    }

    // 다음 연결: PlayerMovement.IsWalkableGroundHit(UnityEngine.RaycastHit2D) 호출.
    private bool HasGroundBelowFoot()
    {
        Bounds bounds = bodyCollider.bounds;
        float horizontalInset = Mathf.Min(
            bounds.extents.x * groundProbeHorizontalInset,
            Mathf.Max(0f, bounds.extents.x - 0.001f)
        );
        float leftX = bounds.min.x + horizontalInset;
        float rightX = bounds.max.x - horizontalInset;
        float originY = bounds.min.y + groundProbeSkin;
        float probeDistance = groundCheckDistance + groundProbeSkin;

        return IsWalkableGroundHit(Physics2D.Raycast(
                   new Vector2(leftX, originY),
                   Vector2.down,
                   probeDistance,
                   groundLayer))
               || IsWalkableGroundHit(Physics2D.Raycast(
                   new Vector2(bounds.center.x, originY),
                   Vector2.down,
                   probeDistance,
                   groundLayer))
               || IsWalkableGroundHit(Physics2D.Raycast(
                   new Vector2(rightX, originY),
                   Vector2.down,
                   probeDistance,
                   groundLayer));
    }

    private bool IsWalkableGroundHit(RaycastHit2D hit)
    {
        return hit.collider != null
               && hit.collider != bodyCollider
               && !hit.collider.isTrigger
               && IsWalkableGroundNormal(hit.normal);
    }

    private bool IsWalkableGroundNormal(Vector2 normal)
    {
        return normal.y >= minGroundNormalY;
    }

    // 핵심 분기: bodyCollider == null 판정.
    // 상태 변경: bodyCollider 갱신.
    // 다음 연결: PlayerMovement.DrawGroundProbeGizmo(float, float, float) 호출.
    private void OnDrawGizmosSelected()
    {
        if (bodyCollider == null)
        {
            bodyCollider = GetComponent<Collider2D>();
        }

        if (bodyCollider == null)
            return;

        Bounds bounds = bodyCollider.bounds;
        float horizontalInset = Mathf.Min(
            bounds.extents.x * groundProbeHorizontalInset,
            Mathf.Max(0f, bounds.extents.x - 0.001f)
        );
        float originY = bounds.min.y + groundProbeSkin;
        float probeDistance = groundCheckDistance + groundProbeSkin;

        Gizmos.color = Color.yellow;
        DrawGroundProbeGizmo(bounds.min.x + horizontalInset, originY, probeDistance);
        DrawGroundProbeGizmo(bounds.center.x, originY, probeDistance);
        DrawGroundProbeGizmo(bounds.max.x - horizontalInset, originY, probeDistance);
    }

    private void DrawGroundProbeGizmo(float x, float y, float distance)
    {
        Vector3 start = new(x, y, transform.position.z);
        Gizmos.DrawLine(start, start + Vector3.down * distance);
    }
}
