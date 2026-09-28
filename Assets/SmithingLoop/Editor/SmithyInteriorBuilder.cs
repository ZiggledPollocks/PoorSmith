using System;
using System.IO;
using System.Linq;
using Blacksmith;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object=UnityEngine.Object;

// Offline prefab authoring only. Never regenerates a saved player scene.
public static class SmithyInteriorBuilder
{
    static TMP_FontAsset font;
    static Sprite Art(string name)=>AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Blacksmith/Art/"+name+".png");
    public static void Run()
    {
        try
        {
            if(!Application.isBatchMode||!File.Exists(".smithy-authoring-copy"))throw new Exception("Isolated authoring copy required");
            font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Blacksmith/Fonts/BlacksmithKorean SDF.asset");
            var go=new GameObject("SmithyInterior",typeof(SmithyInterior));go.transform.position=new(-252,62,0);var room=go.GetComponent<SmithyInterior>();var root=go.transform;
            Image(root,"BackWall","stone_wall",new(0,6),new(50,12),-40,new(.69f,.57f,.4f));
            Image(root,"WoodenFloor","table_wood",new(0,-1),new(50,2),-1,new(.55f,.38f,.22f));
            foreach(int x in new[]{-25,-16,-5,6,16,25})Image(root,"TimberPillar","table_wood",new(x,6),new(.6f,12),-30,new(.35f,.24f,.14f));
            foreach(float y in new[]{.4f,10.7f,11.7f})Image(root,"TimberCrossbeam","table_wood",new(0,y),new(50,.55f),-29,new(.3f,.21f,.13f));
            Image(root,"WindowFrame","table_wood",new(-15,6.2f),new(5,4.8f),-25,new(.3f,.2f,.12f));
            room.window=Image(root,"WindowSky","paper",new(-15,6.2f),new(4.3f,4.1f),-24,new(.64f,.85f,1));
            Image(root,"WindowMullion","table_wood",new(-15,6.2f),new(.22f,4.2f),-22,new(.3f,.2f,.12f));Image(root,"WindowMullion","table_wood",new(-15,6.2f),new(4.4f,.22f),-22,new(.3f,.2f,.12f));
            room.entry=Point(root,"Entry",new(-19,2));room.exit=Point(root,"TownExit",new(-23,2));
            Image(root,"ExitDoor","table_wood",new(-23,2.6f),new(3.2f,5.2f),-20,new(.21f,.15f,.10f));Label(root,"← 마을",new(-23,6),4);
            var rack=Station(root,"EquipmentRack","장비 거치대",-8,SmithyStationKind.Rack);
            Image(rack,"RackBoard","table_wood",new(0,2.6f),new(5.4f,4.8f),-21,new(.42f,.3f,.19f));
            Image(rack,"Sword","sword",new(-1.3f,2.8f),new(1.2f,3.2f),-18,Color.white);Image(rack,"Shield","shield",new(.8f,2.8f),new(2,2.6f),-18,Color.white);
            var chest=Station(root,"StorageChest","상자",0,SmithyStationKind.Chest);Prop(chest,"Chest Wooden",Vector2.zero,2.5f);
            var door=Station(root,"CraftingDoor","제작실",9,SmithyStationKind.CraftingDoor);
            Image(door,"DoorFrame","stone_wall",new(0,3.2f),new(4.3f,6.4f),-21,new(.6f,.54f,.46f));Image(door,"Door","table_wood",new(0,2.9f),new(3.4f,5.8f),-20,new(.3f,.2f,.12f));Image(door,"ForgeSign","anvil",new(0,5),new(1.5f,1),-17,Color.white);
            var bed=Station(root,"Bed","침대",19,SmithyStationKind.Bed);Image(bed,"BedSprite","bed",new(0,1.4f),new(5.5f,2.8f),-15,Color.white);
            Prop(root,"Barrel",new(4,0),2);Prop(root,"Crate Large",new(-12,0),1.4f);
            var grid=new GameObject("InteriorGrid",typeof(Grid));grid.transform.SetParent(root,false);
            var ground=new GameObject("Ground",typeof(Tilemap),typeof(TilemapRenderer),typeof(BoxCollider2D));ground.transform.SetParent(grid.transform,false);ground.layer=LayerMask.NameToLayer("Ground");
            var tile=AssetDatabase.LoadAssetAtPath<TileBase>("Assets/source/2D Pixel Art Platformer Biome - American Forest/Tilemap/TileGround5.asset");
            for(int x=-26;x<26;x++)for(int y=-3;y<0;y++)ground.GetComponent<Tilemap>().SetTile(new(x,y,0),tile);
            ground.GetComponent<TilemapRenderer>().sortingOrder=-3;ground.GetComponent<BoxCollider2D>().size=new(52,3);ground.GetComponent<BoxCollider2D>().offset=new(0,-1.5f);
            foreach(int x in new[]{-26,26}){var wall=new GameObject("RoomBoundary",typeof(BoxCollider2D));wall.transform.SetParent(root,false);wall.transform.localPosition=new(x,6);wall.layer=LayerMask.NameToLayer("Ground");wall.GetComponent<BoxCollider2D>().size=new(1,12);}
            var bounds=new GameObject("InteriorCameraBounds",typeof(BoxCollider2D));bounds.transform.SetParent(root,false);bounds.transform.localPosition=new(0,6);room.cameraBounds=bounds.GetComponent<BoxCollider2D>();room.cameraBounds.size=new(53,20);room.cameraBounds.isTrigger=true;room.cameraBounds.enabled=false;
            PrefabUtility.SaveAsPrefabAsset(go,"Assets/Resources/SmithyInterior.prefab");Object.DestroyImmediate(go);
            var content=Resources.Load<SmithingLoopContent>("SmithingLoopContent");
            content.pickaxeIcon=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/source/UI/TypePickaxe.png");content.axeIcon=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/source/UI/TypeAxe.png");EditorUtility.SetDirty(content);
            foreach(string key in new[]{"pick","axe"})for(int tier=1;tier<=3;tier++)
            {
                string id=$"town_{key}_{tier}";var item=content.catalog.Item(id);
                if(item==null){item=new ItemDefinition{id=id};content.catalog.items.Add(item);}
                item.displayName=(key=="pick"?"곡괭이":"도끼")+$" T{tier}";item.description="장비 거치대에서 장착 · 채집 도구 · 가격/운반 무게 임시";item.sprite="linked_"+key;item.group=ItemGroup.Equipment;item.material=MaterialKind.Other;item.price=120*Mathf.Max(1,tier-1);item.equipmentSlot=key=="pick"?"Pickaxe":"Axe";item.toolKind=key;item.toolTier=tier;
            }
            EditorUtility.SetDirty(content.catalog);AssetDatabase.SaveAssets();File.WriteAllText("smithy-authoring-result.json","{\"passed\":true}");EditorApplication.Exit(0);
        }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static Transform Point(Transform parent,string name,Vector2 position){var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=position;return go.transform;}
    static Transform Station(Transform root,string name,string label,float x,SmithyStationKind kind)
    {var t=Point(root,name,new(x,0));var c=t.gameObject.AddComponent<BoxCollider2D>();c.isTrigger=true;c.offset=new(0,1.5f);c.size=new(3,3);t.gameObject.AddComponent<SmithyStation>().kind=kind;Label(t,label,new(0,6.8f),6);return t;}
    static SpriteRenderer Image(Transform parent,string name,string art,Vector2 pos,Vector2 size,int order,Color tint)
    {var t=Point(parent,name,pos);var r=t.gameObject.AddComponent<SpriteRenderer>();r.sprite=Art(art);if(r.sprite==null)throw new Exception("Missing art: "+art);r.color=tint;r.sortingOrder=order;t.localScale=new(size.x/r.sprite.bounds.size.x,size.y/r.sprite.bounds.size.y,1);return r;}
    static void Label(Transform parent,string value,Vector2 pos,float width)
    {var t=Point(parent,"Sign_"+value,pos);var text=t.gameObject.AddComponent<TextMeshPro>();text.font=font;text.text=value;text.fontSize=4;text.alignment=TextAlignmentOptions.Center;text.color=new(1,.89f,.63f);text.rectTransform.sizeDelta=new(width,1);text.GetComponent<MeshRenderer>().sortingOrder=3;}
    static void Prop(Transform parent,string name,Vector2 position,float height)
    {var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/source/Cainos/Pixel Art Platformer - Village Props/Prefab/PF Village Props - "+name+".prefab");if(prefab==null)return;var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);go.transform.localPosition=position;var rs=go.GetComponentsInChildren<SpriteRenderer>();if(rs.Length>0&&rs[0].bounds.size.y>0)go.transform.localScale*=height/rs[0].bounds.size.y;if(rs.Length>0){var box=rs[0].bounds;foreach(var r in rs)box.Encapsulate(r.bounds);go.transform.position+=new Vector3(parent.TransformPoint(position).x-box.center.x,parent.TransformPoint(position).y-box.min.y,0);}foreach(var r in rs)r.sortingOrder=-15;foreach(var c in go.GetComponentsInChildren<Collider2D>())c.enabled=false;foreach(var body in go.GetComponentsInChildren<Rigidbody2D>())body.simulated=false;}
}
