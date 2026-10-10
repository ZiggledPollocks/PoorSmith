// [코드 지도] CaveFloorOcclusion: 동굴 바닥의 시야 가림 캔버스를 지역에 따라 표시한다.
// 주요 함수: LateUpdate, MakeImage, Awake
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/Campaign/Field/CaveFloorOcclusion.cs.md

using UnityEngine;
using UnityEngine.UI;

/// <summary>Softly hides cave floors above and below the player's current switchback band.</summary>
[DisallowMultipleComponent]
public sealed class CaveFloorOcclusion : MonoBehaviour
{
    [SerializeField] CaveEntranceBackgroundTransition entrance;
    [SerializeField] Transform player;
    [SerializeField] float caveTop = 2f;
    [SerializeField] float caveBottom = -82f;
    [SerializeField] float[] floorBoundaries = { -21f, -39f, -59f };
    [SerializeField, Min(0f)] float overlap = 1f;
    [SerializeField, Min(.1f)] float feather = 2.5f;
    [SerializeField, Range(0f, 1f)] float darkness = .97f;

    Canvas canvas;
    RectTransform lowerSolid, lowerFade, upperFade, upperSolid;
    Texture2D risingAlpha, fallingAlpha;
    float shownTop, shownBottom, topVelocity, bottomVelocity;
    bool initialized;

    void Awake()
    {
        var root = new GameObject("Cave Floor Occlusion", typeof(RectTransform), typeof(Canvas));
        root.transform.SetParent(transform, false);
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // Above world art, below the field HUD (900).
        risingAlpha = GradientTexture(true);
        fallingAlpha = GradientTexture(false);
        lowerSolid = MakeImage(root.transform, "Lower Floor Solid", null);
        lowerFade = MakeImage(root.transform, "Lower Floor Feather", fallingAlpha);
        upperFade = MakeImage(root.transform, "Upper Floor Feather", risingAlpha);
        upperSolid = MakeImage(root.transform, "Upper Floor Solid", null);
        canvas.enabled = false;
    }

    // 핵심 분기: canvas == null || entrance == null || player == null 판정.
    // 상태 변경: canvas.enabled 갱신.
    // 다음 연결: CaveFloorOcclusion.SetBand(UnityEngine.RectTransform, float, float, float) 호출.
    void LateUpdate()
    {
        if (canvas == null || entrance == null || player == null) return;
        canvas.enabled = entrance.IsInsideCave;
        if (!canvas.enabled)
        {
            initialized = false;
            return;
        }

        Camera camera = Camera.main;
        if (camera == null) return;
        int floor = GetFloorIndex(player.position.y);
        float nearestBoundary = float.PositiveInfinity;
        foreach (float boundary in floorBoundaries)
            nearestBoundary = Mathf.Min(nearestBoundary, Mathf.Abs(player.position.y - boundary));
        // Open the seam only while crossing it; elsewhere the adjacent floor stays covered.
        float currentOverlap = overlap + 2f * (1f - Mathf.Clamp01(nearestBoundary / 3f));
        float targetTop = (floor == 0 ? caveTop : floorBoundaries[floor - 1]) + currentOverlap;
        float targetBottom = (floor == floorBoundaries.Length ? caveBottom : floorBoundaries[floor]) - currentOverlap;
        if (!initialized)
        {
            shownTop = targetTop;
            shownBottom = targetBottom;
            initialized = true;
        }
        else
        {
            shownTop = Mathf.SmoothDamp(shownTop, targetTop, ref topVelocity, .22f, Mathf.Infinity, Time.unscaledDeltaTime);
            shownBottom = Mathf.SmoothDamp(shownBottom, targetBottom, ref bottomVelocity, .22f, Mathf.Infinity, Time.unscaledDeltaTime);
        }

        float height = Screen.height;
        float lower = camera.WorldToScreenPoint(new Vector3(player.position.x, shownBottom, 0f)).y;
        float lowerFeather = camera.WorldToScreenPoint(new Vector3(player.position.x, shownBottom + feather, 0f)).y;
        float upper = camera.WorldToScreenPoint(new Vector3(player.position.x, shownTop, 0f)).y;
        float upperFeather = camera.WorldToScreenPoint(new Vector3(player.position.x, shownTop - feather, 0f)).y;
        SetBand(lowerSolid, 0f, lower, height);
        SetBand(lowerFade, lower, lowerFeather, height);
        SetBand(upperFade, upperFeather, upper, height);
        SetBand(upperSolid, upper, height, height);
    }

    // Keep gameplay floor rules aligned with the same boundaries used by the cave mask.
    public int GetFloorIndex(float worldY)
    {
        int floor = 0;
        while (floor < floorBoundaries.Length && worldY < floorBoundaries[floor]) floor++;
        return floor;
    }

    RectTransform MakeImage(Transform parent, string name, Texture2D texture)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        if (texture == null)
        {
            var image = go.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, darkness);
            image.raycastTarget = false;
        }
        else
        {
            var image = go.AddComponent<RawImage>();
            image.texture = texture;
            image.color = new Color(1f, 1f, 1f, darkness);
            image.raycastTarget = false;
        }
        return (RectTransform)go.transform;
    }

    static void SetBand(RectTransform rect, float bottom, float top, float height)
    {
        bottom = Mathf.Clamp(bottom, 0f, height);
        top = Mathf.Clamp(top, 0f, height);
        rect.gameObject.SetActive(top > bottom);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.right;
        rect.offsetMin = new Vector2(0f, bottom);
        rect.offsetMax = new Vector2(0f, top);
    }

    static Texture2D GradientTexture(bool rising)
    {
        var texture = new Texture2D(1, 64, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < 64; y++)
        {
            float alpha = y / 63f;
            texture.SetPixel(0, y, new Color(0f, 0f, 0f, rising ? alpha : 1f - alpha));
        }
        texture.Apply();
        return texture;
    }

    void OnDestroy()
    {
        if (risingAlpha != null) Destroy(risingAlpha);
        if (fallingAlpha != null) Destroy(fallingAlpha);
    }
}
