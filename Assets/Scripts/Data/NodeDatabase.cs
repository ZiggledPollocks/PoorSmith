using System.Collections.Generic;
using UnityEngine;

namespace PoorSmith.Data
{
    /// <summary>모든 노드를 모아두는 진입점. 임포터가 목록을 갱신한다.</summary>
    [CreateAssetMenu(menuName = "PoorSmith/노드 데이터베이스", fileName = "NodeDatabase")]
    public sealed class NodeDatabase : ScriptableObject
    {
        [SerializeField] NodeDef[] nodes = new NodeDef[0];

        Dictionary<string, NodeDef> byId;

        public IReadOnlyList<NodeDef> Nodes => nodes;

        public NodeDef Find(string id)
        {
            if (byId == null)
            {
                byId = new Dictionary<string, NodeDef>(nodes.Length);
                foreach (var node in nodes)
                {
                    if (node == null || string.IsNullOrEmpty(node.Id)) continue;
                    if (!byId.TryAdd(node.Id, node))
                        Debug.LogError($"노드 id가 중복된다: '{node.Id}'", node);
                }
            }

            return byId.GetValueOrDefault(id);
        }

        public void InvalidateIndex() => byId = null;
    }
}
