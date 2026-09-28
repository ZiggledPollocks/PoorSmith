using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Lets the field-test player drop through only marked one-way platforms.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public sealed class FieldPlatformDropController : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float dropSpeed = 3f;
    [SerializeField, Min(0.01f)] private float probeDistance = 0.18f;
    [SerializeField, Min(0f)] private float releaseClearance = 0.08f;
    [SerializeField, Min(0f)] private float minimumIgnoreTime = 0.18f;

    private readonly ContactPoint2D[] contacts = new ContactPoint2D[16];
    private readonly RaycastHit2D[] groundHits = new RaycastHit2D[16];
    private Collider2D body;
    private Rigidbody2D rigidbody2D;
    private ContactFilter2D groundFilter;
    private Collider2D ignoredPlatform;
    private float ignoreStartedAt;
    private bool wasSDown;

    public bool IsStandingOnPlatform =>
        ignoredPlatform == null && body != null && FindStandingPlatform() != null;

    private void Awake()
    {
        body = GetComponent<Collider2D>();
        rigidbody2D = GetComponent<Rigidbody2D>();
        groundFilter = new ContactFilter2D();
        groundFilter.SetLayerMask(LayerMask.GetMask("Ground"));
        groundFilter.useTriggers = false;
    }

    private void Update()
    {
        bool sDown = Keyboard.current != null && Keyboard.current.sKey.isPressed;
        bool sPressed = sDown && !wasSDown;
        wasSDown = sDown;

        if (ignoredPlatform != null)
        {
            if (CanRestoreCollision()) RestoreCollision();
            return;
        }

        if (GameUIController.BlocksGameplayInput || !sPressed)
            return;

        Collider2D platform = FindStandingPlatform();
        if (platform == null) return;

        ignoredPlatform = platform;
        ignoreStartedAt = Time.time;
        Physics2D.IgnoreCollision(body, platform, true);
        Vector2 velocity = rigidbody2D.linearVelocity;
        velocity.y = Mathf.Min(velocity.y, -dropSpeed);
        rigidbody2D.linearVelocity = velocity;
    }

    private Collider2D FindStandingPlatform()
    {
        if (rigidbody2D.linearVelocity.y > 0.1f) return null;

        int count = body.GetContacts(groundFilter, contacts);
        for (int i = 0; i < count; i++)
        {
            ContactPoint2D contact = contacts[i];
            if (contact.normal.y < 0.5f) continue;
            Collider2D other = contact.collider == body
                ? contact.otherCollider : contact.collider;
            if (other != null && other.GetComponent<FieldOneWayPlatform>() != null)
                return other;
        }

        // The player may be a fraction above the surface between physics steps.
        count = body.Cast(Vector2.down, groundFilter, groundHits, probeDistance);
        for (int i = 0; i < count; i++)
        {
            RaycastHit2D hit = groundHits[i];
            if (hit.normal.y >= 0.5f && hit.collider != null &&
                hit.collider.GetComponent<FieldOneWayPlatform>() != null)
                return hit.collider;
        }
        return null;
    }

    private bool CanRestoreCollision()
    {
        if (ignoredPlatform == null || !ignoredPlatform.enabled ||
            !ignoredPlatform.gameObject.activeInHierarchy)
            return true;
        if (Time.time - ignoreStartedAt < minimumIgnoreTime)
            return false;

        Bounds playerBounds = body.bounds;
        Bounds platformBounds = ignoredPlatform.bounds;
        if (playerBounds.max.x < platformBounds.min.x - releaseClearance ||
            playerBounds.min.x > platformBounds.max.x + releaseClearance)
            return true;

        // A long diagonal slope can span many Y cells. Compare the player's
        // position with the local underside, rather than the slope's global min Y.
        if (ignoredPlatform is PolygonCollider2D polygon &&
            TryGetLocalUnderside(polygon, playerBounds.center.x, out float underside))
            return playerBounds.center.y < underside - releaseClearance;

        return playerBounds.center.y < platformBounds.min.y - releaseClearance;
    }

    private static bool TryGetLocalUnderside(
        PolygonCollider2D polygon, float worldX, out float worldY)
    {
        Vector2[] points = polygon.points;
        float localX = polygon.transform.InverseTransformPoint(new Vector3(worldX, 0f, 0f)).x;
        float minimum = float.PositiveInfinity;
        for (int i = 0; i < points.Length; i++)
        {
            Vector2 a = points[i], b = points[(i + 1) % points.Length];
            if (localX < Mathf.Min(a.x, b.x) || localX > Mathf.Max(a.x, b.x))
                continue;
            float y = Mathf.Abs(a.x - b.x) < 0.0001f
                ? Mathf.Min(a.y, b.y)
                : Mathf.Lerp(a.y, b.y, (localX - a.x) / (b.x - a.x));
            minimum = Mathf.Min(minimum, y);
        }
        worldY = float.IsPositiveInfinity(minimum)
            ? 0f : polygon.transform.TransformPoint(new Vector3(localX, minimum, 0f)).y;
        return !float.IsPositiveInfinity(minimum);
    }

    private void RestoreCollision()
    {
        if (body != null && ignoredPlatform != null)
            Physics2D.IgnoreCollision(body, ignoredPlatform, false);
        ignoredPlatform = null;
    }

    private void OnDisable()
    {
        wasSDown = false;
        RestoreCollision();
    }
    private void OnDestroy() => RestoreCollision();
}
