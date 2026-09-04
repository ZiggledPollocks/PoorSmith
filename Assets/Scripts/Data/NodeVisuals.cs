using UnityEngine;

namespace PoorSmith.Data
{
    /// <summary>
    /// 노드 지도에 쓰는 도형들.
    ///
    /// 인스펙터에서 갈아끼울 수 있게 에셋으로 둔다.
    /// 지금은 유니티 기본 UI 스프라이트를 넣어두고, 아트가 나오면 여기만 바꾸면 된다.
    /// 비워두면 코드로 그린 임시 도형을 쓴다.
    /// </summary>
    [CreateAssetMenu(menuName = "PoorSmith/노드 도형", fileName = "NodeVisuals")]
    public sealed class NodeVisuals : ScriptableObject
    {
        [Tooltip("기본·종결 노드. 유니티 기본 스프라이트라면 Knob이 원형이다.")]
        [SerializeField] Sprite circle;

        [Tooltip("시작·파생·혼합·히든 노드. 유니티 기본 스프라이트라면 UISprite를 쓰면 된다.")]
        [SerializeField] Sprite square;

        [Tooltip("선택한 노드를 가리키는 화살표. 유니티 기본 스프라이트라면 DropdownArrow.")]
        [SerializeField] Sprite arrow;

        [Tooltip("노드 사이를 잇는 선. 비워두면 단색 사각형으로 그린다.")]
        [SerializeField] Sprite line;

        public Sprite Circle => circle;
        public Sprite Square => square;
        public Sprite Arrow => arrow;
        public Sprite Line => line;
    }
}
