using System.Collections.Generic;
using System.Linq;
using PoorSmith.Data;
using UnityEngine;

namespace PoorSmith.Crafting
{
    /// <summary>
    /// 노드를 왼쪽에서 오른쪽으로 자라는 트리 모양으로 배치한다.
    /// 노드에 좌표가 직접 지정돼 있으면 그것을 쓰고, 없으면 여기서 계산한다.
    ///
    /// 깊이는 부모까지의 가장 긴 경로로 정한다. 혼합 노드처럼 부모가 둘이면
    /// 늦게 도달하는 쪽에 맞춰야 선이 뒤로 가지 않기 때문이다.
    /// </summary>
    public sealed class NodeLayout
    {
        readonly Dictionary<NodeDef, Vector2> positions = new();

        public IReadOnlyDictionary<NodeDef, Vector2> Positions => positions;

        /// <summary>배치된 영역의 크기. 스크롤 범위를 잡는 데 쓴다.</summary>
        public Vector2 Size { get; private set; }

        public NodeLayout(NodeGraph graph, IEnumerable<NodeDef> nodes, Vector2 spacing)
        {
            var placed = nodes?.Where(n => n != null).Distinct().ToList() ?? new List<NodeDef>();
            if (placed.Count == 0) return;

            var visible = new HashSet<NodeDef>(placed);
            var depths = MeasureDepths(graph, placed, visible);

            var layers = placed
                .GroupBy(node => depths[node])
                .OrderBy(layer => layer.Key)
                .ToList();

            var rows = new Dictionary<NodeDef, float>();

            foreach (var layer in layers)
            {
                // 부모가 놓인 높이의 평균을 따라가면 선이 덜 엉킨다.
                var ordered = layer
                    .OrderBy(node => AverageParentRow(graph, node, rows, visible))
                    .ThenBy(node => node.Id)
                    .ToList();

                for (var i = 0; i < ordered.Count; i++)
                    rows[ordered[i]] = i;
            }

            foreach (var node in placed)
                positions[node] = node.UseManualPosition
                    ? node.MapPosition
                    : new Vector2(depths[node] * spacing.x, -rows[node] * spacing.y);

            var xs = positions.Values.Select(p => p.x).ToList();
            var ys = positions.Values.Select(p => p.y).ToList();
            Size = new Vector2(xs.Max() - xs.Min() + spacing.x, ys.Max() - ys.Min() + spacing.y);
        }

        static Dictionary<NodeDef, int> MeasureDepths(
            NodeGraph graph, IReadOnlyList<NodeDef> placed, HashSet<NodeDef> visible)
        {
            var depths = new Dictionary<NodeDef, int>();
            var resolving = new HashSet<NodeDef>();

            foreach (var node in placed) Measure(node);

            return depths;

            int Measure(NodeDef node)
            {
                if (depths.TryGetValue(node, out var known)) return known;

                // 순환이 있어도 멈추지 않도록 계산 중인 노드는 0으로 끊는다.
                if (!resolving.Add(node)) return 0;

                var parents = graph.ParentsOf(node).Where(p => p != null && visible.Contains(p)).ToList();
                var depth = parents.Count == 0 ? 0 : parents.Max(Measure) + 1;

                resolving.Remove(node);
                depths[node] = depth;
                return depth;
            }
        }

        static float AverageParentRow(
            NodeGraph graph, NodeDef node, IReadOnlyDictionary<NodeDef, float> rows, HashSet<NodeDef> visible)
        {
            var parentRows = graph.ParentsOf(node)
                .Where(p => p != null && visible.Contains(p) && rows.ContainsKey(p))
                .Select(p => rows[p])
                .ToList();

            return parentRows.Count == 0 ? 0f : parentRows.Average();
        }
    }
}
