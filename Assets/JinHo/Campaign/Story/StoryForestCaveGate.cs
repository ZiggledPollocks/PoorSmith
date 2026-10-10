using UnityEngine;

/// <summary>The copied forest mouth uses F and the normal field travel service, never a second player.</summary>
[RequireComponent(typeof(BoxCollider2D))]
public sealed class StoryForestCaveGate : MonoBehaviour,IInteractable
{
    public Vector2 caveDestination=new(44,1.5f);
    public bool CanInteract()=>!FieldSceneTravel.Busy&&SmithingLoop.Instance?.Initialized==true;
    public bool CanUseTool(ToolData tool)=>true;
    public void Interact(PlayerInteraction interactionContext)
    {
        if(!StoryTutorial.AllowAction("cave")||interactionContext==null||!CanInteract())return;
        FieldSceneTravel.BeginToField(caveDestination);
    }
}
