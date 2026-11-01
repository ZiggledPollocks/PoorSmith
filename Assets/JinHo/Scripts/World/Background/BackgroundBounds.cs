// [코드 지도] BackgroundBounds: 배경 구간의 자식 Renderer 경계를 하나의 Bounds로 합친다. 렌더러가 없으면 false와 default 경계를 반환한다.
// 주요 함수: TryGetSectionBounds
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/World/Background/BackgroundBounds.cs.md

using UnityEngine;

/// <summary>Calculates bounds used to position repeating backgrounds.</summary>
public static class BackgroundBounds
{
    public static bool TryGetSectionBounds(Transform section, out Bounds bounds)
    {
        Renderer[] renderers = section.GetComponentsInChildren<Renderer>();
        bounds = default;

        if (renderers.Length == 0)
            return false;

        bounds = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return true;
    }
}
