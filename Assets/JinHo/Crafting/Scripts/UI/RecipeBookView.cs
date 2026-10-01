// [코드 지도] RecipeBookView: 레시피 도감·그래프·발견 효과와 상세 패널을 그린다.
// 주요 함수: DrawGraph, Initialize, Select
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Crafting/Scripts/UI/RecipeBookView.cs.md

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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

    // The ScrollRect viewport receives clicks on empty map space. Vertex
    // buttons handle their own clicks, while dragging remains with ScrollRect.
    public sealed class RecipeGraphBackgroundClick : MonoBehaviour, IPointerClickHandler
    {
        public Action Click;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && !eventData.dragging)
                Click?.Invoke();
        }
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
        readonly List<Tuple<Image, string, string>> focusArrows = new List<Tuple<Image, string, string>>();
        readonly HashSet<string> newReveals = new HashSet<string>();
        readonly HashSet<string> knownItems = new HashSet<string>();
        readonly HashSet<string> visibleHints = new HashSet<string>();
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
        // 핵심 분기: !state.Initialized 판정.
        // 상태 변경: controller 갱신.
        // 다음 연결: Blacksmith.RecipeBookView.Discovered(Blacksmith.RecipeDefinition) 호출.
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
            graph = view.Scroll(transform, "RecipeGraph", new Vector2(.01f, .02f), new Vector2(.99f, .90f));
            DestroyImmediate(graph.GetComponent<VerticalLayoutGroup>());
            DestroyImmediate(graph.GetComponent<ContentSizeFitter>());
            scroller = graph.parent.parent.GetComponent<ScrollRect>();
            scroller.viewport.gameObject.AddComponent<RecipeGraphBackgroundClick>().Click = () => Select(null);
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
        // A discovered output exposes every ingredient generation in its authored ancestry.
        void CollectPrerequisites(string itemId, HashSet<string> nodes, HashSet<string> links = null)
        {
            if (!nodes.Add(itemId)) return;
            foreach (var recipe in Recipes(itemId))
                foreach (var ingredient in recipe.ingredients)
                {
                    links?.Add(ingredient.itemId + ">" + itemId);
                    CollectPrerequisites(ingredient.itemId, nodes, links);
                }
        }

        void RebuildKnownItems()
        {
            knownItems.Clear();
            foreach (var recipe in controller.catalog.recipes.Where(r => r.enabled && Discovered(r)))
                CollectPrerequisites(recipe.outputId, knownItems);
            foreach (var itemId in controller.Inventory.Data.acquiredItems)
                knownItems.Add(itemId);
            // An acquired recipe output can be visible before its recipe is
            // discovered. Expose its full ancestry so selecting it never
            // focuses parent vertices or links that are still hidden.
            visibleHints.Clear();
            foreach (var itemId in knownItems)
                if (Recipes(itemId).Length > 0)
                    CollectPrerequisites(itemId, visibleHints);
            // Each unlocked item exposes its immediate children and every
            // prerequisite of those children, including alternate routes.
            foreach (var recipe in controller.catalog.recipes.Where(r => r.enabled &&
                r.ingredients.Any(i => Unlocked(i.itemId))))
                CollectPrerequisites(recipe.outputId, visibleHints);
        }

        bool Known(string itemId) => !string.IsNullOrEmpty(itemId) &&
            controller.catalog.Item(itemId) != null && knownItems.Contains(itemId);
        // Owning an item or seeing it as an ancestor does not discover its recipe.
        bool Unlocked(string itemId) => !string.IsNullOrEmpty(itemId) && Known(itemId) &&
            (Recipes(itemId).Length == 0 || Recipes(itemId).Any(Discovered));
        bool Visible(string itemId) => !string.IsNullOrEmpty(itemId) &&
            (Known(itemId) || visibleHints.Contains(itemId));

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

        // 핵심 분기: nodes.Count == 0 판정.
        // 상태 변경: graph.sizeDelta 갱신.
        // 다음 연결: Blacksmith.BlacksmithView.Clear(UnityEngine.Transform) 호출.
        void DrawGraph()
        {
            BlacksmithView.Clear(graph);
            vertices.Clear();
            edges.Clear();
            focusArrows.Clear();
            newReveals.Clear();
            RebuildKnownItems();
            // Retired recipes stay retired. Their raw resource remains eligible.
            var ingredients = new HashSet<string>(controller.catalog.recipes.Where(r => r.enabled)
                .SelectMany(r => r.ingredients).Select(i => i.itemId));
            var nodes = RecipeGraphLayout.AllNodes().Where(n => controller.catalog.Item(n.ItemId) != null)
                .Where(n => controller.catalog.Item(n.ItemId).group == ItemGroup.Gathered || Recipes(n.ItemId).Length > 0 || ingredients.Contains(n.ItemId)).ToList();

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
                        // Enter the child from the side facing its parent, even
                        // when an ingredient is placed to the child's right.
                        float direction = node.Position.x >= from.Position.x ? 1f : -1f;
                        var a = from.Position + new Vector2(74 * direction, 0);
                        var b = node.Position - new Vector2(74 * direction, 0);
                        float mid = (a.x + b.x) * .5f;
                        AddLine(a, new Vector2(mid, a.y), from.ItemId, node.ItemId);
                        AddLine(new Vector2(mid, a.y), new Vector2(mid, b.y), from.ItemId, node.ItemId);
                        AddLine(new Vector2(mid, b.y), b, from.ItemId, node.ItemId);
                        AddArrowHead(a, b, mid, from.ItemId, node.ItemId,
                            recipe.station == Station.Workbench ? Mathf.Max(1, ingredient.count) : 1);
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
            if (!vertices.Any(v => v.Node.ItemId == selected && Visible(v.Node.ItemId)))
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

        // Each workbench ingredient uses one arrowhead per required item.
        // Arrowheads appear only while the prerequisite link is highlighted.
        void AddArrowHead(Vector2 start, Vector2 end, float mid, string from, string to, int count)
        {
            var approach = end - new Vector2(mid, end.y);
            if (approach.sqrMagnitude < .01f)
                approach = end - new Vector2(mid, start.y);
            if (approach.sqrMagnitude < .01f)
                approach = end - start;
            if (approach.sqrMagnitude < .01f)
                return;
            var direction = approach.normalized;
            var side = new Vector2(-direction.y, direction.x);
            for (int index = 0; index < count; index++)
            {
                var tip = end - direction * (8f + index * 12f);
                AddArrowSegment(tip - direction * 10f + side * 6f, tip, from, to);
                AddArrowSegment(tip - direction * 10f - side * 6f, tip, from, to);
            }
        }

        void AddArrowSegment(Vector2 start, Vector2 end, string from, string to)
        {
            var arrow = view.Image("RecipeFocusArrow", graph, null, Color.black,
                Vector2.zero, Vector2.zero);
            Place(arrow.rectTransform, (start + end) * .5f,
                new Vector2(Vector2.Distance(start, end), 4));
            arrow.rectTransform.localRotation = Quaternion.Euler(0, 0,
                -Mathf.Atan2(end.y - start.y, end.x - start.x) * Mathf.Rad2Deg);
            arrow.enabled = false;
            focusArrows.Add(Tuple.Create(arrow, from, to));
        }

        // 핵심 분기: !known 판정.
        // 상태 변경: vertex.Button.interactable 갱신.
        // 다음 연결: Blacksmith.RecipeBookView.Known(string) 호출.
        void Reveal(Vertex vertex)
        {
            bool known = Unlocked(vertex.Node.ItemId);
            bool visible = Visible(vertex.Node.ItemId);
            // Locked hints can be selected for the old recipe clue, but not crafted.
            vertex.Button.interactable = visible;
            vertex.Button.gameObject.SetActive(visible);
            if (!visible)
            {
                vertex.Visible = false;
                return;
            }

            vertex.Visible = true;
            var item = controller.catalog.Item(vertex.Node.ItemId);
            var icon = vertex.Button.transform.Find("ItemIcon")?.GetComponent<Image>();
            if (icon == null)
                icon = view.Image("ItemIcon", vertex.Button.transform, null, Color.white,
                    new Vector2(.04f, .20f), new Vector2(.30f, .82f), true);
            icon.sprite = known ? view.ItemArt(item) : null;
            var label = vertex.Button.transform.Find("ItemName")?.GetComponent<TMP_Text>();
            if (label == null)
            {
                label = view.Text("ItemName", vertex.Button.transform, "", 20,
                    new Vector2(.30f, .06f), new Vector2(.99f, .94f), null, TextAlignmentOptions.Center);
                label.enableAutoSizing = true;
                label.fontSizeMin = 16;
                label.fontSizeMax = 20;
            }
            label.text = known ? item.displayName : "미발견";
            var outline = vertex.Button.GetComponent<Outline>();
            if (outline != null)
                outline.effectColor = known ? BlacksmithView.Gold : new Color(.35f, .35f, .35f);
            if (known && state.Revealed.Add(item.id))
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
                // A known node exposes the line to its immediate locked child.
                bool visible = Visible(edge.Item2) && Visible(edge.Item3);
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
            RefreshFocus();
        }

        void RefreshCount()
        {
            var recipeVertices = vertices.Where(v => Recipes(v.Node.ItemId).Length > 0).ToArray();
            count.text = $"발견 {recipeVertices.Count(v => Recipes(v.Node.ItemId).Any(Discovered))} / {recipeVertices.Length} · 드래그로 지도 이동";
        }

        // Give the selected vertex, its entire parent chain, and their links
        // distinct bright treatments while fading unrelated branches.
        void RefreshFocus()
        {
            if (state == null || controller == null) return;
            var focusedNodes = new HashSet<string>();
            var focusedLinks = new HashSet<string>();
            if (!string.IsNullOrEmpty(state.Selection))
            {
                CollectPrerequisites(state.Selection, focusedNodes, focusedLinks);
            }

            bool active = focusedNodes.Count > 0;
            foreach (var vertex in vertices)
            {
                if (!vertex.Button) continue;
                var group = vertex.Button.GetComponent<CanvasGroup>();
                if (!group) group = vertex.Button.gameObject.AddComponent<CanvasGroup>();
                if (!group) continue;
                bool related = !active || focusedNodes.Contains(vertex.Node.ItemId);
                bool selected = vertex.Node.ItemId == state.Selection;
                group.alpha = related ? 1f : .20f;
                vertex.Button.image.color = selected ? new Color(1f, .73f, .25f) :
                    active && related ? new Color(.72f, .46f, .18f) :
                    Unlocked(vertex.Node.ItemId) ? BlacksmithView.Dark : new Color(.20f, .20f, .20f);
                var outline = vertex.Button.GetComponent<Outline>();
                if (outline)
                {
                    outline.effectColor = selected ? Color.white :
                        active && related ? new Color(1f, .83f, .36f) :
                        Unlocked(vertex.Node.ItemId) ? BlacksmithView.Gold : new Color(.35f, .35f, .35f);
                    outline.effectDistance = selected ? new Vector2(5, -5) :
                        active && related ? new Vector2(3, -3) : new Vector2(1, -1);
                }
                var label = vertex.Button.transform.Find("ItemName")?.GetComponent<TMP_Text>();
                if (label != null) label.color = selected ? BlacksmithView.Ink : BlacksmithView.Cream;
            }
            foreach (var edge in edges)
            {
                if (!edge.Item1) continue;
                var group = edge.Item1.GetComponent<CanvasGroup>();
                if (!group) group = edge.Item1.gameObject.AddComponent<CanvasGroup>();
                if (!group) continue;
                bool focused = active && focusedLinks.Contains(edge.Item2 + ">" + edge.Item3);
                group.alpha = focused ? 1f : active ? .16f : .65f;
                edge.Item1.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, focused ? 5f : 2f);
                edge.Item1.color = focused ? new Color(1f, .85f, .32f) :
                    Known(edge.Item2) && Known(edge.Item3)
                        ? new Color(.42f, .39f, .34f) : new Color(.26f, .26f, .26f);
            }
            foreach (var arrow in focusArrows)
            {
                if (!arrow.Item1) continue;
                bool focused = active && Visible(arrow.Item2) && Visible(arrow.Item3) &&
                    focusedLinks.Contains(arrow.Item2 + ">" + arrow.Item3);
                arrow.Item1.enabled = focused;
                if (focused)
                    arrow.Item1.transform.SetAsLastSibling();
            }
        }

        // 핵심 분기: id != null && !Visible(id) 판정.
        // 상태 변경: state.Selection 갱신.
        // 다음 연결: Blacksmith.RecipeBookView.Known(string) 호출.
        void Select(string id)
        {
            if (id != null && !Visible(id))
                return;
            state.Selection = id;
            RefreshFocus();
            BlacksmithView.Clear(details);
            detailsPanel.gameObject.SetActive(id != null);
            if (id == null)
                return;

            var item = controller.catalog.Item(id);
            if (item == null) return;
            if (!Unlocked(id))
            {
                // Restore the clue shown by the recipe map before its routes were unified.
                var hint = Recipes(id).FirstOrDefault(r => r.ingredients.Any(i => Unlocked(i.itemId)));
                Detail("미발견 레시피 · 에고 망치의 힌트\n" +
                    (hint == null ? "다른 재료로 실험해 보세요." :
                        Stations[(int)hint.station] + "에서 " +
                        string.Join(" / ", hint.ingredients.Where(i => Unlocked(i.itemId))
                            .Select(i => controller.catalog.Item(i.itemId).displayName)) +
                        "를 가공하거나 조합해 보세요."));
                return;
            }
            Detail(item.displayName + (string.IsNullOrWhiteSpace(item.description) ? "" : "\n" + item.description));

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
                    method += " / " + Tools[(int)recipe.tool];
                if (recipe.station == Station.Anvil && recipe.anvilHits != null && recipe.anvilHits.Length == 5)
                    method += $" · 상 {recipe.anvilHits[0]} / 하 {recipe.anvilHits[1]} / 좌 {recipe.anvilHits[2]} / 우 {recipe.anvilHits[3]} / 중앙 {recipe.anvilHits[4]}";
                var prepare = view.Button("Prepare_" + recipe.id, details, "재료 배치 · " + recipe.displayName, Vector2.zero, Vector2.one, () => controller.PrepareRecipe(recipe));
                prepare.gameObject.AddComponent<LayoutElement>().preferredHeight = 45;
                Detail($"{item.displayName} ×{recipe.outputCount}    ·    숙련도 {controller.Crafting.Mastery(recipe)} / 제작 {progress.crafts}회\n{ingredients} → {item.displayName}\n{method}");
            }
            if (!Recipes(id).Any(Discovered))
                Detail("제작법을 아직 발견하지 않았습니다. 연결된 선행 재료는 지도에서 확인할 수 있습니다.");
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
            if (!button) yield break;
            var rect = button.GetComponent<RectTransform>();
            var group = button.GetComponent<CanvasGroup>();
            if (!group) group = button.gameObject.AddComponent<CanvasGroup>();
            const float duration = .42f;
            float elapsed = 0;
            while (elapsed < duration && button)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = RevealEase(Mathf.Clamp01(elapsed / duration));
                group.alpha = progress;
                rect.localScale = Vector3.one * Mathf.Lerp(.65f, 1, progress);
                yield return null;
            }
            if (button)
            {
                group.alpha = 1f;
                RefreshFocus();
                rect.localScale = Vector3.one;
            }
        }

        // 핵심 분기: line != null 판정.
        // 상태 변경: elapsed 갱신.
        // 다음 연결: Blacksmith.RecipeBookView.RevealEase(float) 호출.
        IEnumerator AnimateEdge(Image line)
        {
            if (!line) yield break;
            var rect = line.rectTransform;
            float width = rect.sizeDelta.x;
            var color = line.color;
            const float duration = .38f;
            float elapsed = 0;
            while (elapsed < duration && line)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = RevealEase(Mathf.Clamp01(elapsed / duration));
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width * progress);
                line.color = new Color(color.r, color.g, color.b, color.a * progress);
                yield return null;
            }
            if (line)
            {
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                line.color = color;
                RefreshFocus();
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
                    RebuildKnownItems();
                    foreach (var vertex in vertices)
                        Reveal(vertex);
                    RefreshEdges();
                    RefreshCount();
                    Select(Visible(state.Selection) ? state.Selection : null);
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
