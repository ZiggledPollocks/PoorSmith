using UnityEngine;

namespace PoorSmith.UI
{
    /// <summary>
    /// 화면에 쓰는 크기와 색을 한곳에 모아둔다.
    /// 값이 파일마다 흩어져 있으면 한 군데만 고쳐도 균형이 깨지기 때문이다.
    /// 기준 해상도는 1920x1080이다.
    /// </summary>
    internal static class UITheme
    {
        // ---- 글자 ----
        internal const int TitleSize = 28;
        internal const int BodySize = 20;
        internal const int RowSize = 18;
        internal const int SmallSize = 16;
        internal const int NodeLabelSize = 18;

        // ---- 높이 ----
        internal const float HeaderHeight = 36f;
        internal const float RowHeight = 44f;
        internal const float ActionHeight = 54f;

        // ---- 여백 ----
        internal const float Padding = 20f;
        internal const float Spacing = 10f;

        // ---- 색 ----
        internal static readonly Color MapBackground = new(0.10f, 0.11f, 0.13f);
        internal static readonly Color PanelBackground = new(0.13f, 0.13f, 0.15f);
        internal static readonly Color ScrollBackground = new(0.16f, 0.14f, 0.11f);
        internal static readonly Color DebugBackground = new(0.08f, 0.08f, 0.10f);

        internal static readonly Color Title = new(0.96f, 0.94f, 0.88f);
        internal static readonly Color Body = new(0.86f, 0.86f, 0.88f);
        internal static readonly Color Muted = new(0.62f, 0.66f, 0.74f);

        internal static readonly Color ButtonFace = new(0.20f, 0.20f, 0.24f);
        internal static readonly Color ButtonText = new(0.90f, 0.90f, 0.92f);
        internal static readonly Color AccentFace = new(0.30f, 0.42f, 0.28f);
        internal static readonly Color WarnFace = new(0.32f, 0.24f, 0.22f);
        internal static readonly Color ScrollFace = new(0.35f, 0.30f, 0.18f);
        internal static readonly Color BenchFace = new(0.24f, 0.28f, 0.34f);
        internal static readonly Color Seal = new(0.62f, 0.20f, 0.18f);
        internal static readonly Color LockedSlot = new(0.22f, 0.20f, 0.17f);
    }
}
