using UnityEngine;

/// <summary>Keeps a spawned field monster inside the forest or cave, including after knockback.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(10000)]
public sealed class FieldMonsterRegionLimiter2D : MonoBehaviour
{
    private CaveEntranceBackgroundTransition entrance;
    private Rigidbody2D body;
    private Collider2D[] colliders;
    private bool forestRegion;
    private Vector2 lastAllowedPosition;

    public bool Configure(CaveEntranceBackgroundTransition boundary, bool forest)
    {
        entrance = boundary;
        forestRegion = forest;
        body = GetComponent<Rigidbody2D>();
        colliders = GetComponentsInChildren<Collider2D>();
        lastAllowedPosition = body != null ? body.position : (Vector2)transform.position;
        return IsAllowed(Vector2.zero);
    }

    private void FixedUpdate()
    {
        Confine();
        if (body == null || entrance == null)
            return;
        if (!IsAllowed(body.linearVelocity * Time.fixedDeltaTime))
            body.linearVelocity = Vector2.zero;
    }

    private void LateUpdate() => Confine();

    private void Confine()
    {
        if (entrance == null)
        {
            gameObject.SetActive(false);
            return;
        }
        if (IsAllowed(Vector2.zero))
        {
            lastAllowedPosition = body != null ? body.position : (Vector2)transform.position;
            return;
        }
        if (body != null)
        {
            body.position = lastAllowedPosition;
            body.linearVelocity = Vector2.zero;
        }
        else
            transform.position = new Vector3(lastAllowedPosition.x, lastAllowedPosition.y,
                transform.position.z);
    }

    private bool IsAllowed(Vector2 offset)
    {
        if (entrance == null)
            return false;
        bool foundCollider = false;
        foreach (Collider2D collider in colliders)
        {
            if (collider == null || !collider.enabled || collider.isTrigger)
                continue;
            foundCollider = true;
            Bounds bounds = collider.bounds;
            if (WrongRegion(new Vector2(bounds.min.x, bounds.min.y) + offset) ||
                WrongRegion(new Vector2(bounds.min.x, bounds.max.y) + offset) ||
                WrongRegion(new Vector2(bounds.max.x, bounds.min.y) + offset) ||
                WrongRegion(new Vector2(bounds.max.x, bounds.max.y) + offset))
                return false;
        }
        return foundCollider || !WrongRegion((Vector2)transform.position + offset);
    }

    private bool WrongRegion(Vector2 position) =>
        entrance.IsCaveWorldPosition(position) == forestRegion;
}
