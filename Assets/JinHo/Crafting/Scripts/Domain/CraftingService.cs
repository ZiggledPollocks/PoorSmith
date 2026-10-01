// [코드 지도] CraftingService: 선택 재료, 설비 입력, 레시피 조건을 비교하고 제작 결과·품질·숙련도를 계산한다.
// 주요 함수: Finish, Begin, Matching
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Crafting/Scripts/Domain/CraftingService.cs.md

using System;
using System.Linq;

namespace Blacksmith
{
    public sealed class CraftResult
    {
        public Stack stack;
        public System.Collections.Generic.List<Stack> returned = new System.Collections.Generic.List<Stack>();
        public string recipeId;
        public bool discovered, success;
        public string message;
    }

    public sealed class CraftingService
    {
        readonly InventoryService inventory;
        RecipeDefinition intendedRecipe;
        public Station Station { get; private set; }
        public ToolKind Tool { get; private set; }
        public bool Active { get; private set; }
        public int Strokes { get; private set; }
        public int[] Hits { get; private set; } = new int[5];
        public int Score { get; private set; }
        public int Targets { get; private set; }
        public int Completed { get; private set; }

        public CraftingService(InventoryService inv)
        {
            inventory = inv;
        }

        public RecipeProgress Progress(string id)
        {
            var p = inventory.Data.progress.Find(x => x.id == id);
            if (p == null)
            {
                p = new RecipeProgress
                {
                    id = id
                };
                inventory.Data.progress.Add(p);
            }

            return p;
        }

        public int Mastery(RecipeDefinition recipe)
        {
            int crafts = Progress(recipe.id).crafts;
            var item = inventory.Catalog.Item(recipe.outputId);
            bool equipment = item.material == MaterialKind.Weapon || item.material == MaterialKind.Armor;
            return MasteryLevel(crafts, equipment);
        }

        public static int MasteryLevel(int crafts, bool equipment) =>
            crafts > (equipment ? 10 : 30) ? 3 : crafts > (equipment ? 3 : 5) ? 2 : crafts > 0 ? 1 : 0;

        // 핵심 분기: Active 판정.
        // 상태 변경: error 갱신.
        // 다음 연결: Blacksmith.CraftingService.Batches(Blacksmith.RecipeDefinition) 호출.
        public bool Begin(Station station, ToolKind tool, out string error, RecipeDefinition intended = null)
        {
            if (!CanBegin(station, tool, out error, intended)) return false;

            int count = inventory.Selection.Sum(x => x.count);
            Station = station;
            Tool = tool;
            intendedRecipe = intended;
            Active = true;
            Strokes = 0;
            Hits = new int[5];
            Score = 0;
            Completed = 0;
            var workbenchRecipe = station == Station.Workbench ? Matching(false) : null;
            Targets = workbenchRecipe == null ? count :
                workbenchRecipe.ingredients.Sum(ingredient => ingredient.count) * Batches(workbenchRecipe);
            if (station == Station.Furnace)
                inventory.Data.fuel -= count * 3;
            return true;
        }

        // Read-only preflight shared by the UI button and the actual Begin command.
        public bool CanBegin(Station station, ToolKind tool, out string error, RecipeDefinition intended = null)
        {
            error = "";
            if (Active)
            {
                error = "이미 제작 중입니다.";
                return false;
            }

            int count = inventory.Selection.Sum(x => x.count);
            if (count == 0)
            {
                error = "먼저 재료를 선택하세요.";
                return false;
            }

            if (intended != null && (!inventory.Catalog.recipes.Contains(intended) || !intended.enabled || intended.station != station || (station == Station.Tools && intended.tool != tool) || Batches(intended) == 0))
            {
                error = station == Station.Workbench && NeedsMoreMaterials(intended) ?
                    "아이템을 더 넣으세요" : "선택한 결과물의 재료가 맞지 않습니다.";
                return false;
            }

            bool hasRecipe = inventory.Catalog.recipes.Any(r =>
                (intended == null || r == intended) && r.enabled && r.station == station &&
                (station != Station.Tools || r.tool == tool) && Batches(r) > 0);
            if (!hasRecipe)
            {
                error = station == Station.Workbench && inventory.Catalog.recipes.Any(r =>
                    r.enabled && r.station == Station.Workbench && NeedsMoreMaterials(r)) ?
                    "아이템을 더 넣으세요" : "현재 재료로 이 설비에서 작업할 수 없습니다.";
                return false;
            }

            if (station == Station.Anvil && !inventory.Catalog.Item(inventory.Selection[0].itemId).heated)
            {
                error = "모루에는 가열한 재료가 필요합니다.";
                return false;
            }
            if (station == Station.Quench)
            {
                var item = inventory.Catalog.Item(inventory.Selection[0].itemId);
                if (!item.heated && item.id != "leather_prepared")
                {
                    error = "담금질할 수 있는 재료가 아닙니다.";
                    return false;
                }
            }

            if (station == Station.Furnace && inventory.Data.fuel < count * 3)
            {
                error = $"연료가 부족합니다. 필요 연료: {count * 3}";
                return false;
            }

            return true;
        }

        public bool Stroke()
        {
            if (!Active || Station != Station.Tools)
                return false;
            var item = inventory.Catalog.Item(inventory.Selection[0].itemId);
            bool valid = Tool == ToolKind.Whetstone || (Tool == ToolKind.Knife && item.canKnife) || (Tool == ToolKind.Hammer ? item.material == MaterialKind.Stone : item.material == MaterialKind.Wood);
            if (!valid)
                return false;
            Strokes++;
            return true;
        }

        public bool Hit(int index)
        {
            if (!Active || Station != Station.Anvil || index < 0 || index >= 5 || Hits.Sum() >= 5 || !inventory.Catalog.Item(inventory.Selection[0].itemId).heated)
                return false;
            Hits[index]++;
            return true;
        }

        public void Timing(float distance)
        {
            if (!Active || Completed >= Targets)
                return;
            Score += distance <= .10f ? 4 : distance <= .28f ? 2 : 0;
            Completed++;
        }

        public RecipeDefinition Matching(bool useProcessing)
        {
            return inventory.Catalog.recipes.Find(r => (intendedRecipe == null || r == intendedRecipe) && r.enabled && r.station == Station && (Station != Station.Tools || r.tool == Tool) && Batches(r) > 0 && (!useProcessing || ProcessingMatches(r)));
        }

        bool ProcessingMatches(RecipeDefinition r)
        {
            if (Station == Station.Tools)
                return Strokes > 0;
            if (Station == Station.Anvil)
            {
                if (r.anvilHits.SequenceEqual(Hits))
                    return true;
                return r.symmetricAnvil && r.anvilHits[0] == Hits[1] && r.anvilHits[1] == Hits[0] && r.anvilHits.Skip(2).SequenceEqual(Hits.Skip(2));
            }

            return true;
        }

        public int Batches(RecipeDefinition r)
        {
            if (r == null || r.ingredients == null || r.ingredients.Count == 0) return 0;
            // Compare item counts, not the order in which slots were filled.
            var counts = inventory.Selection.GroupBy(x => x.itemId)
                .ToDictionary(g => g.Key, g => g.Sum(x => (long)x.count));
            var required = r.ingredients.GroupBy(x => x.itemId)
                .ToDictionary(g => g.Key, g => g.Sum(x => (long)x.count));
            if (counts.Count != required.Count || required.Values.Any(n => n <= 0))
                return 0;
            long batches = -1;
            foreach (var req in required)
            {
                if (!counts.TryGetValue(req.Key, out long n))
                    return 0;
                long b = n / req.Value;
                if (r.station != Station.Workbench && (n % req.Value != 0 || batches >= 0 && b != batches))
                    return 0;
                batches = batches < 0 ? b : r.station == Station.Workbench ? Math.Min(batches, b) : b;
            }

            return batches > int.MaxValue ? 0 : (int)Math.Max(0, batches);
        }

        bool NeedsMoreMaterials(RecipeDefinition recipe)
        {
            if (recipe == null || !recipe.enabled || recipe.station != Station.Workbench)
                return false;
            var required = recipe.ingredients.GroupBy(ingredient => ingredient.itemId)
                .ToDictionary(group => group.Key, group => group.Sum(ingredient => (long)ingredient.count));
            var selected = inventory.Selection.GroupBy(stack => stack.itemId)
                .ToDictionary(group => group.Key, group => group.Sum(stack => (long)stack.count));
            return selected.Count > 0 && selected.Keys.All(required.ContainsKey) &&
                required.Any(pair => !selected.TryGetValue(pair.Key, out long count) || count < pair.Value);
        }

        public bool NeedsTiming()
        {
            var r = Matching(false);
            if (r == null)
                return false;
            var d = inventory.Catalog.Item(r.outputId);
            return d.material == MaterialKind.Weapon || d.material == MaterialKind.Armor;
        }

        public bool ApplyAutomatic(RecipeDefinition recipe)
        {
            if (!Active || !recipe.enabled || recipe.station != Station || (Station == Station.Tools && recipe.tool != Tool) || Mastery(recipe) < 2 || Batches(recipe) == 0)
                return false;
            Strokes = Station == Station.Tools ? 1 : recipe.strokes;
            Hits = (int[])recipe.anvilHits.Clone();
            Score = Targets * 3;
            Completed = Targets;
            return true;
        }

        // 핵심 분기: !Active 판정.
        // 상태 변경: success 갱신.
        // 다음 연결: Blacksmith.CraftingService.Matching(bool) 호출.
        public CraftResult Finish(bool quenchSuccess = true)
        {
            if (!Active)
                return null;
            var recipe = Matching(true);
            bool success = recipe != null && (Station != Station.Quench || quenchSuccess);
            if (success && Station == Station.Quench)
            {
                var first = inventory.Selection[0];
                var definition = inventory.Catalog.Item(first.itemId);
                // An unheated item has no quenching result, even when a recipe matches.
                if (!definition.heated && first.itemId != "leather_prepared")
                    success = false;
            }
            if (!success)
            {
                // A failed attempt returns its materials, but furnace fuel stays
                // consumed as it was when processing began.
                inventory.ReturnAll();
                Active = false;
                return new CraftResult { success = false, message = "실패했습니다." };
            }

            int recipeBatches = Batches(recipe);
            int batches = recipeBatches * Math.Max(1, recipe.outputCount);
            string output = recipe.outputId;
            Quality quality = Quality.High;
            if (Station == Station.Workbench && success && QualityRules.AppliesTo(inventory.Catalog.Item(output)) && NeedsTiming())
                quality = QualityRules.FromAverage(Targets == 0 ? 0 : (float)Score / Targets);
            var result = new CraftResult
            {
                stack = new Stack(output, batches, quality),
                success = success
            };
            if (Station == Station.Workbench)
                inventory.ConsumeSelectedBatches(recipe, recipeBatches);
            else
                inventory.Selection.Clear();
            if (success && Mastery(recipe) >= 3)
            {
                var outputDef = inventory.Catalog.Item(output);
                bool equipment = outputDef.material == MaterialKind.Weapon || outputDef.material == MaterialKind.Armor;
                // Roll each produced item independently; preserve mixed quality stacks.
                int bonus = 0;
                for (int i = 0; i < batches; i++)
                    if (UnityEngine.Random.value < (equipment ? .10f : .05f))
                        bonus++;
                if (equipment && bonus > 0)
                {
                    var upgraded = new Stack(output, bonus, (Quality)Math.Min((int)Quality.Master, (int)quality + 1));
                    result.returned.Add(upgraded);
                    InventoryService.Add(inventory.Data.chest, upgraded);
                    result.stack.count -= bonus;
                }
                else if (!equipment)
                    result.stack.count += bonus;
            }

            if (success)
            {
                var p = Progress(recipe.id);
                result.discovered = p.crafts == 0;
                p.crafts++;
                result.recipeId = recipe.id;
            }

            InventoryService.Add(inventory.Data.chest, result.stack);
            Active = false;
            inventory.Notify();
            return result;
        }

    }
}
