using UnityEngine;

namespace PoorSmith.Data
{
    /// <summary>
    /// 아이템 한 종류. 자원도 장비도 실패 결과물도 전부 이걸로 표현한다.
    /// 셋을 나누지 않는 이유는 손질 과정에서 자원이 그대로 장비가 되기 때문이다.
    /// (나무 원목 → 판자 → 칼날 → 검)
    /// </summary>
    [CreateAssetMenu(menuName = "PoorSmith/아이템", fileName = "Item_")]
    public sealed class ItemDef : ScriptableObject
    {
        [SerializeField] string id;
        [SerializeField] string displayName;
        [SerializeField] ItemCategory category;
        [SerializeField] Recipe recipe = new();

        [Tooltip("손질이나 조합에 실패했을 때 나오는 물건이면 체크한다.")]
        [SerializeField] bool isFailureResult;

        [Tooltip("노드 지도와 제작 화면에 쓸 아이콘. 없으면 카테고리 색의 도형으로 대체한다.")]
        [SerializeField] Sprite icon;

        public string Id => id;
        public string DisplayName => displayName;
        public ItemCategory Category => category;
        public Recipe Recipe => recipe;
        public bool IsFailureResult => isFailureResult;
        public Sprite Icon => icon;

        public bool IsGathered => recipe.Kind == RecipeKind.Gathered;

        public override string ToString() => displayName;
    }
}
