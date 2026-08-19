using PoorSmith.Data;
using UnityEngine;

namespace PoorSmith.UI
{
    /// <summary>
    /// 노드를 어떤 모양과 색으로 그릴지.
    ///
    /// 프로토타입 피드백 5번 — "모두 같은 모양으로 통일되다보니 어떤 노드가 어떤 레시피인지 알기 어렵다" —
    /// 에 대한 대응이라, 종류가 한눈에 갈리는 것이 이 파일의 목적이다.
    /// 규격은 기획서 '레시피 노드'의 UI 아이콘 표기법을 따른다.
    /// </summary>
    internal static class NodePalette
    {
        internal static readonly Color LockedBorder = new(0.45f, 0.45f, 0.48f);
        internal static readonly Color UnlockedBorder = new(0.95f, 0.78f, 0.25f);
        internal static readonly Color TerminalBorder = new(0.35f, 0.75f, 0.45f);
        internal static readonly Color HiddenBorder = new(0.85f, 0.32f, 0.30f);
        internal static readonly Color SelectedBorder = Color.white;

        internal static readonly Color Line = new(0.30f, 0.30f, 0.34f);
        internal static readonly Color UnlockedLine = new(0.85f, 0.85f, 0.88f);

        /// <summary>기획서의 표기법. 시작 노드는 아주 크고, 파생 계열은 정사각이다.</summary>
        internal static float SizeOf(NodeType type) => type switch
        {
            NodeType.Start => 108f,
            NodeType.Derivative or NodeType.Mixed or NodeType.Hidden => 72f,
            _ => 60f,
        };

        internal static bool IsRound(NodeType type) =>
            type is NodeType.Basic or NodeType.Terminal;

        /// <summary>미해금은 회색, 해금은 노랑. 종결과 히든만 예외로 다른 색을 쓴다.</summary>
        internal static Color BorderOf(NodeType type, bool unlocked)
        {
            if (!unlocked) return LockedBorder;

            return type switch
            {
                NodeType.Terminal => TerminalBorder,
                NodeType.Hidden => HiddenBorder,
                _ => UnlockedBorder,
            };
        }

        /// <summary>아이콘 안쪽은 카테고리 색을 따른다. 미해금이면 어둡게 눌러 잠긴 느낌을 준다.</summary>
        internal static Color FillOf(ItemCategory category, bool unlocked)
        {
            var color = category != null ? category.NodeColor : new Color(0.5f, 0.5f, 0.5f);
            return unlocked ? color : color * 0.35f;
        }
    }
}
