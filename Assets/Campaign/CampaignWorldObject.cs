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
        if(kind!=CampaignObjectKind.Treasure)return;
        var state=SmithingLoop.Instance?.Campaign;
        if(state==null||state.openedChests==null)return;
        var sprite=GetComponent<SpriteRenderer>();
        if(sprite!=null&&state.openedChests.Contains(stableId))
        {
            if(openSprite!=null)sprite.sprite=openSprite;
            else sprite.color=new Color(.55f,.55f,.55f,1f);
        }
    }
    public bool CanInteract()
    {
        var loop=SmithingLoop.Instance;
        if(loop==null||!loop.Initialized||loop.Transitioning||FieldSceneTravel.Busy)return false;
        if(GetComponentInParent<FieldSceneState>()!=null || gameObject.scene.path==FieldSceneTravel.FieldScenePath)
            return (kind==CampaignObjectKind.Warp||kind==CampaignObjectKind.Treasure)&&
                FindFirstObjectByType<FieldSceneState>()?.Ready==true&&
                (kind!=CampaignObjectKind.Treasure||loop.Campaign.openedChests==null||
                 !loop.Campaign.openedChests.Contains(stableId));
        return CampaignController.Instance?.Ready==true&&!CampaignController.Instance.Travelling&&
            (kind!=CampaignObjectKind.Treasure||!CampaignController.Instance.State.openedChests.Contains(stableId));
    }
    public bool CanUseTool(ToolData tool)=>true;
    public void Interact(PlayerInteraction player)
    {
        if(!CanInteract())return;
        if(gameObject.scene.path==FieldSceneTravel.FieldScenePath)
        {
            var loop=SmithingLoop.Instance;
            var fieldHud=FindFirstObjectByType<FieldHud>();
            if(kind==CampaignObjectKind.Treasure)
            {
                loop.Campaign.openedChests??=new System.Collections.Generic.List<string>();
                loop.Campaign.openedChests.Add(stableId);
                loop.Campaign.warpUnlocked=true;
                if(openSprite!=null)GetComponent<SpriteRenderer>().sprite=openSprite;
                fieldHud?.ShowNotice("바람의 결정을 얻었습니다. 워프석이 해금되었습니다.");
                loop.RequestAutosave();
            }
            else if(!loop.Campaign.warpUnlocked)
                fieldHud?.ShowNotice("워프석이 잠겨 있습니다. 동굴 상자에서 바람의 결정을 찾으세요.");
            else
            {
                loop.Campaign.unlockedWarps??=new System.Collections.Generic.List<string>();
                if(!loop.Campaign.unlockedWarps.Contains(stableId))
                {
                    loop.Campaign.unlockedWarps.Add(stableId);
                    loop.RequestAutosave();
                }
                fieldHud?.OpenWarpMap(this);
            }
            return;
        }
        CampaignController.Instance.Interact(this);
    }
}
