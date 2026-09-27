using System;
using System.Linq;

namespace Blacksmith
{
    public sealed class CraftResult
    {
        public Stack stack; public System.Collections.Generic.List<Stack> returned = new System.Collections.Generic.List<Stack>(); public string recipeId; public bool discovered, success; public string message;
    }
    public sealed class CraftingService
    {
        readonly InventoryService inventory;
        RecipeDefinition intendedRecipe;
        public Station Station {get;private set;}
        public ToolKind Tool {get;private set;}
        public bool Active {get;private set;}
        public int Strokes {get;private set;}
        public int[] Hits {get;private set;} = new int[5];
        public int Score {get;private set;}
        public int Targets {get;private set;}
        public int Completed {get;private set;}
        public CraftingService(InventoryService inv){inventory=inv;}
        public RecipeProgress Progress(string id)
        {
            var p=inventory.Data.progress.Find(x=>x.id==id);
            if(p==null){p=new RecipeProgress{id=id};inventory.Data.progress.Add(p);}return p;
        }
        public int Mastery(RecipeDefinition recipe)
        {
            int crafts=Progress(recipe.id).crafts;
            var item=inventory.Catalog.Item(recipe.outputId);
            bool equipment=item.material==MaterialKind.Weapon||item.material==MaterialKind.Armor;
            return crafts>(equipment?10:30)?3:crafts>(equipment?3:5)?2:crafts>0?1:0;
        }
        public bool Begin(Station station,ToolKind tool,out string error,RecipeDefinition intended=null)
        {
            error="";if(Active){error="이미 제작 중입니다.";return false;}
            int count=inventory.Selection.Sum(x=>x.count);
            if(count==0){error="먼저 재료를 선택하세요.";return false;}
            if(intended!=null&&(!inventory.Catalog.recipes.Contains(intended)||!intended.enabled||intended.station!=station||(station==Station.Tools&&intended.tool!=tool)||Batches(intended)==0)){error="선택한 결과물의 재료가 맞지 않습니다.";return false;}
            if(station==Station.Workbench&&intended==null&&inventory.Catalog.recipes.Count(r=>r.enabled&&r.station==station&&Batches(r)>0)>1){error="같은 재료로 가능한 결과물을 먼저 선택하세요.";return false;}
            if(station==Station.Furnace&&inventory.Data.fuel<count*3){error=$"연료가 부족합니다. 필요 연료: {count*3}";return false;}
            Station=station;Tool=tool;intendedRecipe=intended;Active=true;Strokes=0;Hits=new int[5];Score=0;Completed=0;Targets=count;
            if(station==Station.Furnace)inventory.Data.fuel-=count*3;
            return true;
        }
        public bool Stroke()
        {
            if(!Active||Station!=Station.Tools||Strokes>=10)return false;
            var item=inventory.Catalog.Item(inventory.Selection[0].itemId);
            bool valid=Tool==ToolKind.Whetstone||(Tool==ToolKind.Knife&&item.canKnife)||(Tool==ToolKind.Hammer?item.material==MaterialKind.Stone:item.material==MaterialKind.Wood);
            if(!valid)return false;Strokes++;return true;
        }
        public bool Hit(int index)
        {
            if(!Active||Station!=Station.Anvil||index<0||index>=5||Hits.Sum()>=5||!inventory.Catalog.Item(inventory.Selection[0].itemId).heated)return false;
            Hits[index]++;return true;
        }
        public void Timing(float distance)
        {if(!Active||Completed>=Targets)return;Score+=distance<=.10f?4:distance<=.28f?2:0;Completed++;}
        public RecipeDefinition Matching(bool useProcessing)
        {
            return inventory.Catalog.recipes.Find(r=>(intendedRecipe==null||r==intendedRecipe)&&r.enabled&&r.station==Station&&(Station!=Station.Tools||r.tool==Tool)&&Batches(r)>0&&(!useProcessing||ProcessingMatches(r)));
        }
        bool ProcessingMatches(RecipeDefinition r)
        {
            if(Station==Station.Tools)return Strokes>=r.strokes&&Strokes<=Math.Max(r.strokes,r.maxStrokes);
            if(Station==Station.Anvil)
            {if(r.anvilHits.SequenceEqual(Hits))return true;return r.symmetricAnvil&&r.anvilHits[0]==Hits[1]&&r.anvilHits[1]==Hits[0]&&r.anvilHits.Skip(2).SequenceEqual(Hits.Skip(2));}
            return true;
        }
        public int Batches(RecipeDefinition r)
        {
            var counts=inventory.Selection.GroupBy(x=>x.itemId).ToDictionary(g=>g.Key,g=>g.Sum(x=>x.count));
            if(counts.Count!=r.ingredients.Count)return 0;
            int batches=-1;
            foreach(var req in r.ingredients)
            {
                if(req.count<=0||!counts.TryGetValue(req.itemId,out int n)||n%req.count!=0)return 0;
                int b=n/req.count;if(batches>=0&&b!=batches)return 0;batches=b;
            }return Math.Max(0,batches);
        }
        public bool NeedsTiming()
        {var r=Matching(false);if(r==null)return false;var d=inventory.Catalog.Item(r.outputId);return d.material==MaterialKind.Weapon||d.material==MaterialKind.Armor;}
        public bool ApplyAutomatic(RecipeDefinition recipe)
        {
            if(!Active||!recipe.enabled||recipe.station!=Station||(Station==Station.Tools&&recipe.tool!=Tool)||Mastery(recipe)<2||Batches(recipe)==0)return false;
            Strokes=recipe.strokes;Hits=(int[])recipe.anvilHits.Clone();Score=Targets*3;Completed=Targets;return true;
        }
        public CraftResult Finish(bool quenchSuccess=true)
        {
            if(!Active)return null;
            var recipe=Matching(true);
            if(recipe==null&&Station==Station.Workbench)
            {
                var failed=new CraftResult{success=false,message="조합 실패 · 재료마다 50% 확률로 반환했습니다."};
                foreach(var source in inventory.Selection)
                {
                    int count=0;
                    for(int i=0;i<source.count;i++)if(UnityEngine.Random.value<.5f)count++;
                    if(count>0){var refund=source.Copy(count);failed.returned.Add(refund);InventoryService.Add(inventory.Data.chest,refund);}
                }
                inventory.Selection.Clear();Active=false;inventory.Notify();return failed;
            }
            bool success=recipe!=null&&(Station!=Station.Quench||quenchSuccess);
            int batches=success?Batches(recipe)*Math.Max(1,recipe.outputCount):inventory.Selection.Sum(x=>x.count);
            string output=success?recipe.outputId:Byproduct();
            Quality quality=Quality.High;
            if(Station==Station.Workbench&&success&&NeedsTiming())quality=QualityRules.FromAverage(Targets==0?0:(float)Score/Targets);
            var result=new CraftResult{stack=new Stack(output,batches,quality),success=success};
            // Unheated metal, wood and stone return unchanged from quenching, including quality.
            var first=inventory.Selection[0];var def=inventory.Catalog.Item(first.itemId);
            if(Station==Station.Quench&&!def.heated&&first.itemId!="leather_prepared")
            {result.stack=first.Copy(inventory.Selection.Sum(x=>x.count));result.success=false;result.message="변화 없이 돌아왔습니다.";}
            if(success&&Mastery(recipe)>=3)
            {
                var outputDef=inventory.Catalog.Item(output);
                bool equipment=outputDef.material==MaterialKind.Weapon||outputDef.material==MaterialKind.Armor;
                // Roll each produced item independently; preserve mixed quality stacks.
                int bonus=0;
                for(int i=0;i<batches;i++)if(UnityEngine.Random.value<(equipment?.10f:.05f))bonus++;
                if(equipment&&bonus>0)
                {
                    var upgraded=new Stack(output,bonus,(Quality)Math.Min((int)Quality.Master,(int)quality+1));
                    result.returned.Add(upgraded);InventoryService.Add(inventory.Data.chest,upgraded);result.stack.count-=bonus;
                }
                else if(!equipment)result.stack.count+=bonus;
            }
            if(success)
            {var p=Progress(recipe.id);result.discovered=p.crafts==0;p.crafts++;result.recipeId=recipe.id;}
            inventory.Selection.Clear();InventoryService.Add(inventory.Data.chest,result.stack);Active=false;inventory.Notify();return result;
        }
        string Byproduct()
        {
            if(Station==Station.Furnace)return "burnt";
            if(Station==Station.Anvil)return "dented";
            if(Station==Station.Quench)return inventory.Selection[0].itemId=="leather_prepared"?"twisted_leather":"cracked";
            if(Station==Station.Workbench)return "tangled";
            return new[]{"knife_scrap","plane_scrap","saw_scrap","tiny_scrap","stone_scrap"}[(int)Tool];
        }
    }
}
