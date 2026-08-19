using System;
using System.Collections.Generic;
using System.Linq;
using PoorSmith.Crafting;
using PoorSmith.Data;
using UnityEngine;
using UnityEngine.UI;

namespace PoorSmith.UI
{
    /// <summary>
    /// 작업 공간. 손질과 조합을 한 화면에서 한다.
    ///
    /// 도구별로 화면을 나누지 않은 이유는 피드백 2번 — "작업 탭 이동이 답답하게 느껴진다" — 때문이다.
    /// 노드에서 '작업 준비'를 누르면 여기 값이 미리 채워진다.
    /// </summary>
    internal sealed class CraftPanel : MonoBehaviour
    {
        ItemDatabase items;
        Inventory inventory;
        CraftingService crafting;

        ToolDef workbench;
        readonly List<ToolDef> shapingTools = new();

        ToolDef tool;
        ItemDef target;
        int strokes = 1;
        readonly List<ItemDef> bench = new();

        Text toolLabel;
        Text targetLabel;
        Text strokeLabel;
        Text benchLabel;
        Text resultLabel;
        Transform inventoryList;

        internal event Action Changed;

        internal static CraftPanel Create(
            Transform parent, ItemDatabase items, Inventory inventory, CraftingService crafting)
        {
            var root = UIFactory.Rect("CraftPanel", parent);
            UIFactory.Stretch(root);
            var panel = root.gameObject.AddComponent<CraftPanel>();
            panel.items = items;
            panel.inventory = inventory;
            panel.crafting = crafting;

            panel.workbench = items.FindTool("workbench");
            panel.shapingTools.AddRange(items.Tools.Where(t => t.Implemented && t.Station == "rack"));
            panel.tool = panel.shapingTools.FirstOrDefault();

            panel.Build(root);
            panel.Refresh();
            return panel;
        }

        void Build(RectTransform root)
        {
            var background = UIFactory.Panel("Background", root, UITheme.PanelBackground);
            UIFactory.Stretch(background.rectTransform);

            // 손질·조합·가진 것이 한 화면에 다 들어가지 않으므로 스크롤로 둔다.
            var column = UIFactory.ScrollColumn(root);

            Header(column, "손질");
            toolLabel = Row(column, "도구", () =>
            {
                if (shapingTools.Count == 0) return;
                var next = (shapingTools.IndexOf(tool) + 1) % shapingTools.Count;
                tool = shapingTools[next];
                Refresh();
            });
            targetLabel = Line(column, "");
            strokeLabel = Row(column, "횟수 +1", () => { strokes++; Refresh(); });
            Row(column, "횟수 -1", () => { strokes = Mathf.Max(1, strokes - 1); Refresh(); });
            Action(column, "손질하기", UITheme.AccentFace, DoShape);

            Header(column, "조합");
            benchLabel = Line(column, "");
            Action(column, "조합하기", UITheme.AccentFace, DoAssemble);
            Action(column, "작업대 비우기", UITheme.WarnFace, () => { bench.Clear(); Refresh(); });

            Header(column, "결과");
            resultLabel = Line(column, "아직 아무것도 만들지 않았습니다.");

            Header(column, "가진 것 — 이름을 누르면 손질 대상, [작업대에]를 누르면 조합 재료");
            inventoryList = UIFactory.Column(column, 4f);
        }

        // ---- 조작 ----

        /// <summary>노드에서 넘어온 레시피로 작업대를 미리 채운다.</summary>
        internal void PrepareFor(NodeDef node)
        {
            var made = node?.Recipe;
            if (made == null) return;

            var recipe = made.Recipe;

            switch (recipe.Kind)
            {
                case RecipeKind.Shaping:
                    tool = recipe.Tool;
                    target = recipe.Input;
                    strokes = recipe.MinStrokes;
                    bench.Clear();
                    resultLabel.text = $"{made.DisplayName} 준비 — {recipe.Describe()}";
                    break;

                case RecipeKind.Assembly:
                    bench.Clear();
                    foreach (var ingredient in recipe.Ingredients)
                    {
                        // 가진 것 중에서 고르고, 없으면 첫 후보를 올려 무엇이 모자란지 보이게 한다.
                        var option = ingredient.Options.FirstOrDefault(o => inventory.CountOf(o) >= ingredient.Count)
                                     ?? ingredient.Options.FirstOrDefault();
                        for (var i = 0; i < ingredient.Count && option != null; i++) bench.Add(option);
                    }
                    resultLabel.text = $"{made.DisplayName} 준비 — {recipe.Describe()}";
                    break;

                default:
                    resultLabel.text = $"{made.DisplayName}은(는) 채집으로만 얻습니다.";
                    break;
            }

            Refresh();
        }

        void DoShape()
        {
            var result = crafting.Shape(tool, target, strokes);
            Report(result, target != null ? $"{target.DisplayName} 손질" : "손질");
        }

        void DoAssemble()
        {
            var result = crafting.Assemble(workbench, bench);
            if (result.Attempted) bench.Clear();
            Report(result, "조합");
        }

        void Report(CraftResult result, string what)
        {
            if (!result.Attempted)
            {
                resultLabel.text = $"{what} — 재료가 모자랍니다.";
            }
            else if (result.Succeeded)
            {
                resultLabel.text = result.UnlockedNode != null
                    ? $"{result.Output.DisplayName} 완성! 새 노드가 열렸습니다."
                    : $"{result.Output.DisplayName} 완성.";
            }
            else
            {
                resultLabel.text = $"실패 — {result.Output.DisplayName}이(가) 남았습니다.";
            }

            crafting.UnlockReachableStartNodes();
            Refresh();
            Changed?.Invoke();
        }

        internal void Refresh()
        {
            toolLabel.text = $"도구 바꾸기 — 지금은 {(tool != null ? tool.DisplayName : "없음")}";
            targetLabel.text = $"손질 대상 — {(target != null ? target.DisplayName : "고르지 않음")}";
            strokeLabel.text = $"횟수 +1 — 지금은 {strokes}번";
            benchLabel.text = bench.Count == 0
                ? "작업대가 비어 있습니다."
                : "작업대 — " + string.Join(", ", bench.GroupBy(i => i).Select(g => $"{g.Key.DisplayName} x{g.Count()}"));

            RebuildInventory();
        }

        void RebuildInventory()
        {
            for (var i = inventoryList.childCount - 1; i >= 0; i--)
                Destroy(inventoryList.GetChild(i).gameObject);

            foreach (var (item, count) in inventory.Entries.Select(e => (e.Key, e.Value))
                         .OrderBy(e => e.Key.DisplayName))
            {
                var row = UIFactory.Rect($"Item_{item.Id}", inventoryList);
                UIFactory.FixHeight(row, UITheme.RowHeight);

                var rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                rowLayout.spacing = 6f;
                rowLayout.childControlWidth = true;
                rowLayout.childForceExpandWidth = true;
                rowLayout.childControlHeight = true;
                rowLayout.childForceExpandHeight = true;

                var pick = UIFactory.Button("Pick", row, $"{item.DisplayName}  x{count}",
                    target == item ? UITheme.AccentFace : UITheme.ButtonFace, UITheme.ButtonText);
                pick.onClick.AddListener(() => { target = item; Refresh(); });

                var toBench = UIFactory.Button("ToBench", row, "작업대에",
                    UITheme.BenchFace, UITheme.ButtonText);
                var element = toBench.gameObject.AddComponent<LayoutElement>();
                element.preferredWidth = 140f;
                element.flexibleWidth = 0f;
                toBench.onClick.AddListener(() => { bench.Add(item); Refresh(); });
            }
        }

        // ---- 조립 도우미 ----

        static void Header(Transform parent, string text)
        {
            var label = UIFactory.Label("Header", parent, text, UITheme.SmallSize, UITheme.Muted, TextAnchor.LowerLeft);
            UIFactory.FixHeight(label, UITheme.HeaderHeight);
        }

        static Text Line(Transform parent, string text) =>
            UIFactory.Label("Line", parent, text, UITheme.BodySize, UITheme.Body, TextAnchor.UpperLeft);

        static Text Row(Transform parent, string text, UnityEngine.Events.UnityAction onClick)
        {
            var button = UIFactory.Button("Row", parent, text, UITheme.ButtonFace, UITheme.ButtonText);
            UIFactory.FixHeight(button, UITheme.RowHeight);
            button.onClick.AddListener(onClick);
            return button.GetComponentInChildren<Text>();
        }

        static void Action(Transform parent, string text, Color color, UnityEngine.Events.UnityAction onClick)
        {
            var button = UIFactory.Button("Action", parent, text, color, Color.white);
            UIFactory.FixHeight(button, UITheme.ActionHeight);
            button.onClick.AddListener(onClick);
        }
    }
}
