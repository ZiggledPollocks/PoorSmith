using System.Collections.Generic;
using System.Linq;
using PoorSmith.Data;

namespace PoorSmith.Crafting
{
    /// <summary>
    /// 지금 지도에 어떤 노드를 그릴지 판단한다.
    ///
    /// 규칙은 둘로 나뉘고, 둘 다 만족해야 보인다.
    ///  1. 도달 범위 — 해금된 노드와 그 한 칸 앞까지만 보인다.
    ///  2. 파생 격리 — 파생·혼합·히든 노드의 아래는 기본 지도에서 감추고,
    ///     그중 파생·혼합 노드만 뼈대로 남긴다. 나머지는 파생 집중 모드에서만 보인다.
    /// </summary>
    public sealed class NodeVisibility
    {
        readonly NodeGraph graph;
        readonly NodeProgress progress;

        public NodeVisibility(NodeGraph graph, NodeProgress progress)
        {
            this.graph = graph;
            this.progress = progress;
        }

        /// <summary>
        /// 해금됐거나, 해금된 노드 바로 다음이거나, 카테고리 진입점이면 도달 범위 안이다.
        /// 시작 노드를 항상 포함시키는 이유는 아무것도 해금되지 않은 첫 화면에서
        /// 지도가 완전히 비어버리는 것을 막기 위해서다.
        /// </summary>
        public bool IsWithinReach(NodeDef node)
        {
            if (node == null) return false;
            if (progress.IsUnlocked(node)) return true;
            if (node.Type == NodeType.Start) return true;

            return graph.ParentsOf(node).Any(progress.IsUnlocked);
        }

        /// <summary>파생 집중 모드에 들어가지 않은 기본 지도에 나오는가.</summary>
        public bool IsOnBaseMap(NodeDef node)
        {
            if (node == null) return false;

            // 히든 노드는 만들어내기 전까지 지도에 없다.
            if (node.Type == NodeType.Hidden && !progress.IsUnlocked(node)) return false;

            // 격리된 자리에서는 파생·혼합 노드만 뼈대로 드러난다.
            if (graph.HasIsolatingAncestor(node)) return node.Type.IsBackbone();

            return true;
        }

        public bool IsVisible(NodeDef node) => IsWithinReach(node) && IsOnBaseMap(node);

        public IEnumerable<NodeDef> VisibleNodes() => graph.Nodes.Where(IsVisible);

        /// <summary>
        /// 파생 집중 모드. 기준 노드와 그 아래만 보여주고 앞쪽은 가린다.
        /// 이 안에서는 파생 격리를 적용하지 않는다. 감춰뒀던 것을 보려고 들어온 모드이기 때문이다.
        /// </summary>
        public IEnumerable<NodeDef> FocusedNodes(NodeDef root)
        {
            if (root == null) return Enumerable.Empty<NodeDef>();

            return graph.SubtreeOf(root).Where(IsWithinReach);
        }

        /// <summary>파생 집중 모드로 들어갈 수 있는 노드인가.</summary>
        public bool CanFocus(NodeDef node) =>
            node != null
            && node.Type.IsolatesChildren()
            && progress.IsUnlocked(node)
            && graph.ChildrenOf(node).Count > 0;
    }
}
