// [코드 지도] FieldCaveBackgroundRuntime: 동굴 배경의 에디터 배치와 실행 중 표시 상태를 조정한다.
// 주요 함수: Awake
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/Campaign/Field/FieldCaveBackgroundRuntime.cs.md

using UnityEngine;

/// <summary>
/// Keeps the authored cave backdrop visible in the editor, but hands Play Mode
/// to the same 3x3 camera-following background used by SampleScene.
/// </summary>
[DefaultExecutionOrder(900)]
[DisallowMultipleComponent]
public sealed class FieldCaveBackgroundRuntime : MonoBehaviour
{
    [SerializeField] private Transform sectionTemplate;

    private void Awake()
    {
        if (sectionTemplate == null && transform.childCount > 0)
            sectionTemplate = transform.GetChild(0);
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform section = transform.GetChild(i);
            if (section != sectionTemplate)
                section.gameObject.SetActive(false);
        }
    }
}
