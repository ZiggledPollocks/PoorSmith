// [코드 지도] SpawnGeometry: 프리팹 렌더러 경계 합산과 경계 네 모서리의 생성 영역 포함 여부 검사를 공유한다. 오목한 영역에서 네 모서리 검사만으로 내부 전체 포함을 증명하지는 않는다.
// 주요 함수: IsFullyInsideSpawnArea, TryGetPrefabRendererBounds
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/World/Spawning/SpawnGeometry.cs.md

using UnityEngine;

/// <summary>Checks prefab bounds against spawn areas before placement.</summary>
public static class SpawnGeometry
{
    public static bool TryGetPrefabRendererBounds(GameObject prefab,
        out Bounds combinedBounds)
    {
        Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            combinedBounds = default;
            return false;
        }

        combinedBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            combinedBounds.Encapsulate(renderers[i].bounds);
        return true;
    }

    public static bool IsFullyInsideSpawnArea(Collider2D spawnArea,
        Bounds bounds)
    {
        Vector2[] corners =
        {
            new(bounds.min.x, bounds.min.y),
            new(bounds.min.x, bounds.max.y),
            new(bounds.max.x, bounds.min.y),
            new(bounds.max.x, bounds.max.y)
        };

        foreach (Vector2 corner in corners)
        {
            if (!spawnArea.OverlapPoint(corner))
                return false;
        }

        return true;
    }
}
