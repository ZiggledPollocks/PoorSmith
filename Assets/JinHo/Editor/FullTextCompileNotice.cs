using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using TMPro;

// One-shot compile evidence only. No Play session, scene reload, screenshot or save operation.
[InitializeOnLoad]
public static class FullTextCompileNotice
{
 const string Folder="C:/Users/Master/Documents/Codex/UIFullText_20261102";
 [Serializable] sealed class Result { public bool compiled,updatedExistingUI;public string utc,project,runtimeAssembly,editorAssembly,primaryFont; }
 static FullTextCompileNotice(){EditorApplication.update+=Ready;}
 static void Ready()
 {
  if(EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists(Folder+"/compile-request.flag"))return;
  if(!Application.dataPath.Replace('\\','/').Equals("C:/programming/Projects/unity/batterMap/Assets",StringComparison.OrdinalIgnoreCase))return;
  // A loaded old Editor assembly can still tick after a failed compile. Never accept its evidence.
  var projectRoot=Path.GetDirectoryName(Application.dataPath);
  var built=File.GetLastWriteTimeUtc(typeof(FullTextLayout).Assembly.Location);
  foreach(var file in new[]{"Assets/JinHo/Scripts/UI/Common/FullTextLayout.cs","Assets/JinHo/Scripts/UI/Common/UIElementTemplate.cs","Assets/SettingsMenuUI/Scripts/Display/ReleasePreferences.cs","Assets/SettingsMenuUI/Scripts/Input/ControlSettingsController.cs","Assets/JinHo/Scripts/UI/Navigation/TitleSceneController.cs"})
   if(File.GetLastWriteTimeUtc(Path.Combine(projectRoot,file))>built)return;
  var editorBuilt=File.GetLastWriteTimeUtc(typeof(FullTextCompileNotice).Assembly.Location);
  if(File.GetLastWriteTimeUtc(Path.Combine(projectRoot,"Assets/JinHo/Editor/FullTextCompileNotice.cs"))>editorBuilt)return;
  if(EditorApplication.isPlaying){
   FullTextLayout.RefreshLoadedUI();
   foreach(var text in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include,FindObjectsSortMode.None)){
    bool title=false;for(var t=text.transform;t!=null;t=t.parent)if(t.name=="TitleMainMenu"||t.name=="TitleSaveList"||t.name=="TitleInformation"){title=true;break;}
    if(title)text.fontStyle=FontStyles.Normal;
   }
  }
  var result=new Result{updatedExistingUI=EditorApplication.isPlaying,primaryFont=GameTextFonts.Primary!=null?GameTextFonts.Primary.name:"missing",compiled=true,utc=DateTime.UtcNow.ToString("O"),project=Application.dataPath,
   runtimeAssembly=typeof(FullTextLayout).Assembly.Location,editorAssembly=typeof(FullTextCompileNotice).Assembly.Location};
  File.WriteAllText(Folder+"/compile-result.json",JsonUtility.ToJson(result,true));
  File.Delete(Folder+"/compile-request.flag");EditorApplication.update-=Ready;
  Debug.Log("UI full-text policy: compile/import ready. Runtime and visual verification intentionally not performed.");
 }
}
