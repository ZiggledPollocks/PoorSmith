using System;
using System.Linq;
using Blacksmith;
using UnityEditor;
using UnityEngine;

public static class NotionRecipeImporter
{
    [MenuItem("Blacksmith/Import CSV content")]
    public static void Import()
    {
        var catalog=AssetDatabase.LoadAssetAtPath<BlacksmithCatalog>("Assets/JinHo/Crafting/Data/TestCatalog.asset");
        if(!catalog)throw new Exception("Build the Blacksmith scene first.");
        Apply(catalog);Verify(catalog);
    }
    public static void Apply(BlacksmithCatalog catalog)
    {
        CsvContentImporter.Apply(catalog);
    }
    public static void Verify(BlacksmithCatalog catalog)
    {
        int checks=0;
        void Check(bool ok,string name){if(!ok)throw new Exception("NOTION RECIPE TEST FAILED: "+name);checks++;}
        Check(catalog.recipes.Select(x=>x.id).Distinct().Count()==catalog.recipes.Count,"unique IDs");
        foreach(var r in catalog.recipes)
        {
            Check(catalog.Item(r.outputId)!=null&&r.ingredients.All(i=>i.count>0&&catalog.Item(i.itemId)!=null),r.id+" references");
            Check(r.outputCount>0,r.id+" output count");
            if(!r.enabled)continue;
            Check(r.station!=Station.Tools||(r.strokes>=1&&r.maxStrokes<=10&&r.maxStrokes>=r.strokes),r.id+" range");
            Check(r.station!=Station.Anvil||r.anvilHits.Sum()==5,r.id+" anvil total");
            foreach(int repetitions in new[]{1,2})
            foreach(int strokeCount in new[]{r.strokes,r.maxStrokes}.Distinct())
            {
                var d=new SaveData{fuel=50};var inv=new InventoryService(d,catalog);var craft=new CraftingService(inv);
                foreach(var i in r.ingredients)InventoryService.Add(d.chest,new Stack(i.itemId,i.count*repetitions));
                Check(inv.FillRecipe(r,repetitions),r.id+" selection");
                Check(craft.Begin(r.station,r.tool,out _,r),r.id+" begin");
                if(r.station==Station.Tools)for(int n=0;n<strokeCount;n++)Check(craft.Stroke(),r.id+" stroke allowed");
                if(r.station==Station.Anvil)for(int p=0;p<5;p++)for(int n=0;n<r.anvilHits[p];n++)Check(craft.Hit(p),r.id+" hit allowed");
                if(r.station==Station.Workbench&&craft.NeedsTiming())for(int n=0;n<craft.Targets;n++)craft.Timing(0);
                var result=craft.Finish();
                Check(result.success&&result.recipeId==r.id&&result.stack.itemId==r.outputId&&result.stack.count==r.outputCount*repetitions,r.id+" production");
                Check(inv.Selection.Count==0&&d.chest.Sum(s=>s.count)==r.outputCount*repetitions,r.id+" conservation");
                if(r.station==Station.Furnace)Check(d.fuel==50-r.ingredients.Sum(i=>i.count)*repetitions*3,r.id+" fuel");
            }
            if(r.station==Station.Tools)
            foreach(int outside in new[]{r.strokes-1,r.maxStrokes+1}.Where(n=>n>=0&&n<=10))
            {
                var inv=new InventoryService(new SaveData(),catalog);var craft=new CraftingService(inv);
                foreach(var i in r.ingredients)inv.Selection.Add(new Stack(i.itemId,i.count));
                craft.Begin(r.station,r.tool,out _);for(int n=0;n<outside;n++)craft.Stroke();
                var result=craft.Finish();Check(result.recipeId!=r.id,r.id+" rejects outside range");
            }
        }
        // Exact ingredient ratios: a partially supplied arrow recipe must not consume as a successful recipe.
        var arrow=catalog.recipes.First(r=>r.outputId=="arrow");
        var bad=new InventoryService(new SaveData(),catalog);
        foreach(var i in arrow.ingredients)bad.Selection.Add(new Stack(i.itemId,i.count));
        bad.Selection[0].count++;
        var invalid=new CraftingService(bad);invalid.Begin(Station.Workbench,ToolKind.Knife,out _);
        Check(!invalid.Finish().success,"reject unmatched ingredient ratios");
        var ambiguous=new InventoryService(new SaveData(),catalog);
        ambiguous.Selection.Add(new Stack("leather",2));ambiguous.Selection.Add(new Stack("thread",2));
        var ambiguousCraft=new CraftingService(ambiguous);
        Check(!ambiguousCraft.Begin(Station.Workbench,ToolKind.Knife,out _)&&!ambiguousCraft.Active&&ambiguous.Selection.Sum(x=>x.count)==4,"ambiguous outputs need explicit choice and preserve ingredients");
        var fiber=catalog.recipes.First(r=>r.outputId=="fiber");var auto=new InventoryService(new SaveData(),catalog);
        auto.Selection.Add(new Stack("plank",2));auto.Data.progress.Add(new RecipeProgress{id=fiber.id,crafts=6});
        var autoCraft=new CraftingService(auto);autoCraft.Begin(Station.Tools,ToolKind.Plane,out _,fiber);
        Check(autoCraft.ApplyAutomatic(fiber)&&autoCraft.Finish().stack.count==16,"automatic range and batch yield");
        foreach(var r in catalog.recipes.Where(x=>!x.enabled))
        {
            var inv=new InventoryService(new SaveData{fuel=50},catalog);var c=new CraftingService(inv);
            foreach(var i in r.ingredients)inv.Selection.Add(new Stack(i.itemId,i.count));
            c.Begin(r.station,r.tool,out _);Check(c.Matching(false)==null,r.id+" disabled");
            Check(!inv.FillRecipe(r,1),r.id+" disabled auto fill");
        }
        Debug.Log($"NOTION_RECIPE_TESTS: {checks} checks passed; {catalog.recipes.Count(r=>r.enabled)} active / {catalog.recipes.Count} routes");
    }
}

