using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blacksmith
{
    public sealed class RecipeBookState
    {
        public int Tab;
        public string Focus;
        public readonly string[] Selection = new string[4];
        public readonly bool[] Visited = new bool[4];
        public readonly Vector2[] Scroll =
        {
            new Vector2(0, 1),
            new Vector2(0, 1),
            new Vector2(0, 1),
            new Vector2(0, 1)
        };
        public readonly HashSet<string> Revealed = new HashSet<string>();
        public bool Initialized;
    }

    public sealed class RecipeBookView : MonoBehaviour
    {
        sealed class Vertex
        {
            public RecipeGraphLayout.Node Node;
            public Button Button;
            public bool Visible;
        }

        BlacksmithController controller;
        BlacksmithView view;
        RecipeBookState state;
        RectTransform graph, details;
        ScrollRect scroller;
        RecipeFogGraphic fog;
        TMP_Text count;
        readonly List<Vertex> vertices = new List<Vertex>();
        readonly List<Tuple<Image, string, string>> edges = new List<Tuple<Image, string, string>>();
        float nextCheck;
        int progressSignature;
        static readonly string[] Stations =
        {
            "작업대",
            "도구 걸이",
            "모루",
            "담금질",
            "용광로"
        };
        static readonly string[] Tools =
        {
            "칼",
            "대패",
            "톱",
            "숫돌",
            "망치"
        };
        public void Initialize(BlacksmithController owner, RecipeBookState memory, Action close)
        {
            controller = owner;
            view = owner.view;
            state = memory;
            if (!state.Initialized)
            {
                foreach (var recipe in owner.catalog.recipes.Where(r => r.enabled && Discovered(r)))
                    state.Revealed.Add(recipe.outputId);
                state.Initialized = true;
            }

            view.Text("Title", transform, "레시피 지도", 32, new Vector2(.03f, .89f), new Vector2(.5f, .99f), BlacksmithView.Ink);
            view.Button("CloseRecipes", transform, "닫기", new Vector2(.85f, .90f), new Vector2(.98f, .98f), close);
            for (int i = 0; i < 4; i++)
            {
                int tab = i;
                var button = view.Button("RecipeTab_" + i, transform, RecipeGraphLayout.Tabs[i], new Vector2(.03f + i * .18f, .80f), new Vector2(.20f + i * .18f, .89f), () => SwitchTab(tab));
                button.GetComponent<Image>().color = i == state.Tab ? BlacksmithView.Gold : BlacksmithView.Dark;
            }

            count = view.Text("DiscoveryCount", transform, "", 18, new Vector2(.76f, .80f), new Vector2(.98f, .89f), BlacksmithView.Ink, TextAlignmentOptions.Center);
            // Use the established scroll styling, but give the graph fixed 2D coordinates.
            graph = view.Scroll(transform, "RecipeGraph", new Vector2(.025f, .31f), new Vector2(.98f, .79f));
            DestroyImmediate(graph.GetComponent<VerticalLayoutGroup>());
            DestroyImmediate(graph.GetComponent<ContentSizeFitter>());
            scroller = graph.parent.parent.GetComponent<ScrollRect>();
            scroller.horizontal = true;
            scroller.viewport.anchorMin = new Vector2(0, .065f);
            graph.anchorMin = graph.anchorMax = new Vector2(0, 1);
            graph.pivot = new Vector2(0, 1);
            scroller.scrollSensitivity = 55;
            var bar = view.Rect("HorizontalScrollbar", scroller.transform, new Vector2(0, 0), new Vector2(.96f, .045f));
            bar.gameObject.AddComponent<Image>().color = new Color(.25f, .23f, .18f);
            var handle = view.Full("Handle", bar);
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = BlacksmithView.Gold;
            var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            scrollbar.direction = Scrollbar.Direction.LeftToRight;
            scroller.horizontalScrollbar = scrollbar;
            details = view.Scroll(transform, "RecipeDetails", new Vector2(.035f, .035f), new Vector2(.975f, .29f));
            if (owner.State == ScreenState.Codex)
            {
                DrawCodex();
                return;
            }

            DrawGraph();
        }

        void DrawCodex()
        {
            BlacksmithView.Clear(graph);
            graph.sizeDelta = new Vector2(1300, Mathf.Max(500, controller.catalog.recipes.Count(r => r.enabled && Discovered(r)) * 70));
            int n = 0;
            foreach (var recipe in controller.catalog.recipes.Where(r => r.enabled && Discovered(r)))
            {
                var b = view.Button("Discovered_" + recipe.id, graph, recipe.displayName + " · 재료 배치", Vector2.zero, Vector2.zero, () => controller.PrepareRecipe(recipe));
                Place((RectTransform)b.transform, new Vector2(620, 45 + n++ * 70), new Vector2(1180, 60));
            }

            Detail(n == 0 ? "제작에 성공하면 도감에 간단 제작법과 재료 배치 버튼이 추가됩니다." : "발견한 제작법을 클릭하면 해당 설비로 이동하고 재료를 배치합니다.");
            count.text = "발견 제작법 " + n;
        }

        bool VisibleHint(string id)
        {
            if (Known(id))
                return true;
            if (id == "sharp_branch")
                return false;
            return Recipes(id).Any(r => r.ingredients.Any(i => Known(i.itemId)));
        }

        HashSet<string> Descendants(string start)
        {
            var found = new HashSet<string>
            {
                start
            };
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (var r in controller.catalog.recipes.Where(r => r.enabled))
                    if (r.ingredients.Any(i => found.Contains(i.itemId)) && found.Add(r.outputId))
                        changed = true;
            }

            return found;
        }

        bool Discovered(RecipeDefinition recipe) => controller.Inventory.Data.progress.Any(p => p.id == recipe.id && p.Level > 0);
        RecipeDefinition[] Recipes(string itemId) => controller.catalog.recipes.Where(r => r.enabled && r.outputId == itemId).ToArray();
        bool Known(string itemId)
        {
            var item = controller.catalog.Item(itemId);
            return item != null && ((item.group == ItemGroup.Gathered && controller.Inventory.Data.acquiredItems.Contains(itemId)) || Recipes(itemId).Any(Discovered));
        }

        int Signature()
        {
            unchecked
            {
                int hash = 17;
                foreach (var p in controller.Inventory.Data.progress)
                    hash = hash * 31 + (p.id?.GetHashCode() ?? 0) + p.crafts;
                return hash;
            }
        }

        void SwitchTab(int tab)
        {
            state.Scroll[state.Tab] = scroller.normalizedPosition;
            state.Tab = tab;
            state.Focus = null;
            for (int i = 0; i < 4; i++)
                transform.Find("RecipeTab_" + i).GetComponent<Image>().color = i == tab ? BlacksmithView.Gold : BlacksmithView.Dark;
            if (controller.State == ScreenState.Codex)
                DrawCodex();
            else
                DrawGraph();
        }

        void DrawGraph()
        {
            BlacksmithView.Clear(graph);
            vertices.Clear();
            edges.Clear();
            // Retired recipes stay retired. Their raw resource remains visible.
            var nodes = RecipeGraphLayout.Nodes(state.Tab).Where(n => controller.catalog.Item(n.ItemId) != null).Where(n => controller.catalog.Item(n.ItemId).group == ItemGroup.Gathered || Recipes(n.ItemId).Length > 0).ToList();
            if (state.Focus != null)
            {
                var allowed = Descendants(state.Focus);
                nodes = nodes.Where(n => allowed.Contains(n.ItemId)).ToList();
            }

            if (nodes.Count == 0)
            {
                Detail("아직 발견한 제작 경로가 없습니다.");
                return;
            }

            Canvas.ForceUpdateCanvases();
            graph.sizeDelta = new Vector2(Mathf.Max(scroller.viewport.rect.width, nodes.Max(n => n.Position.x) + 120), Mathf.Max(scroller.viewport.rect.height, nodes.Max(n => n.Position.y) + 90));
            var fogRoot = view.Full("MapFog", graph);
            fog = fogRoot.gameObject.AddComponent<RecipeFogGraphic>();
            fog.raycastTarget = false;
            foreach (var node in nodes)
                fog.RegisterNode(node.ItemId, node.Position);
            var byId = nodes.ToDictionary(n => n.ItemId);
            var links = new HashSet<string>();
            foreach (var node in nodes)
                foreach (var recipe in Recipes(node.ItemId))
                    foreach (var ingredient in recipe.ingredients)
                    {
                        if (!byId.TryGetValue(ingredient.itemId, out var from) || !links.Add(from.ItemId + ">" + node.ItemId))
                            continue;
                        // Supporting materials are shown in the detail panel. Only forward
                        // progression links are drawn, keeping rope/handles from crossing the tree.
                        if (from.Position.x >= node.Position.x)
                            continue;
                        var a = from.Position + new Vector2(74, 0);
                        var b = node.Position - new Vector2(74, 0);
                        float mid = (a.x + b.x) * .5f;
                        AddLine(a, new Vector2(mid, a.y), from.ItemId, node.ItemId);
                        AddLine(new Vector2(mid, a.y), new Vector2(mid, b.y), from.ItemId, node.ItemId);
                        AddLine(new Vector2(mid, b.y), b, from.ItemId, node.ItemId);
                    }

            foreach (var node in nodes)
            {
                var button = view.Button("RecipeVertex_" + node.ItemId, graph, "", Vector2.zero, Vector2.zero, () => Select(node.ItemId));
                Place(button.GetComponent<RectTransform>(), node.Position, new Vector2(148, 78));
                var vertex = new Vertex
                {
                    Node = node,
                    Button = button
                };
                vertices.Add(vertex);
                Reveal(vertex, false);
            }

            fog.transform.SetAsLastSibling();
            RefreshEdges();
            RefreshCount();
            Canvas.ForceUpdateCanvases();
            if (!state.Visited[state.Tab])
            {
                // Start each tree with its resource in view, even on the tall iron map.
                float travel = Mathf.Max(0, graph.rect.height - scroller.viewport.rect.height);
                float offset = Mathf.Clamp(nodes[0].Position.y - scroller.viewport.rect.height * .5f, 0, travel);
                state.Scroll[state.Tab] = new Vector2(0, travel > 0 ? 1 - offset / travel : 1);
                state.Visited[state.Tab] = true;
            }

            scroller.normalizedPosition = state.Scroll[state.Tab];
            scroller.velocity = Vector2.zero;
            var selected = state.Selection[state.Tab];
            if (!vertices.Any(v => v.Node.ItemId == selected && v.Visible))
                selected = vertices.FirstOrDefault(v => v.Visible)?.Node.ItemId;
            Select(selected);
            progressSignature = Signature();
        }

        static void Place(RectTransform rect, Vector2 point, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(point.x, -point.y);
            rect.sizeDelta = size;
        }

        void AddLine(Vector2 a, Vector2 b, string from, string to)
        {
            if ((a - b).sqrMagnitude < .01f)
                return;
            var line = view.Image("RecipeLink", graph, null, BlacksmithView.Gold, Vector2.zero, Vector2.zero);
            Place(line.rectTransform, (a + b) * .5f, new Vector2(Vector2.Distance(a, b), 2));
            line.rectTransform.localRotation = Quaternion.Euler(0, 0, -Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
            edges.Add(Tuple.Create(line, from, to));
            fog.Connect(from, to, a, b);
        }

        void Reveal(Vertex vertex, bool animate)
        {
            bool known = Known(vertex.Node.ItemId);
            bool visible = VisibleHint(vertex.Node.ItemId);
            vertex.Button.interactable = visible;
            vertex.Button.gameObject.SetActive(visible);
            if (!visible)
            {
                vertex.Visible = false;
                return;
            }

            if (vertex.Visible)
                return;
            vertex.Visible = true;
            var item = controller.catalog.Item(vertex.Node.ItemId);
            view.Image("ItemIcon", vertex.Button.transform, known ? item.sprite : null, Color.white, new Vector2(.04f, .20f), new Vector2(.30f, .82f), true);
            var label = view.Text("ItemName", vertex.Button.transform, known ? item.displayName : "?", 20, new Vector2(.30f, .06f), new Vector2(.99f, .94f), null, TextAlignmentOptions.Center);
            label.enableAutoSizing = true;
            label.fontSizeMin = 16;
            label.fontSizeMax = 20;
            var outline = vertex.Button.GetComponent<Outline>();
            if (outline != null)
                outline.effectColor = !known ? Color.gray : item.id == "sharp_branch" ? Color.red : !controller.catalog.recipes.Any(r => r.enabled && r.ingredients.Any(i => i.itemId == item.id)) ? Color.green : BlacksmithView.Gold;
            bool newlyDiscovered = state.Revealed.Add(item.id) && item.group != ItemGroup.Gathered;
            fog.Reveal(item.id, vertex.Node.Position, !newlyDiscovered && !animate);
        }

        void RefreshEdges()
        {
            foreach (var edge in edges)
                edge.Item1.enabled = VisibleHint(edge.Item2) && VisibleHint(edge.Item3);
        }

        void RefreshCount()
        {
            var recipes = vertices.Where(v => controller.catalog.Item(v.Node.ItemId).group != ItemGroup.Gathered).ToArray();
            count.text = $"발견 {recipes.Count(v => Known(v.Node.ItemId))} / {recipes.Length}\n드래그로 지도 이동";
        }

        void Select(string id)
        {
            if (id != null && !VisibleHint(id))
                return;
            state.Selection[state.Tab] = id;
            foreach (var vertex in vertices)
                vertex.Button.GetComponent<Image>().color = vertex.Node.ItemId == id ? new Color(.43f, .32f, .16f) : BlacksmithView.Dark;
            BlacksmithView.Clear(details);
            if (id == null)
            {
                Detail("발견한 정점을 선택하면 제작 정보를 볼 수 있습니다.");
                return;
            }

            var item = controller.catalog.Item(id);
            if (!Known(id))
            {
                var hint = Recipes(id).FirstOrDefault(r => r.ingredients.Any(i => Known(i.itemId)));
                Detail("미발견 레시피 · 에고 망치의 힌트\n" + (hint == null ? "다른 재료로 실험해 보세요." : Stations[(int)hint.station] + "에서 " + string.Join(" / ", hint.ingredients.Where(i => Known(i.itemId)).Select(i => controller.catalog.Item(i.itemId).displayName)) + "를 가공하거나 조합해 보세요."));
                return;
            }

            if (controller.catalog.recipes.Any(r => r.enabled && r.ingredients.Any(i => i.itemId == id)))
            {
                var focus = view.Button("FocusBranch", details, state.Focus == id ? "전체 지도" : "파생 집중 모드", Vector2.zero, Vector2.one, () =>
                {
                    state.Focus = state.Focus == id ? null : id;
                    DrawGraph();
                });
                focus.gameObject.AddComponent<LayoutElement>().preferredHeight = 48;
            }

            if (item.group == ItemGroup.Gathered)
            {
                int owned = controller.Inventory.Data.chest.Concat(controller.Inventory.Data.bag).Where(s => s.itemId == id).Sum(s => s.count);
                Detail($"{item.displayName} · 기본 자원\n보유 {owned}개 · 연결된 레시피를 발견하면 검은 안개가 걷힙니다.");
                return;
            }

            foreach (var recipe in Recipes(id).Where(Discovered))
            {
                var progress = controller.Inventory.Data.progress.First(p => p.id == recipe.id);
                string ingredients = string.Join(" + ", recipe.ingredients.Select(i => $"{controller.catalog.Item(i.itemId)?.displayName ?? i.itemId} ×{i.count}"));
                string method = Stations[(int)recipe.station];
                if (recipe.station == Station.Tools)
                    method += " / " + Tools[(int)recipe.tool] + $" {recipe.strokes}" + (recipe.maxStrokes > recipe.strokes ? $"~{recipe.maxStrokes}" : "") + "회";
                if (recipe.station == Station.Anvil && recipe.anvilHits != null && recipe.anvilHits.Length == 5)
                    method += $" · 상 {recipe.anvilHits[0]} / 하 {recipe.anvilHits[1]} / 좌 {recipe.anvilHits[2]} / 우 {recipe.anvilHits[3]} / 중앙 {recipe.anvilHits[4]}";
                var prepare = view.Button("Prepare_" + recipe.id, details, "재료 배치 · " + recipe.displayName, Vector2.zero, Vector2.one, () => controller.PrepareRecipe(recipe));
                prepare.gameObject.AddComponent<LayoutElement>().preferredHeight = 45;
                Detail($"{item.displayName} ×{recipe.outputCount}    ·    숙련도 {controller.Crafting.Mastery(recipe)} / 제작 {progress.crafts}회\n{ingredients} → {item.displayName}\n{method}");
            }
        }

        void Detail(string message)
        {
            var row = view.Panel("RecipeInformation", details, Vector2.zero, Vector2.one);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 112;
            var text = view.Text("RecipeInformationText", row, message, 20, new Vector2(.02f, .02f), new Vector2(.98f, .98f));
            text.enableAutoSizing = true;
            text.fontSizeMin = 16;
            text.fontSizeMax = 20;
        }

        void Update()
        {
            if (controller == null || controller.State == ScreenState.Codex)
                return;
            if (Time.unscaledTime >= nextCheck)
            {
                nextCheck = Time.unscaledTime + .2f;
                int signature = Signature();
                if (signature != progressSignature)
                {
                    progressSignature = signature;
                    foreach (var vertex in vertices)
                        Reveal(vertex, true);
                    RefreshEdges();
                    RefreshCount();
                    Select(state.Selection[state.Tab]);
                }
            }
        }

        void OnDisable()
        {
            if (scroller != null && state != null)
                state.Scroll[state.Tab] = scroller.normalizedPosition;
        }
    }
}