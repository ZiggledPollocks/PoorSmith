using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Blacksmith;

[InitializeOnLoad]
public static class UIReferencePreview
{
 const string D="C:/Users/Master/Documents/Codex/UIReference_20261102/three-screens";
 const string L="Assets/JinHo/Resources/UI/Screens/Layouts/";
 static readonly string[] Screens={"마을 · 장비 상점","대장간 · 재료 선택","메뉴 · 설정"};
 static readonly BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 static readonly Color Main=Hex("241D19"),Surface=Hex("30251F"),Ink=Hex("EEE2C9"),Muted=Hex("BBAF97"),Brass=Hex("B69A62");
 static double deadline,stepAt; static int step; static UITestSceneController test;
 static UIReferencePreview(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=Changed;
  if(Application.dataPath.Replace('\\','/').StartsWith("C:/programming/Projects/unity/batterMap/",StringComparison.OrdinalIgnoreCase)&&File.Exists(D+"/run-three-v4.flag")){File.Delete(D+"/run-three-v4.flag");EditorApplication.delayCall+=Begin;}
 }
 static Color Hex(string s){ColorUtility.TryParseHtmlString("#"+s,out var c);return c;}
 [Serializable] sealed class SceneRestore { public SceneSetup[] scenes; }
 [MenuItem("Tools/BatterMap/UI Reference/Capture Representative Screens")]
 public static void Begin(){
  if(!Application.dataPath.Replace('\\','/').Equals("C:/programming/Projects/unity/batterMap/Assets",StringComparison.OrdinalIgnoreCase))throw new Exception("Original project required");
  if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop: existing Play session must be preserved");
  for(int n=0;n<UnityEngine.SceneManagement.SceneManager.sceneCount;n++)if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(n).isDirty)throw new Exception("Stop: unsaved scene must be preserved");
  if(File.Exists(D+"/style-brown.flag")){AuthorBrown();File.Delete(D+"/style-brown.flag");}
  if(File.Exists(D+"/style-three.flag")){UIReferenceThreeAuthor.Author();File.Delete(D+"/style-three.flag");}
  if(string.IsNullOrEmpty(SessionState.GetString("UIRef.restore","")))SessionState.SetString("UIRef.restore",JsonUtility.ToJson(new SceneRestore{scenes=EditorSceneManager.GetSceneManagerSetup()}));
  SessionState.SetBool("UIRef.run",true);SessionState.SetString("UIRef.phase","after");
  Size(1280,720);EditorSceneManager.OpenScene("Assets/UITest/UITestScene.unity");EditorApplication.EnterPlaymode();
 }
 static void Changed(PlayModeStateChange state){
  if(!SessionState.GetBool("UIRef.run",false)){if(state==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool("UIRef.failed",false)){SessionState.SetBool("UIRef.failed",false);Restore();}return;}
  if(state==PlayModeStateChange.EnteredPlayMode){SessionState.SetBool("UIRef.background",Application.runInBackground);Application.runInBackground=true;step=0;test=null;deadline=EditorApplication.timeSinceStartup+60;stepAt=0;SessionState.SetInt("UIRef.screen",0);SessionState.SetInt("UIRef.res",0);}
  if(state==PlayModeStateChange.EnteredEditMode){
   Application.runInBackground=SessionState.GetBool("UIRef.background",false);SessionState.SetBool("UIRef.run",false);Restore();
   File.WriteAllText(D+"/original-capture-complete.txt","Actual original-project UI test screenshots. Test data is memory-only. Check text-layout-review.txt; capture is not visual acceptance. Native input unverified.");
  }
 }
 static void Restore(){
  var saved=SessionState.GetString("UIRef.restore","");
  if(!string.IsNullOrEmpty(saved)){SessionState.EraseString("UIRef.restore");EditorSceneManager.RestoreSceneManagerSetup(JsonUtility.FromJson<SceneRestore>(saved).scenes);}
 }
 static void Tick(){
  if(SessionState.GetBool("UIRef.run",false)&&!EditorApplication.isPlayingOrWillChangePlaymode&&File.Exists(D+"/run-three-v4.flag")){SessionState.SetBool("UIRef.run",false);Restore();}
  if(!SessionState.GetBool("UIRef.run",false)&&!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling&&File.Exists(D+"/run-three-v4.flag")){File.Delete(D+"/run-three-v4.flag");EditorApplication.delayCall+=Begin;return;}
  if(!SessionState.GetBool("UIRef.run",false)||!EditorApplication.isPlaying)return;
  EditorApplication.QueuePlayerLoopUpdate();
  try{
   if(test==null)test=UnityEngine.Object.FindFirstObjectByType<UITestSceneController>();
   if(test==null||SmithingLoop.Instance?.Initialized!=true||!(bool)typeof(UITestSceneController).GetField("ready",F).GetValue(test)){if(EditorApplication.timeSinceStartup>deadline)throw new Exception("UITest not ready");return;}
   if(!SmithingLoop.Instance.SaveBlocked)throw new Exception("UITest saving must be blocked");
   int i=SessionState.GetInt("UIRef.screen",0),res=SessionState.GetInt("UIRef.res",0);int[] widths={1280,1440,1920},heights={720,900,1080};string phase=SessionState.GetString("UIRef.phase","");
   if(step==0){typeof(UITestSceneController).GetField("showSelector",F)?.SetValue(test,false);typeof(UITestSceneController).GetMethod("Show",F).Invoke(test,new object[]{Screens[i]});if(i==0){var category=UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None).FirstOrDefault(b=>b.isActiveAndEnabled&&b.name=="Category_tools");category?.onClick.Invoke();}step=1;stepAt=EditorApplication.timeSinceStartup+1;return;}
   if(EditorApplication.timeSinceStartup<stepAt)return;
   if(step==1){if(i==2){var tabs=UnityEngine.Object.FindFirstObjectByType<SettingsMenuUI.SettingsTabController>();tabs?.SelectTab(0);if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(null);}if(i==0){var product=UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None).Where(b=>b.isActiveAndEnabled&&b.name.StartsWith("Goods_")).OrderBy(b=>b.transform.GetSiblingIndex()).FirstOrDefault();product?.onClick.Invoke();}step=2;stepAt=EditorApplication.timeSinceStartup+.8;return;}
   if(step==2){if(Screen.width!=widths[res]||Screen.height!=heights[res]){Size(widths[res],heights[res]);stepAt=EditorApplication.timeSinceStartup+1;return;}Canvas.ForceUpdateCanvases();if(phase=="after")AuditText();if(phase=="after"&&i==0){var c=(CampaignUI)typeof(UITestSceneController).GetField("campaignUI",F).GetValue(test);File.AppendAllText(D+"/measurements.txt",$"{Screen.width}x{Screen.height} slots="+string.Join(";",c.GetComponentsInChildren<GridLayoutGroup>().Select(g=>$"{g.name}:{g.cellSize}:{g.constraintCount}"))+"\n");}ScreenCapture.CaptureScreenshot(D+$"/screen-{i}-{widths[res]}.png");step=3;stepAt=EditorApplication.timeSinceStartup+.5;return;}
   i++;if(i>=Screens.Length){i=0;res++;if(phase=="before"||res>=3){EditorApplication.isPlaying=false;return;}Size(widths[res],heights[res]);stepAt=EditorApplication.timeSinceStartup+.8;}
   SessionState.SetInt("UIRef.screen",i);SessionState.SetInt("UIRef.res",res);step=0;
  }catch(Exception e){Fail(e);}
 }
 // Overflow rendering is not acceptance: report bounds violations before user review.
 static string SpriteName(Sprite sprite)=>sprite!=null?sprite.name:"none";
 static void AuditText(){
  if(Screen.width==1280){var lines=new System.Collections.Generic.List<string>();foreach(var image in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Image>(FindObjectsSortMode.None)){
   if(!image.isActiveAndEnabled||image.color.a<.1f||image.canvas==null||!image.canvas.isActiveAndEnabled)continue;
   var c=new Vector3[4];image.rectTransform.GetWorldCorners(c);if(c[2].x-c[0].x<250||c[2].y-c[0].y<80)continue;
   string path=image.name;for(var t=image.transform.parent;t!=null;t=t.parent)path=t.name+"/"+path;
   lines.Add($"{path} {c[0]} -> {c[2]} sprite={SpriteName(image.overrideSprite)}");
  }File.WriteAllLines(D+"/geometry-"+SessionState.GetInt("UIRef.screen",0)+".txt",lines);}

  foreach(var b in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None))if(b.isActiveAndEnabled&&b.name=="SplitStack")File.AppendAllText(D+"/button-style.txt",$"{Screen.width}x{Screen.height} {b.name} transition={b.transition} sprite={SpriteName(b.image.sprite)} rendered={SpriteName(b.image.overrideSprite)} color={b.image.color}\n");
  foreach(var tx in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)){
   if(!tx.isActiveAndEnabled||tx.canvas==null||!tx.canvas.isActiveAndEnabled||string.IsNullOrWhiteSpace(tx.text)||tx.GetComponentInParent<TMP_InputField>()!=null)continue;
   tx.ForceMeshUpdate();var rect=tx.rectTransform.rect;
   var need=tx.GetPreferredValues(tx.text,Mathf.Max(1,rect.width),Mathf.Infinity);
   bool badMode=tx.overflowMode==TextOverflowModes.Ellipsis||tx.overflowMode==TextOverflowModes.Truncate;
   bool badSize=need.y>rect.height+1;
   bool clipped=false;
   if(badMode||badSize||clipped)File.AppendAllText(D+"/text-layout-review.txt",$"{Screens[SessionState.GetInt("UIRef.screen",0)]} {Screen.width}x{Screen.height} {tx.transform.name} mode={tx.overflowMode} area={rect.size} preferred={need} font={tx.font?.name} text={tx.text.Replace('\n',' ')}\n");
  }
 }
 static void Fail(Exception e){File.WriteAllText(D+"/failure.txt",e.ToString());SessionState.SetBool("UIRef.run",false);SessionState.SetBool("UIRef.failed",true);Debug.LogException(e);if(EditorApplication.isPlaying)EditorApplication.isPlaying=false;else Restore();}
 static void Size(int w,int h){
  var a=typeof(Editor).Assembly;var t=a.GetType("UnityEditor.GameViewSizes");var sing=typeof(ScriptableSingleton<>).MakeGenericType(t);var instance=sing.GetProperty("instance",BindingFlags.Public|BindingFlags.Static).GetValue(null);
  var gt=a.GetType("UnityEditor.GameViewSizeGroupType");var group=t.GetMethod("GetGroup").Invoke(instance,new[]{Enum.ToObject(gt,1)});
  var st=a.GetType("UnityEditor.GameViewSize");var et=a.GetType("UnityEditor.GameViewSizeType");var size=Activator.CreateInstance(st,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,new object[]{Enum.ToObject(et,1),w,h,"UIReference "+w+"x"+h},null);
  group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});var count=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null);var view=EditorWindow.GetWindow(a.GetType("UnityEditor.GameView"));view.GetType().GetProperty("selectedSizeIndex",F).SetValue(view,count-1);view.Repaint();
 }

 // Persist approved appearance in existing guides; never copy live items or button callbacks.
 internal static UIElementTemplate Guide(Transform t){var g=t.GetComponent<UIElementTemplate>()??t.gameObject.AddComponent<UIElementTemplate>();g.appearance=UIElementTemplate.Appearance.PrefabOverrides;g.minimumHeight=0;return g;}
 internal static Transform Child(Transform root,string path){foreach(var name in path.Split('/')){var t=root.Find(name);if(t==null){t=new GameObject(name,typeof(RectTransform)).transform;t.SetParent(root,false);}root=t;}return root;}
 internal static void Caption(Transform t,float size){
  var tx=t.GetComponent<TMP_Text>()??t.gameObject.AddComponent<TextMeshProUGUI>();
  tx.font=GameTextFonts.Primary;tx.fontStyle=FontStyles.Normal;tx.fontSize=tx.fontSizeMin=tx.fontSizeMax=size;tx.enableAutoSizing=false;
  tx.color=Ink;tx.textWrappingMode=TextWrappingModes.Normal;tx.overflowMode=TextOverflowModes.Overflow;tx.margin=new Vector4(8,3,8,3);tx.raycastTarget=false;
  var g=Guide(t);g.typography=g.textColor=true;
 }
 internal static void BrownButton(Transform t){
  var im=t.GetComponent<UnityEngine.UI.Image>()??t.gameObject.AddComponent<UnityEngine.UI.Image>();
  im.sprite=GameArtSkin.Current.tab;im.type=UnityEngine.UI.Image.Type.Sliced;im.pixelsPerUnitMultiplier=1;im.color=new Color(.78f,.78f,.78f,1);
  var button=t.GetComponent<UnityEngine.UI.Button>()??t.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=im;button.transition=Selectable.Transition.ColorTint;button.spriteState=default;
  var c=button.colors;c.normalColor=Color.white;c.highlightedColor=new Color(1.16f,1.13f,1.06f);c.selectedColor=c.highlightedColor;c.pressedColor=new Color(.72f,.72f,.72f);c.disabledColor=new Color(.65f,.65f,.65f);c.fadeDuration=.08f;button.colors=c;
  var guide=Guide(t);guide.image=guide.imageColor=guide.selectable=true;
  var label=Child(t,"Label");var rt=(RectTransform)label;rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;
  Caption(label,26);label.GetComponent<TMP_Text>().alignment=TextAlignmentOptions.Center;
 }
 static void AuthorBrown(){
  foreach(var name in new[]{"TownInventory","OwnedItemDetails"}){
   var path=L+name+".prefab";var root=PrefabUtility.LoadPrefabContents(path);
   try{
    foreach(var label in root.GetComponentsInChildren<TMP_Text>(true))if(label.GetComponentInParent<InventorySlotView>()==null)Caption(label.transform,label.fontSize);
    if(name=="TownInventory"){
     foreach(var button in new[]{"Close","OwnedSourceTools/Sort","OwnedSourceTools/Filter","OwnedChestTools/Sort","OwnedChestTools/Filter"})BrownButton(Child(root.transform,button));
    }else{BrownButton(Child(root.transform,"Tooltip/SplitStack"));}
    PrefabUtility.SaveAsPrefabAsset(root,path);
   }finally{PrefabUtility.UnloadPrefabContents(root);}
  }
  AssetDatabase.SaveAssets();Debug.Log("UIReference: approved brown buttons, cream captions, Hangeuljaemin4 guides saved.");
 }
}

