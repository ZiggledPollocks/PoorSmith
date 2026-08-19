using System;
using System.Collections.Generic;
using UnityEngine;

namespace PoorSmith.Data
{
    /// <summary>
    /// 레시피 노드 하나. 노드마다 아이템 하나가 대응되고, 그 아이템의 레시피가 노드의 내용이 된다.
    /// 혼합 노드는 부모가 둘이므로 부모를 배열로 둔다.
    /// </summary>
    [CreateAssetMenu(menuName = "PoorSmith/노드", fileName = "Node_")]
    public sealed class NodeDef : ScriptableObject
    {
        [SerializeField] string id;
        [SerializeField] string displayName;
        [SerializeField] NodeType type = NodeType.Basic;

        [Tooltip("아이콘의 메인 색이 되는 주체 카테고리.")]
        [SerializeField] ItemCategory category;

        [Tooltip("혼합 노드에서 꼭짓점에 표시할 부속 카테고리. 혼합이 아니면 비워둔다.")]
        [SerializeField] ItemCategory secondaryCategory;

        [Tooltip("시작 노드는 비어 있다. 혼합 노드는 둘이다.")]
        [SerializeField] NodeDef[] parents = Array.Empty<NodeDef>();

        [Tooltip("이 노드에 대응하는 아이템. 시작 노드는 비어 있다.")]
        [SerializeField] ItemDef recipe;

        [Tooltip("시작 노드 전용. 이 아이템을 얻으면 해금된다.")]
        [SerializeField] ItemDef startResource;

        [TextArea(2, 4)]
        [Tooltip("미해금 상태에서 보여줄 힌트. 에고 망치가 두루뭉술하게 알려주는 말투.")]
        [SerializeField] string hint;

        [TextArea(2, 4)]
        [Tooltip("해금 후 설명. 시작 노드는 카테고리 설명이 들어간다.")]
        [SerializeField] string description;

        [Tooltip("체크하면 아래 좌표를 그대로 쓰고, 아니면 자동으로 배치한다.")]
        [SerializeField] bool useManualPosition;
        [SerializeField] Vector2 mapPosition;

        public string Id => id;
        public string DisplayName => displayName;
        public NodeType Type => type;
        public ItemCategory Category => category;
        public ItemCategory SecondaryCategory => secondaryCategory;
        public IReadOnlyList<NodeDef> Parents => parents;
        public ItemDef Recipe => recipe;
        public ItemDef StartResource => startResource;
        public string Hint => hint;
        public string Description => description;
        public bool UseManualPosition => useManualPosition;
        public Vector2 MapPosition => mapPosition;

        /// <summary>카테고리의 진입점이라 위로 이어지는 노드가 없다.</summary>
        public bool IsRoot => parents.Length == 0;

        /// <summary>노드 지도에 그릴 아이콘. 대응 아이템의 아이콘을 그대로 쓴다.</summary>
        public Sprite Icon => recipe != null ? recipe.Icon : null;

        public override string ToString() => $"{displayName}({id})";
    }
}
