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
