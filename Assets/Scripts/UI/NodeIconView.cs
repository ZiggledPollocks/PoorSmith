using System;
using PoorSmith.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PoorSmith.UI
{
    /// <summary>
    /// 노드 지도 위의 아이콘 하나.
    /// 테두리(상태) / 안쪽(카테고리) / 아이콘 또는 이름 세 겹으로 그린다.
    /// </summary>
    internal sealed class NodeIconView : MonoBehaviour, IPointerClickHandler
    {
        Image border;
        Image fill;
        Image icon;
        Text label;

        internal NodeDef Node { get; private set; }
        internal event Action<NodeDef> Clicked;

        internal static NodeIconView Create(NodeDef node, Transform parent)
        {
            var root = UIFactory.Rect($"Node_{node.Id}", parent);
            var view = root.gameObject.AddComponent<NodeIconView>();
            view.Build(node, root);
            return view;
        }

        void Build(NodeDef node, RectTransform root)
        {
            Node = node;

            var round = NodePalette.IsRound(node.Type);
            var sprite = round ? UIFactory.Circle : UIFactory.Square;
            var size = NodePalette.SizeOf(node.Type);
            root.sizeDelta = new Vector2(size, size);

            border = UIFactory.Panel("Border", root, Color.white, sprite);
            UIFactory.Stretch(border.rectTransform);
            border.raycastTarget = true; // 클릭을 받는 면

            fill = UIFactory.Panel("Fill", border.transform, Color.white, sprite);
            UIFactory.Stretch(fill.rectTransform, size * 0.12f);

            icon = UIFactory.Panel("Icon", fill.transform, Color.white);
            UIFactory.Stretch(icon.rectTransform, size * 0.18f);
            icon.preserveAspect = true;

            // 아이콘이 없는 동안에는 이름을 아래에 적어 어떤 노드인지 알아볼 수 있게 한다.
            label = UIFactory.Label("Name", root, node.DisplayName, UITheme.NodeLabelSize, UITheme.Body);
            label.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            label.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            label.rectTransform.pivot = new Vector2(0.5f, 1f);
            label.rectTransform.anchoredPosition = new Vector2(0f, -6f);
            label.rectTransform.sizeDelta = new Vector2(200f, 48f);
        }

        internal void Refresh(bool unlocked, bool selected)
        {
            border.color = selected
                ? NodePalette.SelectedBorder
                : NodePalette.BorderOf(Node.Type, unlocked);

            fill.color = NodePalette.FillOf(Node.Category, unlocked);

            var sprite = Node.Icon;
            icon.sprite = sprite;
            icon.enabled = sprite != null;

            // 미해금 노드는 무엇인지 감추는 것이 기획 의도라 이름을 가린다.
            label.text = unlocked ? Node.DisplayName : "?";
            label.color = unlocked ? UITheme.Body : UITheme.Muted;
        }

        public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke(Node);
    }
}
