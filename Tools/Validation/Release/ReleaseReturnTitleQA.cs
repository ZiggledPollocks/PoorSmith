#if UNITY_EDITOR
using System;using System.IO;using System.Reflection;using System.Linq;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine.UI;using UnityEngine.EventSystems;using Object=UnityEngine.Object;
public static class ReleaseReturnTitleQA{
 static int step;static double next,deadline;static string D=>SessionState.GetString("PoorSmithReleaseQA.Output",null)??throw new InvalidOperationException("Set a fresh isolated QA output directory before running");static string before;static object Get(object o,string n)=>o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);static void Click(Button b){if(b==null||!b.IsInteractable()||!b.gameObject.activeInHierarchy)throw new Exception("UI not actionable "+b?.name);var e=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left};ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerClickHandler);}
 // Runs only in an isolated Editor; external file locks exercise the real save/UI/exit path.
 public static void Run(){if(PlayerSettings.companyName!="BatterMapValidation")throw new Exception("isolation");SessionState.SetBool("ReleaseReturnTitleQA",true);EditorSceneManager.OpenScene(TitleSceneController.ScenePath);EditorApplication.isPlaying=true;}
 [InitializeOnLoadMethod]static void Resume(){if(!SessionState.GetBool("ReleaseReturnTitleQA",false))return;next=EditorApplication.timeSinceStartup+2;deadline=next+100;EditorApplication.update+=Tick;}
 static void Tick(){EditorApplication.QueuePlayerLoopUpdate();if(!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+.2;try{if(EditorApplication.timeSinceStartup>deadline)throw new Exception("timeout "+step);switch(step){
 case 0:var t=Object.FindFirstObjectByType<TitleSceneController>();if(t==null)return;Click((Button)Get(t,"continueButton"));step=1;break;
 case 1:if(Object.FindFirstObjectByType<FieldSceneState>()?.Ready==true&&!FieldSceneTravel.Busy){step=2;break;}if(CampaignController.Instance?.Ready==true&&!FieldSceneTravel.Busy)FieldSceneTravel.BeginToField();break;
 case 2:GameUIController.Instance.OpenSettings();File.WriteAllText(D+"/return-ready.txt",GameSavePaths.File(AutoSaveHistory.PrimaryName));step=3;break;
 case 3:if(!File.Exists(D+"/return-lock-ready.flag"))return;SmithingLoop.Instance.Campaign.gold=6543;Click((Button)Get(GameUIController.Instance,"mainMenuButton"));step=4;next+=1;break;
 case 4:var popup=GameUIController.Instance.GetComponent<ReleaseDecisionUI>();if(popup?.IsOpen!=true)throw new Exception("failure prompt missing");File.WriteAllText(D+"/return-live.json","{\"fieldKept\":true,\"popup\":true,\"gold\":"+SmithingLoop.Instance.Campaign.gold+"}");if(!File.Exists(D+"/return-unlocked.flag"))return;Click((Button)Get(popup,"yes"));step=5;next+=1;break;
 case 5:t=Object.FindFirstObjectByType<TitleSceneController>();if(t==null)return;var saved=ReleaseProgressFiles.Read(GameSavePaths.File(AutoSaveHistory.PrimaryName));if(!saved.Valid||saved.progress.campaign.gold!=6543)throw new Exception("retry state not saved");Click((Button)Get(t,"continueButton"));step=6;next+=1;break;
 case 6:if(Object.FindFirstObjectByType<FieldSceneState>()?.Ready!=true)return;if(SmithingLoop.Instance.Campaign.gold!=6543||GameUIController.BlocksGameplayInput||Time.timeScale<=0)throw new Exception("resume mismatch/input lock");Finish(true,"Actual settings Main Menu button failed-save popup/retry, title and field Continue; isolated locked-save reproduction.");break;
 }}catch(Exception e){Finish(false,e.ToString());}}
 static void Finish(bool ok,string m){SessionState.SetBool("ReleaseReturnTitleQA",false);EditorApplication.update-=Tick;File.WriteAllText(D+"/return-title.json",JsonUtility.ToJson(new Report{passed=ok,message=m},true));EditorApplication.isPlaying=false;}
 [Serializable]class Report{public bool passed;public string message;}
}
#endif
