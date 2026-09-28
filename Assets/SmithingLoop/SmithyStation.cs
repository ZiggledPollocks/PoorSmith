using UnityEngine;
public enum SmithyStationKind { Rack,Chest,Bed,CraftingDoor }
[RequireComponent(typeof(BoxCollider2D))]
public sealed class SmithyStation : MonoBehaviour,IInteractable
{
    public SmithyStationKind kind;
    void Awake(){gameObject.layer=LayerMask.NameToLayer("Interactable");GetComponent<BoxCollider2D>().isTrigger=true;}
    public bool CanInteract()=>SmithyInterior.Instance?.Inside==true&&!GameUIController.BlocksGameplayInput&&CampaignController.Instance?.Travelling!=true&&!SmithingLoop.Instance.Transitioning;
    public bool CanUseTool(ToolData tool)=>true;
    public void Interact(PlayerInteraction player){if(CanInteract())SmithyInterior.Instance.Open(kind);}
}
