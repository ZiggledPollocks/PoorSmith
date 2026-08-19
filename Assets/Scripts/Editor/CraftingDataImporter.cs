using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PoorSmith.Data;
using UnityEditor;
using UnityEngine;

namespace PoorSmith.Editor
{
    /// <summary>
    /// Design/notion/items.json + nodes.json → 카테고리 / 도구 / 아이템 / 노드 에셋.
    /// 기존 에셋은 지우지 않고 값만 덮어써서, 다른 곳에서 걸어둔 참조를 유지한다.
    /// </summary>
    internal static class CraftingDataImporter
    {
        const string ItemsFile = "items.json";
        const string NodesFile = "nodes.json";

        const string Root = "Assets/Content";
        const string CategoryFolder = Root + "/Categories";
        const string ToolFolder = Root + "/Tools";
        const string ItemFolder = Root + "/Items";
        const string NodeFolder = Root + "/NodeDefs";

        [MenuItem("PoorSmith/노션 데이터 다시 불러오기 %#i")]
        internal static void Import()
        {
            var itemFile = NotionSourceFiles.Read<ItemFile>(ItemsFile);
            var nodeFile = NotionSourceFiles.Read<NodeFile>(NodesFile);
            if (itemFile == null || nodeFile == null) return;

            var report = new ImportReport();

            // 폴더는 일괄 편집 블록 밖에서 만든다. 블록 안에서는 방금 만든 폴더가 곧바로 보이지 않을 수 있다.
            foreach (var folder in new[] { CategoryFolder, ToolFolder, ItemFolder, NodeFolder })
                NotionSourceFiles.EnsureFolder(folder);

            try
            {
                AssetDatabase.StartAssetEditing();

                var categories = ImportCategories(itemFile.categories, report);
                var tools = ImportTools(itemFile.tools, report);

                // 레시피와 부모가 서로를 참조하므로, 에셋을 전부 만든 뒤에 값을 채운다.
                var items = CreateAssets<ItemDef, ItemEntry>(itemFile.items, e => e.id, ItemFolder, "Item_", report);
                var failures = CreateAssets<ItemDef, FailureEntry>(itemFile.failures, e => e.id, ItemFolder, "Item_", report);
                var nodes = CreateAssets<NodeDef, NodeEntry>(nodeFile.nodes, e => e.id, NodeFolder, "Node_", report);

                var allItems = items.Concat(failures).ToDictionary(pair => pair.Key, pair => pair.Value);

                FillItems(itemFile.items, allItems, categories, tools, report);
                FillFailures(itemFile.failures, failures, categories, report);
                FillNodes(nodeFile.nodes, nodes, allItems, categories, report);

                WriteItemDatabase(itemFile, categories, tools, allItems, report);
                WriteNodeDatabase(nodeFile, nodes, report);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            report.Log();
        }

        // ---- 만들기 ----

        static Dictionary<string, T> CreateAssets<T, TEntry>(
            TEntry[] entries, Func<TEntry, string> idOf, string folder, string prefix, ImportReport report)
            where T : ScriptableObject
        {
            var map = new Dictionary<string, T>();
            if (entries == null) return map;

            foreach (var entry in entries)
            {
                var id = idOf(entry);
                if (string.IsNullOrWhiteSpace(id))
                {
                    report.Error($"id가 비어 있는 항목이 있어 건너뛴다 ({folder}).");
                    continue;
                }

                var asset = NotionSourceFiles.LoadOrCreate<T>($"{folder}/{prefix}{id}.asset", out var created);
                report.Count(created);
                map[id] = asset;
            }

            return map;
        }

        static Dictionary<string, ItemCategory> ImportCategories(CategoryEntry[] entries, ImportReport report)
        {
            var map = CreateAssets<ItemCategory, CategoryEntry>(entries, e => e.id, CategoryFolder, "Category_", report);

            foreach (var entry in entries ?? Array.Empty<CategoryEntry>())
            {
                if (!map.TryGetValue(entry.id, out var asset)) continue;

                var so = new SerializedObject(asset);
                so.FindProperty("id").stringValue = entry.id;
                so.FindProperty("displayName").stringValue = entry.displayName;
                so.FindProperty("nodeColor").colorValue = ParseColor(entry.color, entry.id, report);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            return map;
        }

        static Dictionary<string, ToolDef> ImportTools(ToolEntry[] entries, ImportReport report)
        {
            var map = CreateAssets<ToolDef, ToolEntry>(entries, e => e.id, ToolFolder, "Tool_", report);

            foreach (var entry in entries ?? Array.Empty<ToolEntry>())
            {
                if (!map.TryGetValue(entry.id, out var asset)) continue;

                var so = new SerializedObject(asset);
                so.FindProperty("id").stringValue = entry.id;
                so.FindProperty("displayName").stringValue = entry.displayName;
                so.FindProperty("station").stringValue = entry.station;
                so.FindProperty("strokeLimit").intValue = Mathf.Max(0, entry.strokeLimit);
                so.FindProperty("implemented").boolValue = entry.implemented;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            return map;
        }

        // ---- 채우기 ----

        static void FillItems(
            ItemEntry[] entries,
            IReadOnlyDictionary<string, ItemDef> items,
            IReadOnlyDictionary<string, ItemCategory> categories,
            IReadOnlyDictionary<string, ToolDef> tools,
            ImportReport report)
        {
            foreach (var entry in entries ?? Array.Empty<ItemEntry>())
            {
                if (!items.TryGetValue(entry.id, out var asset)) continue;

                var so = new SerializedObject(asset);
                so.FindProperty("id").stringValue = entry.id;
                so.FindProperty("displayName").stringValue = entry.displayName;
                so.FindProperty("isFailureResult").boolValue = false;
                AssignCategory(so, entry.category, categories, entry.id, report);
                WriteRecipe(so, entry.recipe, items, tools, entry.id, report);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static void FillFailures(
            FailureEntry[] entries,
            IReadOnlyDictionary<string, ItemDef> failures,
            IReadOnlyDictionary<string, ItemCategory> categories,
            ImportReport report)
        {
            foreach (var entry in entries ?? Array.Empty<FailureEntry>())
            {
                if (!failures.TryGetValue(entry.id, out var asset)) continue;

                var so = new SerializedObject(asset);
                so.FindProperty("id").stringValue = entry.id;
                so.FindProperty("displayName").stringValue = entry.displayName;
                so.FindProperty("isFailureResult").boolValue = true;
                so.FindProperty("recipe.kind").enumValueIndex = (int)RecipeKind.Gathered;

                // 실패 결과물의 카테고리는 비워둘 수 있다. 조합 실패는 계열을 가리지 않는다.
                if (!string.IsNullOrEmpty(entry.category))
                    AssignCategory(so, entry.category, categories, entry.id, report);

                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static void WriteRecipe(
            SerializedObject so,
            RecipeEntry recipe,
            IReadOnlyDictionary<string, ItemDef> items,
            IReadOnlyDictionary<string, ToolDef> tools,
            string ownerId,
            ImportReport report)
        {
            var kind = ParseKind(recipe?.kind, ownerId, report);
            so.FindProperty("recipe.kind").enumValueIndex = (int)kind;

            var toolProperty = so.FindProperty("recipe.tool");
            var inputProperty = so.FindProperty("recipe.input");
            var ingredientsProperty = so.FindProperty("recipe.ingredients");

            toolProperty.objectReferenceValue = null;
            inputProperty.objectReferenceValue = null;
            ingredientsProperty.arraySize = 0;

            switch (kind)
            {
                case RecipeKind.Shaping:
                    toolProperty.objectReferenceValue = Resolve(tools, recipe.tool, ownerId, "도구", report);
                    inputProperty.objectReferenceValue = Resolve(items, recipe.input, ownerId, "손질 대상", report);
                    so.FindProperty("recipe.minStrokes").intValue = Mathf.Max(1, recipe.minStrokes);
                    so.FindProperty("recipe.maxStrokes").intValue = Mathf.Max(1, recipe.maxStrokes);

                    if (recipe.maxStrokes < recipe.minStrokes)
                        report.Error($"'{ownerId}': 손질 횟수 범위가 뒤집혀 있다 ({recipe.minStrokes}~{recipe.maxStrokes}).");
                    break;

                case RecipeKind.Assembly:
                    var lines = recipe.ingredients ?? Array.Empty<IngredientEntry>();
                    ingredientsProperty.arraySize = lines.Length;

                    for (var i = 0; i < lines.Length; i++)
                    {
                        var element = ingredientsProperty.GetArrayElementAtIndex(i);
                        element.FindPropertyRelative("count").intValue = Mathf.Max(1, lines[i].count);

                        var options = element.FindPropertyRelative("options");
                        var ids = lines[i].options ?? Array.Empty<string>();
                        options.arraySize = ids.Length;

                        for (var j = 0; j < ids.Length; j++)
                            options.GetArrayElementAtIndex(j).objectReferenceValue =
                                Resolve(items, ids[j], ownerId, "재료", report);

                        if (ids.Length == 0) report.Error($"'{ownerId}': 재료 후보가 비어 있는 줄이 있다.");
                    }
                    break;
            }
        }

        static void FillNodes(
            NodeEntry[] entries,
            IReadOnlyDictionary<string, NodeDef> nodes,
            IReadOnlyDictionary<string, ItemDef> items,
            IReadOnlyDictionary<string, ItemCategory> categories,
            ImportReport report)
        {
            foreach (var entry in entries ?? Array.Empty<NodeEntry>())
            {
                if (!nodes.TryGetValue(entry.id, out var asset)) continue;

                var so = new SerializedObject(asset);
                so.FindProperty("id").stringValue = entry.id;
                so.FindProperty("displayName").stringValue = entry.displayName;
                so.FindProperty("type").enumValueIndex = (int)ParseNodeType(entry.type, entry.id, report);
                so.FindProperty("hint").stringValue = entry.hint ?? string.Empty;
                so.FindProperty("description").stringValue = entry.description ?? string.Empty;
                AssignCategory(so, entry.category, categories, entry.id, report);

                so.FindProperty("recipe").objectReferenceValue =
                    string.IsNullOrEmpty(entry.recipe) ? null : Resolve(items, entry.recipe, entry.id, "레시피", report);

                so.FindProperty("startResource").objectReferenceValue =
                    string.IsNullOrEmpty(entry.startResource) ? null : Resolve(items, entry.startResource, entry.id, "원천 재료", report);

                var parents = so.FindProperty("parents");
                var parentIds = entry.parents ?? Array.Empty<string>();
                parents.arraySize = parentIds.Length;
                for (var i = 0; i < parentIds.Length; i++)
                    parents.GetArrayElementAtIndex(i).objectReferenceValue =
                        Resolve(nodes, parentIds[i], entry.id, "부모 노드", report);

                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // ---- 데이터베이스 ----

        static void WriteItemDatabase(
            ItemFile file,
            IReadOnlyDictionary<string, ItemCategory> categories,
            IReadOnlyDictionary<string, ToolDef> tools,
            IReadOnlyDictionary<string, ItemDef> items,
            ImportReport report)
        {
            var database = NotionSourceFiles.LoadOrCreate<ItemDatabase>($"{Root}/ItemDatabase.asset", out var created);
            report.Count(created);

            var so = new SerializedObject(database);
            // JSON에 적힌 순서를 유지해 인스펙터에서 보기 좋게 한다.
            WriteArray(so.FindProperty("categories"), file.categories, e => categories.GetValueOrDefault(e.id));
            WriteArray(so.FindProperty("tools"), file.tools, e => tools.GetValueOrDefault(e.id));

            var itemOrder = (file.items ?? Array.Empty<ItemEntry>()).Select(e => e.id)
                .Concat((file.failures ?? Array.Empty<FailureEntry>()).Select(e => e.id))
                .ToArray();
            WriteArray(so.FindProperty("items"), itemOrder, id => items.GetValueOrDefault(id));

            var rules = so.FindProperty("failureRules");
            var failures = file.failures ?? Array.Empty<FailureEntry>();
            rules.arraySize = failures.Length;

            for (var i = 0; i < failures.Length; i++)
            {
                var element = rules.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("tool").objectReferenceValue =
                    Resolve(tools, failures[i].tool, failures[i].id, "도구", report);
                element.FindPropertyRelative("category").objectReferenceValue =
                    string.IsNullOrEmpty(failures[i].category) ? null : categories.GetValueOrDefault(failures[i].category);
                element.FindPropertyRelative("output").objectReferenceValue = items.GetValueOrDefault(failures[i].id);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            database.InvalidateIndex();
            EditorUtility.SetDirty(database);
        }

        static void WriteNodeDatabase(NodeFile file, IReadOnlyDictionary<string, NodeDef> nodes, ImportReport report)
        {
            var database = NotionSourceFiles.LoadOrCreate<NodeDatabase>($"{Root}/NodeDatabase.asset", out var created);
            report.Count(created);

            var so = new SerializedObject(database);
            WriteArray(so.FindProperty("nodes"), file.nodes, e => nodes.GetValueOrDefault(e.id));
            so.ApplyModifiedPropertiesWithoutUndo();

            database.InvalidateIndex();
            EditorUtility.SetDirty(database);
        }

        // ---- 잡동사니 ----

        static void WriteArray<T>(SerializedProperty property, T[] entries, Func<T, UnityEngine.Object> resolve)
        {
            entries ??= Array.Empty<T>();
            property.arraySize = entries.Length;
            for (var i = 0; i < entries.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = resolve(entries[i]);
        }

        static void AssignCategory(
            SerializedObject so, string id,
            IReadOnlyDictionary<string, ItemCategory> categories, string ownerId, ImportReport report)
        {
            so.FindProperty("category").objectReferenceValue = Resolve(categories, id, ownerId, "카테고리", report);
        }

        static T Resolve<T>(IReadOnlyDictionary<string, T> map, string id, string ownerId, string what, ImportReport report)
            where T : UnityEngine.Object
        {
            if (!string.IsNullOrEmpty(id) && map.TryGetValue(id, out var found)) return found;

            report.Error($"'{ownerId}': {what} '{id}' 을(를) 찾을 수 없다.");
            return null;
        }

        static RecipeKind ParseKind(string kind, string ownerId, ImportReport report)
        {
            switch (kind)
            {
                case "gathered": return RecipeKind.Gathered;
                case "shaping": return RecipeKind.Shaping;
                case "assembly": return RecipeKind.Assembly;
                default:
                    report.Error($"'{ownerId}': 알 수 없는 레시피 종류 '{kind}'. 채집으로 둔다.");
                    return RecipeKind.Gathered;
            }
        }

        static NodeType ParseNodeType(string type, string ownerId, ImportReport report)
        {
            switch (type)
            {
                case "basic": return NodeType.Basic;
                case "start": return NodeType.Start;
                case "derivative": return NodeType.Derivative;
                case "mixed": return NodeType.Mixed;
                case "terminal": return NodeType.Terminal;
                case "hidden": return NodeType.Hidden;
                default:
                    report.Error($"'{ownerId}': 알 수 없는 노드 종류 '{type}'. 기본 노드로 둔다.");
                    return NodeType.Basic;
            }
        }

        static Color ParseColor(string hex, string ownerId, ImportReport report)
        {
            if (string.IsNullOrEmpty(hex)) return Color.gray;
            if (ColorUtility.TryParseHtmlString(hex, out var color)) return color;

            report.Error($"카테고리 '{ownerId}': 색 '{hex}' 을 해석할 수 없어 회색으로 대체한다.");
            return Color.gray;
        }

        sealed class ImportReport
        {
            readonly List<string> errors = new();
            int createdCount;
            int updatedCount;

            internal void Count(bool created)
            {
                if (created) createdCount++;
                else updatedCount++;
            }

            internal void Error(string message) => errors.Add(message);

            internal void Log()
            {
                var summary = new StringBuilder(
                    $"[노션 임포터] 완료 — 새로 만듦 {createdCount}개, 갱신 {updatedCount}개");

                if (errors.Count == 0)
                {
                    Debug.Log(summary.ToString());
                    return;
                }

                summary.Append($", 문제 {errors.Count}건");
                foreach (var error in errors) summary.Append($"\n  · {error}");
                Debug.LogError(summary.ToString());
            }
        }

        // ---- JSON 대응 구조. JsonUtility는 '_' 로 시작하는 주석 필드를 무시한다. ----

        [Serializable] sealed class ItemFile
        {
            public CategoryEntry[] categories;
            public ToolEntry[] tools;
            public ItemEntry[] items;
            public FailureEntry[] failures;
        }

        [Serializable] sealed class NodeFile
        {
            public NodeEntry[] nodes;
        }

        [Serializable] sealed class CategoryEntry
        {
            public string id;
            public string displayName;
            public string color;
        }

        [Serializable] sealed class ToolEntry
        {
            public string id;
            public string displayName;
            public string station;
            public int strokeLimit;
            public bool implemented;
        }

        [Serializable] sealed class ItemEntry
        {
            public string id;
            public string displayName;
            public string category;
            public RecipeEntry recipe;
        }

        [Serializable] sealed class RecipeEntry
        {
            public string kind;
            public string tool;
            public string input;
            public int minStrokes;
            public int maxStrokes;
            public IngredientEntry[] ingredients;
        }

        [Serializable] sealed class IngredientEntry
        {
            public int count;
            public string[] options;
        }

        [Serializable] sealed class FailureEntry
        {
            public string id;
            public string displayName;
            public string tool;
            public string category;
        }

        [Serializable] sealed class NodeEntry
        {
            public string id;
            public string displayName;
            public string type;
            public string category;
            public string[] parents;
            public string recipe;
            public string startResource;
            public string hint;
            public string description;
        }
    }
}
