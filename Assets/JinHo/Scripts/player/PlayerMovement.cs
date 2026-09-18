using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerInputHandler inputHandler;

    [Header("Horizontal Movement (World Units / Second)")]
    [Tooltip("Maximum horizontal speed while walking.")]
    [SerializeField, Min(0f)] private float walkSpeed = 5f;
    [Tooltip("Maximum horizontal speed while running.")]
    [SerializeField, Min(0f)] private float runSpeed = 7.5f;
    [Tooltip("How quickly the player reaches the target speed while grounded.")]
    [SerializeField, Min(0.01f)] private float groundAcceleration = 45f;
    [Tooltip("How quickly the player stops after releasing movement while grounded.")]
    [SerializeField, Min(0.01f)] private float groundDeceleration = 60f;
    [Tooltip("Horizontal control strength while airborne.")]
    [SerializeField, Min(0.01f)] private float airAcceleration = 25f;
    [Tooltip("How quickly horizontal air movement slows without input.")]
    [SerializeField, Min(0.01f)] private float airDeceleration = 10f;
    [Tooltip("Extra acceleration applied when reversing direction.")]
    [SerializeField, Min(1f)] private float turnAccelerationMultiplier = 1.5f;

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

    [Header("Ground Detection")]
    [SerializeField] private Collider2D bodyCollider;
    [SerializeField] private PhysicsMaterial2D frictionlessMaterial;
    [SerializeField, Min(0.01f)] private float groundCheckDistance = 0.1f;
    [SerializeField, Range(0f, 1f)] private float minGroundNormalY = 0.65f;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField, Min(0f)] private float groundProbeSkin = 0.02f;
    [SerializeField, Range(0f, 0.49f)] private float groundProbeHorizontalInset = 0.1f;

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
    private readonly ContactPoint2D[] groundContacts = new ContactPoint2D[8];

    private Rigidbody2D rb;
    private Camera mainCamera;
    private ContactFilter2D groundContactFilter;
    private float defaultGravityScale;
    private float coyoteTimeCounter;
    private float jumpBufferCounter;
    private int flameContactCount;
    private readonly List<UpDraftZone> activeUpDrafts = new();
    private Vector2 rollDirection;
    private float rollEndTime;
    private float nextRollTime;
    private bool isRolling;

    public bool IsInFlame => flameContactCount > 0;
    public bool IsInUpDraft => GetActiveUpDraft() != null;
    public bool IsRolling => isRolling;
    public Vector2 RollDirection => rollDirection;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        mainCamera = Camera.main;
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
    }

    private void Update()
    {
        UpdateJumpTimers();
        UpdateRollInput();
    }

    private void Start()
    {
        RefreshGroundTilemapColliders();
    }

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

    private void FixedUpdate()
    {
        if (isRolling)
        {
            if (Time.time < rollEndTime)
            {
                HandleRollMovement();
                return;
            }

            FinishRoll();
        }

        if (IsInFlame)
        {
            HandleFlameMovement();
            return;
        }

        if (IsInUpDraft)
        {
            HandleUpDraftMovement();
            return;
        }

        HandleMovement();
        TryJump();
        ApplyJumpGravity();
    }

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

        if (!inputHandler.ConsumeRollInput(out Vector2 pointerPosition, out bool hasPointerPosition))
            return;

        if (isRolling || Time.time < nextRollTime)
            return;

        if (requireGroundedForRoll && !IsGrounded())
            return;

        float directionX = GetRollDirectionX(pointerPosition, hasPointerPosition);
        rollDirection = new Vector2(directionX, 0f);
        isRolling = true;
        rollEndTime = Time.time + rollDuration;
        nextRollTime = rollEndTime + rollCooldown;
        coyoteTimeCounter = 0f;
        jumpBufferCounter = 0f;
        inputHandler.ConsumeJumpInput();
    }

    private float GetRollDirectionX(Vector2 pointerPosition, bool hasPointerPosition)
    {
        float directionX = 0f;

        if (hasPointerPosition)
        {
            if (mainCamera == null)
                mainCamera = Camera.main;

            if (mainCamera != null)
            {
                Vector3 pointerWorldPosition = mainCamera.ScreenToWorldPoint(pointerPosition);
                directionX = pointerWorldPosition.x - rb.position.x;
            }
        }

        if (Mathf.Abs(directionX) <= 0.05f)
            directionX = inputHandler.MoveInput.x;
        if (Mathf.Abs(directionX) <= 0.05f)
            directionX = rb.linearVelocity.x;
        if (Mathf.Abs(directionX) <= 0.05f)
            directionX = 1f;

        return Mathf.Sign(directionX);
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

    private void HandleUpDraftMovement()
    {
        UpDraftZone upDraft = GetActiveUpDraft();

        if (upDraft == null)
            return;

        rb.gravityScale = 0f;

        float speed = inputHandler.IsRunning ? runSpeed : walkSpeed;
        float horizontalInput = Mathf.Clamp(inputHandler.MoveInput.x, -1f, 1f);
        float targetHorizontalSpeed =
            horizontalInput * speed * upDraftHorizontalSpeedMultiplier;

        float nextHorizontalSpeed = Mathf.MoveTowards(
            rb.linearVelocity.x,
            targetHorizontalSpeed,
            upDraftHorizontalAcceleration * Time.fixedDeltaTime);

        float playerCenterY = bodyCollider != null
            ? bodyCollider.bounds.center.y
            : rb.position.y;
        float desiredVerticalSpeed = upDraft.GetDesiredVerticalSpeed(
            playerCenterY,
            bodyCollider);

        float nextVerticalSpeed = Mathf.MoveTowards(
            rb.linearVelocity.y,
            desiredVerticalSpeed,
            upDraft.RiseAcceleration * Time.fixedDeltaTime);

        rb.linearVelocity = new Vector2(
            nextHorizontalSpeed,
            nextVerticalSpeed);
    }

    private void HandleFlameMovement()
    {
        rb.gravityScale = 0f;

        float speed = inputHandler.IsRunning ? runSpeed : walkSpeed;
        float horizontalInput = Mathf.Clamp(inputHandler.MoveInput.x, -1f, 1f);
        float targetHorizontalSpeed =
            horizontalInput * speed * flameHorizontalSpeedMultiplier;

        float nextHorizontalSpeed = Mathf.MoveTowards(
            rb.linearVelocity.x,
            targetHorizontalSpeed,
            flameHorizontalAcceleration * Time.fixedDeltaTime);

        float nextVerticalSpeed = rb.linearVelocity.y;

        if (inputHandler.JumpHeld)
        {
            nextVerticalSpeed = Mathf.MoveTowards(
                nextVerticalSpeed,
                flameRiseSpeed,
                flameRiseAcceleration * Time.fixedDeltaTime);
        }
        else
        {
            // 점프 키를 놓으면 남아 있는 상승 속도를 제거한다.
            // 따라서 수면에서 입력 없이 위아래로 떠오르지 않는다.
            nextVerticalSpeed = Mathf.Min(nextVerticalSpeed, 0f);

            float sinkAcceleration =
                nextVerticalSpeed < -flameSinkSpeed
                    ? flameResistance
                    : flameSinkAcceleration;

            nextVerticalSpeed = Mathf.MoveTowards(
                nextVerticalSpeed,
                -flameSinkSpeed,
                sinkAcceleration * Time.fixedDeltaTime);
        }

        rb.linearVelocity = new Vector2(
            nextHorizontalSpeed,
            nextVerticalSpeed
        );
    }

    private void HandleMovement()
    {
        float speed =
            inputHandler.IsRunning
                ? runSpeed
                : walkSpeed;

        float horizontalInput = Mathf.Clamp(inputHandler.MoveInput.x, -1f, 1f);
        float targetHorizontalSpeed = horizontalInput * speed;
        float currentHorizontalSpeed = rb.linearVelocity.x;
        bool hasHorizontalInput = Mathf.Abs(horizontalInput) > 0.01f;
        bool isGrounded = IsGrounded();

        float acceleration;

        if (hasHorizontalInput)
            acceleration = isGrounded ? groundAcceleration : airAcceleration;
        else
            acceleration = isGrounded ? groundDeceleration : airDeceleration;

        bool isChangingDirection =
            hasHorizontalInput &&
            Mathf.Abs(currentHorizontalSpeed) > 0.01f &&
            Mathf.Sign(targetHorizontalSpeed) != Mathf.Sign(currentHorizontalSpeed);

        if (isChangingDirection)
            acceleration *= turnAccelerationMultiplier;

        float nextHorizontalSpeed = Mathf.MoveTowards(
            currentHorizontalSpeed,
            targetHorizontalSpeed,
            acceleration * Time.fixedDeltaTime);

        rb.linearVelocity = new Vector2(
            nextHorizontalSpeed,
            rb.linearVelocity.y
        );
    }

    private void UpdateJumpTimers()
    {
        if (GameUIController.BlocksGameplayInput)
        {
            coyoteTimeCounter = 0f;
            jumpBufferCounter = 0f;
            inputHandler.ConsumeJumpInput();
            return;
        }
        if (IsInFlame || IsInUpDraft)
        {
            coyoteTimeCounter = 0f;
            jumpBufferCounter = 0f;

            if (inputHandler.JumpPressed)
                inputHandler.ConsumeJumpInput();

            return;
        }

        bool isGrounded = IsGrounded();

        if (isGrounded && rb.linearVelocity.y <= 0.1f)
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

    private void TryJump()
    {
        if (jumpBufferCounter <= 0f || coyoteTimeCounter <= 0f)
            return;

        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpSpeed);
        jumpBufferCounter = 0f;
        coyoteTimeCounter = 0f;
    }

    private void ApplyJumpGravity()
    {
        float verticalVelocity = rb.linearVelocity.y;

        if (verticalVelocity < 0f)
        {
            rb.gravityScale = defaultGravityScale * fallGravityMultiplier;
        }
        else if (verticalVelocity > 0f && !inputHandler.JumpHeld)
        {
            rb.gravityScale = defaultGravityScale * jumpCutGravityMultiplier;
        }
        else
        {
            rb.gravityScale = defaultGravityScale;
        }

        if (rb.linearVelocity.y < -maxFallSpeed)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, -maxFallSpeed);
    }

    private void OnDisable()
    {
        isRolling = false;
        rollDirection = Vector2.zero;
        flameContactCount = 0;
        activeUpDrafts.Clear();

        if (rb != null)
            rb.gravityScale = defaultGravityScale;
    }

    public void EnterUpDraft(UpDraftZone upDraft)
    {
        if (upDraft == null || activeUpDrafts.Contains(upDraft))
            return;

        activeUpDrafts.Add(upDraft);
        coyoteTimeCounter = 0f;
        jumpBufferCounter = 0f;
        rb.gravityScale = 0f;
    }

    public void ExitUpDraft(UpDraftZone upDraft)
    {
        if (upDraft == null)
            return;

        activeUpDrafts.Remove(upDraft);

        if (!IsInFlame && !IsInUpDraft)
            rb.gravityScale = defaultGravityScale;
    }

    private UpDraftZone GetActiveUpDraft()
    {
        for (int i = activeUpDrafts.Count - 1; i >= 0; i--)
        {
            if (activeUpDrafts[i] != null && activeUpDrafts[i].isActiveAndEnabled)
                return activeUpDrafts[i];

            activeUpDrafts.RemoveAt(i);
        }

        return null;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsInLayerMask(other.gameObject.layer, flameLayer))
            return;

        flameContactCount++;
        coyoteTimeCounter = 0f;
        jumpBufferCounter = 0f;

        if (flameContactCount == 1)
        {
            rb.gravityScale = 0f;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsInLayerMask(other.gameObject.layer, flameLayer))
            return;

        flameContactCount = Mathf.Max(0, flameContactCount - 1);

        if (!IsInFlame && !IsInUpDraft)
        {
            rb.gravityScale = defaultGravityScale;
        }
    }

    private static bool IsInLayerMask(int layer, LayerMask layerMask)
    {
        return (layerMask.value & (1 << layer)) != 0;
    }

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
