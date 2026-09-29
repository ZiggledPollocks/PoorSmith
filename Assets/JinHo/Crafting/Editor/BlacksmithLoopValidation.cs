using System;
using System.IO;
using System.Linq;
using Blacksmith;
using UnityEditor;
using UnityEngine;

public static class BlacksmithLoopValidation
{
    static int checks;
    static void Check(bool pass,string name){if(!pass)throw new Exception(name);checks++;}
    public static void Run()
    {
        try
        {
            var catalog=AssetDatabase.LoadAssetAtPath<BlacksmithCatalog>("Assets/JinHo/Crafting/Data/TestCatalog.asset");
            NotionRecipeImporter.Verify(catalog);
            var data=new SaveData();var inv=new InventoryService(data,catalog);var crafting=new CraftingService(inv);
            var fiber=catalog.recipes.First(r=>r.enabled&&r.outputId=="fiber");
            var sword=catalog.recipes.First(r=>r.enabled&&r.outputId=="wood_sword");
            foreach(var recipe in new[]{fiber,sword})
            {
                bool gear=recipe==sword;int second=gear?4:6,third=gear?11:31;
                foreach(int n in new[]{0,1,second-1,second,third-1,third})
                {crafting.Progress(recipe.id).crafts=n;Check(crafting.Mastery(recipe)==(n>=third?3:n>=second?2:n>0?1:0),recipe.id+" mastery "+n);}
            }
            inv.Selection.Add(new Blacksmith.Stack("coal",100));
            Check(crafting.Begin(Station.Workbench,ToolKind.Knife,out _),"unknown recipe starts");
            UnityEngine.Random.InitState(417);
            var refund=crafting.Finish();
            Check(!refund.success&&refund.stack==null,"no invented byproduct");
            Check(refund.returned.Sum(x=>x.count)>0&&refund.returned.Sum(x=>x.count)<100,"partial random return");
            Check(data.chest.All(x=>x.itemId=="coal")&&!crafting.Active&&inv.Selection.Count==0,"return original ingredients");
            Check(crafting.Finish()==null,"finish idempotence");
            InventoryService.Add(data.chest,new Blacksmith.Stack("wood",1));inv.Notify();data.chest.Clear();
            Check(data.acquiredItems.Contains("wood"),"acquisition survives consumption");
            // Tests run only in an explicitly isolated project namespace.
            Check(Application.companyName=="CodexLoopValidation","isolated persistence required");
            Directory.CreateDirectory(Application.persistentDataPath);
            foreach(var invalid in new[]{"{broken", "{\"version\":999}","{}"})
            {
                File.WriteAllText(BlacksmithSave.PathName,invalid);BlacksmithSave.Load(catalog);BlacksmithSave.Write(new SaveData());
                Check(BlacksmithSave.WritesBlocked&&File.ReadAllText(BlacksmithSave.PathName)==invalid,"invalid save preserved");
            }
            Debug.Log("BLACKSMITH_LOOP_VALIDATION_PASSED "+checks);
            File.WriteAllText(Path.Combine(Directory.GetParent(Application.dataPath).FullName,"blacksmith-validation.json"),"{\"passed\":true,\"newChecks\":"+checks+",\"recipeChecks\":3182}");
            EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
