// [코드 지도] ResourceData: 채집 자원이 떨어뜨릴 프리팹과 수량을 정의하는 데이터 에셋이다. 실제 생성은 ItemDropSpawner가 맡는다. 두 목록의 같은 번호를 한 드롭 항목으로 묶는다.
// 주요 함수: OnValidate, TryGetDrop, DropPrefabs
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Gathering/Scripts/Data/ResourceData.cs.md

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
