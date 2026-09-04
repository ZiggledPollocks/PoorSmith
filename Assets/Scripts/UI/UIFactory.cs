using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace PoorSmith.UI
{
    /// <summary>
    /// 화면을 코드로 조립하기 위한 도구들.
    /// 프리팹 없이 만드는 이유는 회색 상자 단계에서 배치를 자주 갈아엎기 때문이다.
    /// 아트가 들어오면 프리팹으로 옮기면 된다.
    /// </summary>
    internal static class UIFactory
    {
        static Font cachedFont;
        static Sprite cachedCircle;
        static Sprite cachedSquare;
        static Sprite cachedTriangle;

        /// <summary>
        /// 한글이 나오는 글꼴을 찾는다. 내장 글꼴에는 한글이 없어 네모로 보이기 때문이다.
        /// 빌드에 넣으려면 한글 글꼴 에셋을 따로 넣어야 한다.
        /// </summary>
        internal static Font Font
        {
            get
            {
                if (cachedFont != null) return cachedFont;

                var installed = Font.GetOSInstalledFontNames();
                var preferred = new[]
                {
                    "Apple SD Gothic Neo", "AppleGothic", "Malgun Gothic",
                    "Noto Sans KR", "NanumGothic", "Pretendard",
                };

                var found = preferred.FirstOrDefault(name => installed.Contains(name));
                cachedFont = found != null
                    ? Font.CreateDynamicFontFromOSFont(found, 16)
                    : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

                return cachedFont;
            }
        }

        static PoorSmith.Data.NodeVisuals visuals;

        /// <summary>인스펙터에서 지정한 도형 묶음. 비어 있는 항목은 코드로 그린 것으로 채운다.</summary>
        internal static void UseVisuals(PoorSmith.Data.NodeVisuals assigned) => visuals = assigned;

        internal static Sprite Circle =>
            visuals != null && visuals.Circle != null ? visuals.Circle : cachedCircle ??= MakeCircle(96);

        internal static Sprite Square =>
            visuals != null && visuals.Square != null ? visuals.Square : cachedSquare ??= MakeSquare();

        /// <summary>위를 가리키는 삼각형. 돌려서 화살표로 쓴다.</summary>
        internal static Sprite Triangle =>
            visuals != null && visuals.Arrow != null ? visuals.Arrow : cachedTriangle ??= MakeTriangle(64);

        /// <summary>노드 사이를 잇는 선. 지정된 게 없으면 단색 사각형으로 그린다.</summary>
        internal static Sprite Line => visuals != null ? visuals.Line : null;

        internal static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        internal static Image Panel(string name, Transform parent, Color color, Sprite sprite = null)
        {
            var image = Rect(name, parent).gameObject.AddComponent<Image>();
            image.color = color;
            if (sprite != null) image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        internal static Text Label(
            string name, Transform parent, string text, int size,
            Color color, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var label = Rect(name, parent).gameObject.AddComponent<Text>();
            label.font = Font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = anchor;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
        }

        internal static Button Button(string name, Transform parent, string text, Color background, Color foreground)
        {
            var image = Rect(name, parent).gameObject.AddComponent<Image>();
            image.color = background;

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            var label = Label($"{name}Label", image.transform, text, 14, foreground);
            Stretch(label.rectTransform);

            return button;
        }

        /// <summary>
        /// 내용에 맞춰 세로로 늘어나는 칸. 자식은 각자의 높이를 스스로 정한다.
        /// 글자는 줄 수에 따라, 버튼은 LayoutElement에 적힌 높이에 따라 잡힌다.
        /// </summary>
        internal static RectTransform Column(Transform parent, float spacing = UITheme.Spacing, float padding = 0f)
        {
            var column = Rect("Column", parent);

            var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset((int)padding, (int)padding, (int)padding, (int)padding);
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;

            return column;
        }

        /// <summary>
        /// 넘치면 스크롤되는 칸. 작업 공간처럼 내용이 화면보다 길어지는 곳에 쓴다.
        /// 반환값은 내용을 담을 칸이다.
        /// </summary>
        internal static RectTransform ScrollColumn(Transform parent, float padding = UITheme.Padding)
        {
            var viewport = Rect("Viewport", parent);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            scroll.viewport = viewport;

            var content = Column(viewport, UITheme.Spacing, padding);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(0f, content.offsetMin.y);
            content.offsetMax = new Vector2(0f, content.offsetMax.y);

            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = content;
            return content;
        }

        /// <summary>세로 칸 안에서 이 요소가 차지할 높이를 못박는다.</summary>
        internal static void FixHeight(Component target, float height)
        {
            var element = target.gameObject.GetComponent<LayoutElement>()
                          ?? target.gameObject.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            element.flexibleHeight = 0f;
        }

        /// <summary>부모를 가득 채우도록 늘린다.</summary>
        internal static void Stretch(RectTransform rect, float padding = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
        }

        /// <summary>부모의 왼쪽/오른쪽 일부를 세로로 가득 차지하게 한다.</summary>
        internal static void SplitVertical(RectTransform rect, float from, float to)
        {
            rect.anchorMin = new Vector2(from, 0f);
            rect.anchorMax = new Vector2(to, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static Sprite MakeSquare()
        {
            var texture = new Texture2D(4, 4);
            var pixels = Enumerable.Repeat(Color.white, 16).ToArray();
            texture.SetPixels(pixels);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
        }

        static Sprite MakeTriangle(int size)
        {
            var texture = new Texture2D(size, size) { filterMode = FilterMode.Bilinear };

            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                // 위로 갈수록 좁아지는 이등변삼각형.
                var half = (size - 1 - y) * 0.5f;
                var distance = Mathf.Abs(x - (size - 1) * 0.5f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(half - distance)));
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        static Sprite MakeCircle(int size)
        {
            var texture = new Texture2D(size, size) { filterMode = FilterMode.Bilinear };
            var center = (size - 1) * 0.5f;
            var radius = center;

            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                // 가장자리를 한 픽셀에 걸쳐 부드럽게 깎는다.
                var alpha = Mathf.Clamp01(radius - distance);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
