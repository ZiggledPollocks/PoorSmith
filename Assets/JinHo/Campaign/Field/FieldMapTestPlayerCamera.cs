// [코드 지도] FieldMapTestPlayerCamera: 필드 테스트에서 플레이어를 따라가는 카메라 동작을 제공한다.
// 주요 함수: LateUpdate, Target, Awake
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/Campaign/Field/FieldMapTestPlayerCamera.cs.md

using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Camera follow used only by the standalone field-map movement test.</summary>
[RequireComponent(typeof(Camera))]
public sealed class FieldMapTestPlayerCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector2 lowerLeft = new(-65f, -62f);
    [SerializeField] private Vector2 upperRight = new(115f, 19f);
    [SerializeField, Min(1f)] private float minSize = 6f;
    [SerializeField, Min(1f)] private float maxSize = 15f;

    private Camera sceneCamera;

    public Transform Target => target;

    private void Awake() => sceneCamera = GetComponent<Camera>();

    private void LateUpdate()
    {
        if (target == null) return;
        if (sceneCamera == null) sceneCamera = GetComponent<Camera>();

        float scroll = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
        if (Mathf.Abs(scroll) > 0.01f)
            sceneCamera.orthographicSize = Mathf.Clamp(
                sceneCamera.orthographicSize - Mathf.Sign(scroll), minSize, maxSize);

        float halfHeight = sceneCamera.orthographicSize;
        float halfWidth = halfHeight * sceneCamera.aspect;
        float x = Mathf.Clamp(target.position.x,
            lowerLeft.x + halfWidth, upperRight.x - halfWidth);
        float y = Mathf.Clamp(target.position.y,
            lowerLeft.y + halfHeight, upperRight.y - halfHeight);
        transform.position = new Vector3(x, y, -10f);
    }
}
