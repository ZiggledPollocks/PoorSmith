using System.Collections.Generic;
using System.Linq;
using PoorSmith.Data;

namespace PoorSmith.Crafting
{
    /// <summary>
    /// 노드들의 연결 관계를 훑기 좋게 정리해둔 것.
    /// 자식 목록은 부모 참조에서 거꾸로 만든다. 양쪽에 적어두면 언젠가 어긋나기 때문이다.
    /// 혼합 노드가 부모를 둘 가지므로 트리가 아니라 DAG로 다룬다.
    /// </summary>
    public sealed class NodeGraph
    {
        static readonly IReadOnlyList<NodeDef> None = new NodeDef[0];

        readonly Dictionary<NodeDef, List<NodeDef>> childrenOf = new();
        readonly Dictionary<NodeDef, bool> isolatedCache = new();
        readonly List<string> problems = new();

        public IReadOnlyList<NodeDef> Nodes { get; }

        /// <summary>순환 참조나 빠진 부모 같은, 데이터가 잘못돼서 생긴 문제들.</summary>
        public IReadOnlyList<string> Problems => problems;

        public NodeGraph(IEnumerable<NodeDef> nodes)
        {
            Nodes = nodes?.Where(n => n != null).ToArray() ?? new NodeDef[0];

            BuildChildrenIndex();
            DetectCycles();
        }

        public IReadOnlyList<NodeDef> ChildrenOf(NodeDef node) =>
            node != null && childrenOf.TryGetValue(node, out var list) ? list : None;

        public IReadOnlyList<NodeDef> ParentsOf(NodeDef node) => node?.Parents ?? None;

        /// <summary>파생·혼합·히든 노드 아래에 놓여 있어 기본 지도에서 격리되는 위치인가.</summary>
        public bool HasIsolatingAncestor(NodeDef node)
        {
            if (node == null) return false;
            if (isolatedCache.TryGetValue(node, out var cached)) return cached;

            // 계산 중 다시 들어오는 경우(순환)는 false로 끊는다.
            isolatedCache[node] = false;

            var isolated = false;
            foreach (var parent in ParentsOf(node))
            {
                if (parent == null) continue;
                if (parent.Type.IsolatesChildren() || HasIsolatingAncestor(parent))
                {
                    isolated = true;
                    break;
                }
            }

            isolatedCache[node] = isolated;
            return isolated;
        }

        public bool IsDescendantOf(NodeDef node, NodeDef ancestor)
        {
            if (node == null || ancestor == null || node == ancestor) return false;

            var seen = new HashSet<NodeDef>();
            var pending = new Stack<NodeDef>(ParentsOf(node).Where(p => p != null));

            while (pending.Count > 0)
            {
                var current = pending.Pop();
                if (current == ancestor) return true;
                if (!seen.Add(current)) continue;

                foreach (var parent in ParentsOf(current))
                    if (parent != null) pending.Push(parent);
            }

            return false;
        }

        /// <summary>해당 노드와 그 아래 전부. 파생 집중 모드에서 보여줄 범위다.</summary>
        public IEnumerable<NodeDef> SubtreeOf(NodeDef root)
        {
            if (root == null) yield break;

            var seen = new HashSet<NodeDef> { root };
            var pending = new Stack<NodeDef>();
            pending.Push(root);

            while (pending.Count > 0)
            {
                var current = pending.Pop();
                yield return current;

                foreach (var child in ChildrenOf(current))
                    if (seen.Add(child)) pending.Push(child);
            }
        }

        void BuildChildrenIndex()
        {
            foreach (var node in Nodes)
                childrenOf[node] = new List<NodeDef>();

            foreach (var node in Nodes)
            {
                var parents = node.Parents;

                if (parents.Count == 0 && node.Type != NodeType.Start)
                    problems.Add($"{node}: 부모가 없는데 시작 노드가 아니다.");

                if (node.Type == NodeType.Mixed && parents.Count != 2)
                    problems.Add($"{node}: 혼합 노드인데 부모가 {parents.Count}개다. 둘이어야 한다.");

                foreach (var parent in parents)
                {
                    if (parent == null)
                    {
                        problems.Add($"{node}: 부모 목록에 빈 칸이 있다.");
                        continue;
                    }

                    if (!childrenOf.TryGetValue(parent, out var siblings))
                    {
                        problems.Add($"{node}: 부모 {parent} 가 데이터베이스에 없다.");
                        continue;
                    }

                    siblings.Add(node);
                }
            }

            foreach (var node in Nodes)
                if (node.Type == NodeType.Terminal && childrenOf[node].Count > 0)
                    problems.Add($"{node}: 종결 노드인데 하위 노드가 있다.");
        }

        void DetectCycles()
        {
            var state = new Dictionary<NodeDef, int>(); // 0 미방문, 1 방문중, 2 완료

            foreach (var node in Nodes)
                Walk(node, new List<NodeDef>());

            void Walk(NodeDef node, List<NodeDef> path)
            {
                if (state.TryGetValue(node, out var mark))
                {
                    if (mark == 1)
                    {
                        var loop = string.Join(" → ", path.Select(n => n.Id).Append(node.Id));
                        problems.Add($"부모 관계가 순환한다: {loop}");
                    }
                    return;
                }

                state[node] = 1;
                path.Add(node);

                foreach (var parent in node.Parents)
                    if (parent != null && childrenOf.ContainsKey(parent)) Walk(parent, path);

                path.RemoveAt(path.Count - 1);
                state[node] = 2;
            }
        }
    }
}
