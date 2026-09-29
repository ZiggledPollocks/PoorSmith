using System;
using System.IO;
using System.Linq;
using Blacksmith;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public static class CampaignBuildScene
{
    const string Root="Assets/Campaign";
    const string ScenePath=Root+"/Scenes/NotionCampaign.unity";
    static Transform props,spawns,terrain;
    static TileBase groundTile,groundFill,caveTile;
    static TMP_FontAsset font;
    static Sprite Art(string name)=>AssetDatabase.LoadAssetAtPath<Sprite>("Assets/JinHo/Crafting/Art/"+name+".png");
    static void Number(Object obj,string name,float value)
    {var so=new SerializedObject(obj);var p=so.FindProperty(name);if(p==null)throw new Exception(name+" missing on "+obj);if(p.propertyType==SerializedPropertyType.Integer)p.intValue=(int)value;else p.floatValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
    static void Ref(Object obj,string name,Object value)
    {var so=new SerializedObject(obj);var p=so.FindProperty(name);if(p==null)throw new Exception(name+" missing");p.objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
    public static void Run()
    {
        if(!Application.isBatchMode||!PlayerSettings.companyName.StartsWith("BatterMapValidation"))throw new Exception("Isolated stage only");
        try
        {
            foreach(string dir in new[]{"Scenes","Prefabs","Data"})Directory.CreateDirectory(Root+"/"+dir);
            AssetDatabase.Refresh();
            foreach(var file in Directory.GetFiles(Root+"/Art","*.png"))
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(file.Replace('\\','/'));
                importer.textureType=TextureImporterType.Sprite;importer.spritePixelsPerUnit=32;importer.filterMode=FilterMode.Point;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.alphaIsTransparency=true;importer.SaveAndReimport();
            }
            if(!File.Exists(ScenePath))AssetDatabase.CopyAsset("Assets/Scenes/SampleScene.unity",ScenePath);
            var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            var player=Object.FindFirstObjectByType<PlayerAssimilate>().gameObject;
            foreach(var root in scene.GetRootGameObjects())
                if(root!=player&&root.name!="Main Camera"&&root.name!="CinemachineCamera"&&root.name!="Global Light 2D"&&root.name!="GameUI")Object.DestroyImmediate(root);
            player.transform.position=new Vector3(-100,3,0);
            var bounds=Camera.main.gameObject.GetComponent<CampaignCameraBounds>()??Camera.main.gameObject.AddComponent<CampaignCameraBounds>();
            Camera.main.backgroundColor=new Color(.34f,.53f,.55f);
            var cm=Object.FindFirstObjectByType<Unity.Cinemachine.CinemachineConfiner2D>();if(cm!=null)Object.DestroyImmediate(cm);
            new GameObject("ItemDropSpawner").AddComponent<ItemDropSpawner>();
            var grid=new GameObject("CampaignGrid",typeof(Grid));terrain=grid.transform;
            props=new GameObject("CampaignProps").transform;spawns=new GameObject("CampaignSpawns").transform;
            groundTile=AssetDatabase.LoadAssetAtPath<TileBase>("Assets/source/2D Pixel Art Platformer Biome - American Forest/Tilemap/TileGround2.asset");
            groundFill=AssetDatabase.LoadAssetAtPath<TileBase>("Assets/source/2D Pixel Art Platformer Biome - American Forest/Tilemap/TileGround5.asset");
            caveTile=AssetDatabase.LoadAssetAtPath<TileBase>("Assets/JinHo/Tiles/Cave/CaveRock111.asset");
            if(groundTile==null||caveTile==null)throw new Exception("Required reusable terrain tile missing");
            // Each route is independent reusable terrain tiles plus an explicit matching slope collider.
            Route("TownGround",new[]{new Vector2(-135,0),new Vector2(-65,0)},false);
            Route("SurfaceToShallow",new[]{new Vector2(0,0),new(70,0),new(100,-6),new(140,-20),new(200,-25)},false);
            Route("CaveMain",new[]{new Vector2(0,-48),new(45,-35),new(105,-35),new(155,-29),new(200,-25)},true);
            Route("TreasureBranch",new[]{new Vector2(40,-35),new(65,-20),new(110,-20)},true);
            Route("DeepLeft",new[]{new Vector2(0,-85),new(25,-65),new(45,-35)},true);
            Route("DeepMain",new[]{new Vector2(45,-35),new(80,-65),new(120,-85),new(205,-92)},true);
            Route("DeepReward",new[]{new Vector2(72,-59),new(120,-57),new(145,-57)},true);
            Route("WindGround",new[]{new Vector2(235,0),new(315,0)},false);
            Route("WindLeftPlatform",new[]{new Vector2(246,15),new(257,15)},true);
            Route("WindRightPlatform",new[]{new Vector2(286,22),new(299,22)},true);
            Route("WindBossPlatform",new[]{new Vector2(263,35),new(283,35)},true);
            CampaignBuildScenery.Populate(props);
            var far=new GameObject("WindDistantBackground",typeof(SpriteRenderer));far.transform.SetParent(props);far.transform.position=new Vector3(275,20,0);var sr=far.GetComponent<SpriteRenderer>();sr.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(Root+"/Art/WindBackground.png");sr.sortingOrder=-30;far.transform.localScale=new Vector3(80/sr.sprite.bounds.size.x,60/sr.sprite.bounds.size.y,1);
            Region("Town",new(-100,10),new(70,30),true,false,new(.36f,.55f,.54f));
            Region("Forest",new(35,10),new(70,25),false,false,new(.12f,.29f,.22f));
            Region("Caves",new(138,-47),new(135,108),false,false,new(.08f,.13f,.17f));
            Region("Wind",new(275,20),new(80,55),false,true,new(.13f,.33f,.29f));
            font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/JinHo/Crafting/Fonts/BlacksmithKorean SDF.asset");
            Town();
            World("forest_exit","마을 입구",CampaignObjectKind.ReturnGate,new(2,1),Art("book"));
            World("surface_warp","지상 워프석",CampaignObjectKind.Warp,new(48,1),Icon("MapWarp"));
            World("cave_warp","동굴 워프석",CampaignObjectKind.Warp,new(70,-19),Icon("MapWarp"));
            World("deep_warp","깊은 동굴 워프석",CampaignObjectKind.Warp,new(9,-77),Icon("MapWarp"));
            World("wind_gate","바람 신전 입구",CampaignObjectKind.WindEntrance,new(201,-90),Icon("MapEntrance"));
            World("wind_exit","동굴로",CampaignObjectKind.WindExit,new(237,1),Icon("MapEntrance"));
            World("wind_warp","바람 신전 워프석",CampaignObjectKind.Warp,new(241,1),Icon("MapWarp"));
            World("wind_crystal_chest","낡은 상자",CampaignObjectKind.Treasure,new(77,-19),Art("chest_wood"));
            Altar("surface_altar",new(67,1));Altar("cave_altar",new(38,-37));Altar("deep_altar",new(142,-56));Altar("wind_altar",new(245,1));
            for(int i=0;i<6;i++){Spawn("Resource/tree",new(12+i*9,1),120,false,7);if(i<4)Spawn("Resource/Thicket",new(17+i*12,1),120,false,1.3f);}
            Spawn("Monster/Deer",new(24,2),120,true,2);Spawn("Monster/Deer",new(52,2),120,true,2);
            Vector2[] rocks={new(92,-3),new(130,-15),new(183,-23),new(16,-42),new(56,-34),new(98,-34),new(16,-71),new(105,-76),new(144,-86),new(185,-90)};
            for(int i=0;i<rocks.Length;i++)Spawn(i%3==0?"Resource/Coal":i%4==0?"Resource/Steel":"Resource/Stone",rocks[i],i%3==0?240:i%4==0?300:120,false,2.5f);
            Spawn("Monster/VampireBat",new(112,-5),180,true,1.5f);Spawn("Monster/VampireBat",new(81,-13),180,true,1.5f);
            Spawn("Monster/MossSlime",new(123,-33),180,true,1.7f);Spawn("Monster/MossSlime",new(15,-70),180,true,1.7f);
            Spawn("Monster/StoneGolem",new(172,-88),420,true,3.5f);
            for(int i=0;i<4;i++){Spawn("Monster/WindSpirit",new(252+i*14,5+i*3),180,true,2);Spawn("Resource/Storm Stone",new(249+i*17,1),300,false,2);}
            Spawn("Resource/Floating Ore",new(290,23),300,false,2);
            Updraft("WindLiftLow",new(259,8),new(6,18));Updraft("WindLiftRight",new(300,12),new(5,26));Updraft("WindLiftHigh",new(285,29),new(5,30));
            Boss();
            var campaignGo=new GameObject("Campaign");var campaign=campaignGo.AddComponent<CampaignController>();campaignGo.AddComponent<CampaignExploration>();var ui=campaignGo.AddComponent<CampaignUI>();var view=campaignGo.AddComponent<BlacksmithView>();view.font=font;
            view.sprites=AssetDatabase.FindAssets("t:Sprite",new[]{"Assets/JinHo/Crafting/Art"}).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<Sprite>).Where(s=>s!=null).ToArray();
            var rules=AssetDatabase.LoadAssetAtPath<CampaignRules>(Root+"/Data/CampaignRules.asset");if(rules==null){rules=ScriptableObject.CreateInstance<CampaignRules>();AssetDatabase.CreateAsset(rules,Root+"/Data/CampaignRules.asset");}campaign.rules=rules;ui.view=view;ui.rowPrefab=BuildRow(font);ui.mapPlayer=Icon("MapPlayer");ui.mapWarp=Icon("MapWarp");ui.mapAltar=Icon("MapAltar");ui.mapEntrance=Icon("MapEntrance");
            var combat=player.GetComponent<CampaignCombat>()??player.AddComponent<CampaignCombat>();combat.arrowSprite=Art("arrow");
            var content=Resources.Load<SmithingLoopContent>("SmithingLoopContent");
            var floating=content.materials.First(x=>x.fieldItem.ItemId=="5");floating.smithItemId="floating_ore";
            if(content.catalog.Item("floating_ore")==null)content.catalog.items.Add(new ItemDefinition{id="floating_ore",displayName="부유 광석",description="바람 지역 채집물 · 판매가 임시",sprite="ore",group=ItemGroup.Gathered,material=MaterialKind.Other,price=15});
            EditorUtility.SetDirty(content);EditorUtility.SetDirty(content.catalog);
            var particle=content.materials.First(x=>x.smithItemId=="wind_particle").fieldItem;
            var drop=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/JinHo/Gathering/Prefabs/Drops/Resource/Wood.prefab"));Ref(drop.GetComponent<ItemDropInteractable>(),"itemData",particle);
            if(particle.Icon!=null)drop.GetComponentInChildren<SpriteRenderer>().sprite=particle.Icon;
            campaign.windParticleDrop=PrefabUtility.SaveAsPrefabAsset(drop,Root+"/Prefabs/WindParticleDrop.prefab");Object.DestroyImmediate(drop);
            Number(player.GetComponent<PlayerAssimilate>(),"currentAssimilation",100);
            EditorSceneManager.SaveScene(scene,ScenePath);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true),new EditorBuildSettingsScene("Assets/JinHo/Crafting/Scenes/BlacksmithShop.unity",true)};
            font.TryAddCharacters(string.Join("",Directory.GetFiles(Root,"*.cs",SearchOption.AllDirectories).Select(File.ReadAllText)),out string unusedMissingGlyphs);EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            File.WriteAllText("campaign-build-scene.json","{\"passed\":true,\"scene\":\""+ScenePath+"\",\"tilemaps\":"+Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None).Length+",\"objects\":"+Object.FindObjectsByType<CampaignWorldObject>(FindObjectsSortMode.None).Length+"}");
            EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static Sprite Icon(string name)=>AssetDatabase.LoadAssetAtPath<Sprite>(Root+"/Art/"+name+".png");
    static void Route(string name,Vector2[] points,bool cave)
    {
        var go=new GameObject(name,typeof(Tilemap),typeof(TilemapRenderer),typeof(PolygonCollider2D));go.transform.SetParent(terrain,false);go.layer=LayerMask.NameToLayer("Ground");
        var map=go.GetComponent<Tilemap>();go.GetComponent<TilemapRenderer>().sortingOrder=-3;
        for(int i=0;i<points.Length-1;i++)
        {
            var a=points[i];var b=points[i+1];for(int x=Mathf.FloorToInt(a.x);x<=Mathf.CeilToInt(b.x);x++)
            {int y=Mathf.FloorToInt(Mathf.Lerp(a.y,b.y,Mathf.InverseLerp(a.x,b.x,x)));for(int d=1;d<=3;d++)map.SetTile(new Vector3Int(x,y-d,0),cave?caveTile:d==1?groundTile:groundFill);}
        }
        var poly=points.Concat(points.Reverse().Select(x=>x+Vector2.down*3)).ToArray();go.GetComponent<PolygonCollider2D>().points=poly;
    }
    static void Background(string name,Vector2 pos,Vector2 size,Color color)
    {
        var go=new GameObject(name,typeof(SpriteRenderer));go.transform.SetParent(props);go.transform.position=pos;
        var sprite=go.GetComponent<SpriteRenderer>();sprite.sprite=Art("leather_mat");sprite.color=color;sprite.sortingOrder=-40;
        go.transform.localScale=new Vector3(size.x/sprite.sprite.bounds.size.x,size.y/sprite.sprite.bounds.size.y,1);
    }
    static void Region(string name,Vector2 pos,Vector2 size,bool town,bool wind,Color color)
    {
        var go=new GameObject(name+"Region",typeof(BoxCollider2D),typeof(CampaignRegion));go.transform.position=pos;go.transform.SetParent(props);
        var box=go.GetComponent<BoxCollider2D>();box.isTrigger=true;box.size=size;var region=go.GetComponent<CampaignRegion>();region.regionName=name;region.town=town;region.wind=wind;region.background=color;
    }
    static void Town()
    {
        World("town_forest_left","숲 입구",CampaignObjectKind.ForestGate,new(-130,1),Icon("MapEntrance"));
        World("pawn","전당포",CampaignObjectKind.PawnShop,new(-118,2),Art("chest_wood"));
        World("equipment","장비 상점",CampaignObjectKind.EquipmentShop,new(-108,2),Art("hammer"));
        World("smithy","대장간",CampaignObjectKind.Smithy,new(-97,2),Art("anvil"));
        World("delivery","납품 상자",CampaignObjectKind.Delivery,new(-91,1),Art("chest_wood"));
        World("home_warp","마을 워프석",CampaignObjectKind.Warp,new(-86,1),Icon("MapWarp"));
        World("facility","시설 상점",CampaignObjectKind.FacilityShop,new(-77,2),Art("anvil"));
        World("town_forest_right","숲 입구",CampaignObjectKind.ForestGate,new(-67,1),Icon("MapEntrance"));
    }
    static void World(string id,string title,CampaignObjectKind kind,Vector2 pos,Sprite sprite)
    {
        var go=new GameObject(title,typeof(SpriteRenderer),typeof(BoxCollider2D),typeof(CampaignWorldObject));go.transform.SetParent(props);go.transform.position=pos;
        var sr=go.GetComponent<SpriteRenderer>();sr.sprite=sprite;sr.sortingOrder=1;
        if(sprite!=null){float scale=(kind==CampaignObjectKind.Smithy?1.3f:kind==CampaignObjectKind.EquipmentShop?1:2)/sprite.bounds.size.y;go.transform.localScale=Vector3.one*scale;}
        var c=go.GetComponent<BoxCollider2D>();c.isTrigger=true;c.size=sprite!=null?(Vector2)sprite.bounds.size:Vector2.one;
        var obj=go.GetComponent<CampaignWorldObject>();obj.stableId=id;obj.displayName=title;obj.kind=kind;obj.closedSprite=sprite;obj.openSprite=kind==CampaignObjectKind.Treasure?Art("chest_slot"):null;
        go.layer=LayerMask.NameToLayer("Interactable");
        PrefabUtility.SaveAsPrefabAsset(go,Root+"/Prefabs/"+id+".prefab");
        var label=new GameObject("WorldLabel",typeof(TextMeshPro));label.transform.SetParent(props);label.transform.position=pos+Vector2.up*2;var text=label.GetComponent<TextMeshPro>();text.font=font;text.text=title+" [F]";text.fontSize=3;text.alignment=TextAlignmentOptions.Center;text.GetComponent<RectTransform>().sizeDelta=new Vector2(10,2);text.GetComponent<MeshRenderer>().sortingOrder=5;
    }
    static void Altar(string id,Vector2 pos)
    {
        var go=new GameObject(id,typeof(SpriteRenderer),typeof(BoxCollider2D),typeof(AssissZone));go.transform.SetParent(props);go.transform.position=pos;
        var sr=go.GetComponent<SpriteRenderer>();sr.sprite=Icon("MapAltar");sr.sortingOrder=1;go.transform.localScale=Vector3.one*(2/sr.sprite.bounds.size.y);go.GetComponent<BoxCollider2D>().size=sr.sprite.bounds.size;go.GetComponent<AssissZone>().stableId=id;
        PrefabUtility.SaveAsPrefabAsset(go,Root+"/Prefabs/"+id+".prefab");
    }
    static void Spawn(string path,Vector2 pos,float delay,bool monster,float height)
    {
        var original=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/JinHo/Prefab/"+path+".prefab");if(original==null)throw new Exception(path);
        string asset=Root+"/Prefabs/Spawn_"+Path.GetFileName(path)+".prefab";
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(asset);
        if(prefab==null)
        {
            var clone=Object.Instantiate(original);var renderer=clone.GetComponentInChildren<SpriteRenderer>();
            if(renderer!=null&&renderer.bounds.size.y>.01f)clone.transform.localScale*=height/renderer.bounds.size.y;
            prefab=PrefabUtility.SaveAsPrefabAsset(clone,asset);Object.DestroyImmediate(clone);
        }
        var go=new GameObject("Spawn_"+Path.GetFileName(path),typeof(CampaignSpawnPoint));go.transform.SetParent(spawns);go.transform.position=pos;var spawn=go.GetComponent<CampaignSpawnPoint>();spawn.prefab=prefab;spawn.delay=delay;spawn.monster=monster;
    }
    static void Updraft(string name,Vector2 pos,Vector2 size)
    {
        var go=new GameObject(name,typeof(BoxCollider2D),typeof(UpDraftZone));go.transform.SetParent(props);go.transform.position=pos;var box=go.GetComponent<BoxCollider2D>();box.isTrigger=true;box.size=size;
        var line=go.AddComponent<LineRenderer>();line.sharedMaterial=new Material(Shader.Find("Sprites/Default"));line.startColor=line.endColor=new Color(.2f,.85f,.7f,.45f);line.startWidth=line.endWidth=.12f;line.positionCount=3;line.SetPositions(new[]{(Vector3)(pos-Vector2.up*size.y*.5f),(Vector3)(pos+Vector2.up*size.y*.5f),(Vector3)(pos+new Vector2(-1,size.y*.5f-2))});
    }
    static void Boss()
    {
        var go=new GameObject("WindBossArena",typeof(BoxCollider2D),typeof(StormBossArena));go.transform.SetParent(props);go.transform.position=new Vector2(273,39);var box=go.GetComponent<BoxCollider2D>();box.size=new Vector2(22,12);box.isTrigger=true;
        var arena=go.GetComponent<StormBossArena>();Ref(arena,"arenaCollider",box);Ref(arena,"bossPrefab",AssetDatabase.LoadAssetAtPath<GameObject>("Assets/JinHo/Prefab/Monster/StormBoss.prefab"));
        var so=new SerializedObject(arena);so.FindProperty("groundLayers").intValue=LayerMask.GetMask("Ground");so.ApplyModifiedPropertiesWithoutUndo();
    }
    static Button BuildRow(TMP_FontAsset fontAsset)
    {
        string path=Root+"/Prefabs/CampaignRow.prefab";var go=new GameObject("CampaignRow",typeof(RectTransform),typeof(Image),typeof(Button),typeof(LayoutElement));
        go.GetComponent<LayoutElement>().preferredHeight=72;go.GetComponent<Image>().color=new Color(.20f,.22f,.18f);var button=go.GetComponent<Button>();button.targetGraphic=go.GetComponent<Image>();
        var label=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI));label.transform.SetParent(go.transform,false);var rect=(RectTransform)label.transform;rect.anchorMax=Vector2.one;rect.offsetMin=new Vector2(12,4);rect.offsetMax=new Vector2(-12,-4);var tmp=label.GetComponent<TextMeshProUGUI>();tmp.font=fontAsset;tmp.fontSize=24;tmp.color=BlacksmithView.Cream;tmp.alignment=TextAlignmentOptions.MidlineLeft;tmp.raycastTarget=false;
        var prefab=PrefabUtility.SaveAsPrefabAsset(go,path);Object.DestroyImmediate(go);return prefab.GetComponent<Button>();
    }
}
