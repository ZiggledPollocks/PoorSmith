using UnityEngine;

/// <summary>An F-only passage on one side of the forest/cave entrance.</summary>
[RequireComponent(typeof(BoxCollider2D))]
public sealed class FieldCavePassageInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] CaveEntranceBackgroundTransition entrance;
    [SerializeField] bool enterCave;

    public bool CanInteract() => entrance != null && entrance.CanBeginFieldCrossing(enterCave);

    public bool CanUseTool(ToolData toolData) => true;

    public void Interact(PlayerInteraction interactionContext)
    {
        if (interactionContext != null && CanInteract())
            entrance.TryBeginFieldCrossing(enterCave);
    }
}
