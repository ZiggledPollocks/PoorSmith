using UnityEngine;

/// <summary>Freezes only the authored pass-through crate/barrel placements, never vendor prefabs.</summary>
[DefaultExecutionOrder(-200), DisallowMultipleComponent]
public sealed class FixedTownDecoration2D : MonoBehaviour
{
    public bool Ready { get; private set; }
    Rigidbody2D[] bodies;
    void Awake() => Prepare();
    void OnEnable() => Prepare();

    // Colliders already have their authored enabled state; do not add blockers to decorations.
    public void Prepare()
    {
        bodies ??= GetComponentsInChildren<Rigidbody2D>(true);
        foreach (var body in bodies)
        {
            if (body == null) continue;
            if (body.simulated) { body.linearVelocity = Vector2.zero; body.angularVelocity = 0f; }
            body.simulated = false;
        }
        Ready = true;
    }
}
