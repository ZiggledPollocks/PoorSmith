using UnityEngine;

public enum CampaignObjectKind { ForestGate, ReturnGate, Smithy, EquipmentShop, PawnShop, FacilityShop, Delivery, Warp, Treasure, WindEntrance, WindExit }

[RequireComponent(typeof(BoxCollider2D))]
public sealed class CampaignWorldObject : MonoBehaviour,IInteractable
{
    public string stableId;
    public string displayName;
    public CampaignObjectKind kind;
    public Sprite closedSprite,openSprite;
    void Awake(){GetComponent<BoxCollider2D>().isTrigger=true;gameObject.layer=LayerMask.NameToLayer("Interactable");}
    void Update()
    {
        if(kind!=CampaignObjectKind.Treasure||CampaignController.Instance?.Ready!=true)return;
        var sprite=GetComponent<SpriteRenderer>();
        if(sprite!=null&&openSprite!=null&&CampaignController.Instance.State.openedChests.Contains(stableId))sprite.sprite=openSprite;
    }
    public bool CanInteract()=>CampaignController.Instance?.Ready==true&&(kind!=CampaignObjectKind.Treasure||!CampaignController.Instance.State.openedChests.Contains(stableId));
    public bool CanUseTool(ToolData tool)=>true;
    public void Interact(PlayerInteraction player){if(CanInteract())CampaignController.Instance.Interact(this);}
}
