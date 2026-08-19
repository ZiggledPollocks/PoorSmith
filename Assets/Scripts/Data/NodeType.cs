namespace PoorSmith.Data
{
    /// <summary>
    /// 노드의 종류. 아이콘 모양·테두리 색과 지도 표시 규칙이 여기서 갈린다.
    /// 노션 '(NEW) 레시피 노드' 문서의 분류를 그대로 따른다.
    /// </summary>
    public enum NodeType
    {
        /// <summary>기본 노드. 원형 테두리.</summary>
        Basic = 0,

        /// <summary>카테고리의 진입점. 매우 큰 정사각 테두리. 원천 재료를 얻으면 해금된다.</summary>
        Start = 1,

        /// <summary>소 카테고리를 여는 노드. 정사각 테두리. 하위는 파생 집중 모드에서만 보인다.</summary>
        Derivative = 2,

        /// <summary>두 노드가 합쳐진 노드. 정사각 테두리. 파생 노드와 같은 규칙을 따른다.</summary>
        Mixed = 3,

        /// <summary>하위가 없는 노드. 원형 테두리에 녹색.</summary>
        Terminal = 4,

        /// <summary>기본 지도에 없다가 만들어내면 나타나는 노드. 정사각 테두리에 빨강.</summary>
        Hidden = 5,
    }

    public static class NodeTypeExtensions
    {
        /// <summary>
        /// 자기 하위를 기본 지도에서 감추고 '파생 집중 모드'로만 보여주는 종류인가.
        /// 기획서상 혼합·히든 노드는 파생 노드와 시스템을 공유한다.
        /// </summary>
        public static bool IsolatesChildren(this NodeType type) =>
            type is NodeType.Derivative or NodeType.Mixed or NodeType.Hidden;

        /// <summary>파생 격리 안에서도 기본 지도에 계속 드러나는 뼈대 노드인가.</summary>
        public static bool IsBackbone(this NodeType type) =>
            type is NodeType.Derivative or NodeType.Mixed;

        public static string DisplayName(this NodeType type) => type switch
        {
            NodeType.Basic => "기본",
            NodeType.Start => "시작",
            NodeType.Derivative => "파생",
            NodeType.Mixed => "혼합",
            NodeType.Terminal => "종결",
            NodeType.Hidden => "히든",
            _ => type.ToString(),
        };
    }
}
