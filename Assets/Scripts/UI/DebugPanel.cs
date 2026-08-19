using System;
using System.Linq;
using PoorSmith.Crafting;
using PoorSmith.Data;
using UnityEngine;
using UnityEngine.UI;

namespace PoorSmith.UI
{
    /// <summary>
    /// 기획팀이 원하는 상태를 직접 만들어보라고 두는 패널.
    /// 프로토타입에서는 본 기능만큼 중요하다. "노드를 30개 열면 지도가 어떻게 보이지?"를
    /// 손으로 확인할 수 없으면 판단을 못 하기 때문이다.
    /// </summary>
    internal sealed class DebugPanel : MonoBehaviour
    {
        ItemDatabase items;
        Inventory inventory;
        NodeProgress progress;
        NodeGraph graph;
        CraftingService crafting;

        Text status;

        internal event Action Changed;

        internal static DebugPanel Create(
            Transform parent, ItemDatabase items, NodeGraph graph,
            NodeProgress progress, Inventory inventory, CraftingService crafting)
        {
            var root = UIFactory.Rect("DebugPanel", parent);
            UIFactory.Stretch(root);
            var panel = root.gameObject.AddComponent<DebugPanel>();
            panel.items = items;
            panel.graph = graph;
            panel.progress = progress;
            panel.inventory = inventory;
            panel.crafting = crafting;
            panel.Build(root);
            panel.Refresh();
            return panel;
        }

        void Build(RectTransform root)
        {
            var background = UIFactory.Panel("Background", root, UITheme.DebugBackground);
            UIFactory.Stretch(background.rectTransform);

            var row = UIFactory.Rect("Row", root);
            UIFactory.Stretch(row, 12f);

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = true;
            layout.childAlignment = TextAnchor.MiddleLeft;

            Button(row, "채집 자원 +5", GiveGathered);
            Button(row, "전부 해금", UnlockAll);
            Button(row, "처음으로", ResetAll);

            status = UIFactory.Label("Status", row, "", UITheme.SmallSize, UITheme.Muted, TextAnchor.MiddleRight);
        }

        void GiveGathered()
        {
            foreach (var item in items.Items.Where(i => i.IsGathered && !i.IsFailureResult))
                inventory.Add(item, 5);

            crafting.UnlockReachableStartNodes();
            Notify();
        }

        void UnlockAll()
        {
            foreach (var node in graph.Nodes) progress.Unlock(node);
            Notify();
        }

        void ResetAll()
        {
            progress.Clear();
            inventory.Clear();
            Notify();
        }

        void Notify()
        {
            Refresh();
            Changed?.Invoke();
        }

        internal void Refresh()
        {
            var craftable = crafting.CraftableNow().Count();
            status.text =
                $"해금 {progress.UnlockedCount} / {graph.Nodes.Count}    지금 만들 수 있는 노드 {craftable}개";
        }

        static void Button(Transform parent, string text, UnityEngine.Events.UnityAction onClick)
        {
            var button = UIFactory.Button("Debug", parent, text, UITheme.ButtonFace, UITheme.ButtonText);
            var element = button.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = 200f;
            element.flexibleWidth = 0f;
            button.onClick.AddListener(onClick);
        }
    }
}
