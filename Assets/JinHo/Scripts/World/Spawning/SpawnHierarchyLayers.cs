using UnityEngine;

/// <summary>Applies a spawn layer to an instantiated object and all of its children.</summary>
public static class SpawnHierarchyLayers
{
    public static void SetRecursively(GameObject instance, int layer)
    {
        instance.layer = layer;
        foreach (Transform child in instance.transform)
            SetRecursively(child.gameObject, layer);
    }
}
