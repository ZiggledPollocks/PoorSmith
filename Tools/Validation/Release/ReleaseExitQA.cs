#if UNITY_EDITOR
using System;using System.IO;using System.Linq;using System.Reflection;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine.UI;
public static class ReleaseExitQA{
 static int step;static double next,deadline;static bool attempted;static string D=>SessionState.GetString("PoorSmithReleaseQA.Output",null)??throw new InvalidOperationException("Set a fresh isolated QA output directory before running");
 static object Get(object o,string n)=>o.GetType().GetField(n,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
 // Runs only in an isolated Editor; external file locks exercise the real save/UI/exit path.
 public static void Run(){if(PlayerSettings.companyName!="BatterMapValidation")throw new Exception("isolation");SessionState.SetBool("ReleaseExitQA",true);SessionState.SetBool("ReleaseExitAttempt",false);SessionState.SetBool("ReleaseExitHadPrompt",false);EditorSceneManager.OpenScene(TitleSceneController.ScenePath);EditorApplication.isPlaying=true;}
 [InitializeOnLoadMethod]static void Resume(){EditorApplication.playModeStateChanged-=Stopped;EditorApplication.playModeStateChanged+=Stopped;if(!SessionState.GetBool("ReleaseExitQA",false))return;next=EditorApplication.timeSinceStartup+2;deadline=next+100;EditorApplication.update+=Tick;}
 static void Stopped(PlayModeStateChange s){if(s==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool("ReleaseExitAttempt",false)){File.WriteAllText(D+"/exit-stopped.json","{\"stopped\":true,\"expectedFailurePrompt\":"+SessionState.GetBool("ReleaseExitHadPrompt",false).ToString().ToLowerInvariant()+"}");SessionState.SetBool("ReleaseExitQA",false);SessionState.SetBool("ReleaseExitAttempt",false);}}
 static void Tick(){EditorApplication.QueuePlayerLoopUpdate();if(!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+.2;try{if(EditorApplication.timeSinceStartup>deadline)throw new Exception("timeout "+step);switch(step){
 case 0:var t=UnityEngine.Object.FindFirstObjectByType<TitleSceneController>();if(t==null)return;((Button)Get(t,"continueButton")).onClick.Invoke();step=1;break;
 case 1:if(UnityEngine.Object.FindFirstObjectByType<FieldSceneState>()?.Ready==true&&!FieldSceneTravel.Busy){step=2;break;}if(CampaignController.Instance?.Ready!=true)return;if(SmithyInterior.Instance.Inside){SmithyInterior.Instance.Leave();return;}if(CampaignController.Instance.Travelling)return;if(FieldSceneTravel.BeginToField())step=2;break;
 case 2:if(FieldSceneTravel.Busy||UnityEngine.Object.FindFirstObjectByType<FieldSceneState>()?.Ready!=true)return;SmithingLoop.Instance.Campaign.gold=1234;File.WriteAllText(D+"/exit-ready.txt",GameSavePaths.File(AutoSaveHistory.PrimaryName));step=3;break;
 case 3:if(!File.Exists(D+"/exit-lock-ready.flag"))return;SessionState.SetBool("ReleaseExitAttempt",true);SmithingLoop.Instance.Campaign.gold=5432;GameUIController.Instance.Quit();attempted=true;step=4;next+=1;break;
 case 4:var ui=GameUIController.Instance.GetComponent<ReleaseDecisionUI>();bool prompt=ui?.IsOpen==true;if(prompt&&!File.Exists(D+"/exit-failure-popup.png"))ScreenCapture.CaptureScreenshot(D+"/exit-failure-popup.png");SessionState.SetBool("ReleaseExitHadPrompt",prompt);File.WriteAllText(D+"/exit-live.json","{\"isPlaying\":true,\"prompt\":"+prompt.ToString().ToLowerInvariant()+",\"gold\":"+SmithingLoop.Instance.Campaign.gold+"}");if(!File.Exists(D+"/exit-unlocked.flag"))return;if(ui!=null&&ui.IsOpen)((Button)Get(ui,"yes")).onClick.Invoke();else GameUIController.Instance.Quit();step=5;break;
 }}catch(Exception e){File.WriteAllText(D+"/exit-error.txt",e.ToString());SessionState.SetBool("ReleaseExitQA",false);EditorApplication.update-=Tick;EditorApplication.isPlaying=false;}}
}
#endif


