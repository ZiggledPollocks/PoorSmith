#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Repeatable file-transaction regression checks in an owned temporary directory.</summary>
public static class ReleaseStabilityValidation
{
    [Serializable] sealed class StackSnapshot { public System.Collections.Generic.List<Blacksmith.Stack> stacks; }
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
            string imported=Path.Combine(root,"legacy-import.json");File.WriteAllText(imported,legacy);
            ReleaseProgressFiles.Install(ReleaseProgressFiles.Read(imported),root);
            Check(File.ReadAllText(imported)==legacy&&Directory.GetFiles(Path.Combine(root,"Migrations")).Length==1,"Import keeps version1 original and migration backup");
            new FileTextStore(primary).Write("{corrupt");
            Check(!ReleaseProgressFiles.Read(primary).Valid&&ReleaseProgressFiles.Read(primary+".bak").Valid,"Corrupt primary recognized with valid previous backup");
            ReleaseProgressFiles.Install(ReleaseProgressFiles.Read(primary+".bak"),root);
            Check(ReleaseProgressFiles.Read(primary).Valid&&Directory.GetFiles(Path.Combine(root,"Recovery")).Length>0,"Validated backup recovery archives corrupt original");
            string future=Path.Combine(root,"future.json");File.WriteAllText(future,"{\"version\":99}");
            Check(!ReleaseProgressFiles.Read(future).Valid&&File.ReadAllText(future)=="{\"version\":99}","Unsupported future save remains untouched");
            var catalog=Resources.Load<SmithingLoopContent>("SmithingLoopContent").catalog;
            var recipe=catalog.recipes.First(r=>r.enabled&&r.station==Blacksmith.Station.Tools&&r.outputId=="plank");
            var data=new Blacksmith.SaveData{night=true};var inventory=new Blacksmith.InventoryService(data,catalog);
            foreach(var ingredient in recipe.ingredients)inventory.Selection.Add(new Blacksmith.Stack(ingredient.itemId,ingredient.count));
            Blacksmith.InventoryService.Add(data.chest,new Blacksmith.Stack(recipe.outputId,int.MaxValue,Blacksmith.Quality.High));
            var craft=new Blacksmith.CraftingService(inventory);
            Check(craft.Begin(recipe.station,recipe.tool,out _,recipe)&&craft.Stroke(),"Capacity regression starts actual processing");
            string selection=JsonUtility.ToJson(new StackSnapshot{stacks=inventory.Selection});
            bool overflow=false;try{craft.Finish();}catch(OverflowException){overflow=true;}
            Check(overflow&&selection==JsonUtility.ToJson(new StackSnapshot{stacks=inventory.Selection})&&
                data.chest.Single().count==int.MaxValue&&!data.progress.Any(x=>x.crafts>0),"Full output stack preserves inputs/progress");
            data.chest.Clear();var result=craft.Finish();
            Check(result.success&&data.chest.Sum(x=>x.count)==recipe.outputCount&&inventory.Selection.Count==0,"Retry commits once after capacity is available");
            Check(craft.Finish()==null,"Capacity retry cannot duplicate output");
            var furnace=catalog.recipes.First(r=>r.enabled&&r.station==Blacksmith.Station.Furnace&&r.ingredients.Count==1);
            var fuelData=new Blacksmith.SaveData{night=true,fuel=50};var fuelInventory=new Blacksmith.InventoryService(fuelData,catalog);
            int largeCount=checked(((715827883+furnace.ingredients[0].count-1)/furnace.ingredients[0].count)*furnace.ingredients[0].count);
            fuelInventory.Selection.Add(new Blacksmith.Stack(furnace.ingredients[0].itemId,largeCount));
            Check(!new Blacksmith.CraftingService(fuelInventory).Begin(furnace.station,furnace.tool,out var fuelError,furnace)&&fuelError.Contains("연료")&&fuelData.fuel==50&&
                fuelInventory.Selection.Single().count==largeCount,"Large furnace input cannot overflow cost or create fuel");
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

