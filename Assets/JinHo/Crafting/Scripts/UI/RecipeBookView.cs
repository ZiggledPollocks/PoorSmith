using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blacksmith
{
    public sealed class RecipeBookState
    {
        public string Selection;
        public bool Visited;
        public Vector2 Scroll = new Vector2(0, 1);
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
        RectTransform graph, details, detailsPanel;
        ScrollRect scroller;
        TMP_Text count;
        readonly List<Vertex> vertices = new List<Vertex>();
        readonly List<Tuple<Image, string, string>> edges = new List<Tuple<Image, string, string>>();
        readonly HashSet<string> newReveals = new HashSet<string>();
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
                foreach (var itemId in owner.Inventory.Data.acquiredItems)
                    state.Revealed.Add(itemId);
                state.Initialized = true;
            }

            view.Text("Title", transform, "레시피 지도", 32, new Vector2(.02f, .91f), new Vector2(.40f, .99f), BlacksmithView.Ink);
            view.Button("CloseRecipes", transform, "닫기", new Vector2(.87f, .92f), new Vector2(.99f, .99f), close);
            count = view.Text("DiscoveryCount", transform, "", 18, new Vector2(.60f, .92f), new Vector2(.86f, .99f), BlacksmithView.Ink, TextAlignmentOptions.Center);
            // One two-dimensional canvas contains all four authored recipe routes.
            graph = view.Scroll(transform, "RecipeGraph", new Vector2(.01f, .02f), new Vector2(.99f, .91f));
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
            detailsPanel = view.Panel("RecipeDetailsPanel", transform, new Vector2(.66f, .06f), new Vector2(.985f, .50f));
            details = view.Scroll(detailsPanel, "RecipeDetails", new Vector2(.025f, .025f), new Vector2(.975f, .90f));
            view.Button("CloseRecipeDetails", detailsPanel, "×", new Vector2(.88f, .90f), new Vector2(.98f, .99f), () => Select(null));
            detailsPanel.gameObject.SetActive(false);
            if (owner.State == ScreenState.Codex)
            {
                DrawCodex();
                return;
            }

            DrawGraph();
        }

        void DrawCodex()
        {
            detailsPanel.gameObject.SetActive(true);
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
                foreach (var itemId in controller.Inventory.Data.acquiredItems.OrderBy(id => id))
                    hash = hash * 31 + (itemId?.GetHashCode() ?? 0);
                return hash;
            }
        }

        void DrawGraph()
        {
            BlacksmithView.Clear(graph);
            vertices.Clear();
            edges.Clear();
            newReveals.Clear();
            // Retired recipes stay retired. Their raw resource remains eligible.
            var nodes = RecipeGraphLayout.AllNodes().Where(n => controller.catalog.Item(n.ItemId) != null)
                .Where(n => controller.catalog.Item(n.ItemId).group == ItemGroup.Gathered || Recipes(n.ItemId).Length > 0).ToList();

            if (nodes.Count == 0)
            {
                Detail("아직 발견한 제작 경로가 없습니다.");
                return;
            }

            Canvas.ForceUpdateCanvases();
            graph.sizeDelta = new Vector2(Mathf.Max(scroller.viewport.rect.width, nodes.Max(n => n.Position.x) + 120), Mathf.Max(scroller.viewport.rect.height, nodes.Max(n => n.Position.y) + 90));
            var byId = nodes.ToDictionary(n => n.ItemId);
            var links = new HashSet<string>();
            foreach (var node in nodes)
                foreach (var recipe in Recipes(node.ItemId))
                    foreach (var ingredient in recipe.ingredients)
                    {
                        if (!byId.TryGetValue(ingredient.itemId, out var from) || !links.Add(from.ItemId + ">" + node.ItemId))
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
                Reveal(vertex);
            }

            RefreshEdges();
            RefreshCount();
            Canvas.ForceUpdateCanvases();
            if (!state.Visited)
            {
                state.Scroll = new Vector2(0, 1);
                state.Visited = true;
            }

            scroller.normalizedPosition = state.Scroll;
            scroller.velocity = Vector2.zero;
            var selected = state.Selection;
            if (!vertices.Any(v => v.Node.ItemId == selected && v.Visible))
                selected = null;
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
            line.enabled = false;
            edges.Add(Tuple.Create(line, from, to));
        }

        void Reveal(Vertex vertex)
        {
            bool known = Known(vertex.Node.ItemId);
            vertex.Button.interactable = known;
            vertex.Button.gameObject.SetActive(known);
            if (!known)
            {
                vertex.Visible = false;
                return;
            }

            if (vertex.Visible)
                return;
            vertex.Visible = true;
            var item = controller.catalog.Item(vertex.Node.ItemId);
            view.Image("ItemIcon", vertex.Button.transform, item.sprite, Color.white, new Vector2(.04f, .20f), new Vector2(.30f, .82f), true);
            var label = view.Text("ItemName", vertex.Button.transform, item.displayName, 20, new Vector2(.30f, .06f), new Vector2(.99f, .94f), null, TextAlignmentOptions.Center);
            label.enableAutoSizing = true;
            label.fontSizeMin = 16;
            label.fontSizeMax = 20;
            var outline = vertex.Button.GetComponent<Outline>();
            if (outline != null)
                outline.effectColor = item.id == "sharp_branch" ? Color.red : !controller.catalog.recipes.Any(r => r.enabled && r.ingredients.Any(i => i.itemId == item.id)) ? Color.green : BlacksmithView.Gold;
            if (state.Revealed.Add(item.id))
            {
                newReveals.Add(item.id);
                if (Application.isPlaying)
                    StartCoroutine(AnimateVertex(vertex.Button));
            }
        }

        void RefreshEdges()
        {
            foreach (var edge in edges)
            {
                bool visible = Known(edge.Item2) && Known(edge.Item3);
                if (visible && !edge.Item1.enabled)
                {
                    edge.Item1.enabled = true;
                    if (Application.isPlaying && (newReveals.Contains(edge.Item2) || newReveals.Contains(edge.Item3)))
                        StartCoroutine(AnimateEdge(edge.Item1));
                }
                else if (!visible)
                    edge.Item1.enabled = false;
            }
            newReveals.Clear();
        }

        void RefreshCount()
        {
            var recipes = vertices.Where(v => controller.catalog.Item(v.Node.ItemId).group != ItemGroup.Gathered).ToArray();
            count.text = $"발견 {recipes.Count(v => Known(v.Node.ItemId))} / {recipes.Length} · 드래그로 지도 이동";
        }

        void Select(string id)
        {
            if (id != null && !Known(id))
                return;
            state.Selection = id;
            foreach (var vertex in vertices)
                vertex.Button.GetComponent<Image>().color = vertex.Node.ItemId == id ? new Color(.43f, .32f, .16f) : BlacksmithView.Dark;
            BlacksmithView.Clear(details);
            detailsPanel.gameObject.SetActive(id != null);
            if (id == null)
                return;

            var item = controller.catalog.Item(id);

            if (item.group == ItemGroup.Gathered)
            {
                int owned = controller.Inventory.Data.chest.Concat(controller.Inventory.Data.bag).Where(s => s.itemId == id).Sum(s => s.count);
                Detail($"{item.displayName} · 기본 자원\n보유 {owned}개 · 제작법을 발견하면 연결된 노드가 나타납니다.");
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

        static float RevealEase(float t) => Mathf.SmoothStep(0, 1, t);

        IEnumerator AnimateVertex(Button button)
        {
            var rect = button.GetComponent<RectTransform>();
            var group = button.gameObject.AddComponent<CanvasGroup>();
            const float duration = .42f;
            float elapsed = 0;
            while (elapsed < duration && button != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = RevealEase(Mathf.Clamp01(elapsed / duration));
                group.alpha = progress;
                rect.localScale = Vector3.one * Mathf.Lerp(.65f, 1, progress);
                yield return null;
            }
            if (button != null)
            {
                group.alpha = 1;
                rect.localScale = Vector3.one;
            }
        }

        IEnumerator AnimateEdge(Image line)
        {
            var rect = line.rectTransform;
            float width = rect.sizeDelta.x;
            var color = line.color;
            const float duration = .38f;
            float elapsed = 0;
            while (elapsed < duration && line != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = RevealEase(Mathf.Clamp01(elapsed / duration));
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width * progress);
                line.color = new Color(color.r, color.g, color.b, color.a * progress);
                yield return null;
            }
            if (line != null)
            {
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                line.color = color;
            }
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
                        Reveal(vertex);
                    RefreshEdges();
                    RefreshCount();
                    Select(state.Selection);
                }
            }
        }

        void OnDisable()
        {
            if (scroller != null && state != null)
                state.Scroll = scroller.normalizedPosition;
        }
    }
}
