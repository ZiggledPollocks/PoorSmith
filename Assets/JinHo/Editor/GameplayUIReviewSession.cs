#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Review-only host. Uses actual product scenes; never changes a scene asset or ordinary save.
[InitializeOnLoad]
public static class GameplayUIReviewSession
{
    const string Active="GameplayUIReview.Active", Root="GameplayUIReview.Root", Mode="GameplayUIReview.Mode";
    const string GodRoot="batterMap.EditorGodMode.Root";
    static GameplayUIReviewSession()
    {
        // Install after the existing session cleanup so its EnteredPlayMode erase cannot drop our guard.
        _ = EditorGodModeSession.IsActive;
        EditorApplication.playModeStateChanged += state =>
        {
            if(!SessionState.GetBool(Active,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)InstallRoot();
            if(state==PlayModeStateChange.EnteredEditMode){SessionState.EraseString(GodRoot);SessionState.SetBool(Active,false);}
        };
    }
    [MenuItem("Tools/BatterMap/Gameplay UI Review/Play Current Scene - Isolated Progress")]
    public static void Normal()=>Begin(false);
    [MenuItem("Tools/BatterMap/Gameplay UI Review/Play Current Scene - New Tutorial")]
    public static void Tutorial()=>Begin(true);
    [MenuItem("Tools/BatterMap/Gameplay UI Review/Play Park Deoksu Checkpoint")]
    static void ParkCheckpoint()=>Begin(false,"C:/Users/Master/Documents/Codex/GameplayUI_20261102/computer-use/park-checkpoint.json");
    static void Begin(bool tutorial, string fixture="Assets/UITest/Data/UITestSave.json")
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Existing Play must be preserved.");
        for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Unsaved scene must be preserved.");
        string root=Path.Combine("C:/Users/Master/Documents/Codex/GameplayUI_20261102","computer-use",Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        if(!tutorial)File.Copy(fixture,Path.Combine(root,AutoSaveHistory.PrimaryName));
        SessionState.SetString(Root,root);SessionState.SetBool(Mode,tutorial);SessionState.SetBool(Active,true);
        EditorApplication.EnterPlaymode();
    }
    static void InstallRoot()=>SessionState.SetString(GodRoot,SessionState.GetString(Root,""));
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void BeforeScene()
    {
        if(!SessionState.GetBool(Active,false))return;
        InstallRoot();
        if(SessionState.GetBool(Mode,false))StoryTutorial.PrepareNewGame();else StoryTutorial.PrepareContinue();
    }
    [MenuItem("Tools/BatterMap/Gameplay UI Review/Preview Park Deoksu")]
    static void PreviewNpc()
    {
        if(!EditorApplication.isPlaying||!SessionState.GetBool(Active,false))throw new InvalidOperationException("Review Play required.");
        var go=new GameObject("Park Deoksu Presentation Review",typeof(RectTransform));
        var view=go.AddComponent<StoryTutorialView>();
        var actor=UnityEngine.Object.FindFirstObjectByType<PlayerAssimilate>();
        view.Build(actor!=null?actor.GetComponentInChildren<SpriteRenderer>()?.sprite:null,GameTextFonts.Primary,false,true);
        view.StartCoroutine(NpcSequence(view));
    }
    static System.Collections.IEnumerator NpcSequence(StoryTutorialView view)
    {
        yield return view.Visitor(true);
        yield return view.Speak(new[]{"박덕수: 찾아왔네. NPC 스프라이트 표시 확인."},false);
        yield return view.Speak(new[]{"박덕수: 작은 대화창 NPC 스프라이트 표시 확인."},false,true);
        yield return view.Visitor(false);
        UnityEngine.Object.Destroy(view.gameObject);
    }
    [MenuItem("Tools/BatterMap/Gameplay UI Review/Capture Current Game View")]
    static void Capture()
    {
        if(!EditorApplication.isPlaying||!SessionState.GetBool(Active,false))throw new InvalidOperationException("Review Play required.");
        string path=Path.Combine(SessionState.GetString(Root,""),"screen-"+DateTime.Now.ToString("HHmmssfff")+".png");
        ScreenCapture.CaptureScreenshot(path);
        File.WriteAllText(path+".txt",Screen.width+"x"+Screen.height+" scene="+UnityEngine.SceneManagement.SceneManager.GetActiveScene().path+" root="+GameSavePaths.Root);
        Debug.Log("Review capture: "+path);
    }
}
#endif

