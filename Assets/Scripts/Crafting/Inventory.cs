using System;
using System.Collections.Generic;
using System.Linq;
using PoorSmith.Data;

namespace PoorSmith.Crafting
{
    /// <summary>
    /// 플레이어가 들고 있는 아이템. 프로토타입에서는 무게 없이 개수만 센다.
    /// (채집 기획서의 배낭 무게는 채집 시스템을 만들 때 다룬다.)
    /// </summary>
    public sealed class Inventory
    {
        readonly Dictionary<ItemDef, int> counts = new();

        public event Action Changed;

        public IEnumerable<KeyValuePair<ItemDef, int>> Entries =>
            counts.Where(pair => pair.Value > 0);

        public int CountOf(ItemDef item) =>
            item != null && counts.TryGetValue(item, out var count) ? count : 0;

        public void Add(ItemDef item, int amount = 1)
        {
            if (item == null || amount <= 0) return;

            counts[item] = CountOf(item) + amount;
            Changed?.Invoke();
        }

        /// <summary>모자라면 아무것도 빼지 않고 false를 돌려준다.</summary>
        public bool TryConsume(IReadOnlyDictionary<ItemDef, int> cost)
        {
            if (cost == null) return true;
            if (cost.Any(line => CountOf(line.Key) < line.Value)) return false;

            foreach (var line in cost)
                counts[line.Key] = CountOf(line.Key) - line.Value;

            Changed?.Invoke();
            return true;
        }

        public bool TryConsume(ItemDef item, int amount = 1) =>
            TryConsume(new Dictionary<ItemDef, int> { [item] = amount });

        public void Clear()
        {
            counts.Clear();
            Changed?.Invoke();
        }

        // ---- 저장 / 불러오기 ----

        public IEnumerable<(string id, int count)> ToSaveData() =>
            Entries.Select(pair => (pair.Key.Id, pair.Value)).OrderBy(entry => entry.Id);

        public void LoadFrom(IEnumerable<(string id, int count)> entries, ItemDatabase database)
        {
            counts.Clear();

            if (entries != null && database != null)
                foreach (var (id, count) in entries)
                {
                    var item = database.FindItem(id);
                    if (item != null && count > 0) counts[item] = count;
                }

            Changed?.Invoke();
        }
    }
}
