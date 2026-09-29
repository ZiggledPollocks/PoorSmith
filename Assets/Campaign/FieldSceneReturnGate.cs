using UnityEngine;

/// <summary>F interaction at the forest boundary; town travel keeps its Yes/No prompt.</summary>
[RequireComponent(typeof(BoxCollider2D))]
public sealed class FieldSceneReturnGate : MonoBehaviour, IInteractable
{
    public bool CanInteract() =>
        !FieldSceneTravel.Busy &&
        SmithingLoop.Instance?.Initialized == true &&
        !GameUIController.BlocksGameplayInput &&
        FieldTravelPrompt.Active != null &&
        !FieldTravelPrompt.Active.IsOpen;

    public bool CanUseTool(ToolData toolData) => true;

    public void Interact(PlayerInteraction interactionContext)
    {
        if (interactionContext != null && CanInteract())
            FieldTravelPrompt.Active.Open(FieldSceneTravel.BeginToTown);
    }
}
