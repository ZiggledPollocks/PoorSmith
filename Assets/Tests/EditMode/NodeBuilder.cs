using PoorSmith.Data;
using UnityEditor;
using UnityEngine;

namespace PoorSmith.Crafting.Tests
{
    /// <summary>
    /// 테스트용 NodeDef를 메모리에만 만들어준다.
    /// NodeDef의 필드가 전부 비공개라 SerializedObject로 채운다.
    /// </summary>
    internal static class NodeBuilder
    {
        internal static NodeDef Node(string id, NodeType type = NodeType.Basic, params NodeDef[] parents)
        {
            var node = ScriptableObject.CreateInstance<NodeDef>();
            node.name = id;
            node.hideFlags = HideFlags.HideAndDontSave;

            var so = new SerializedObject(node);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayName").stringValue = id;
            so.FindProperty("type").enumValueIndex = (int)type;

            var parentsProperty = so.FindProperty("parents");
            parentsProperty.arraySize = parents.Length;
            for (var i = 0; i < parents.Length; i++)
                parentsProperty.GetArrayElementAtIndex(i).objectReferenceValue = parents[i];

            so.ApplyModifiedPropertiesWithoutUndo();
            return node;
        }
    }
}
