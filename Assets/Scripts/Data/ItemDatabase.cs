using System.Collections.Generic;
using UnityEngine;

namespace PoorSmith.Data
{
    /// <summary>아이템·도구·카테고리와 실패 규칙을 모아두는 진입점. 임포터가 목록을 갱신한다.</summary>
    [CreateAssetMenu(menuName = "PoorSmith/아이템 데이터베이스", fileName = "ItemDatabase")]
    public sealed class ItemDatabase : ScriptableObject
    {
        [SerializeField] ItemCategory[] categories = new ItemCategory[0];
        [SerializeField] ToolDef[] tools = new ToolDef[0];
        [SerializeField] ItemDef[] items = new ItemDef[0];
        [SerializeField] FailureRule[] failureRules = new FailureRule[0];

        Dictionary<string, ItemCategory> categoryById;
        Dictionary<string, ToolDef> toolById;
        Dictionary<string, ItemDef> itemById;

        public IReadOnlyList<ItemCategory> Categories => categories;
        public IReadOnlyList<ToolDef> Tools => tools;
        public IReadOnlyList<ItemDef> Items => items;

        public ItemCategory FindCategory(string id) =>
            Index(ref categoryById, categories, c => c.Id).GetValueOrDefault(id);

        public ToolDef FindTool(string id) =>
            Index(ref toolById, tools, t => t.Id).GetValueOrDefault(id);

        public ItemDef FindItem(string id) =>
            Index(ref itemById, items, i => i.Id).GetValueOrDefault(id);

        /// <summary>해당 도구와 카테고리 조합에서 실패했을 때 나오는 물건. 규칙이 없으면 null.</summary>
        public ItemDef FailureFor(ToolDef tool, ItemCategory category)
        {
            foreach (var rule in failureRules)
                if (rule.Matches(tool, category)) return rule.Output;

            return null;
        }

        public void InvalidateIndex()
        {
            categoryById = null;
            toolById = null;
            itemById = null;
        }

        static Dictionary<string, T> Index<T>(ref Dictionary<string, T> cache, T[] source, System.Func<T, string> idOf)
            where T : Object
        {
            if (cache != null) return cache;

            cache = new Dictionary<string, T>(source.Length);
            foreach (var entry in source)
            {
                if (entry == null) continue;

                var id = idOf(entry);
                if (string.IsNullOrEmpty(id))
                {
                    Debug.LogWarning($"id가 비어 있는 항목을 건너뛴다: {entry.name}", entry);
                    continue;
                }

                if (!cache.TryAdd(id, entry))
                    Debug.LogError($"id가 중복된다: '{id}' ({entry.name})", entry);
            }

            return cache;
        }
    }
}
