using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

/// <summary>Shared, scene-local presentation of saved exploration cells and discovered landmarks.</summary>
public static class ExplorationMapLayout
{
    public static Vector2 Project(Vector2 world, Bounds bounds)
    {
        return new Vector2(
            Mathf.Clamp01((world.x - bounds.min.x) / Mathf.Max(1f, bounds.size.x)),
            Mathf.Clamp01((world.y - bounds.min.y) / Mathf.Max(1f, bounds.size.y)));
    }

    public static bool Contains(Bounds bounds, Vector2 world)
    {
        return world.x >= bounds.min.x && world.x <= bounds.max.x &&
               world.y >= bounds.min.y && world.y <= bounds.max.y;
    }

    public static void DrawVisited(RectTransform parent, IEnumerable<string> cells,
        float cellSize, Bounds bounds, Color forest, Color cave, float caveEntranceX, float caveDepthY)
    {
        if (cells == null || cellSize <= 0f) return;
        foreach (string cell in cells)
        {
            if (string.IsNullOrEmpty(cell)) continue;
            string[] parts = cell.Split(',');
            if (parts.Length != 2 || !int.TryParse(parts[0], out int x) ||
                !int.TryParse(parts[1], out int y)) continue;
            Vector2 worldMin = new Vector2(x * cellSize, y * cellSize);
            Vector2 worldMax = worldMin + Vector2.one * cellSize;
            if (worldMax.x <= bounds.min.x || worldMin.x >= bounds.max.x ||
                worldMax.y <= bounds.min.y || worldMin.y >= bounds.max.y) continue;
            Vector2 min = Project(worldMin, bounds);
            Vector2 max = Project(worldMax, bounds);
            if (max.x <= min.x || max.y <= min.y) continue;
            bool isCave = worldMin.y < caveDepthY || worldMin.x >= caveEntranceX;
            RectTransform rect = Rect("Discovered_" + cell, parent, min, max);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = isCave ? cave : forest;
            image.raycastTarget = false;
        }
    }

    public static void DrawTerrain(RectTransform parent, CampaignExploration exploration,
        Bounds bounds, Color color)
    {
        if (exploration == null) return;
        const int step = 3;
        const int maxPatches = 1400;
        int drawn = 0;
        foreach (Tilemap tilemap in Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
        {
            BoundsInt cells = tilemap.cellBounds;
            for (int x = cells.xMin; x < cells.xMax; x += step)
            for (int y = cells.yMin; y < cells.yMax; y += step)
            {
                var cell = new Vector3Int(x, y, 0);
                if (!tilemap.HasTile(cell)) continue;
                Vector2 world = tilemap.CellToWorld(cell);
                if (!Contains(bounds, world) || !exploration.IsExplored(world)) continue;
                float size = Mathf.Max(1f, exploration.cellSize);
                Vector2 cellEnd = new Vector2(
                    (Mathf.FloorToInt(world.x / size) + 1) * size,
                    (Mathf.FloorToInt(world.y / size) + 1) * size);
                Vector2 max = Vector2.Min(world + Vector2.one * step, cellEnd);
                var image = Rect("Terrain", parent, Project(world, bounds),
                    Project(max, bounds)).gameObject.AddComponent<Image>();
                image.color = color;
                image.raycastTarget = false;
                if (++drawn >= maxPatches) return;
            }
        }
    }

    public static RectTransform Marker(RectTransform parent, string name, Vector2 world,
        Bounds bounds, string glyph, Color color, TMP_FontAsset font, Sprite sprite = null)
    {
        if (!Contains(bounds, world)) return null;
        var root = Rect(name, parent, Vector2.zero, Vector2.zero);
        root.anchorMin = root.anchorMax = Project(world, bounds);
        root.sizeDelta = new Vector2(34f, 34f);
        root.anchoredPosition = Vector2.zero;
        Image image = root.gameObject.AddComponent<Image>();
        image.color = sprite == null ? color : Color.white;
        image.sprite = sprite;
        image.preserveAspect = sprite != null;
        image.raycastTarget = false;
        if (sprite == null)
        {
            var label = Rect("Icon", root, Vector2.zero, Vector2.one);
            var text = label.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font ?? TMP_Settings.defaultFontAsset;
            text.fontSize = 19f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.black;
            text.raycastTarget = false;
            text.text = glyph;
        }
        return root;
    }

    public static void MoveMarker(RectTransform marker, Vector2 world, Bounds bounds)
    {
        if (marker != null)
            marker.anchorMin = marker.anchorMax = Project(world, bounds);
    }

    public static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }
}
