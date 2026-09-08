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
            SetLayerRecursively(itemObject.transform, itemDropLayer);
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

    private static void SetLayerRecursively(Transform target, int layer)
    {
        target.gameObject.layer = layer;

        for (int i = 0; i < target.childCount; i++)
        {
            SetLayerRecursively(target.GetChild(i), layer);
        }
    }
}
