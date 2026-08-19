using System.Collections.Generic;
using System.Linq;
using PoorSmith.Data;

namespace PoorSmith.Crafting
{
    /// <summary>
    /// 실제로 물건을 만드는 부분.
    ///
    /// 기획상 제작은 두 갈래다.
    ///  · 손질 — 도구로 대상 하나를 원하는 횟수만큼 다듬는다. 횟수가 어느 레시피에도 안 맞으면 실패한다.
    ///  · 조합 — 작업대에 재료를 올린다. 등록되지 않은 조합이면 실패한다.
    ///
    /// 제작에 성공하면 그 아이템에 대응하는 노드가 열린다.
    /// 실패해도 재료는 소모되고 실패 결과물이 남는다.
    /// </summary>
    public sealed class CraftingService
    {
        readonly ItemDatabase items;
        readonly NodeGraph graph;
        readonly NodeProgress progress;
        readonly Inventory inventory;
        readonly Dictionary<ItemDef, NodeDef> nodeByItem;

        public CraftingService(ItemDatabase items, NodeGraph graph, NodeProgress progress, Inventory inventory)
        {
            this.items = items;
            this.graph = graph;
            this.progress = progress;
            this.inventory = inventory;

            nodeByItem = graph.Nodes
                .Where(node => node.Recipe != null)
                .GroupBy(node => node.Recipe)
                .ToDictionary(group => group.Key, group => group.First());
        }

        public NodeDef NodeFor(ItemDef item) =>
            item != null ? nodeByItem.GetValueOrDefault(item) : null;

        // ---- 손질 ----

        /// <summary>
        /// 도구로 대상을 정해진 횟수만큼 손질한다. 대상은 한 개 소모된다.
        /// 횟수가 어느 레시피에도 맞지 않거나 도구의 한계를 넘으면 실패 결과물이 나온다.
        /// </summary>
        public CraftResult Shape(ToolDef tool, ItemDef input, int strokes)
        {
            if (tool == null || input == null || strokes <= 0) return CraftResult.NotEnough();
            if (inventory.CountOf(input) <= 0) return CraftResult.NotEnough();

            inventory.TryConsume(input);

            if (tool.ExceedsLimit(strokes)) return Fail(tool, input.Category);

            var made = items.Items.FirstOrDefault(candidate =>
                candidate.Recipe.MatchesShaping(tool, input, strokes));

            return made != null ? Succeed(made) : Fail(tool, input.Category);
        }

        /// <summary>손질을 몇 번 해야 하는지 미리 알려준다. UI에서 목표 횟수를 보여주는 데 쓴다.</summary>
        public IEnumerable<ItemDef> ShapingOutcomesOf(ToolDef tool, ItemDef input) =>
            items.Items.Where(candidate =>
                candidate.Recipe.Kind == RecipeKind.Shaping
                && candidate.Recipe.Tool == tool
                && candidate.Recipe.Input == input);

        // ---- 조합 ----

        /// <summary>
        /// 작업대에 올린 재료들로 조합한다. 올린 것은 모두 소모된다.
        /// 남는 재료가 하나라도 있으면 등록되지 않은 조합으로 본다.
        /// </summary>
        public CraftResult Assemble(ToolDef workbench, IReadOnlyList<ItemDef> placed)
        {
            if (workbench == null || placed == null || placed.Count == 0) return CraftResult.NotEnough();

            var basket = new Dictionary<ItemDef, int>();
            foreach (var item in placed.Where(i => i != null))
                basket[item] = basket.GetValueOrDefault(item) + 1;

            if (basket.Any(line => inventory.CountOf(line.Key) < line.Value)) return CraftResult.NotEnough();

            inventory.TryConsume(basket);

            var made = items.Items.FirstOrDefault(candidate =>
                candidate.Recipe.Kind == RecipeKind.Assembly && Matches(candidate.Recipe, basket));

            // 조합 실패는 재료 계열과 무관하게 한 가지다.
            return made != null ? Succeed(made) : Fail(workbench, null);
        }

        /// <summary>
        /// 올린 재료가 이 레시피와 정확히 맞아떨어지는가.
        /// 재료 한 줄에 후보가 여럿이면 그중 하나를 골라 필요 개수만큼 쓴다. 섞어 쓰지는 않는다.
        /// </summary>
        static bool Matches(Recipe recipe, IReadOnlyDictionary<ItemDef, int> basket)
        {
            var remaining = new Dictionary<ItemDef, int>(basket);
            if (!Consume(recipe.Ingredients, 0, remaining)) return false;

            return remaining.Values.All(count => count == 0);
        }

        static bool Consume(IReadOnlyList<Ingredient> ingredients, int index, Dictionary<ItemDef, int> remaining)
        {
            if (index >= ingredients.Count) return true;

            var ingredient = ingredients[index];
            foreach (var option in ingredient.Options)
            {
                if (option == null || remaining.GetValueOrDefault(option) < ingredient.Count) continue;

                remaining[option] -= ingredient.Count;
                if (Consume(ingredients, index + 1, remaining)) return true;
                remaining[option] += ingredient.Count;
            }

            return false;
        }

        // ---- 노드 해금 ----

        /// <summary>
        /// 원천 재료를 갖고 있는 시작 노드를 연다.
        /// 채집으로 새 자원을 얻었을 때 불러주면 된다.
        /// </summary>
        public IEnumerable<NodeDef> UnlockReachableStartNodes()
        {
            var opened = new List<NodeDef>();

            foreach (var node in graph.Nodes)
            {
                if (node.Type != NodeType.Start || progress.IsUnlocked(node)) continue;
                if (node.StartResource == null || inventory.CountOf(node.StartResource) <= 0) continue;

                progress.Unlock(node);
                opened.Add(node);
            }

            return opened;
        }

        /// <summary>
        /// 지금 가진 것만으로 만들 수 있는 노드들.
        /// "찾아낸 레시피를 미리 세팅해달라"는 피드백에 대응하는 조회다.
        /// </summary>
        public IEnumerable<NodeDef> CraftableNow() =>
            graph.Nodes.Where(node =>
                node.Recipe != null
                && !progress.IsUnlocked(node)
                && CanMakeWithCurrentItems(node.Recipe.Recipe));

        bool CanMakeWithCurrentItems(Recipe recipe) => recipe.Kind switch
        {
            RecipeKind.Shaping => inventory.CountOf(recipe.Input) > 0,
            RecipeKind.Assembly => recipe.Ingredients.All(ingredient =>
                ingredient.Options.Any(option => inventory.CountOf(option) >= ingredient.Count)),
            _ => false,
        };

        /// <summary>디버그 패널용. 조건과 재료를 무시하고 연다.</summary>
        public void ForceUnlock(NodeDef node) => progress.Unlock(node);

        // ---- 내부 ----

        CraftResult Succeed(ItemDef made)
        {
            inventory.Add(made);

            var node = NodeFor(made);
            var newlyUnlocked = node != null && progress.Unlock(node);

            return CraftResult.Success(made, newlyUnlocked ? node : null);
        }

        CraftResult Fail(ToolDef tool, ItemCategory category)
        {
            var output = items.FailureFor(tool, category);
            if (output != null) inventory.Add(output);

            return CraftResult.Failed(output);
        }
    }
}
