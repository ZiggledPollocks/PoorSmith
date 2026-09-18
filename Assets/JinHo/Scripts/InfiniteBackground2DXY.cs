using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(1000)]
[DisallowMultipleComponent]
public sealed class InfiniteBackground2DXY : MonoBehaviour
{
    private const int GridWidth = 3;
    private const int GridHeight = 3;
    private const int RequiredSectionCount = GridWidth * GridHeight;

    [SerializeField] private Camera targetCamera;
    [SerializeField] private Transform sectionTemplate;
    [SerializeField] private Vector2 seamOverlap = new(0.05f, 0.05f);

    private readonly List<Transform> sections = new(RequiredSectionCount);
    private Vector2 sectionSpacing;
    private Vector2Int currentCenterCell = new(int.MinValue, int.MinValue);
    private bool isInitialized;

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        if (!isInitialized)
            Initialize();
    }

    private void LateUpdate()
    {
        if (!isInitialized)
        {
            Initialize();
        }

        if (!isInitialized)
            return;

        LayoutAroundCamera();
    }

    private void Initialize()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (sectionTemplate == null && transform.childCount > 0)
            sectionTemplate = transform.GetChild(0);

        if (targetCamera == null || sectionTemplate == null ||
            !TryGetSectionBounds(sectionTemplate, out Bounds templateBounds))
        {
            return;
        }

        sectionSpacing = new Vector2(
            Mathf.Max(0.01f, templateBounds.size.x - Mathf.Max(0f, seamOverlap.x)),
            Mathf.Max(0.01f, templateBounds.size.y - Mathf.Max(0f, seamOverlap.y)));

        sections.Clear();
        sections.Add(sectionTemplate);

        for (int i = 1; i < RequiredSectionCount; i++)
        {
            Transform clone = Instantiate(sectionTemplate, transform);
            clone.name = $"CaveBackgroundSection_{i + 1}";
            sections.Add(clone);
        }

        isInitialized = true;
        currentCenterCell = new Vector2Int(int.MinValue, int.MinValue);
        LayoutAroundCamera();
    }

    private void LayoutAroundCamera()
    {
        Vector3 cameraPosition = targetCamera.transform.position;
        Vector2Int centerCell = new(
            Mathf.RoundToInt(cameraPosition.x / sectionSpacing.x),
            Mathf.RoundToInt(cameraPosition.y / sectionSpacing.y));

        if (centerCell == currentCenterCell)
            return;

        currentCenterCell = centerCell;

        int index = 0;
        for (int row = 0; row < GridHeight; row++)
        {
            for (int column = 0; column < GridWidth; column++)
            {
                Transform section = sections[index++];
                if (!TryGetSectionBounds(section, out Bounds bounds))
                    continue;

                int cellX = centerCell.x + column - GridWidth / 2;
                int cellY = centerCell.y + row - GridHeight / 2;
                Vector3 targetCenter = new(
                    cellX * sectionSpacing.x,
                    cellY * sectionSpacing.y,
                    bounds.center.z);

                section.position += targetCenter - bounds.center;
            }
        }
    }

    private static bool TryGetSectionBounds(Transform section, out Bounds bounds)
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
