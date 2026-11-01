#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Blacksmith;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
[InitializeOnLoad]
public static class FocusedUIReview
{
    static FocusedUIReview(){if(SessionState.GetBool("FocusedUIReview.Pending",false)){deadline=EditorApplication.timeSinceStartup+40;EditorApplication.update+=Prepare;}}
    static int step;static double deadline;static string output="C:/Users/Master/Documents/Codex/2026-11-01/files-pasted-by-the-user-fix/outputs/video-fixes";
    static BlacksmithController shop;
    static void Guard(){if(!EditorApplication.isPlaying||!SessionState.GetBool("GameplayUIReview.Active",false))throw new InvalidOperationException("Isolated review Play only.");}
    [MenuItem("Tools/BatterMap/Focused UI Review/Start Isolated Selection")]
    static void Start(){if(EditorApplication.isPlaying)throw new InvalidOperationException("Preserve existing Play.");SessionState.SetBool("FocusedUIReview.Pending",true);GameplayUIReviewSession.Normal();step=0;deadline=EditorApplication.timeSinceStartup+40;EditorApplication.update+=Prepare;}
    [MenuItem("Tools/BatterMap/Focused UI Review/Start Isolated Result Capture")]
    static void StartResult(){SessionState.SetBool("FocusedUIReview.AutoResult",true);Start();}
    static void Prepare()
    {
        if(EditorApplication.timeSinceStartup>deadline){SessionState.SetBool("FocusedUIReview.Pending",false);EditorApplication.update-=Prepare;Debug.LogError("Focused review setup timeout");return;}
        if(!EditorApplication.isPlaying)return;
        var loop=SmithingLoop.Instance;var campaign=CampaignController.Instance;
        if(step==0&&loop?.Initialized==true&&campaign?.Ready==true&&!GameUIController.BlocksGameplayInput){campaign.Teleport(SmithyInterior.Instance.entry.position);loop.OpenInteriorPanel(ScreenState.Workshop,false);step=1;}
        if(step==1){shop=UnityEngine.Object.FindFirstObjectByType<BlacksmithController>();if(shop?.Inventory==null)return;step=2;}
        if(step==2){var facility=shop.view.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.name=="Facility_ToolRack");if(facility==null)return;facility.onClick.Invoke();shop.SetState(ScreenState.Selecting);SessionState.SetBool("FocusedUIReview.Pending",false);EditorApplication.update-=Prepare;Capture("selection");if(SessionState.GetBool("FocusedUIReview.AutoResult",false)){SessionState.SetBool("FocusedUIReview.AutoResult",false);EditorApplication.delayCall+=()=>{Result();ThreeSizes();};}}
    }
    [MenuItem("Tools/BatterMap/Focused UI Review/Capture Current")]
    static void Current(){Guard();Capture("current");}
    static void Capture(string name){Directory.CreateDirectory(output);string path=output+"/"+name+"-"+DateTime.Now.ToString("HHmmssfff");ScreenCapture.CaptureScreenshot(path+".png");File.WriteAllText(path+".txt",Screen.width+"x"+Screen.height+"; "+UnityEngine.SceneManagement.SceneManager.GetActiveScene().path+"; "+GameSavePaths.Root+"\n"+string.Join("\n",UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c=>c.isActiveAndEnabled).Select(c=>c.name+" order="+c.sortingOrder+" override="+c.overrideSorting+" rect="+((RectTransform)c.transform).rect))+"\n"+string.Join("\n",UnityEngine.Object.FindObjectsByType<Image>(FindObjectsSortMode.None).Where(i=>i.name=="ModalShade").Select(i=>i.name+" color="+i.color+" rect="+i.rectTransform.rect)));}
    [MenuItem("Tools/BatterMap/Focused UI Review/Show Nested Decision")]
    static void Decision(){Guard();shop=UnityEngine.Object.FindFirstObjectByType<BlacksmithController>();ReleaseDecisionUI.For(shop.gameObject).Show("재료 선택을 유지한 채 확인창을 닫습니다.","확인","취소",()=>Capture("decision-confirm"),()=>Capture("decision-cancel"));Capture("decision-open");}
    [MenuItem("Tools/BatterMap/Focused UI Review/Show Result Fixture")]
    static void Result(){Guard();shop=UnityEngine.Object.FindFirstObjectByType<BlacksmithController>();var result=new CraftResult{stack=new Blacksmith.Stack("wood_sword",1),success=true,message="표시 검증"};typeof(BlacksmithController).GetField("result",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(shop,result);shop.SetState(ScreenState.Result);Capture("result");}
    [MenuItem("Tools/BatterMap/Focused UI Review/Show Tutorial Guidance Fixture")]
    static void Guide(){Guard();var go=new GameObject("Tutorial guidance presentation fixture",typeof(RectTransform));var v=go.AddComponent<StoryTutorialView>();v.Build(null,GameTextFonts.Primary,false,true);v.StartCoroutine(v.Speak(new[]{"에고 망치: 필요한 재료를 선택하고 시작하기를 누르자."},false,true));Capture("guidance");}
    static int captureIndex,capturePhase,originalSize;static double captureAt;
    [MenuItem("Tools/BatterMap/Focused UI Review/Capture Current Three Sizes")]
    static void ThreeSizes(){Guard();captureIndex=capturePhase=0;var type=typeof(Editor).Assembly.GetType("UnityEditor.GameView");var window=EditorWindow.GetWindow(type);originalSize=(int)type.GetProperty("selectedSizeIndex",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).GetValue(window);EditorApplication.update+=CaptureTick;}
    static void CaptureTick()
    {
        if(!EditorApplication.isPlaying){EditorApplication.update-=CaptureTick;return;}
        if(EditorApplication.timeSinceStartup<captureAt)return;
        if(captureIndex>=3){var type=typeof(Editor).Assembly.GetType("UnityEditor.GameView");type.GetProperty("selectedSizeIndex",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SetValue(EditorWindow.GetWindow(type),originalSize);EditorApplication.update-=CaptureTick;return;}
        int[] widths={1920,1440,1280},heights={1080,900,720};
        if(capturePhase==0){typeof(UIReferencePreview).GetMethod("Size",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{widths[captureIndex],heights[captureIndex]});capturePhase=1;captureAt=EditorApplication.timeSinceStartup+.6;}
        else{Capture("three-"+(UnityEngine.Object.FindFirstObjectByType<BlacksmithController>()?.State.ToString()??"world")+"-"+Screen.width+"x"+Screen.height);captureIndex++;capturePhase=0;captureAt=EditorApplication.timeSinceStartup+.3;}
    }

}
#endif
