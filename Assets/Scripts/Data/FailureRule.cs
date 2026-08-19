using System;
using UnityEngine;

namespace PoorSmith.Data
{
    /// <summary>
    /// 제작에 실패했을 때 무엇이 나오는지.
    /// 기획상 실패는 도구와 재료 계열에 따라 다른 물건을 남긴다.
    /// (대패로 나무를 너무 밀면 "너무 얇은 나무 조각")
    /// </summary>
    [Serializable]
    public struct FailureRule
    {
        [SerializeField] ToolDef tool;

        [Tooltip("비워두면 모든 카테고리에 적용된다.")]
        [SerializeField] ItemCategory category;

        [SerializeField] ItemDef output;

        public ToolDef Tool => tool;
        public ItemCategory Category => category;
        public ItemDef Output => output;

        public bool Matches(ToolDef usedTool, ItemCategory usedCategory) =>
            tool == usedTool && (category == null || category == usedCategory);
    }
}
