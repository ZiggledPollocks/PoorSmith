using System;
using System.Collections.Generic;
using System.Linq;
using PoorSmith.Crafting;
using PoorSmith.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PoorSmith.UI
{
    /// <summary>
    /// 노드 지도. 화면 왼쪽에 늘 떠 있다.
    ///
    /// 탭으로 감추지 않는 이유는 프로토타입 피드백 9번 — "가이드 노드 탭을 잘 보지 않게 됨" — 때문이다.
    /// 지도를 항상 보이게 두고, 여기서 바로 제작으로 넘어갈 수 있게 하는 것이 이번 구조의 요지다.
    /// </summary>
    internal sealed class NodeMapView : MonoBehaviour, IDragHandler
    {
        static readonly Vector2 Spacing = new(220f, 130f);

        NodeGraph graph;
        NodeProgress progress;
        NodeVisibility visibility;

        RectTransform content;
        Transform lineLayer;
        Transform nodeLayer;

        readonly Dictionary<NodeDef, NodeIconView> icons = new();

        internal NodeDef Selected { get; private set; }

        /// <summary>파생 집중 모드의 기준 노드. null이면 기본 지도다.</summary>
        internal NodeDef FocusRoot { get; private set; }

        internal event Action<NodeDef> SelectionChanged;
        internal event Action FocusChanged;

        internal static NodeMapView Create(
            Transform parent, NodeGraph graph, NodeProgress progress, NodeVisibility visibility)
        {
            var root = UIFactory.Rect("NodeMap", parent);
            UIFactory.Stretch(root);

            var view = root.gameObject.AddComponent<NodeMapView>();
            view.graph = graph;
            view.progress = progress;
            view.visibility = visibility;
            view.Build(root);
            view.Rebuild();
            return view;
        }

        void Build(RectTransform root)
        {
            var background = UIFactory.Panel("Background", root, UITheme.MapBackground);
            UIFactory.Stretch(background.rectTransform);
            background.raycastTarget = true; // 드래그로 지도를 미는 면

            var mask = UIFactory.Rect("Viewport", root);
            UIFactory.Stretch(mask);
            mask.gameObject.AddComponent<RectMask2D>();

            content = UIFactory.Rect("Content", mask);
            content.anchorMin = content.anchorMax = new Vector2(0.5f, 0.5f);
            content.pivot = new Vector2(0.5f, 0.5f);

            lineLayer = UIFactory.Rect("Lines", content);
            UIFactory.Stretch((RectTransform)lineLayer);
            nodeLayer = UIFactory.Rect("Nodes", content);
            UIFactory.Stretch((RectTransform)nodeLayer);
        }

        internal void Rebuild()
        {
            var visible = (FocusRoot != null
                    ? visibility.FocusedNodes(FocusRoot)
                    : visibility.VisibleNodes())
                .ToList();

            ClearLayer(nodeLayer);
            ClearLayer(lineLayer);
            icons.Clear();

            if (visible.Count == 0) return;

            var layout = new NodeLayout(graph, visible, Spacing);
            var center = Center(layout);

            foreach (var node in visible)
            {
                var icon = NodeIconView.Create(node, nodeLayer);
                icon.transform.localPosition = layout.Positions[node] - center;
                icon.Clicked += Select;
                icons[node] = icon;
            }

            foreach (var node in visible)
            foreach (var parent in graph.ParentsOf(node))
            {
                if (parent == null || !icons.ContainsKey(parent)) continue;
                DrawLine(layout.Positions[parent] - center, layout.Positions[node] - center,
                    progress.IsUnlocked(node));
            }

            RefreshIcons();
        }

        static Vector2 Center(NodeLayout layout)
        {
            var positions = layout.Positions.Values;
            var minX = positions.Min(p => p.x);
            var maxX = positions.Max(p => p.x);
            var minY = positions.Min(p => p.y);
            var maxY = positions.Max(p => p.y);
            return new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
        }

        void DrawLine(Vector2 from, Vector2 to, bool unlocked)
        {
            var line = UIFactory.Panel("Line", lineLayer,
                unlocked ? NodePalette.UnlockedLine : NodePalette.Line);

            var delta = to - from;
            var rect = line.rectTransform;
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.localPosition = from;
            rect.sizeDelta = new Vector2(delta.magnitude, unlocked ? 3f : 2f);
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        internal void RefreshIcons()
        {
            foreach (var (node, icon) in icons)
                icon.Refresh(progress.IsUnlocked(node), node == Selected);
        }

        internal void Select(NodeDef node)
        {
            Selected = node;
            RefreshIcons();
            SelectionChanged?.Invoke(node);
        }

        /// <summary>파생 집중 모드로 들어간다. 앞쪽 노드는 가려지고 하위만 보인다.</summary>
        internal void EnterFocus(NodeDef root)
        {
            if (!visibility.CanFocus(root)) return;

            FocusRoot = root;
            content.localPosition = Vector3.zero;
            Rebuild();
            FocusChanged?.Invoke();
        }

        internal void ExitFocus()
        {
            if (FocusRoot == null) return;

            FocusRoot = null;
            content.localPosition = Vector3.zero;
            Rebuild();
            FocusChanged?.Invoke();
        }

        public void OnDrag(PointerEventData eventData) =>
            content.localPosition += (Vector3)eventData.delta;

        static void ClearLayer(Transform layer)
        {
            for (var i = layer.childCount - 1; i >= 0; i--)
                Destroy(layer.GetChild(i).gameObject);
        }
    }
}
