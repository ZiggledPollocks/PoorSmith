using System;
using System.IO;
using System.Linq;
using PoorSmith.Data;
using UnityEngine;

namespace PoorSmith.Crafting
{
    /// <summary>
    /// 해금 상태와 소지품을 파일로 남긴다.
    ///
    /// 기획서상 해금된 노드는 회차가 바뀌어도 계속 열려 있어야 하므로,
    /// 노드 진행도는 저장이 전제인 데이터다.
    /// </summary>
    public static class SaveService
    {
        const string FileName = "node-progress.json";

        static string Path => System.IO.Path.Combine(Application.persistentDataPath, FileName);

        public static bool Exists => File.Exists(Path);

        public static void Save(NodeProgress progress, Inventory inventory)
        {
            var data = new SaveData
            {
                unlockedNodes = progress.ToSaveData().ToArray(),
                items = inventory.ToSaveData()
                    .Select(entry => new ItemStack { id = entry.id, count = entry.count })
                    .ToArray(),
            };

            try
            {
                File.WriteAllText(Path, JsonUtility.ToJson(data, true));
            }
            catch (Exception e)
            {
                Debug.LogError($"[저장] 실패 — {e.Message}");
            }
        }

        /// <summary>저장된 게 없거나 읽지 못하면 false. 이 경우 호출한 쪽이 새 판을 준비하면 된다.</summary>
        public static bool Load(
            NodeProgress progress, Inventory inventory, NodeDatabase nodes, ItemDatabase items)
        {
            if (!Exists) return false;

            SaveData data;
            try
            {
                data = JsonUtility.FromJson<SaveData>(File.ReadAllText(Path));
            }
            catch (Exception e)
            {
                Debug.LogError($"[저장] 불러오기 실패 — {e.Message}");
                return false;
            }

            if (data == null) return false;

            progress.LoadFrom(data.unlockedNodes, nodes);
            inventory.LoadFrom(
                (data.items ?? Array.Empty<ItemStack>()).Select(stack => (stack.id, stack.count)),
                items);

            return true;
        }

        public static void Delete()
        {
            if (Exists) File.Delete(Path);
        }

        [Serializable]
        sealed class SaveData
        {
            public string[] unlockedNodes;
            public ItemStack[] items;
        }

        [Serializable]
        struct ItemStack
        {
            public string id;
            public int count;
        }
    }
}
