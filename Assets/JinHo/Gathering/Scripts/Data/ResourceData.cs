using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "NewResourceData",
    menuName = "Game/Resource Data"
)]
public class ResourceData : ScriptableObject
{
    [SerializeField] private List<GameObject> dropPrefabs = new();
    [SerializeField] private List<int> dropAmounts = new();

    public IReadOnlyList<GameObject> DropPrefabs => dropPrefabs;
    public IReadOnlyList<int> DropAmounts => dropAmounts;
    public int DropCount => Mathf.Min(dropPrefabs.Count, dropAmounts.Count);

    public bool TryGetDrop(int index, out GameObject prefab, out int amount)
    {
        prefab = null;
        amount = 0;

        if (index < 0 || index >= DropCount)
            return false;

        prefab = dropPrefabs[index];
        amount = Mathf.Max(0, dropAmounts[index]);

        return prefab != null && amount > 0;
    }

    private void OnValidate()
    {
        if (dropPrefabs.Count != dropAmounts.Count)
        {
            Debug.LogWarning(
                $"{name}: Drop Prefabs와 Drop Amounts의 리스트 크기가 다릅니다.",
                this
            );
        }

        for (int i = 0; i < dropAmounts.Count; i++)
        {
            dropAmounts[i] = Mathf.Max(0, dropAmounts[i]);
        }
    }
}
