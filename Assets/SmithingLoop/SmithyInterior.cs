using Blacksmith;
using UnityEngine;

// A saved world prefab, using the same player, input, physics and camera as the town.
public sealed class SmithyInterior : MonoBehaviour
{
    public static SmithyInterior Instance {get;private set;}
    public Transform entry,exit;public BoxCollider2D cameraBounds;public SpriteRenderer window;
    public bool Contains(Vector2 p)
    {
        if(cameraBounds==null)return false;
        Vector2 local=cameraBounds.transform.InverseTransformPoint(p);
        return new Rect(cameraBounds.offset-cameraBounds.size*.5f,cameraBounds.size).Contains(local);
    }
    public bool Inside=>CampaignController.Instance?.Player!=null&&Contains(CampaignController.Instance.Player.position);
    public bool PanelOpen=>SmithingLoop.Instance!=null&&SmithingLoop.Instance.InShop&&Inside;
    void Awake(){Instance=this;}
    void OnDestroy(){if(Instance==this)Instance=null;}
    public void Enter()
    {
        var c=CampaignController.Instance;if(c==null||Inside||c.Travelling||SmithingLoop.Instance.InShop)return;
        c.TransitionTo(entry.position);
    }
    public void Leave()
    {
        var c=CampaignController.Instance;if(c==null||!Inside||c.Travelling||SmithingLoop.Instance.InShop||GameUIController.BlocksGameplayInput)return;
        Vector2 destination=c.townScene!=null?(Vector2)c.townScene.blackSmith.position+Vector2.up*2:c.townSpawn;
        c.TransitionTo(destination);
    }
    public void Open(SmithyStationKind kind)
    {
        if(!Inside||GameUIController.BlocksGameplayInput)return;
        SmithingLoop.Instance.OpenInteriorPanel(kind==SmithyStationKind.Bed?ScreenState.Sleep:kind==SmithyStationKind.CraftingDoor?ScreenState.Workshop:ScreenState.Chest,kind==SmithyStationKind.Rack);
    }
    void Update()
    {
        var c=CampaignController.Instance;if(c?.Ready!=true)return;
        if(window!=null)window.color=SmithingLoop.Instance.SmithData.night?new Color(.2f,.31f,.55f):new Color(.64f,.85f,1);
        if(Inside&&!c.Travelling&&!SmithingLoop.Instance.InShop&&c.Player.position.x<exit.position.x+.8f)Leave();
    }
}
