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
        public event Action AttemptChanged;
        void JournalAction(string action)
        { ExperimentJournal.Safe(() => { ExperimentJournal.Action(inventory.Data, action); AttemptChanged?.Invoke(); }); }
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
            ExperimentJournal.Safe(() => ExperimentJournal.Begin(inventory.Data, station, tool,
                inventory.Selection, station == Station.Furnace ? count * 3 : 0));
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

            if(!inventory.Data.night&&!StoryTutorial.Guiding)
            {
                error="낮에는 준비하고 밤에 제작하세요. 침대에서 밤으로 전환할 수 있습니다.";return false;
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

            // Widen before multiplication: valid saved stack counts can overflow an Int32 fuel cost.
            if (station == Station.Furnace && inventory.Data.fuel < (long)count * 3)
            {
                error = $"연료가 부족합니다. 필요 연료: {(long)count * 3}";
                return false;
            }

            return true;
        }

        // Records a real release gesture; the existing UI remains responsible for its unchanged timing verdict.
        public void QuenchRelease(float elapsed)
        {
            if (Active && Station == Station.Quench)
                JournalAction("담금질 놓기 " + elapsed.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "초");
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
            JournalAction("가공 " + Strokes);
            return true;
        }

        public bool Hit(int index)
        {
            if (!Active || Station != Station.Anvil || index < 0 || index >= 5 || Hits.Sum() >= 5 || !inventory.Catalog.Item(inventory.Selection[0].itemId).heated)
                return false;
            Hits[index]++;
            JournalAction("모루 " + new[] { "위", "아래", "좌", "우", "중앙" }[index]);
            return true;
        }

        public void Timing(float distance)
        {
            if (!Active || Completed >= Targets)
                return;
            Score += distance <= .10f ? 4 : distance <= .28f ? 2 : 0;
            Completed++;
            JournalAction("타이밍 입력 " + distance.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        }

        // Cancellation uses the existing return transaction; the record is finalized only after it succeeds.
        public void Cancel()
        {
            if (!Active) return;
            var returned = inventory.Selection.Select(x => x.Copy(x.count)).ToArray();
            inventory.ReturnAll(); Active = false;
            ExperimentJournal.Safe(() => ExperimentJournal.Complete(inventory.Data, "중단", Array.Empty<Stack>(), returned));
        }

        public RecipeDefinition Matching(bool useProcessing)
        {
            bool Eligible(RecipeDefinition r) => r.enabled && r.station == Station &&
                (Station != Station.Tools || r.tool == Tool) && Batches(r) > 0;
            var exact = inventory.Catalog.recipes.Find(r => Eligible(r) && !r.anvilByproduct &&
                (intendedRecipe == null || r == intendedRecipe) && (!useProcessing || ProcessingMatches(r)));
            if (exact != null) return exact;
            if (!useProcessing)
                return inventory.Catalog.recipes.Find(r => Eligible(r) && r.anvilByproduct &&
                    (intendedRecipe == null || r == intendedRecipe));
            // Finish calls Matching(true): only a full five-hit, otherwise unmatched pattern is a byproduct.
            // Check all normal recipes even when an intended result was selected, so their valid patterns stay intact.
            if (Station != Station.Anvil || Hits.Sum() != 5 ||
                inventory.Catalog.recipes.Any(r => Eligible(r) && !r.anvilByproduct && ProcessingMatches(r)))
                return null;
            return inventory.Catalog.recipes.Find(r => Eligible(r) && r.anvilByproduct);
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
            JournalAction("발견한 레시피 자동 작업");
            return true;
        }

        // Validate prospective output/remainder capacity without changing live inventory.
        void ValidateStorage(RecipeDefinition recipe, int batches, CraftResult result)
        {
            var staged = inventory.Data.chest.Select(s => s.Copy(s.count)).ToList();
            if (Station == Station.Workbench)
            {
                var remaining = inventory.Selection.Select(s => s.Copy(s.count)).ToList();
                var required = new System.Collections.Generic.Dictionary<string, int>();
                foreach (var ingredient in recipe.ingredients)
                    if (!InventoryRequirements.TryScale(ingredient.count, batches, out int amount) ||
                        !InventoryRequirements.TryAdd(required, ingredient.itemId, amount))
                        throw new OverflowException("Recipe material count overflow");
                if (!InventoryStackLedger.TryConsume(remaining, required,
                    s => s.itemId, s => s.count, (s, n) => s.count = n))
                    throw new InvalidOperationException("Selected materials changed before crafting finished.");
                foreach (var stack in remaining) InventoryService.Add(staged, stack);
            }
            foreach (var stack in result.returned) InventoryService.Add(staged, stack);
            InventoryService.Add(staged, result.stack);
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
                var returned = inventory.Selection.Select(x => x.Copy(x.count)).ToArray();
                inventory.ReturnAll();
                Active = false;
                ExperimentJournal.Safe(() => ExperimentJournal.Complete(inventory.Data, "실패", Array.Empty<Stack>(), returned));
                return new CraftResult { success = false, message = "실패했습니다." };
            }

            int recipeBatches = Batches(recipe);
            int batches = checked(recipeBatches * Math.Max(1, recipe.outputCount));
            string output = recipe.outputId;
            Quality quality = Quality.High;
            if (Station == Station.Workbench && success && QualityRules.AppliesTo(inventory.Catalog.Item(output)) && NeedsTiming())
                quality = QualityRules.FromAverage(Targets == 0 ? 0 : (float)Score / Targets);
            var result = new CraftResult
            {
                stack = new Stack(output, batches, quality),
                success = success
            };
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
                    result.stack.count -= bonus;
                }
                else if (!equipment)
                    result.stack.count = checked(result.stack.count + bonus);
            }

            // Finish preflights the same outputs and unused inputs before committing materials.
            // The UI catches capacity errors and keeps Selection available for retry/cancel.
            ValidateStorage(recipe, recipeBatches, result);
            System.Collections.Generic.List<Stack> unused = null;
            // Journal-only snapshots must never prevent the already validated inventory transaction.
            ExperimentJournal.Safe(() =>
            {
                unused = inventory.Selection.Select(x => x.Copy(x.count)).ToList();
                if (Station == Station.Workbench)
                {
                    var costs = recipe.ingredients.GroupBy(x => x.itemId).ToDictionary(g => g.Key, g => (int)(g.Sum(x => (long)x.count) * recipeBatches));
                    InventoryStackLedger.TryConsume(unused, costs, x => x.itemId, x => x.count, (x,n) => x.count = n);
                }
                else unused.Clear();
            });
            if (Station == Station.Workbench)
                inventory.ConsumeSelectedBatches(recipe, recipeBatches);
            else
                inventory.Selection.Clear();
            foreach (var extra in result.returned)
                InventoryService.Add(inventory.Data.chest, extra);

            if (success)
            {
                var p = Progress(recipe.id);
                result.discovered = p.crafts == 0;
                p.crafts++;
                result.recipeId = recipe.id;
            }

            InventoryService.Add(inventory.Data.chest, result.stack);
            Active = false;
            ExperimentJournal.Safe(() => ExperimentJournal.Complete(inventory.Data, "성공",
                result.returned.Concat(new[] { result.stack }), unused ?? new System.Collections.Generic.List<Stack>()));
            inventory.Notify();
            return result;
        }

    }
}
