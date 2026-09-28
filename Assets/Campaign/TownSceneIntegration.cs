using System.Linq;
using Blacksmith;
using Unity.Cinemachine;
using UnityEngine;

// Saved references bind the artist-authored town without replacing its terrain or art.
[DefaultExecutionOrder(-100)]
public sealed class TownSceneIntegration : MonoBehaviour
{
    public Transform town, forestIn, forestOut, blackSmith, pawnshop, store;
    public Bounds TownBounds { get; private set; }
    CinemachineConfiner2D confiner;
    Collider2D fieldBounds, townBounds;
    bool cameraInTown;

    void Awake()
    {
        var renderers=town.GetComponentsInChildren<SpriteRenderer>();
        var bounds=new Bounds(forestIn.position,Vector3.one);
        foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
        bounds.Expand(new Vector3(12,24,10));TownBounds=bounds;
        var player=FindFirstObjectByType<InventorySystem>();
        if(player!=null){var combat=player.GetComponent<CampaignCombat>()??player.gameObject.AddComponent<CampaignCombat>();combat.arrowSprite=GetComponent<BlacksmithView>().Art("arrow");}
        var campaign=GetComponent<CampaignController>();
        campaign.townScene=this;
        campaign.townSpawn=forestIn.position;
        campaign.fieldSpawn=forestOut != null ? forestOut.position : forestIn.position;
        Bind(forestIn,"forest_in","숲으로 출발",CampaignObjectKind.ForestGate);
        if (forestOut != null) Bind(forestOut,"forest_out","마을로 귀환",CampaignObjectKind.ReturnGate);
        Bind(blackSmith,"blacksmith","대장간",CampaignObjectKind.Smithy);
        Bind(pawnshop,"pawnshop","전당포",CampaignObjectKind.PawnShop);
        Bind(store,"store","장비 상점",CampaignObjectKind.EquipmentShop);
        confiner=FindFirstObjectByType<CinemachineConfiner2D>();
        if(confiner!=null)fieldBounds=confiner.BoundingShape2D;
        townBounds=town.GetComponentsInChildren<Collider2D>(true).FirstOrDefault(c=>c.name=="camerBounds");
        if(townBounds!=null)TownBounds=townBounds.bounds;
        else
        {
            var go=new GameObject("TownCameraBounds",typeof(BoxCollider2D));go.transform.SetParent(transform);go.transform.position=bounds.center;
            var box=go.GetComponent<BoxCollider2D>();box.size=bounds.size;box.isTrigger=true;box.enabled=false;townBounds=box;
        }
    }
    static void Bind(Transform target,string id,string label,CampaignObjectKind kind)
    {
        var box=target.GetComponent<BoxCollider2D>();
        if(box==null)
        {
            box=target.gameObject.AddComponent<BoxCollider2D>();
            var sprite=target.GetComponent<SpriteRenderer>()?.sprite;
            if(sprite!=null){box.size=sprite.bounds.size;box.offset=sprite.bounds.center;}
        }
        box.isTrigger=true;
        var obj=target.GetComponent<CampaignWorldObject>()??target.gameObject.AddComponent<CampaignWorldObject>();
        obj.stableId=id;obj.displayName=label;obj.kind=kind;
    }
    public bool InWind(Vector2 position)
    {return FindObjectsByType<AssimilatelZone>(FindObjectsSortMode.None).Any(z=>z.name=="StormAssimilatelZone"&&z.GetComponent<Collider2D>().OverlapPoint(position));}
    public bool Contains(Vector2 position)=>TownBounds.Contains(new Vector3(position.x,position.y,TownBounds.center.z));
    public void PrepareCamera(Vector2 position)
    {
        if(confiner==null)return;
        var interior=SmithyInterior.Instance;
        bool inInterior=interior!=null&&interior.Contains(position);
        bool enteringTown=inInterior||Contains(position);
        if(enteringTown&&!cameraInTown)fieldBounds=confiner.BoundingShape2D;
        if(enteringTown||cameraInTown)
        {confiner.BoundingShape2D=inInterior?interior.cameraBounds:enteringTown?townBounds:fieldBounds;confiner.InvalidateBoundingShapeCache();}
        cameraInTown=enteringTown;
    }
}
