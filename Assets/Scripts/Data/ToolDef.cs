using UnityEngine;

namespace PoorSmith.Data
{
    /// <summary>
    /// 대장간에 놓인 손질·조합 도구. 대패, 칼, 작업대 같은 것들.
    /// 기획상 용광로·모루·담금질 물은 아직 레시피가 없어 implemented가 false다.
    /// </summary>
    [CreateAssetMenu(menuName = "PoorSmith/도구", fileName = "Tool_")]
    public sealed class ToolDef : ScriptableObject
    {
        [SerializeField] string id;
        [SerializeField] string displayName;

        [Tooltip("이 도구가 놓인 자리. 같은 자리의 도구끼리는 화면 이동 없이 바꿔 쓸 수 있다.")]
        [SerializeField] string station;

        [Tooltip("이 횟수를 넘기면 무조건 실패한다. 0이면 한계가 없다.")]
        [SerializeField, Min(0)] int strokeLimit;

        [Tooltip("이 도구를 쓰는 레시피가 아직 기획에 없으면 false.")]
        [SerializeField] bool implemented = true;

        public string Id => id;
        public string DisplayName => displayName;
        public string Station => station;
        public int StrokeLimit => strokeLimit;
        public bool Implemented => implemented;

        public bool ExceedsLimit(int strokes) => strokeLimit > 0 && strokes > strokeLimit;

        public override string ToString() => displayName;
    }
}
