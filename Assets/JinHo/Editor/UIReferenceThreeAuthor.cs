using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Authors only presentation guides. Existing screen owners retain data, callbacks and transactions.
public static class UIReferenceThreeAuthor
{
 const string Folder="Assets/JinHo/Resources/UI/Screens/Layouts/";
 static Color Ink=new Color(.933333f,.886275f,.788235f), Surface=new Color(.188235f,.145098f,.121569f), Main=new Color(.141176f,.113725f,.098039f), Brass=new Color(.713725f,.603922f,.384314f);
 static Transform Find(Transform r,string p)=>UIReferencePreview.Child(r,p);
 static UIElementTemplate Rule(Transform t)=>UIReferencePreview.Guide(t);
 static void Box(Transform t,float x0,float y0,float x1,float y1){var r=(RectTransform)t;r.anchorMin=new Vector2(x0,y0);r.anchorMax=new Vector2(x1,y1);r.offsetMin=r.offsetMax=Vector2.zero;Rule(t).placement=true;}
 static void Box(Transform r,string p,float x0,float y0,float x1,float y1)=>Box(Find(r,p),x0,y0,x1,y1);
 static void Text(Transform t,float size){UIReferencePreview.Caption(t,size);t.GetComponent<TMP_Text>().color=Ink;}
 static void Text(Transform r,string p,float size)=>Text(Find(r,p),size);
 static void Button(Transform t){UIReferencePreview.BrownButton(t);foreach(var name in new[]{"Text","Label"}){var label=t.Find(name);if(label==null)continue;Box(label,0,0,1,1);Text(label,26);label.GetComponent<TMP_Text>().alignment=TextAlignmentOptions.Center;}}
 static void Button(Transform r,string p)=>Button(Find(r,p));
 static void Panel(Transform t,bool outer=false){var im=t.GetComponent<UnityEngine.UI.Image>()??t.gameObject.AddComponent<UnityEngine.UI.Image>();im.sprite=outer?GameArtSkin.Current.window:null;im.type=outer?UnityEngine.UI.Image.Type.Sliced:UnityEngine.UI.Image.Type.Simple;im.color=outer?new Color(.60f,.60f,.64f):Surface;im.pixelsPerUnitMultiplier=1;Rule(t).image=Rule(t).imageColor=true;}
 static void Root(Transform root){var r=(RectTransform)root;r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);r.sizeDelta=new Vector2(1560,970);r.anchoredPosition=Vector2.zero;Rule(root).placement=true;}
 static void Common(GameObject root){
  foreach(var tx in root.GetComponentsInChildren<TMP_Text>(true))Text(tx.transform,Mathf.Clamp(tx.fontSize,24,34));
  foreach(var b in root.GetComponentsInChildren<UnityEngine.UI.Button>(true))Button(b.transform);
  foreach(var im in root.GetComponentsInChildren<UnityEngine.UI.Image>(true)){
   string n=im.name;
   if(im.GetComponent<UnityEngine.UI.Button>()!=null||im.GetComponent<Mask>()!=null||im.GetComponent<RectMask2D>()!=null)continue;
   if(n=="BagArt"){var c=im.color;c.a=.045f;im.color=c;Rule(im.transform).imageColor=true;continue;}
   if(n.Contains("Icon")||n.Contains("Station")||n.Contains("Object")||n.Contains("Arrow")||n.Contains("Check")||n.Contains("Handle")||n.Contains("Fill"))continue;
   if(n.Contains("Panel")||n=="PurchaseDetails"||n=="OwnedInventory"||n=="SelectedMaterials"||n=="AutomaticRecipes")Panel(im.transform);
   if(n=="TopEdge"||n=="BottomEdge"){im.color=new Color(.34f,.25f,.17f,.6f);Rule(im.transform).imageColor=true;}
  }
 }
 static void Scroll(Transform r,string path,float x0,float y0,float x1,float y1){
  var t=Find(r,path);Box(t,x0,y0,x1,y1);Box(t,"Viewport",0,0,.97f,1);Box(t,"Scrollbar",.978f,0,1,1);
  var track=Find(t,"Scrollbar").GetComponent<UnityEngine.UI.Image>();if(track!=null){track.sprite=GameArtSkin.Current.scrollTrack;track.color=Main;Rule(track.transform);}
  var handle=Find(t,"Scrollbar/Handle").GetComponent<UnityEngine.UI.Image>();if(handle!=null){handle.sprite=GameArtSkin.Current.grabber;handle.color=Brass;Rule(handle.transform);}
 }
 static void Slots(Transform r,string path,int columns,float cell){
  var t=Find(r,path);var grid=t.GetComponent<GridLayoutGroup>()??t.gameObject.AddComponent<GridLayoutGroup>();grid.cellSize=new Vector2(cell,cell);grid.spacing=new Vector2(12,12);grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=columns;grid.padding=new RectOffset(8,8,8,8);Rule(t).layout=true;
  var item=Find(t,"_ItemStyle");var im=item.GetComponent<UnityEngine.UI.Image>()??item.gameObject.AddComponent<UnityEngine.UI.Image>();im.sprite=GameArtSkin.Current.inset;im.type=UnityEngine.UI.Image.Type.Sliced;im.color=new Color(.52f,.54f,.57f);im.pixelsPerUnitMultiplier=1;
  var guide=Rule(item);guide.placement=false;guide.listItemStyle=true;guide.imageColor=false;
  var icon=Find(item,"Icon");Box(icon,.13f,.13f,.87f,.87f);Rule(icon).image=false;
 }
 static void Edit(string name,Action<Transform> apply){var path=Folder+name+".prefab";var root=PrefabUtility.LoadPrefabContents(path);try{Common(root);apply(root.transform);PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}}
 public static void Author(){
  Edit("WeaponShopBuy",r=>{
   Root(r);Panel(r,true);Box(r,"Title",.035f,.92f,.59f,.98f);Text(r,"Title",34);Box(r,"Gold",.60f,.92f,.825f,.98f);Text(r,"Gold",26);
   Box(r,"Close",.85f,.92f,.965f,.98f);Button(r,"Close");Box(r,"FinishTrade",.83f,.035f,.965f,.105f);Button(r,"FinishTrade");
   Box(r,"Tab_buy",.035f,.825f,.21f,.895f);Box(r,"Tab_sell",.225f,.825f,.40f,.895f);Button(r,"Tab_buy");Button(r,"Tab_sell");Rule(Find(r,"Tab_buy")).imageColor=false;Rule(Find(r,"Tab_sell")).imageColor=false;
   Box(r,"TradeFeedback",.035f,.035f,.80f,.105f);Text(r,"TradeFeedback",24);
   Box(r,"OwnedInventory",.035f,.14f,.30f,.80f);Panel(Find(r,"OwnedInventory"));Box(r,"OwnedInventory/OwnedTitle",.04f,.90f,.96f,.98f);Text(r,"OwnedInventory/OwnedTitle",26);
   Box(r,"OwnedInventory/OwnedTools",.035f,.79f,.965f,.88f);Box(r,"OwnedInventory/OwnedTools/Search",0,0,.46f,1);Box(r,"OwnedInventory/OwnedTools/Sort",.48f,0,.73f,1);Box(r,"OwnedInventory/OwnedTools/Filter",.75f,0,1,1);Button(r,"OwnedInventory/OwnedTools/Sort");Button(r,"OwnedInventory/OwnedTools/Filter");Text(r,"OwnedInventory/OwnedTools/Sort/Label",22);Text(r,"OwnedInventory/OwnedTools/Filter/Label",22);
   Scroll(r,"OwnedInventory/OwnedItems",.035f,.415f,.965f,.765f);Slots(r,"OwnedInventory/OwnedItems/Viewport/Content",3,90);
   Box(r,"OwnedDetails",.035f,.14f,.30f,.40f);Panel(Find(r,"OwnedDetails"));
   Box(r,"PurchaseDetails",.32f,.14f,.615f,.80f);Panel(Find(r,"PurchaseDetails"));
   Box(r,"PurchaseDetails/SelectedIcon",.34f,.71f,.66f,.94f);Rule(Find(r,"PurchaseDetails/SelectedIcon")).image=false;
   Box(r,"PurchaseDetails/SelectedName",.05f,.59f,.95f,.70f);Text(r,"PurchaseDetails/SelectedName",28);
   Box(r,"PurchaseDetails/ProductInfo",.06f,.23f,.94f,.58f);Text(r,"PurchaseDetails/ProductInfo",24);
   Box(r,"PurchaseDetails/Purchase",.16f,.055f,.84f,.17f);Button(r,"PurchaseDetails/Purchase");Text(r,"PurchaseDetails/SelectHint",26);
   Box(r,"ProductPanel",.635f,.14f,.965f,.80f);Panel(Find(r,"ProductPanel"));
   string[] cats={"all","tools","items","bags"};for(int n=0;n<4;n++){float x=n%2==0?.03f:.515f;float y=n<2?.89f:.79f;Box(r,"ProductPanel/Category_"+cats[n],x,y,x+.455f,y+.085f);Button(r,"ProductPanel/Category_"+cats[n]);Rule(Find(r,"ProductPanel/Category_"+cats[n])).imageColor=false;}
   Scroll(r,"ProductPanel/Products",.025f,.025f,.975f,.765f);
   var row=Find(r,"ProductPanel/Products/Viewport/Content/_ItemStyle");Button(row);var g=Rule(row);g.placement=false;g.listItemStyle=true;
   var height=row.GetComponent<LayoutElement>()??row.gameObject.AddComponent<LayoutElement>();height.minHeight=height.preferredHeight=124;height.flexibleWidth=1;g.layout=true;
   Box(row,"Label",.23f,.04f,.97f,.96f);Text(row,"Label",24);Find(row,"Label").GetComponent<TMP_Text>().alignment=TextAlignmentOptions.MidlineLeft;
   Box(row,"ItemIcon",.025f,.20f,.21f,.80f);Rule(Find(row,"ItemIcon")).image=false;
  });
  Edit("SmithySelectionOverlay",r=>{
   Panel(Find(r,"InventoryPanel"));Box(r,"InventoryPanel",.655f,.26f,.965f,.89f);
   Box(r,"InventoryPanel/Heading",.04f,.925f,.96f,.99f);Text(r,"InventoryPanel/Heading",29);
   for(int n=0;n<3;n++){string p="InventoryPanel/Group"+n;Box(r,p,.025f+n*.325f,.835f,.325f+n*.325f,.91f);Button(r,p);Text(r,p+"/Label",24);Rule(Find(r,p)).imageColor=false;}
   Box(r,"InventoryPanel/Search",.025f,.745f,.68f,.815f);Box(r,"InventoryPanel/OwnedSort",.70f,.745f,.98f,.815f);Button(r,"InventoryPanel/OwnedSort");
   for(int n=0;n<7;n++){string p="InventoryPanel/MaterialFilter"+n;float x=.025f+n%4*.245f;float y=n<4?.645f:.565f;Box(r,p,x,y,x+.225f,y+.07f);Button(r,p);Text(r,p+"/Label",24);Rule(Find(r,p)).imageColor=false;}
   Scroll(r,"InventoryPanel/InventoryScroll",.025f,.025f,.975f,.54f);Slots(r,"InventoryPanel/InventoryScroll/Viewport/Content",4,90);
   Box(r,"AutomaticRecipes",.035f,.57f,.29f,.84f);Panel(Find(r,"AutomaticRecipes"));Box(r,"AutomaticRecipes/AutoTitle",.05f,.80f,.95f,.985f);Text(r,"AutomaticRecipes/AutoTitle",29);
   Scroll(r,"AutomaticRecipes/Recipes",.025f,.035f,.975f,.85f);Box(r,"AutomaticRecipes/Locked",.06f,.28f,.94f,.79f);Text(r,"AutomaticRecipes/Locked",24);
   Box(r,"SelectedMaterials",.315f,.35f,.635f,.60f);Panel(Find(r,"SelectedMaterials"));Box(r,"SelectedMaterials/SelectionLabel",.025f,.72f,.975f,.98f);Text(r,"SelectedMaterials/SelectionLabel",24);Box(r,"SelectedMaterials/Slots",.035f,.06f,.965f,.71f);Slots(r,"SelectedMaterials/Slots",4,108);
   Box(r,"SelectionStation",.315f,.82f,.635f,.90f);Text(r,"SelectionStation",32);
   Box(r,"StartCraft",.355f,.225f,.595f,.30f);Button(r,"StartCraft");Box(r,"StartUnavailablePanel",.315f,.145f,.635f,.215f);Panel(Find(r,"StartUnavailablePanel"));Box(r,"StartUnavailable",.325f,.15f,.625f,.21f);Text(r,"StartUnavailable",24);
   Box(r,"CloseSelection",.655f,.145f,.83f,.22f);Box(r,"Reset",.845f,.145f,.965f,.22f);Button(r,"CloseSelection");Button(r,"Reset");
  });
  Edit("SmithySelectionHUD",r=>{Button(r,"Tooltip/SplitStack");Box(r,"Controls",0,0,1,1);Box(r,"Controls/ExperimentHistory",.66f,.935f,.80f,.989f);Button(r,"Controls/ExperimentHistory");Box(r,"Controls/Back",.015f,.07f,.17f,.14f);Button(r,"Controls/Back");Box(r,"Controls/RecipeShortcut",.82f,.938f,.928f,.989f);Text(r,"Controls/RecipeShortcut",24);});
  Edit("SettingsMenu",r=>{
   Root(r);Panel(Find(r,"Background"),true);Panel(Find(r,"ContentArea"));Box(r,"TopTabs",.035f,.83f,.965f,.915f);Box(r,"ContentArea",.035f,.17f,.965f,.80f);
   Box(r,"BackButton",.84f,.93f,.965f,.985f);Button(r,"BackButton");Box(r,"BottomButtons",.035f,.055f,.725f,.135f);Box(r,"MainMenuButton",.745f,.055f,.965f,.135f);Button(r,"MainMenuButton");
   Box(r,"BottomButtons/ResetSettings",0,0,.30f,1);Button(r,"BottomButtons/ResetSettings");Box(r,"BottomButtons/SaveButton",.35f,0,.65f,1);Box(r,"BottomButtons/SaveAndExitButton",.70f,0,1,1);Button(r,"BottomButtons/SaveButton");Button(r,"BottomButtons/SaveAndExitButton");
   var tab=Find(r,"TopTabs/_ItemStyle");Button(tab);var tg=Rule(tab);tg.listItemStyle=true;tg.placement=false;var text=Find(tab,"Text");Box(text,0,0,1,1);Text(text,28);
   foreach(string name in new[]{"MasterVolumeRow","BGMVolumeRow","SFXVolumeRow"}){
    var row=Find(r,"ContentArea/SoundSettingsPanel/"+name);Rule(row).placement=false;var le=row.GetComponent<LayoutElement>()??row.gameObject.AddComponent<LayoutElement>();le.minHeight=le.preferredHeight=138;
    Panel(Find(row,"Label"));Box(row,"Label",.02f,.16f,.25f,.84f);Box(row,"VolumePercentText",.65f,.23f,.75f,.77f);Text(row,"VolumePercentText",26);Box(row,"MuteButton",.79f,.23f,.96f,.77f);Button(row,"MuteButton");
   }
   string content="ContentArea/GameSettingsPanel/GameSettingsScrollView/Viewport/Content/";
   foreach(var name in new[]{"ResolutionRow","ScreenModeRow","AutoSaveRow"}){
    var row=Find(r,content+name);Rule(row).placement=false;var le=row.GetComponent<LayoutElement>()??row.gameObject.AddComponent<LayoutElement>();le.minHeight=le.preferredHeight=126;
    Panel(Find(row,"Label"));Box(row,"Label",.025f,.18f,.26f,.82f);Text(row,"Label/Text",26);Box(Find(row,"Label/Text"),0,0,1,1);Find(row,"Label/Text").GetComponent<TMP_Text>().alignment=TextAlignmentOptions.MidlineLeft;
    if(name=="ResolutionRow"){
     Box(row,"ResolutionDropdown",.33f,.24f,.69f,.76f);Box(row,"ResolutionDropdown/Label",.05f,.08f,.88f,.92f);Text(row,"ResolutionDropdown/Label",26);Box(row,"ApplyButton",.75f,.22f,.96f,.78f);Button(row,"ApplyButton");
    }else{
     Box(row,"PreviousButton",.33f,.22f,.40f,.78f);Box(row,"ValueText",.415f,.22f,.605f,.78f);Box(row,"NextButton",.62f,.22f,.69f,.78f);Button(row,"PreviousButton");Button(row,"NextButton");Text(row,"ValueText",26);
     if(name=="ScreenModeRow"){Box(row,"ApplyButton",.75f,.22f,.96f,.78f);Button(row,"ApplyButton");}
    }
   }
   var release=Find(r,content+"ReleasePreferences");Rule(release).placement=false;var re=release.GetComponent<LayoutElement>()??release.gameObject.AddComponent<LayoutElement>();re.minHeight=re.preferredHeight=190;
   Box(release,"ReleaseValues",.02f,.58f,.98f,.98f);Text(release,"ReleaseValues",24);
   for(int n=0;n<5;n++){string name="ReleaseSetting_"+n;Box(release,name,.015f+n*.197f,.08f,.197f+n*.197f,.50f);Button(release,name);Text(release,name+"/Text",24);}
  });
  AssetDatabase.SaveAssets();Debug.Log("UIReference: three-r4 presentation guides saved; reserved owned details, compact footer, separated crafting regions.");
 }
}
