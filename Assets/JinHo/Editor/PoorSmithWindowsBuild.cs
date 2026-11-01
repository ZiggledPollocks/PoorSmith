using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Requested Windows build; preserves open scenes, product settings and ordinary saves.
[InitializeOnLoad]
public static class PoorSmithWindowsBuild
{
    const string Request="C:/Users/Master/Documents/Codex/ReleasePoorSmith_20261102/build-request.json";
    [Serializable] sealed class Job { public string output,report; }
    [Serializable] sealed class Result { public string result,output,error;public int errors,warnings;public string[] scenes;public double seconds; }
    static PoorSmithWindowsBuild(){EditorApplication.update+=Tick;}
    static void Tick()
    {
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists(Request))return;
        if(!Application.dataPath.Replace('\\','/').Equals("C:/programming/Projects/unity/batterMap/Assets",StringComparison.OrdinalIgnoreCase))return;
        var job=JsonUtility.FromJson<Job>(File.ReadAllText(Request));
        if(EditorApplication.isPlayingOrWillChangePlaymode){File.WriteAllText(job.report,JsonUtility.ToJson(new Result{result="blocked",error="Existing Play session preserved"},true));return;}
        for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)
            if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty){File.WriteAllText(job.report,JsonUtility.ToJson(new Result{result="blocked",error="Unsaved scene preserved"},true));return;}
        File.Delete(Request);EditorApplication.update-=Tick;
        try{
            Directory.CreateDirectory(Path.GetDirectoryName(job.output));
            var scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray();
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=scenes,locationPathName=job.output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            var s=report.summary;
            File.WriteAllText(job.report,JsonUtility.ToJson(new Result{result=s.result.ToString(),output=job.output,errors=(int)s.totalErrors,warnings=(int)s.totalWarnings,scenes=scenes,seconds=s.totalTime.TotalSeconds},true));
            Debug.Log("PoorSmith Windows build: "+s.result+" -> "+job.output);
        }catch(Exception e){File.WriteAllText(job.report,JsonUtility.ToJson(new Result{result="failed",error=e.ToString()},true));Debug.LogException(e);}
    }
}
