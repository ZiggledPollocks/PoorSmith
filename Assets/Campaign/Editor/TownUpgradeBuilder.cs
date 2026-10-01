using System;
using System.IO;
using System.Linq;
using Blacksmith;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object=UnityEngine.Object;

// Explicit offline authoring command. Never executes on scene load or in the user's live editor.
public static class TownUpgradeBuilder
{
    const string Root="Assets/Campaign/Town";
    const string Village="Assets/source/Cainos/Pixel Art Platformer - Village Props/Prefab/";
    static Transform scenery;static TMP_FontAsset font;static Material material;
    static Sprite Art(string key)=>AssetDatabase.LoadAssetAtPath<Sprite>("Assets/JinHo/Crafting/Art/"+key+".png");
    public static void Run()
    {
        try
        {
            if(!Application.isBatchMode||!File.Exists(".town-authoring-copy"))throw new Exception("Run only in the approved isolated authoring copy.");
            Directory.CreateDirectory(Root);AssetDatabase.Refresh();
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity",OpenSceneMode.Single);
            var bridge=Object.FindFirstObjectByType<TownSceneIntegration>();if(bridge==null)throw new Exception("Saved town references missing");
            if(bridge.town.Find("TownPresentation")!=null)throw new Exception("Already authored; do not overwrite existing presentation");
            font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/JinHo/Resources/Fonts/Pretendard SDF.asset");
            material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Campaign/Data/Scenery.mat");
            scenery=new GameObject("TownPresentation").transform;scenery.SetParent(bridge.town,false);
            // The approved change reorders only town buildings. Gathering field geometry is untouched.
            Building(bridge.pawnshop,-112,"전당포",new(.32f,.35f,.32f),"bag_top");
            Building(bridge.store,-94,"장비 상점",new(.36f,.42f,.29f),"sword");
            Building(bridge.blackSmith,-76,"대장간",new(.39f,.24f,.19f),"anvil");
            var facility=new GameObject("FacilityStore").transform;facility.SetParent(scenery,false);
            Building(facility,-59,"시설 상점",new(.32f,.34f,.43f),"hammer");Bind(facility,"town_facility","시설 상점",CampaignObjectKind.FacilityShop);
            bridge.forestIn.position=new(-46,64,0);Gate(bridge.forestIn);
            var left=new GameObject("ForestInWest").transform;left.SetParent(scenery,false);left.position=new(-127,64,0);Gate(left);Bind(left,"forest_in_west","숲으로 출발",CampaignObjectKind.ForestGate);
            var delivery=new GameObject("DeliveryChest").transform;delivery.SetParent(scenery,false);delivery.position=new(-70.5f,63,0);
            Prop(delivery,"Chest Wooden",Vector2.zero,1.8f);Bind(delivery,"town_delivery","납품 상자",CampaignObjectKind.Delivery);Label(delivery,"납품 상자",new(0,1.5f),2.5f);
            var warp=new GameObject("TownWarp").transform;warp.SetParent(scenery,false);warp.position=new(-66.5f,63.5f,0);
            SpriteLayer(warp,"WarpCrystal",AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Campaign/Art/MapWarp.png"),Vector2.zero,new(1.1f,2.8f),-1);Bind(warp,"town_warp","워프석",CampaignObjectKind.Warp);Label(warp,"워프석",new(0,2),2.4f);
            Ground();Backdrop();
            foreach(var r in bridge.town.GetComponents<SpriteRenderer>())r.enabled=false;
            var cameraBounds=bridge.town.GetComponentsInChildren<BoxCollider2D>(true).FirstOrDefault(x=>x.name=="camerBounds");
            if(cameraBounds!=null){cameraBounds.transform.position=new(-86,69,0);cameraBounds.offset=Vector2.zero;cameraBounds.size=new(90,20);}
            foreach(var boundary in bridge.town.GetComponentsInChildren<Transform>(true))
            {if(boundary.name=="playerLeftBounds")boundary.position=new(-131,66,0);if(boundary.name=="playerRightBounds")boundary.position=new(-41,66,0);}
            var atmosphere=scenery.gameObject.AddComponent<TownAtmosphere>();atmosphere.layers=scenery.GetComponentsInChildren<SpriteRenderer>().Where(x=>x.name.StartsWith("Backdrop")).ToArray();
            var content=Resources.Load<SmithingLoopContent>("SmithingLoopContent");
            foreach(string key in new[]{"pick","axe"})for(int tier=2;tier<=3;tier++)
            {
                string id="town_"+key+"_"+tier;if(content.catalog.Item(id)!=null)continue;
                content.catalog.items.Add(new ItemDefinition{id=id,displayName=(key=="pick"?"곡괭이":"도끼")+" T"+tier,description="구매한 채집 도구 · 가방에서 장착 · 가격 임시",sprite=key=="pick"?"hammer":"saw",group=ItemGroup.Equipment,material=MaterialKind.Other,price=120*(tier-1),equipmentSlot=key=="pick"?"Pickaxe":"Axe",toolKind=key,toolTier=tier});
            }
            EditorUtility.SetDirty(content.catalog);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            File.WriteAllText("town-authoring-result.json","{\"passed\":true,\"scene\":\"SampleScene\",\"layout\":\"pawnshop-store-blacksmith-facility\"}");EditorApplication.Exit(0);
        }
        catch(Exception ex){Debug.LogException(ex);EditorApplication.Exit(1);}
    }
    static void Bind(Transform target,string id,string label,CampaignObjectKind kind)
    {var box=target.GetComponent<BoxCollider2D>();if(box==null)box=target.gameObject.AddComponent<BoxCollider2D>();box.size=new(2.5f,3);box.offset=Vector2.zero;box.isTrigger=true;var obj=target.GetComponent<CampaignWorldObject>();if(obj==null)obj=target.gameObject.AddComponent<CampaignWorldObject>();obj.stableId=id;obj.displayName=label;obj.kind=kind;}
    static void Building(Transform target,float x,string title,Color roof,string icon)
    {
        target.position=new(x,62,0);target.localScale=Vector3.one;
        foreach(var r in target.GetComponents<SpriteRenderer>())r.enabled=false;
        var box=target.GetComponent<BoxCollider2D>();if(box==null)box=target.gameObject.AddComponent<BoxCollider2D>();box.isTrigger=true;box.size=new(4,3);box.offset=new(0,1.5f);
        var facade=new GameObject("Facade").transform;facade.SetParent(target,false);
        SpriteLayer(facade,"Wall",Art("stone_wall"),new(0,2.7f),new(10,5.4f),-12,new(.79f,.69f,.50f));
        foreach(float px in new[]{-4.8f,0,4.8f})SpriteLayer(facade,"Beam",Art("table_wood"),new(px,2.8f),new(.35f,5.6f),-10,new(.42f,.29f,.16f));
        SpriteLayer(facade,"Foundation",Art("stone_wall"),new(0,.35f),new(10.3f,.7f),-8,new(.44f,.43f,.36f));
        var mesh=new Mesh{name=target.name+"Roof"};mesh.vertices=new[]{new Vector3(-5.8f,5.2f),new Vector3(0,8),new Vector3(5.8f,5.2f)};mesh.triangles=new[]{0,1,2};mesh.colors=new[]{roof,roof,roof};mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh,Root+"/"+target.name+"Roof.asset");var roofGo=new GameObject("Roof",typeof(MeshFilter),typeof(MeshRenderer));roofGo.transform.SetParent(facade,false);roofGo.GetComponent<MeshFilter>().sharedMesh=mesh;roofGo.GetComponent<MeshRenderer>().sharedMaterial=material;roofGo.GetComponent<MeshRenderer>().sortingOrder=-9;
        foreach(float px in new[]{-3.1f,3.1f}){SpriteLayer(facade,"WindowFrame",Art("table_wood"),new(px,2.7f),new(1.8f,2.3f),-8,new(.34f,.22f,.13f));SpriteLayer(facade,"WindowLight",Art("paper"),new(px,2.7f),new(1.45f,1.9f),-7,new(.94f,.73f,.38f));}
        SpriteLayer(facade,"Door",Art("table_wood"),new(0,1.75f),new(1.8f,3.5f),-6,new(.37f,.25f,.16f));
        SpriteLayer(facade,"ShopIcon",Art(icon),new(0,4.35f),new(1.25f,1),-4);
        Label(facade,title,new(0,6.2f),5.8f);Prop(facade,"Barrel",new(4.1f,.5f),1.3f);
        PrefabUtility.SaveAsPrefabAsset(facade.gameObject,Root+"/"+target.name+"Facade.prefab");
    }
    static void Gate(Transform target)
    {
        foreach(var r in target.GetComponents<SpriteRenderer>())r.enabled=false;target.localScale=Vector3.one;
        var box=target.GetComponent<BoxCollider2D>();if(box==null)box=target.gameObject.AddComponent<BoxCollider2D>();box.isTrigger=true;box.size=new(2.6f,4);box.offset=Vector2.zero;
        Prop(target,"Tree 01",new(-1.7f,-2),5);Prop(target,"Tree 01",new(1.7f,-2),5);
        SpriteLayer(target,"ForestGlow",AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Campaign/Art/MapEntrance.png"),Vector2.zero,new(2.5f,3.5f),-3,new(.73f,1,.75f));Label(target,"숲 입구",new(0,4),3);
    }
    static void Ground()
    {
        var grid=new GameObject("TownGrid",typeof(Grid));grid.transform.SetParent(scenery,false);
        var go=new GameObject("TownGround",typeof(Tilemap),typeof(TilemapRenderer),typeof(BoxCollider2D));go.transform.SetParent(grid.transform,false);go.layer=LayerMask.NameToLayer("Ground");
        var map=go.GetComponent<Tilemap>();go.GetComponent<TilemapRenderer>().sortingOrder=-2;
        string path="Assets/source/2D Pixel Art Platformer Biome - American Forest/Tilemap/";
        var top=AssetDatabase.LoadAssetAtPath<TileBase>(path+"TileGround2.asset");var fill=AssetDatabase.LoadAssetAtPath<TileBase>(path+"TileGround5.asset");
        for(int x=-133;x<=-39;x++)for(int y=57;y<=61;y++)map.SetTile(new(x,y,0),y==61?top:fill);
        var collider=go.GetComponent<BoxCollider2D>();collider.offset=new(-85.5f,59.5f);collider.size=new(95,5);
    }
    static void Backdrop()
    {
        var forest=AssetDatabase.LoadAllAssetsAtPath("Assets/source/2D Pixel Art Platformer Biome - American Forest/Sprites.png").OfType<Sprite>().ToArray();
        foreach(int x in new[]{-136,-112,-88,-64,-40})
        {SpriteLayer(scenery,"BackdropHills",forest.First(s=>s.name=="Background3"),new(x,70),new(25,28),-39);SpriteLayer(scenery,"BackdropTrees",forest.First(s=>s.name=="Background1"),new(x,67),new(25,20),-35);}
        foreach(int x in new[]{-121,-103,-85,-68,-50}){Prop(scenery,"Bush 01",new(x,62),2);Prop(scenery,"Crate Large",new(x+1,62),1.2f);}
    }
    static void Prop(Transform parent,string name,Vector2 position,float height)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Village+"PF Village Props - "+name+".prefab");if(prefab==null)return;
        var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);go.transform.localPosition=position;
        var rs=go.GetComponentsInChildren<SpriteRenderer>();if(rs.Length==0)return;float size=rs[0].bounds.size.y;if(size>0)go.transform.localScale*=height/size;
        foreach(var r in rs)r.sortingOrder=-5;foreach(var col in go.GetComponentsInChildren<Collider2D>())col.enabled=false;
    }
    static void Label(Transform parent,string value,Vector2 pos,float width)
    {var go=new GameObject("Sign_"+value,typeof(TextMeshPro));go.transform.SetParent(parent,false);go.transform.localPosition=pos;var t=go.GetComponent<TextMeshPro>();t.font=font;t.text=value;t.fontSize=4;t.alignment=TextAlignmentOptions.Center;t.color=new(1,.9f,.66f);t.rectTransform.sizeDelta=new(width,1);t.GetComponent<MeshRenderer>().sortingOrder=4;}
    static void SpriteLayer(Transform parent,string name,Sprite sprite,Vector2 pos,Vector2 size,int order,Color? tint=null)
    {if(sprite==null)return;var go=new GameObject(name,typeof(SpriteRenderer));go.transform.SetParent(parent,false);go.transform.localPosition=pos;var r=go.GetComponent<SpriteRenderer>();r.sprite=sprite;r.color=tint??Color.white;r.sortingOrder=order;go.transform.localScale=new(size.x/sprite.bounds.size.x,size.y/sprite.bounds.size.y,1);}
}
