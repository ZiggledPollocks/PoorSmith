using UnityEngine;

/// <summary>Prompts for town travel when the player presses against the forest's solid west boundary.</summary>
[RequireComponent(typeof(BoxCollider2D))]
public sealed class FieldSceneReturnGate : MonoBehaviour
{
    bool promptedUntilExit;
    Collider2D promptedPlayerCollider;
    BoxCollider2D boundaryCollider;

    void Awake() => boundaryCollider = GetComponent<BoxCollider2D>();

    void FixedUpdate()
    {
        // Teleports and scene restores can skip a collision-exit callback.
        if (promptedUntilExit && promptedPlayerCollider != null &&
            !boundaryCollider.IsTouching(promptedPlayerCollider))
        {
            promptedUntilExit = false;
            promptedPlayerCollider = null;
        }
    }

    void OnCollisionEnter2D(Collision2D collision) => TryPrompt(collision);

    void OnCollisionStay2D(Collision2D collision) => TryPrompt(collision);

    void TryPrompt(Collision2D collision)
    {
        if (promptedUntilExit || collision.gameObject.GetComponentInParent<InventorySystem>() == null ||
            FieldSceneTravel.Busy || SmithingLoop.Instance?.Initialized != true ||
            GameUIController.BlocksGameplayInput) return;

        var prompt = FieldTravelPrompt.Active;
        if (prompt != null && prompt.Open(FieldSceneTravel.BeginToTown))
        {
            promptedUntilExit = true;
            promptedPlayerCollider = collision.gameObject.GetComponent<Collider2D>();
        }
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.GetComponentInParent<InventorySystem>() != null)
        {
            promptedUntilExit = false;
            promptedPlayerCollider = null;
        }
    }
}
