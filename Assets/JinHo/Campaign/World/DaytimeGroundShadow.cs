using UnityEngine;

/// <summary>Small daytime-only pixel contact cue for explicitly authored outdoor props/resources.</summary>
[DisallowMultipleComponent]
public sealed class DaytimeGroundShadow : MonoBehaviour
{
    public SpriteRenderer source;
    [Range(.05f, 1f)] public float widthFraction = .45f;
    [Min(.1f)] public float maximumWidth = 2.8f;
    [Range(.03f, .3f)] public float height = .16f;
    public Color tint = new(.18f, .12f, .08f, .16f);
    public LayerMask groundLayers;
    static Sprite shared;
    SpriteRenderer shadow;
    readonly RaycastHit2D[] hits = new RaycastHit2D[8];
    bool placed;
    int attempts;

    public static void Attach(GameObject target, LayerMask ground)
    {
        var cue = target.GetComponent<DaytimeGroundShadow>();
        if (cue == null) cue = target.AddComponent<DaytimeGroundShadow>();
        cue.groundLayers = ground;
    }
    void Awake()
    {
        if (source == null) source = GetComponentInChildren<SpriteRenderer>();
        if (source == null) { enabled = false; return; }
        if (shared == null)
        {
            // Code-native four-row pixel ellipse; no imported art or global material is edited.
            var texture = new Texture2D(32, 4, TextureFormat.RGBA32, false) { name = "Daytime contact pixels", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[128];
            int[] widths = { 16, 28, 32, 22 };
            for (int y = 0; y < 4; y++) for (int x = (32 - widths[y]) / 2; x < (32 + widths[y]) / 2; x++) pixels[y * 32 + x] = new Color32(255,255,255,255);
            texture.SetPixels32(pixels); texture.Apply(false, true);
            shared = Sprite.Create(texture, new Rect(0,0,32,4), new Vector2(.5f,.5f), 16);
            shared.name = "Daytime contact ellipse";
        }
        var node = new GameObject("Daytime contact shadow");
        // Sibling visuals stay outside resource renderer-bound queries used for placement/hit feedback.
        node.transform.SetParent(transform.parent, false);
        shadow = node.AddComponent<SpriteRenderer>(); shadow.sprite = shared;
        shadow.sharedMaterial = source.sharedMaterial; shadow.sortingLayerID = source.sortingLayerID;
        shadow.sortingOrder = source.sortingOrder - 1; shadow.color = tint; shadow.enabled = false;
    }
    void LateUpdate()
    {
        if (shadow == null) return;
        if (source == null) { shadow.enabled = false; return; }
        var loop = SmithingLoop.Instance;
        // Only explicitly marked outdoor placements have this component. Cave assets are not marked.
        bool visible = loop != null && loop.Initialized && !loop.SmithData.night && source.enabled && source.color.a > .001f && source.gameObject.activeInHierarchy && source.sprite != null;
        if (!placed && visible && attempts++ < 10) Place();
        var color = shadow.color;
        float alpha = tint.a * source.color.a;
        if (Mathf.Abs(color.a - alpha) > .001f) { color.a = alpha; shadow.color = color; }
        shadow.enabled = visible && placed;
    }
    void Place()
    {
        var bounds = source.bounds;
        var filter = new ContactFilter2D { useLayerMask = true, layerMask = groundLayers.value != 0 ? groundLayers.value : LayerMask.GetMask("Ground"), useTriggers = false };
        int count = Physics2D.Raycast(new Vector2(bounds.center.x, bounds.min.y + .5f), Vector2.down, filter, hits, 1.25f);
        float best = float.PositiveInfinity; RaycastHit2D floor = default;
        for (int i = 0; i < count; i++)
            if (hits[i].collider != null && !hits[i].transform.IsChildOf(transform) && hits[i].normal.y > .65f && hits[i].distance < best)
            { best = hits[i].distance; floor = hits[i]; }
        if (float.IsPositiveInfinity(best) || Mathf.Abs(floor.point.y - bounds.min.y) > .7f) return;
        float width = Mathf.Clamp(bounds.size.x * widthFraction, .3f, maximumWidth);
        Vector3 parentScale = shadow.transform.parent != null ? shadow.transform.parent.lossyScale : Vector3.one;
        shadow.transform.localScale = new Vector3(width / (2f * Mathf.Max(.001f, Mathf.Abs(parentScale.x))), height / (.25f * Mathf.Max(.001f, Mathf.Abs(parentScale.y))), 1);
        shadow.transform.position = new Vector3(bounds.center.x, floor.point.y + .015f, transform.position.z);
        placed = true;
    }
    void OnEnable() { placed = false; attempts = 0; }
    void OnDisable() { if (shadow != null) shadow.enabled = false; }
    void OnDestroy() { if (shadow != null) Destroy(shadow.gameObject); }
}
