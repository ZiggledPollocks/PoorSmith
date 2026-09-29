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
