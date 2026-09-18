#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ResourceSpawnZoneSceneSetup
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const int TargetCount = 4;
    private const float SpawnInterval = 5f;

    private readonly struct ZoneDefinition
    {
        public ZoneDefinition(string zoneName, string spawnName, string prefabPath, Type resourceType)
        {
            ZoneName = zoneName;
            SpawnName = spawnName;
            PrefabPath = prefabPath;
            ResourceType = resourceType;
        }

        public string ZoneName { get; }
        public string SpawnName { get; }
        public string PrefabPath { get; }
        public Type ResourceType { get; }
    }

    private static readonly ZoneDefinition[] Definitions =
    {
        new("ForestAssimilatelZone", "ForestResourceSpawnZone", "Assets/JinHo/Prefab/Resource/tree.prefab", typeof(TreeInteractable)),
        new("StoneAssimilatelZone", "StoneResourceSpawnZone", "Assets/JinHo/Prefab/Resource/Stone.prefab", typeof(StoneInteractable)),
        new("CoalAssimilatelZone", "CoalResourceSpawnZone", "Assets/JinHo/Prefab/Resource/Coal.prefab", typeof(CoalInteractable)),
        new("SteelAssimilatelZone", "SteelResourceSpawnZone", "Assets/JinHo/Prefab/Resource/Steel.prefab", typeof(SteelInteractable))
    };

    [MenuItem("Tools/batterMap/Configure Resource Spawn Zones")]
    public static void ApplyToSampleScene()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        ConfigureScene(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Resource spawn zones configured in SampleScene.");
    }

    public static void ApplyToSampleSceneBatch()
    {
        try
        {
            ApplyToSampleScene();
            ValidateScene(EditorSceneManager.GetActiveScene());
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    public static void ValidateSampleSceneBatch()
    {
        try
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ValidateScene(scene);
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void ConfigureScene(Scene scene)
    {
        RemoveExistingSpawnZones();
        RemovePlacedResources();
        RemoveEmptyResourceGroups();

        Camera playerCamera = FindSceneObjects<Camera>().FirstOrDefault(camera => camera.CompareTag("MainCamera"))
            ?? FindSceneObjects<Camera>().FirstOrDefault();
        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer < 0)
            throw new InvalidOperationException("Ground layer does not exist.");

        foreach (ZoneDefinition definition in Definitions)
        {
            GameObject zone = FindSceneGameObject(definition.ZoneName);
            if (zone == null)
                throw new InvalidOperationException($"Could not find {definition.ZoneName}.");

            BoxCollider2D sourceCollider = zone.GetComponent<BoxCollider2D>();
            if (sourceCollider == null)
                throw new InvalidOperationException($"{definition.ZoneName} needs a BoxCollider2D.");

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(definition.PrefabPath);
            if (prefab == null)
                throw new InvalidOperationException($"Could not load {definition.PrefabPath}.");

            GameObject host = new(definition.SpawnName);
            SceneManager.MoveGameObjectToScene(host, scene);
            host.transform.SetParent(zone.transform, false);

            BoxCollider2D spawnCollider = host.AddComponent<BoxCollider2D>();
            spawnCollider.offset = sourceCollider.offset;
            spawnCollider.size = sourceCollider.size;
            spawnCollider.edgeRadius = sourceCollider.edgeRadius;
            spawnCollider.isTrigger = true;

            ResourceSpawnZone2D spawner = host.AddComponent<ResourceSpawnZone2D>();
            spawner.Configure(spawnCollider, host.transform, playerCamera, prefab,
                TargetCount, SpawnInterval, 1 << groundLayer);
        }
    }

    private static void RemoveExistingSpawnZones()
    {
        HashSet<GameObject> hosts = FindSceneObjects<ResourceSpawnZone2D>()
            .Select(spawner => spawner.gameObject)
            .ToHashSet();

        GameObject legacyHost = FindSceneGameObject("ResourceSpawnZone");
        if (legacyHost != null)
            hosts.Add(legacyHost);

        foreach (GameObject host in hosts)
            UnityEngine.Object.DestroyImmediate(host);
    }

    private static void RemovePlacedResources()
    {
        HashSet<GameObject> roots = new();
        foreach (ZoneDefinition definition in Definitions)
        {
            foreach (Component resource in FindSceneObjects(definition.ResourceType))
            {
                GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(resource.gameObject);
                roots.Add(root != null ? root : resource.gameObject);
            }
        }

        foreach (GameObject root in roots)
            UnityEngine.Object.DestroyImmediate(root);
    }

    private static void RemoveEmptyResourceGroups()
    {
        string[] names = { "treeGroup", "TreeGroup", "StoneGroup", "CoalGroup", "SteelGroup" };
        foreach (string groupName in names)
        {
            GameObject group = FindSceneGameObject(groupName);
            if (group != null && group.transform.childCount == 0)
                UnityEngine.Object.DestroyImmediate(group);
        }
    }

    private static void ValidateScene(Scene scene)
    {
        ResourceSpawnZone2D[] spawners = FindSceneObjects<ResourceSpawnZone2D>();
        if (spawners.Length != Definitions.Length)
            throw new InvalidOperationException($"Expected 4 resource spawn zones, found {spawners.Length}.");

        foreach (ZoneDefinition definition in Definitions)
        {
            GameObject zone = FindSceneGameObject(definition.ZoneName);
            GameObject host = FindSceneGameObject(definition.SpawnName);
            if (zone == null || host == null)
                throw new InvalidOperationException($"Missing zone pair for {definition.ZoneName}.");

            BoxCollider2D source = zone.GetComponent<BoxCollider2D>();
            BoxCollider2D copy = host.GetComponent<BoxCollider2D>();
            ResourceSpawnZone2D spawner = host.GetComponent<ResourceSpawnZone2D>();
            GameObject expectedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(definition.PrefabPath);

            if (host.transform.parent != zone.transform || copy == null || spawner == null)
                throw new InvalidOperationException($"{definition.SpawnName} is not configured under its zone.");
            if (copy.offset != source.offset || copy.size != source.size)
                throw new InvalidOperationException($"{definition.SpawnName} does not match the source collider.");
            if (spawner.TargetResourceCount != TargetCount
                || !Mathf.Approximately(spawner.SpawnInterval, SpawnInterval)
                || spawner.ResourcePrefab != expectedPrefab)
                throw new InvalidOperationException($"{definition.SpawnName} has incorrect spawn settings.");
            if (FindSceneObjects(definition.ResourceType).Length != 0)
                throw new InvalidOperationException($"Placed {definition.ResourceType.Name} objects remain in the scene.");
        }

        Debug.Log("RESOURCE_SPAWN_ZONE_VALIDATION_PASS");
    }

    private static GameObject FindSceneGameObject(string objectName)
    {
        return FindSceneObjects<Transform>()
            .FirstOrDefault(transform => transform.name == objectName)?.gameObject;
    }

    private static T[] FindSceneObjects<T>() where T : UnityEngine.Object
    {
        return UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(item => item is Component component && component.gameObject.scene.IsValid())
            .ToArray();
    }

    private static Component[] FindSceneObjects(Type type)
    {
        return UnityEngine.Object.FindObjectsByType(type, FindObjectsInactive.Include, FindObjectsSortMode.None)
            .OfType<Component>()
            .Where(component => component.gameObject.scene.IsValid())
            .ToArray();
    }
}
#endif
