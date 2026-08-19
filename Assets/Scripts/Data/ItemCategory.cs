using UnityEngine;

namespace PoorSmith.Data
{
    /// <summary>
    /// 아이템의 기초 분류. 노드 아이콘의 메인 색이 여기서 온다.
    /// 손질 실패 결과물도 카테고리별로 갈린다.
    /// </summary>
    [CreateAssetMenu(menuName = "PoorSmith/아이템 카테고리", fileName = "Category_")]
    public sealed class ItemCategory : ScriptableObject
    {
        [SerializeField] string id;
        [SerializeField] string displayName;
        [SerializeField] Color nodeColor = Color.gray;

        public string Id => id;
        public string DisplayName => displayName;
        public Color NodeColor => nodeColor;

        public override string ToString() => displayName;
    }
}
