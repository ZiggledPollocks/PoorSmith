// [코드 지도] RecipeFogGraphic: 미발견 레시피 지도 위의 안개·통로를 메쉬로 그린다.
// 주요 함수: SampleOpacity, OnPopulateMesh, Update
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Crafting/Scripts/UI/RecipeFogGraphic.cs.md

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Blacksmith
{
    // One continuous field covers the map. Discoveries carve merging clearings.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RecipeFogGraphic : MaskableGraphic
    {
        sealed class Clearing
        {
            public Vector2 Center;
            public float Progress;
        }

        struct Passage
        {
            public string From, To;
            public Vector2 A, B;
        }

        readonly Dictionary<string, Clearing> clearings = new Dictionary<string, Clearing>();
        readonly List<Passage> passages = new List<Passage>();
        readonly Dictionary<string, Vector2> nodes = new Dictionary<string, Vector2>();
        public const float RevealDuration = 1.4f;
        public void RegisterNode(string id, Vector2 center)
        {
            nodes[id] = center;
            SetVerticesDirty();
        }

        public void Reveal(string id, Vector2 center, bool immediate)
        {
            if (clearings.ContainsKey(id))
                return;
            clearings.Add(id, new Clearing { Center = center, Progress = immediate ? 1 : 0 });
            SetVerticesDirty();
        }

        public void Connect(string from, string to, Vector2 a, Vector2 b)
        {
            passages.Add(new Passage { From = from, To = to, A = a, B = b });
            SetVerticesDirty();
        }

        public float RevealProgress(string id) => clearings.TryGetValue(id, out var hole) ? hole.Progress : 0;
        static float Feather(float inner, float outer, float distance)
        {
            return 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(inner, outer, distance));
        }

        static float Noise(Vector2 p)
        {
            return (Mathf.PerlinNoise(p.x * .018f + 17, p.y * .018f + 31) - .5f) * .14f + (Mathf.PerlinNoise(p.x * .043f, p.y * .043f) - .5f) * .05f;
        }

        // Top-left map coordinates, matching RecipeGraphLayout.
        public float SampleOpacity(Vector2 point)
        {
            float clear = 0, noise = Noise(point), leftmost = float.PositiveInfinity, leftGrowth = 0;
            foreach (var hole in clearings.Values)
            {
                float growth = Mathf.SmoothStep(0, 1, hole.Progress);
                float scale = Mathf.Lerp(.16f, 1, growth);
                Vector2 delta = point - hole.Center;
                // Clear a broader region, extending all the way to the left edge.
                float distance = new Vector2(Mathf.Max(0, delta.x) / 205, delta.y / 145).magnitude / scale;
                clear = Mathf.Max(clear, Feather(.72f, 1.22f, distance + noise) * growth);
                if (hole.Center.x < leftmost)
                {
                    leftmost = hole.Center.x;
                    leftGrowth = growth;
                }
                else if (Mathf.Approximately(hole.Center.x, leftmost))
                    leftGrowth = Mathf.Max(leftGrowth, growth);
            }

            if (!float.IsPositiveInfinity(leftmost))
                clear = Mathf.Max(clear, Feather(leftmost + 85, leftmost + 170, point.x + noise * 30) * leftGrowth);
            foreach (var passage in passages)
            {
                if (!clearings.TryGetValue(passage.From, out var from) || !clearings.TryGetValue(passage.To, out var to))
                    continue;
                float growth = Mathf.SmoothStep(0, 1, Mathf.Min(from.Progress, to.Progress));
                Vector2 ab = passage.B - passage.A;
                float t = ab.sqrMagnitude > 0 ? Mathf.Clamp01(Vector2.Dot(point - passage.A, ab) / ab.sqrMagnitude) : 0;
                float distance = Vector2.Distance(point, passage.A + ab * t);
                clear = Mathf.Max(clear, Feather(24, 70, distance / Mathf.Lerp(.16f, 1, growth) + noise * 45) * growth);
            }

            // Unknown cards and their immediate surroundings take priority over
            // the expanded clearings. Known cards remain fully readable.
            float protection = 0, knownInterior = 0, completion = nodes.Count > 0 ? 1 : 0;
            foreach (var node in nodes)
            {
                float growth = clearings.TryGetValue(node.Key, out var hole) ? Mathf.SmoothStep(0, 1, hole.Progress) : 0;
                completion = Mathf.Min(completion, growth);
                Vector2 delta = point - node.Value;
                float surrounding = Feather(1.03f, 1.72f, new Vector2(delta.x / 100, delta.y / 68).magnitude + noise);
                protection = Mathf.Max(protection, surrounding * (1 - growth));
                knownInterior = Mathf.Max(knownInterior, Feather(1.08f, 1.45f, new Vector2(delta.x / 90, delta.y / 60).magnitude) * growth);
            }

            return Mathf.Max(1 - clear, protection) * (1 - knownInterior) * (1 - completion);
        }

        void Update()
        {
            bool changed = false;
            foreach (var hole in clearings.Values)
            {
                if (hole.Progress >= 1)
                    continue;
                hole.Progress = Mathf.MoveTowards(hole.Progress, 1, Time.unscaledDeltaTime / RevealDuration);
                changed = true;
            }

            if (changed)
                SetVerticesDirty();
        }

        // 핵심 분기: rect.width <= 0 || rect.height <= 0 판정.
        // 다음 연결: Blacksmith.RecipeFogGraphic.SampleOpacity(UnityEngine.Vector2) 호출.
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = rectTransform.rect;
            if (rect.width <= 0 || rect.height <= 0)
                return;
            // Stay under Canvas's 65k vertex limit, including on large maps.
            int columns = Mathf.Clamp(Mathf.CeilToInt(rect.width / 12), 1, 240);
            int rows = Mathf.Clamp(Mathf.CeilToInt(rect.height / 12), 1, 240);
            for (int y = 0; y <= rows; y++)
                for (int x = 0; x <= columns; x++)
                {
                    var local = new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, (float)x / columns), Mathf.Lerp(rect.yMax, rect.yMin, (float)y / rows));
                    var point = new Vector2(local.x - rect.xMin, rect.yMax - local.y);
                    float cloud = Mathf.PerlinNoise(point.x * .008f + 7, point.y * .008f + 19);
                    var tint = new Color(.013f + cloud * .028f, .015f + cloud * .028f, .022f + cloud * .034f, SampleOpacity(point));
                    vh.AddVert(local, tint, Vector2.zero);
                }

            for (int y = 0; y < rows; y++)
                for (int x = 0; x < columns; x++)
                {
                    int a = y * (columns + 1) + x, b = a + columns + 1;
                    vh.AddTriangle(a, a + 1, b);
                    vh.AddTriangle(a + 1, b + 1, b);
                }
        }
    }
}