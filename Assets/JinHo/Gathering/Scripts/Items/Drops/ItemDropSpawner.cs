// [코드 지도] ItemDropSpawner: 자원과 몬스터의 드롭 프리팹을 월드에 생성하는 공통 서비스다. 생성 개수는 GameObject 여러 개가 아니라 한 드롭 객체의 amount로 전달한다. 자동 획득은 ItemDropInteractable이 담당한다.
// 주요 함수: Spawn, Awake, ConfigureItemDropCollisions
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Gathering/Scripts/Items/Drops/ItemDropSpawner.cs.md

using UnityEngine;

public class ItemDropSpawner : MonoBehaviour
{
    private const int LayerCount = 32;

    [SerializeField] private Transform dropParent;

    private int itemDropLayer = -1;
    private int groundLayer = -1;

    private void Awake()
    {
        itemDropLayer = LayerMask.NameToLayer("ItemDrop");
        groundLayer = LayerMask.NameToLayer("Ground");

        if (itemDropLayer < 0 || groundLayer < 0)
        {
            Debug.LogError("ItemDrop 또는 Ground Layer가 Project Settings에 등록되지 않았습니다.");
            return;
        }

        ConfigureItemDropCollisions();
    }

    // 핵심 분기: itemPrefab == null 판정.
    // 다음 연결: SpawnHierarchyLayers.SetRecursively 호출.
    public GameObject Spawn(
        GameObject itemPrefab,
        Vector3 position,
        int amount)
    {
        if (itemPrefab == null)
        {
            Debug.LogWarning("생성할 아이템 프리팹이 연결되지 않았습니다.");
            return null;
        }

        if (amount <= 0)
        {
            Debug.LogWarning("생성할 아이템 수량이 올바르지 않습니다.");
            return null;
        }

        GameObject itemObject = Instantiate(
            itemPrefab,
            position,
            Quaternion.identity,
            dropParent
        );

        if (itemDropLayer >= 0)
        {
            SpawnHierarchyLayers.SetRecursively(itemObject, itemDropLayer);
        }

        ItemDropInteractable itemDrop =
            itemObject.GetComponent<ItemDropInteractable>();

        if (itemDrop == null)
        {
            Debug.LogError(
                $"{itemPrefab.name} 프리팹에 {nameof(ItemDropInteractable)} 컴포넌트가 없습니다."
            );
            Destroy(itemObject);
            return null;
        }

        itemDrop.Initialize(amount);
        return itemObject;
    }

    private void ConfigureItemDropCollisions()
    {
        for (int layer = 0; layer < LayerCount; layer++)
        {
            bool shouldIgnore = layer != groundLayer;
            Physics2D.IgnoreLayerCollision(itemDropLayer, layer, shouldIgnore);
        }
    }

}
