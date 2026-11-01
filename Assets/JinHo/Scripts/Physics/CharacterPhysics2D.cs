// [코드 지도] CharacterPhysics2D: 플레이어와 몬스터에 공통 넉백/옆면 접촉 안정화 정책을 제공한다. 지형 전체를 통과시키는 것이 아니라 CharacterPhysics2D를 가진 이웃과의 수평 속도를 제한한다. 이동 스크립트는 IsKnockbackActive를 보고 넉백을 덮어쓰지 않는다.
// 주요 함수: RegisterCharacterContact, FixedUpdate, ConfigureCollisionBody
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/Physics/CharacterPhysics2D.cs.md

using UnityEngine;

/// <summary>
/// Provides consistent knockback and stable character-to-character contacts
/// without changing collision behaviour against terrain.
/// </summary>
[DefaultExecutionOrder(200)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public sealed class CharacterPhysics2D : MonoBehaviour
{
    [Header("Knockback")]
    [SerializeField, Min(0f)] private float horizontalKnockbackSpeed = 5f;
    [SerializeField, Min(0f)] private float upwardKnockbackSpeed = 2.25f;
    [SerializeField, Min(0f)] private float knockbackControlLockDuration = 0.22f;

    [Header("Character Contact")]
    [Tooltip("How horizontal a contact must be before it blocks sideways movement.")]
    [SerializeField, Range(0.1f, 0.95f)] private float sideNormalThreshold = 0.45f;
    [Tooltip("Minimum vertical body overlap required to treat a contact as side-to-side.")]
    [SerializeField, Range(0f, 0.75f)] private float minimumVerticalOverlap = 0.2f;
    [Tooltip("Keeps a contact constraint briefly between physics steps to prevent jitter.")]
    [SerializeField, Min(0f)] private float contactMemory = 0.04f;

    private static PhysicsMaterial2D characterMaterial;

    private Rigidbody2D body;
    private Collider2D bodyCollider;
    private float knockbackEndsAt;
    private float blockLeftUntil;
    private float blockRightUntil;
    private float leftSurfaceVelocity;
    private float rightSurfaceVelocity;

    public bool IsKnockbackActive => Time.time < knockbackEndsAt;

    public static CharacterPhysics2D Attach(
        GameObject owner,
        Rigidbody2D rigidbody2D,
        Collider2D collider2D)
    {
        if (owner == null || rigidbody2D == null || collider2D == null)
            return null;

        CharacterPhysics2D physics = owner.GetComponent<CharacterPhysics2D>();
        if (physics == null)
            physics = owner.AddComponent<CharacterPhysics2D>();

        physics.Configure(rigidbody2D, collider2D);
        return physics;
    }

    public void Configure(Rigidbody2D rigidbody2D, Collider2D collider2D)
    {
        body = rigidbody2D;
        bodyCollider = collider2D;
        ConfigureCollisionBody();
    }

    public void ApplyKnockbackFrom(Vector2 sourcePosition, float multiplier = 1f)
    {
        if (body == null || !body.simulated || multiplier <= 0f)
            return;

        float direction = Mathf.Sign(body.worldCenterOfMass.x - sourcePosition.x);
        if (Mathf.Abs(direction) < 0.01f)
            direction = transform.position.x >= sourcePosition.x ? 1f : -1f;

        float horizontalSpeed = horizontalKnockbackSpeed * multiplier;
        float verticalSpeed = upwardKnockbackSpeed * multiplier;
        body.linearVelocity = new Vector2(
            direction * horizontalSpeed,
            Mathf.Max(body.linearVelocity.y, verticalSpeed));
        knockbackEndsAt = Mathf.Max(
            knockbackEndsAt,
            Time.time + knockbackControlLockDuration);
    }

    private void Awake()
    {
        body ??= GetComponent<Rigidbody2D>();
        bodyCollider ??= GetComponent<Collider2D>();
        ConfigureCollisionBody();
    }

    // 핵심 분기: body == null || !body.simulated || IsKnockbackActive 판정.
    // 상태 변경: velocity.x 갱신.
    private void FixedUpdate()
    {
        if (body == null || !body.simulated || IsKnockbackActive)
            return;

        Vector2 velocity = body.linearVelocity;
        float now = Time.time;
        if (now <= blockLeftUntil)
        {
            // Match a neighbour moving in the same direction, but never pull
            // this body into a neighbour that is moving toward it.
            float minimumAllowedVelocity = Mathf.Min(0f, leftSurfaceVelocity);
            if (velocity.x < minimumAllowedVelocity)
                velocity.x = minimumAllowedVelocity;
        }

        if (now <= blockRightUntil)
        {
            float maximumAllowedVelocity = Mathf.Max(0f, rightSurfaceVelocity);
            if (velocity.x > maximumAllowedVelocity)
                velocity.x = maximumAllowedVelocity;
        }

        body.linearVelocity = velocity;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        RegisterCharacterContact(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        RegisterCharacterContact(collision);
    }

    // 핵심 분기: collision == null || collision.collider == null 판정.
    // 상태 변경: hasHorizontalContact 갱신.
    private void RegisterCharacterContact(Collision2D collision)
    {
        if (collision == null || collision.collider == null)
            return;

        CharacterPhysics2D other = collision.collider.GetComponentInParent<CharacterPhysics2D>();
        if (other == null || other == this)
            return;

        Bounds ownBounds = bodyCollider.bounds;
        Bounds otherBounds = collision.collider.bounds;
        float verticalOverlap = Mathf.Min(ownBounds.max.y, otherBounds.max.y)
            - Mathf.Max(ownBounds.min.y, otherBounds.min.y);
        float smallerHeight = Mathf.Min(ownBounds.size.y, otherBounds.size.y);
        if (verticalOverlap < smallerHeight * minimumVerticalOverlap)
            return;

        bool hasHorizontalContact = false;
        for (int index = 0; index < collision.contactCount; index++)
        {
            if (Mathf.Abs(collision.GetContact(index).normal.x) >= sideNormalThreshold)
            {
                hasHorizontalContact = true;
                break;
            }
        }

        if (!hasHorizontalContact)
            return;

        float contactHold = Time.time + Mathf.Max(
            contactMemory,
            Time.fixedDeltaTime * 1.5f);
        float otherVelocity = other.body != null ? other.body.linearVelocity.x : 0f;

        if (ownBounds.center.x <= otherBounds.center.x)
        {
            blockRightUntil = Mathf.Max(blockRightUntil, contactHold);
            rightSurfaceVelocity = otherVelocity;
        }
        else
        {
            blockLeftUntil = Mathf.Max(blockLeftUntil, contactHold);
            leftSurfaceVelocity = otherVelocity;
        }
    }

    // 핵심 분기: body == null || bodyCollider == null 판정.
    // 상태 변경: characterMaterial 갱신.
    private void ConfigureCollisionBody()
    {
        if (body == null || bodyCollider == null)
            return;

        if (characterMaterial == null)
        {
            characterMaterial = new PhysicsMaterial2D("Runtime Character Material")
            {
                friction = 0f,
                bounciness = 0f
            };
        }

        if (bodyCollider.sharedMaterial == null)
            bodyCollider.sharedMaterial = characterMaterial;

        body.constraints |= RigidbodyConstraints2D.FreezeRotation;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    private void OnValidate()
    {
        horizontalKnockbackSpeed = Mathf.Max(0f, horizontalKnockbackSpeed);
        upwardKnockbackSpeed = Mathf.Max(0f, upwardKnockbackSpeed);
        knockbackControlLockDuration = Mathf.Max(0f, knockbackControlLockDuration);
        sideNormalThreshold = Mathf.Clamp(sideNormalThreshold, 0.1f, 0.95f);
        minimumVerticalOverlap = Mathf.Clamp(minimumVerticalOverlap, 0f, 0.75f);
        contactMemory = Mathf.Max(0f, contactMemory);
    }
}
