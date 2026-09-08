using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(1000)]
public class InfiniteBackground2D : MonoBehaviour
{
    private const int RequiredSectionCount = 3;

    [SerializeField] private Camera targetCamera;
    [SerializeField, Min(0f)] private float recyclePadding = 0.5f;
    [SerializeField, Min(0f)] private float seamOverlap = 0.1f;

    private readonly List<Transform> backgroundSections = new();
    private bool isInitialized;

    private void Awake()
    {
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

        RecycleBackgroundSections();
    }

    private void Initialize()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        backgroundSections.Clear();

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform section = transform.GetChild(i);

            if (TryGetSectionBounds(section, out _))
            {
                if (backgroundSections.Count < RequiredSectionCount)
                {
                    backgroundSections.Add(section);
                }
                else
                {
                    section.gameObject.SetActive(false);
                }
            }
        }

        if (targetCamera == null)
        {
            Debug.LogWarning("무한 배경이 추적할 Main Camera를 찾을 수 없습니다.", this);
            return;
        }

        if (!targetCamera.orthographic)
        {
            Debug.LogWarning("InfiniteBackground2D는 Orthographic Camera를 기준으로 동작합니다.", this);
            return;
        }

        if (backgroundSections.Count != RequiredSectionCount)
        {
            Debug.LogWarning("무한 배경에는 SpriteRenderer를 가진 활성 자식 구간이 정확히 3개 필요합니다.", this);
            return;
        }

        SortSectionsByPosition();
        AlignSections();
        isInitialized = true;
    }

    private void RecycleBackgroundSections()
    {
        float cameraHalfWidth = targetCamera.orthographicSize * targetCamera.aspect;
        float cameraLeft = targetCamera.transform.position.x - cameraHalfWidth - recyclePadding;
        float cameraRight = targetCamera.transform.position.x + cameraHalfWidth + recyclePadding;
        int maxIterations = backgroundSections.Count * 4;

        for (int i = 0; i < maxIterations; i++)
        {
            SortSectionsByPosition();

            Transform leftSection = backgroundSections[0];
            Transform rightSection = backgroundSections[^1];

            if (!TryGetSectionBounds(leftSection, out Bounds leftBounds) ||
                !TryGetSectionBounds(rightSection, out Bounds rightBounds))
            {
                isInitialized = false;
                return;
            }

            if (cameraLeft > leftBounds.max.x)
            {
                MoveSectionAfter(leftSection, leftBounds, rightBounds);
                continue;
            }

            if (cameraRight < rightBounds.min.x)
            {
                MoveSectionBefore(rightSection, rightBounds, leftBounds);
                continue;
            }

            break;
        }
    }

    private void SortSectionsByPosition()
    {
        backgroundSections.Sort((first, second) =>
        {
            float firstX = GetSectionCenterX(first);
            float secondX = GetSectionCenterX(second);
            return firstX.CompareTo(secondX);
        });
    }

    private static float GetSectionCenterX(Transform section)
    {
        return TryGetSectionBounds(section, out Bounds bounds)
            ? bounds.center.x
            : section.position.x;
    }

    private void AlignSections()
    {
        for (int i = 1; i < backgroundSections.Count; i++)
        {
            Transform previousSection = backgroundSections[i - 1];
            Transform currentSection = backgroundSections[i];

            if (!TryGetSectionBounds(previousSection, out Bounds previousBounds) ||
                !TryGetSectionBounds(currentSection, out Bounds currentBounds))
            {
                continue;
            }

            float targetMinX = previousBounds.max.x - seamOverlap;
            float moveDistance = targetMinX - currentBounds.min.x;
            currentSection.position += Vector3.right * moveDistance;
        }
    }

    private void MoveSectionAfter(
        Transform section,
        Bounds sectionBounds,
        Bounds rightBounds)
    {
        float targetMinX = rightBounds.max.x - seamOverlap;
        float moveDistance = targetMinX - sectionBounds.min.x;
        section.position += Vector3.right * moveDistance;
    }

    private void MoveSectionBefore(
        Transform section,
        Bounds sectionBounds,
        Bounds leftBounds)
    {
        float targetMaxX = leftBounds.min.x + seamOverlap;
        float moveDistance = targetMaxX - sectionBounds.max.x;
        section.position += Vector3.right * moveDistance;
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
