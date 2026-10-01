using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Blacksmith;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

public static class BlacksmithProjectBuilder
{
    const string Root="Assets/JinHo/Crafting";
    [MenuItem("Blacksmith/Build playable scene and verify")]
    public static void Build()
    {
        Directory.CreateDirectory(Root+"/Prefabs");Directory.CreateDirectory(Root+"/Scenes");Directory.CreateDirectory(Root+"/Data");
        AssetDatabase.Refresh();
        if(!AssetDatabase.LoadAssetAtPath<TMP_Settings>("Assets/TextMesh Pro/Resources/TMP Settings.asset"))
        {
            var package=UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TMP_Text).Assembly);
            AssetDatabase.ImportPackage(package.resolvedPath+"/Package Resources/TMP Essential Resources.unitypackage",false);
            AssetDatabase.Refresh();
        }
        foreach(var path in Directory.GetFiles(Root+"/Art","*.png"))
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(path.Replace('\\','/'));
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.filterMode=FilterMode.Point;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.alphaIsTransparency=true;importer.spritePixelsPerUnit=100;importer.maxTextureSize=2048;importer.SaveAndReimport();
        }
        var catalog=CreateCatalog();
        NotionRecipeImporter.Apply(catalog);
        var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/JinHo/Resources/Fonts/Pretendard SDF.asset");
        if(!font) throw new InvalidOperationException("Pretendard TMP font asset is missing.");
        string chars="ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 /+-.×→←‹›[]:·…"+string.Concat(catalog.items.Select(x=>x.displayName+x.description))+"대장간전체보기설비선택재료가공장비완성보관함체력연료아침밤일도감숙련도제작자동해금실패성공최상급중하잠자기취소정렬검색목재석철무기방어구기타칼망치톱대패숫돌시작하기초기화닫기반환일괄선택버리기배낭페이지공격속도방패화살양손무기잠김공격력테스트카탈로그수치레시피별도교체가능";
        font.TryAddCharacters(chars,out string missing);EditorUtility.SetDirty(font);AssetDatabase.SaveAssets();
        var temp=new GameObject("UIBuilder",typeof(RectTransform),typeof(BlacksmithView));var v=temp.GetComponent<BlacksmithView>();v.font=font;v.sprites=Directory.GetFiles(Root+"/Art","*.png").Select(p=>AssetDatabase.LoadAssetAtPath<Sprite>(p.Replace('\\','/'))).ToArray();
        v.slotPrefab=CreateSlot(v);v.recipePrefab=CreateRecipe(v);
        var spriteAssets=v.sprites;var slotAsset=v.slotPrefab;var recipeAsset=v.recipePrefab;
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var camera=new GameObject("Main Camera",typeof(Camera));camera.tag="MainCamera";camera.GetComponent<Camera>().clearFlags=CameraClearFlags.SolidColor;camera.GetComponent<Camera>().backgroundColor=Color.black;camera.transform.position=new Vector3(0,0,-10);
        var canvas=new GameObject("BlacksmithUI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(BlacksmithView));
        canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
        var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=.5f;
        var view=canvas.GetComponent<BlacksmithView>();view.font=font;view.sprites=spriteAssets;view.slotPrefab=slotAsset;view.recipePrefab=recipeAsset;view.Build();
        var runtime=new GameObject("BlacksmithGame",typeof(BlacksmithController),typeof(BlacksmithSmokeCapture));var c=runtime.GetComponent<BlacksmithController>();c.catalog=catalog;c.view=view;
        new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
        EditorSceneManager.SaveScene(scene,Root+"/Scenes/BlacksmithShop.unity");
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(Root+"/Scenes/BlacksmithShop.unity",true)};
        PlayerSettings.companyName="BlacksmithPrototype";PlayerSettings.productName="Test Blacksmith Shop";PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=900;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.runInBackground=true;
        AssetDatabase.SaveAssets();RunDomainTests(CreateCatalog(true));NotionRecipeImporter.Verify(catalog);
        var report=new{scene=Root+"/Scenes/BlacksmithShop.unity",sprites=view.sprites.Length,recipes=catalog.recipes.Count,items=catalog.items.Count,tests="passed"};
        File.WriteAllText("blacksmith-validation.txt",$"PASS: scene generated; {view.sprites.Length} sprites; {catalog.items.Count} items; {catalog.recipes.Count} recipes.\nDomain assertions passed.\n");
        Debug.Log("BLACKSMITH_VALIDATION_PASSED");
    }
    public static void BuildAndPlayer()
    {
        Build();Directory.CreateDirectory("Build");
        var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Root+"/Scenes/BlacksmithShop.unity"},locationPathName="Build/BlacksmithShop.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
        if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Player build failed: "+result.summary.result);
    }
    static InventorySlotView CreateSlot(BlacksmithView v)
    {
        var root=new GameObject("InventorySlot",typeof(RectTransform),typeof(Image),typeof(InventorySlotView),typeof(LayoutElement));root.GetComponent<RectTransform>().sizeDelta=new Vector2(104,104);root.GetComponent<LayoutElement>().preferredHeight=104;
        var slot=root.GetComponent<InventorySlotView>();slot.background=root.GetComponent<Image>();slot.background.sprite=v.Art("slot");
        slot.icon=v.Image("ItemIcon",root.transform,"ore",Color.white,new Vector2(.14f,.20f),new Vector2(.86f,.91f),true);
        slot.count=v.Text("Count",root.transform,"1",22,new Vector2(.08f,.01f),new Vector2(.95f,.31f),Color.white,TextAlignmentOptions.BottomRight);
        var p=PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/InventorySlot.prefab");UnityEngine.Object.DestroyImmediate(root);return p.GetComponent<InventorySlotView>();
    }
    static RecipeEntryView CreateRecipe(BlacksmithView v)
    {
        var root=new GameObject("RecipeEntry",typeof(RectTransform),typeof(LayoutElement),typeof(RecipeEntryView));root.GetComponent<LayoutElement>().preferredHeight=180;
        var r=root.GetComponent<RecipeEntryView>();r.title=v.Text("Name",root.transform,"레시피",23,new Vector2(0,.74f),new Vector2(1,1),BlacksmithView.Ink);
        r.ingredients=v.Text("Ingredients",root.transform,"재료",18,new Vector2(0,.30f),new Vector2(1,.75f),BlacksmithView.Ink);
        r.minus=v.Button("Minus",root.transform,"−",new Vector2(0,0),new Vector2(.15f,.28f),null);
        r.quantity=v.Text("Quantity",root.transform,"1",20,new Vector2(.15f,0),new Vector2(.3f,.28f),BlacksmithView.Ink,TextAlignmentOptions.Center);
        r.plus=v.Button("Plus",root.transform,"+",new Vector2(.30f,0),new Vector2(.45f,.28f),null);
        r.craft=v.Button("Craft",root.transform,"제작하기",new Vector2(.48f,0),new Vector2(1,.28f),null);
        var p=PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/RecipeEntry.prefab");UnityEngine.Object.DestroyImmediate(root);return p.GetComponent<RecipeEntryView>();
    }
    static BlacksmithCatalog CreateCatalog(bool fixture=false)
    {
        var path=Root+"/Data/TestCatalog.asset";var cat=AssetDatabase.LoadAssetAtPath<BlacksmithCatalog>(path);
        if(cat&&!fixture)return cat;cat=ScriptableObject.CreateInstance<BlacksmithCatalog>();
        void Item(string id,string name,string sprite,MaterialKind material=MaterialKind.Other,ItemGroup group=ItemGroup.Crafted,int fuel=0,bool hot=false,string equip=null,float attack=0,float defense=0,bool two=false,bool bow=false,bool arrow=false)
        {cat.items.Add(new ItemDefinition{id=id,displayName=name,sprite=sprite,material=material,group=group,fuelValue=fuel,heated=hot,equipmentSlot=equip,attack=attack,defense=defense,twoHanded=two,bow=bow,arrow=arrow,description="테스트용 데이터",price=group==ItemGroup.Equipment?100:10});}
        Item("wood","나무 원목","wood",MaterialKind.Wood,ItemGroup.Gathered,3);Item("ore","철 원석","ore",MaterialKind.Iron,ItemGroup.Gathered);Item("stone","돌","ore",MaterialKind.Stone,ItemGroup.Gathered);Item("coal","석탄","coal",MaterialKind.Other,ItemGroup.Gathered,6);
        Item("hot_iron","달궈진 철","ingot",MaterialKind.Iron,hot:true);Item("hot_plate","달궈진 긴 철제 판","plate",MaterialKind.Iron,hot:true);
        Item("ingot","철 덩어리","ingot",MaterialKind.Iron);Item("plate","긴 철제 판","plate",MaterialKind.Iron);Item("blade","철 칼날","sword",MaterialKind.Iron);
        Item("handle","나무 손잡이","wood",MaterialKind.Wood);Item("plank","나무 판자","wood",MaterialKind.Wood);Item("smooth_wood","손질된 나무","wood",MaterialKind.Wood);Item("stone_piece","석재 부품","ore",MaterialKind.Stone);
        Item("leather_prepared","손질된 가죽","leather");Item("leather","가죽","leather");
        Item("sword","철 검","sword",MaterialKind.Weapon,ItemGroup.Equipment,equip:"Weapon",attack:23);Item("shield","철 방패","shield",MaterialKind.Armor,ItemGroup.Equipment,equip:"Shield",defense:12);
        Item("bow","활","wood",MaterialKind.Weapon,ItemGroup.Equipment,equip:"Weapon",attack:17,two:true,bow:true);Item("arrow","철 화살","arrow",MaterialKind.Other,ItemGroup.Equipment,equip:"Arrow",arrow:true);
        Item("warhammer","전투 망치","hammer",MaterialKind.Weapon,ItemGroup.Equipment,equip:"Weapon",attack:30,two:true);Item("armor","철 갑옷","shield",MaterialKind.Armor,ItemGroup.Equipment,equip:"Armor",defense:20);
        string[] ids={"burnt","dented","cracked","twisted_leather","tangled","knife_scrap","plane_scrap","saw_scrap","tiny_scrap","stone_scrap"};
        string[] names={"다 타버린 무언가","찌그러진 금속 덩어리","금이 가버린 금속 조각","뒤틀려버린 가죽","알아볼 수 없게 뒤엉킨 무언가","기괴하게 깎여버린 나무 조각","울퉁불퉁 이상한 나무 조각","어중간하게 잘못 잘린 나무 조각","너무 작아져버린 무언가","산산조각난 무언가"};
        for(int i=0;i<ids.Length;i++)Item(ids[i],names[i],"scrap",i>=5&&i<=7?MaterialKind.Wood:MaterialKind.Other,fuel:i>=5&&i<=7?1:0);
        RecipeDefinition Recipe(string id,Station station,string output,string input,int amount=1,int strokes=0,ToolKind tool=ToolKind.Knife)
        {var r=new RecipeDefinition{id=id,station=station,outputId=output,displayName=cat.Item(output).displayName,strokes=strokes,tool=tool};r.ingredients.Add(new Ingredient{itemId=input,count=amount});cat.recipes.Add(r);return r;}
        Recipe("smelt_iron",Station.Furnace,"hot_iron","ore");Recipe("reheat_plate",Station.Furnace,"hot_plate","plate");
        Recipe("knife_handle",Station.Tools,"handle","wood",strokes:3);Recipe("saw_plank",Station.Tools,"plank","wood",strokes:2,tool:ToolKind.Saw);Recipe("plane_wood",Station.Tools,"smooth_wood","wood",strokes:2,tool:ToolKind.Plane);Recipe("hammer_stone",Station.Tools,"stone_piece","stone",strokes:3,tool:ToolKind.Hammer);Recipe("grind_blade",Station.Tools,"blade","plate",strokes:3,tool:ToolKind.Whetstone);
        var anvil=Recipe("forge_plate",Station.Anvil,"hot_plate","hot_iron");anvil.anvilHits=new[]{4,1,0,0,0};anvil.symmetricAnvil=true;
        Recipe("quench_plate",Station.Quench,"plate","hot_plate");Recipe("quench_iron",Station.Quench,"ingot","hot_iron");Recipe("quench_leather",Station.Quench,"leather","leather_prepared");
        var sword=Recipe("assemble_sword",Station.Workbench,"sword","blade");sword.ingredients.Add(new Ingredient{itemId="handle",count=1});
        Recipe("assemble_shield",Station.Workbench,"shield","plate",2);Recipe("assemble_armor",Station.Workbench,"armor","plate",3);
        var arrowRecipe=Recipe("assemble_arrow",Station.Workbench,"arrow","handle");arrowRecipe.ingredients.Add(new Ingredient{itemId="stone_piece",count=1});
        if(!fixture)AssetDatabase.CreateAsset(cat,path);return cat;
    }
    public static void RunDomainTests(BlacksmithCatalog cat)
    {
        int passed=0;void Check(bool ok,string name){if(!ok)throw new Exception("TEST FAILED: "+name);passed++;}
        var d=BlacksmithSave.CreateDemo();var inv=new InventoryService(d,cat);var craft=new CraftingService(inv);
        var wood=d.chest.Find(x=>x.itemId=="wood");int count=wood.count;Check(inv.Select(wood,Station.Tools),"select");Check(wood.count==count-1,"selection deducts");Check(!inv.Select(d.chest.Find(x=>x.itemId=="ore"),Station.Tools),"single material limit");inv.ReturnAll();Check(wood.count==count,"cancel conserves");
        d.fuel=48;Check(inv.AddFuel(d.chest.Find(x=>x.itemId=="coal"))&&d.fuel==50,"fuel clamps");int coal=d.chest.Find(x=>x.itemId=="coal").count;Check(!inv.AddFuel(d.chest.Find(x=>x.itemId=="coal"))&&coal==d.chest.Find(x=>x.itemId=="coal").count,"full fuel consumes nothing");
        inv.Select(d.chest.Find(x=>x.itemId=="ore"),Station.Furnace);d.fuel=2;Check(!craft.Begin(Station.Furnace,ToolKind.Knife,out _),"insufficient fuel");d.fuel=3;Check(craft.Begin(Station.Furnace,ToolKind.Knife,out _)&&d.fuel==0,"fuel consumption");Check(craft.Finish().stack.itemId=="hot_iron","smelting recipe");
        inv.Select(d.chest.Find(x=>x.itemId=="wood"),Station.Tools);craft.Begin(Station.Tools,ToolKind.Knife,out _);craft.Stroke();craft.Stroke();craft.Stroke();Check(craft.Finish().stack.itemId=="handle","knife count");
        inv.Select(d.chest.Find(x=>x.itemId=="ore"),Station.Tools);craft.Begin(Station.Tools,ToolKind.Knife,out _);Check(!craft.Stroke()&&craft.Strokes==0,"invalid tool material");Check(craft.Finish().stack.itemId=="knife_scrap","tool byproduct");
        inv.Select(d.chest.Find(x=>x.itemId=="hot_iron"),Station.Anvil);craft.Begin(Station.Anvil,ToolKind.Knife,out _);for(int i=0;i<4;i++)craft.Hit(0);craft.Hit(1);Check(!craft.Hit(2),"anvil max five");Check(craft.Finish().stack.itemId=="hot_plate","anvil pattern");
        inv.Select(d.chest.Find(x=>x.itemId=="hot_plate"),Station.Quench);craft.Begin(Station.Quench,ToolKind.Knife,out _);Check(craft.Finish(false).stack.itemId=="cracked","failed quench");
        inv.Select(d.chest.Find(x=>x.itemId=="wood"),Station.Quench);craft.Begin(Station.Quench,ToolKind.Knife,out _);Check(craft.Finish(false).stack.itemId=="wood","unheated returned");
        Check(QualityRules.FromAverage(4)==Quality.Finest&&QualityRules.FromAverage(3)==Quality.High&&QualityRules.FromAverage(2)==Quality.Medium&&QualityRules.FromAverage(1.99f)==Quality.Low,"quality boundaries");
        var recipe=cat.recipes.Find(x=>x.id=="assemble_shield");Check(inv.FillRecipe(recipe,1),"fill recipe");craft.Begin(Station.Workbench,ToolKind.Knife,out _);Check(craft.NeedsTiming()&&craft.Targets==2,"equipment timing targets");craft.Timing(0);craft.Timing(0);Check(craft.Finish().stack.quality==Quality.Finest,"perfect equipment");
        var shield=d.chest.Find(x=>x.itemId=="shield");Check(inv.Equip(shield),"equip shield");Check(inv.Equip(d.chest.Find(x=>x.itemId=="bow")),"equip bow");Check(!d.equipment.Any(x=>x.slot=="Shield"),"two handed returns shield");Check(inv.Equip(d.chest.Find(x=>x.itemId=="arrow")),"bow arrows");inv.Unequip("Weapon");Check(!d.equipment.Any(x=>x.slot=="Arrow"),"unequip bow returns arrows");
        inv.Select(d.chest.Find(x=>x.itemId=="wood"),Station.Tools);var before=inv.Selection[0].count;Check(!inv.FillRecipe(recipe,999)&&inv.Selection[0].count==before,"failed auto fill preserves selection");inv.ReturnAll();
        Debug.Log("BLACKSMITH_DOMAIN_TESTS: "+passed+" passed");
    }
}
