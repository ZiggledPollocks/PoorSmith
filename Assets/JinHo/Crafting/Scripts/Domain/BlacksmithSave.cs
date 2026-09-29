using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Blacksmith
{
    public static class BlacksmithSave
    {
        public static string PathName
        {
            get
            {
                return Path.Combine(Application.persistentDataPath, "blacksmith-ui-save-v1.json");
            }
        }

        public static bool WritesBlocked { get; private set; }

        public static SaveData Load(BlacksmithCatalog catalog)
        {
            WritesBlocked = false;
            if (!File.Exists(PathName))
                return CreateDemo(catalog);
            WritesBlocked = true;
            try
            {
                var json = File.ReadAllText(PathName);
                var d = JsonUtility.FromJson<SaveData>(json);
                if (d == null || !json.Contains("\"version\"") || d.version != 1 || d.chest == null || d.bag == null || d.equipment == null || d.progress == null)
                    throw new InvalidDataException("Unsupported or incomplete save");
                if (d.chest.Concat(d.bag).Any(x => x == null || catalog.Item(x.itemId) == null || x.count <= 0 || (int)x.quality < 0 || (int)x.quality > 4) || d.equipment.Any(x => x == null || x.stack == null || catalog.Item(x.stack.itemId) == null || x.stack.count <= 0))
                    throw new InvalidDataException("Save contains unknown or invalid items");
                d.acquiredItems ??= new System.Collections.Generic.List<string>();
                WritesBlocked = false;
                return d;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Save preserved; writing disabled: " + e.Message);
                return new SaveData();
            }
        }

        public static SaveData CreateDemo(BlacksmithCatalog catalog = null)
        {
            var d = new SaveData
            {
                fuel = 12
            };
            foreach (var s in new[]
            {
                new Stack("wood", 30),
                new Stack("ore", 30),
                new Stack("stone", 12),
                new Stack("coal", 10),
                new Stack("hot_iron", 5),
                new Stack("plate", 5),
                new Stack("handle", 5),
                new Stack("leather_prepared", 4),
                new Stack("sword", 1),
                new Stack("shield", 1),
                new Stack("bow", 1),
                new Stack("arrow", 20),
                new Stack("warhammer", 1)
            }

            )
                d.chest.Add(s);
            d.bag.Add(new Stack("wood", 8));
            d.bag.Add(new Stack("ore", 6));
            if (catalog != null)
                SupplyRecipeMaterials(d, catalog);
            return d;
        }

        public static void SupplyRecipeMaterials(SaveData data, BlacksmithCatalog catalog)
        {
            // Explicit prototype supply, including new resources for pre-existing saves.
            var used = catalog.recipes.Where(r => r.enabled).SelectMany(r => r.ingredients).Select(i => i.itemId).ToHashSet();
            foreach (var item in catalog.items.Where(i => i.group == ItemGroup.Gathered && (used.Contains(i.id) || i.fuelValue > 0)))
            {
                int owned = data.chest.Where(s => s.itemId == item.id).Sum(s => s.count);
                if (owned < 30)
                    InventoryService.Add(data.chest, new Stack(item.id, 30 - owned));
            }
        }

        public static void Write(SaveData d)
        {
            if (WritesBlocked)
                return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(PathName));
                string temp = PathName + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(d, true));
                if (File.Exists(PathName))
                    File.Replace(temp, PathName, PathName + ".bak");
                else
                    File.Move(temp, PathName);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Save failed: " + e.Message);
            }
        }
    }
}