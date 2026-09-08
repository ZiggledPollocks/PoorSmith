using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerInputHandler inputHandler;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float runSpeed = 8f;
    [SerializeField, Min(0.01f)] private float groundAcceleration = 55f;
    [SerializeField, Min(0.01f)] private float groundDeceleration = 70f;
    [SerializeField, Min(0.01f)] private float airAcceleration = 30f;
    [SerializeField, Min(0.01f)] private float airDeceleration = 15f;
    [SerializeField, Min(1f)] private float turnAccelerationMultiplier = 1.35f;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 10f;
    [SerializeField, Min(0.01f)] private float coyoteTime = 0.12f;
    [SerializeField, Min(0.01f)] private float jumpBufferTime = 0.12f;
    [SerializeField, Min(1f)] private float fallGravityMultiplier = 2f;
    [SerializeField, Min(1f)] private float jumpCutGravityMultiplier = 2.5f;
    [SerializeField, Min(0.1f)] private float maxFallSpeed = 18f;
    [SerializeField] private Collider2D bodyCollider;
    [SerializeField] private PhysicsMaterial2D frictionlessMaterial;
    [SerializeField, Min(0.01f)] private float groundCheckDistance = 0.08f;
    [SerializeField, Range(0f, 1f)] private float minGroundNormalY = 0.65f;
    [SerializeField] private LayerMask groundLayer;

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

    private Rigidbody2D rb;
    private ContactFilter2D groundContactFilter;
    private float defaultGravityScale;
    private float coyoteTimeCounter;
    private float jumpBufferCounter;
    private int flameContactCount;
    private readonly List<UpDraftZone> activeUpDrafts = new();

    public bool IsInFlame => flameContactCount > 0;
    public bool IsInUpDraft => GetActiveUpDraft() != null;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        defaultGravityScale = rb.gravityScale;

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

        groundContactFilter = new ContactFilter2D();
        groundContactFilter.SetLayerMask(groundLayer);
        groundContactFilter.useTriggers = false;
    }

    private void Update()
    {
        UpdateJumpTimers();
    }

    private void FixedUpdate()
    {
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

        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
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
        int hitCount = bodyCollider.Cast(
            Vector2.down,
            groundContactFilter,
            groundHits,
            groundCheckDistance
        );

        for (int i = 0; i < hitCount; i++)
        {
            if (groundHits[i].normal.y >= minGroundNormalY)
            {
                return true;
            }
        }

        return false;
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
        Vector3 start = new(bounds.center.x, bounds.min.y, transform.position.z);
        Vector3 end = start + Vector3.down * groundCheckDistance;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(start, end);
    }
}
