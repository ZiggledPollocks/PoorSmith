using PoorSmith.Data;

namespace PoorSmith.Crafting
{
    /// <summary>
    /// 제작을 한 번 시도한 결과.
    /// 실패해도 빈손으로 끝나지 않고 실패 결과물이 남는다는 것이 기획의 요지다.
    /// </summary>
    public readonly struct CraftResult
    {
        /// <summary>성공하면 만들어진 물건, 실패하면 실패 결과물. 재료가 모자라면 null.</summary>
        public ItemDef Output { get; }

        public bool Succeeded { get; }

        /// <summary>이번 제작으로 새로 열린 노드. 이미 열려 있었거나 실패했으면 null.</summary>
        public NodeDef UnlockedNode { get; }

        /// <summary>재료가 모자라 시도조차 못 한 경우.</summary>
        public bool Attempted => Output != null;

        CraftResult(ItemDef output, bool succeeded, NodeDef unlockedNode)
        {
            Output = output;
            Succeeded = succeeded;
            UnlockedNode = unlockedNode;
        }

        public static CraftResult Success(ItemDef output, NodeDef unlockedNode) => new(output, true, unlockedNode);
        public static CraftResult Failed(ItemDef output) => new(output, false, null);
        public static CraftResult NotEnough() => new(null, false, null);
    }
}
