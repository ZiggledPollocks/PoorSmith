using System;
using System.Collections.Generic;
using System.Linq;
using PoorSmith.Data;

namespace PoorSmith.Crafting
{
    /// <summary>
    /// 어떤 노드가 해금됐는지에 대한 기록.
    /// 기획서상 해금 상태는 회차가 바뀌어도 유지되므로 저장 대상이다.
    /// </summary>
    public sealed class NodeProgress
    {
        readonly HashSet<string> unlocked = new();

        /// <summary>노드가 새로 해금될 때마다 불린다.</summary>
        public event Action<NodeDef> Unlocked;

        public int UnlockedCount => unlocked.Count;

        public bool IsUnlocked(NodeDef node) => node != null && unlocked.Contains(node.Id);

        /// <summary>이미 해금돼 있었으면 false를 돌려주고 아무 일도 하지 않는다.</summary>
        public bool Unlock(NodeDef node)
        {
            if (node == null || !unlocked.Add(node.Id)) return false;

            Unlocked?.Invoke(node);
            return true;
        }

        public void Clear() => unlocked.Clear();

        // ---- 저장 / 불러오기 ----

        public IEnumerable<string> ToSaveData() => unlocked.OrderBy(id => id);

        /// <summary>저장된 id를 되살린다. 지금 데이터에 없는 id는 조용히 버린다.</summary>
        public void LoadFrom(IEnumerable<string> ids, NodeDatabase database)
        {
            unlocked.Clear();
            if (ids == null) return;

            foreach (var id in ids)
                if (database == null || database.Find(id) != null)
                    unlocked.Add(id);
        }
    }
}
