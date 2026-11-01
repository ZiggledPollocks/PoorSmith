using UnityEngine;
using System.Collections;

/// <summary>One visual-only ON/OFF renderer; the existing landmark remains the gameplay authority.</summary>
[DisallowMultipleComponent]
public sealed class WarpStoneArt : MonoBehaviour
{
    CampaignWorldObject source;
    SpriteRenderer original, visual;
    GameArtSkin skin;
    bool on, grounded;
    float elapsed, groundY, centerX;

    void Awake()
    {
        source = GetComponent<CampaignWorldObject>();
        skin = GameArtSkin.Current;
        original = GetComponent<SpriteRenderer>();
        // The town stone owns a child WarpCrystal rather than a renderer on its functional root.
        if (original == null) original = GetComponentInChildren<SpriteRenderer>(true);
        if (source == null || skin == null || skin.warpOff == null || original == null) { enabled = false; return; }
        Bounds before = original.bounds;
        var child = new GameObject("WarpStoneArt");
        child.layer = gameObject.layer;
        child.transform.SetParent(transform, false);
        visual = child.AddComponent<SpriteRenderer>();
        visual.sharedMaterial = original.sharedMaterial;
        visual.sortingLayerID = original.sortingLayerID;
        visual.sortingOrder = original.sortingOrder;
        visual.color = original.color;
        visual.sprite = skin.warpOff;
        float worldScale = before.size.y / Mathf.Max(.001f, skin.warpOff.bounds.size.y);
        Vector3 parentScale = transform.lossyScale;
        child.transform.localScale = new Vector3(worldScale / Mathf.Max(.001f, Mathf.Abs(parentScale.x)), worldScale / Mathf.Max(.001f, Mathf.Abs(parentScale.y)), 1f);
        child.transform.position = before.center;
        centerX = before.center.x;
        groundY = before.min.y;
        original.enabled = false;
        AlignBase();
    }

    IEnumerator Start()
    {
        var hits = new RaycastHit2D[8];
        var filter = new ContactFilter2D { useLayerMask = true, layerMask = LayerMask.GetMask("Ground"), useTriggers = false };
        for (int attempt = 0; attempt < 10 && !grounded; attempt++)
        {
            Physics2D.SyncTransforms();
            int count = Physics2D.Raycast(new Vector2(centerX, transform.position.y + .1f), Vector2.down, filter, hits, 12f);
            float distance = float.MaxValue;
            for (int i = 0; i < count; i++)
                if (hits[i].collider != null && hits[i].normal.y > .3f && !hits[i].transform.IsChildOf(transform) && hits[i].distance < distance)
                { distance = hits[i].distance; groundY = hits[i].point.y + .015f; grounded = true; }
            AlignBase();
            if (!grounded) yield return null;
        }
    }

    void Update()
    {
        if (visual == null) return;
        var state = SmithingLoop.Instance?.Campaign;
        bool active = state != null && state.warpUnlocked && state.unlockedWarps != null && state.unlockedWarps.Contains(source.stableId);
        if (active != on) { on = active; elapsed = 0f; }
        Sprite sprite = skin.warpOff;
        // Authored frame durations, including hitch catch-up; no duplicate terminal clip key.
        if (on && skin.warpOn != null && skin.warpOn.Length > 0)
        {
            float total = 0f;
            for (int i = 0; i < skin.warpOn.Length; i++) total += Duration(i);
            elapsed = Mathf.Repeat(elapsed + Time.deltaTime, total);
            float remaining = elapsed;
            int frame = 0;
            while (frame < skin.warpOn.Length - 1 && remaining >= Duration(frame)) remaining -= Duration(frame++);
            sprite = skin.warpOn[frame];
        }
        visual.sprite = sprite;
        AlignBase();
    }

    float Duration(int frame) => skin.warpDurations != null && skin.warpDurations.Length == skin.warpOn.Length ? Mathf.Max(.001f, skin.warpDurations[frame]) : .1f;
    // Bounds, not the imported pivot, keep every frame on the same authored ground contact.
    void AlignBase()
    {
        if (visual == null) return;
        var delta = new Vector3(centerX - visual.bounds.center.x, groundY - visual.bounds.min.y, 0f);
        visual.transform.position += delta;
    }
    void OnDisable() { if (visual != null) visual.enabled = false; }
    void OnEnable() { if (visual != null) { visual.enabled = true; original.enabled = false; } }
}
