#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Repeatable file-transaction regression checks in an owned temporary directory.</summary>
public static class ReleaseStabilityValidation
{
    [MenuItem("Tools/PoorSmith/Validate release save transactions")]
    public static void RunMenu() { Debug.Log("Release save checks passed: "+Run()); }
    public static int Run()
    {
        int checks=0;
        void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);checks++;}
        Check(ReleaseDiagnostics.Folder==Path.Combine(Application.temporaryCachePath,"BatterMapDiagnostics"),"Editor log button uses the isolated recorded log folder");
        string parent=Path.Combine(Application.temporaryCachePath,"ReleaseSaveChecks");
        string root=Path.Combine(parent,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            var p=new SmithingLoop.Progress();p.version=1;
            string legacy=JsonUtility.ToJson(p,true);
            Check(ReleaseProgressFiles.Decode(legacy,out var migrated,out _)&&migrated.version==2,"Version 1 migration");
            p.version=99;Check(!ReleaseProgressFiles.Decode(JsonUtility.ToJson(p),out _,out var unsupported)&&unsupported.Contains("새로운"),"Refuse future version");
            Check(!ReleaseProgressFiles.Decode("{broken",out _,out _),"Refuse corrupt JSON");
            Check(AutoSaveHistory.List(root).Count==0,"No fabricated save history");
            for(int day=1;day<=8;day++)AutoSaveHistory.Save(root,legacy,day);
            Check(AutoSaveHistory.List(root).Count==8,"Eight histories");
            var files=AutoSaveHistory.List(root).ToDictionary(e=>e.Path,e=>File.ReadAllBytes(e.Path));
            string primary=Path.Combine(root,AutoSaveHistory.PrimaryName);byte[] old=File.ReadAllBytes(primary);bool failed=false;
            using(var locked=new FileStream(primary,FileMode.Open,FileAccess.Read,FileShare.None))
                try{AutoSaveHistory.Save(root,legacy,9);}catch(IOException){failed=true;}
            Check(failed&&File.ReadAllBytes(primary).SequenceEqual(old)&&files.All(e=>File.Exists(e.Key)&&File.ReadAllBytes(e.Key).SequenceEqual(e.Value)),"Failed write preserves all snapshots");
            AutoSaveHistory.Save(root,legacy,9);Check(AutoSaveHistory.List(root).Count==8&&AutoSaveHistory.List(root).All(e=>e.Day!=1),"Prune only after success");
            AutoSaveHistory.Save(root,legacy,2);Check(AutoSaveHistory.List(root)[0].Day==2,"Save order wins over game day");
            old=File.ReadAllBytes(primary);using(var reset=AutoSaveHistory.BeginNewGame(root)){}
            Check(File.ReadAllBytes(primary).SequenceEqual(old),"Uncommitted reset restores bytes");
            new FileTextStore(Path.Combine(root,"settings-retained.txt")).Write("keep");
            using(var reset=AutoSaveHistory.BeginNewGame(root)){reset.Commit();}
            Check(!File.Exists(primary)&&AutoSaveHistory.List(root).Count==0&&File.ReadAllText(Path.Combine(root,"settings-retained.txt"))=="keep","Reset affects progress only");
            return checks;
        }
        finally
        {
            // Only this invocation's freshly generated child directory is eligible for cleanup.
            if(Path.GetDirectoryName(Path.GetFullPath(root))==Path.GetFullPath(parent)&&Directory.Exists(root))Directory.Delete(root,true);
        }
    }
}
#endif
