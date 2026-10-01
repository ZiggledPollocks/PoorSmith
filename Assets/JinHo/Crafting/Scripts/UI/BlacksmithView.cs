// [코드 지도] BlacksmithView: 대장간의 패널·이미지·버튼·텍스트·슬롯 UI를 만든다.
// 주요 함수: Button, Scroll, Art
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Crafting/Scripts/UI/BlacksmithView.cs.md

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blacksmith
{
    /// <summary>Creates and styles the blacksmith UI from reusable view helpers.</summary>
    public class BlacksmithView : MonoBehaviour
    {
        public TMP_FontAsset font;
        public Sprite[] sprites;
        public Sprite rackIdleSprite;
        public InventorySlotView slotPrefab;
        public RecipeEntryView recipePrefab;
        public RectTransform stage, overlay, hud, tooltip;
        public TMP_Text status, notice;
        public static readonly Color Ink = new Color(.14f, .12f, .10f), Cream = new Color(.96f, .88f, .70f), Gold = new Color(.76f, .56f, .28f), Dark = new Color(.10f, .12f, .12f, .97f);
        Dictionary<string, Sprite> cache;
        // 핵심 분기: cache == null 판정.
        // 상태 변경: cache 갱신.
        // 다음 연결: SmithingLoop.ToolIcon(string) 호출.
        public Sprite Art(string name)
        {
            if (cache == null)
            {
                cache = new Dictionary<string, Sprite>();
                foreach (var s in sprites)
                    if (s)
                        cache[s.name] = s;
            }

            if (name == null)
                return null;
            if (cache.TryGetValue(name, out var value))
                return value;
            if (name.StartsWith("item_", StringComparison.Ordinal))
            {
                var frames = Resources.LoadAll<Sprite>("ItemIcons/" + name);
                value = frames.Length > 0 ? frames[0] : null;
                if (value)
                    cache[name] = value;
                return value;
            }
            value = Resources.Load<Sprite>("SharedUi/" + name);
            if (!value)
            {
                var frames = Resources.LoadAll<Sprite>("SharedUi/" + name);
                value = frames.Length > 0 ? frames[0] : null;
            }
            if (value)
            {
                cache[name] = value;
                return value;
            }
            value = Resources.Load<Sprite>("ItemIcons/" + name);
            if (value)
            {
                cache[name] = value;
                return value;
            }
            if (name.StartsWith("Station", StringComparison.Ordinal))
            {
                value = Resources.Load<Sprite>("SmithyStationBackgrounds/" + name);
                if (value)
                    cache[name] = value;
                return value;
            }
            return SmithingLoop.Instance?.ToolIcon(name);
        }

        public Sprite ItemArt(ItemDefinition item) => item == null ? null : Art("item_" + item.id) ?? Art(item.sprite);
        public string ItemArtKey(ItemDefinition item) => item != null && Art("item_" + item.id) != null ? "item_" + item.id : item?.sprite;

        public Sprite WorkshopArt(string name)
        {
            var sprites = Resources.LoadAll<Sprite>("SharedUi/" + name);
            return sprites.Length > 0 ? sprites[0] : null;
        }

        public static void Clear(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var go = parent.GetChild(i).gameObject;
                go.SetActive(false);
                if (Application.isPlaying)
                    Destroy(go);
                else
                    DestroyImmediate(go);
            }
        }

        public RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 inset = default)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var r = go.GetComponent<RectTransform>();
            r.SetParent(parent, false);
            r.anchorMin = min;
            r.anchorMax = max;
            r.offsetMin = inset;
            r.offsetMax = -inset;
            return r;
        }

        public RectTransform Full(string name, Transform parent)
        {
            return Rect(name, parent, Vector2.zero, Vector2.one);
        }

        public Image Image(string name, Transform parent, string sprite, Color color, Vector2 min, Vector2 max, bool aspect = false)
        {
            var r = Rect(name, parent, min, max);
            var im = r.gameObject.AddComponent<Image>();
            im.sprite = Art(sprite);
            im.color = color;
            im.preserveAspect = aspect;
            im.raycastTarget = false;
            return im;
        }

        public RectTransform Panel(string name, Transform parent, Vector2 min, Vector2 max, bool paper = false)
        {
            var r = Rect(name, parent, min, max);
            var im = r.gameObject.AddComponent<Image>();
            im.color = paper ? Color.white : Dark;
            im.sprite = paper ? Art("paper") : null;
            if (paper)
            {
                Image("TopRoll", r, "paper_cap", Color.white, new Vector2(-.015f, .965f), new Vector2(1.015f, 1.035f));
                Image("BottomRoll", r, "paper_cap", Color.white, new Vector2(-.015f, -.035f), new Vector2(1.015f, .035f));
            }
            else
            {
                var outline = r.gameObject.AddComponent<Outline>();
                outline.effectColor = Gold;
                outline.effectDistance = new Vector2(1, -1);
            }

            return r;
        }

        public TMP_Text Text(string name, Transform parent, string value, float size, Vector2 min, Vector2 max, Color? color = null, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
        {
            var r = Rect(name, parent, min, max, new Vector2(6, 3));
            var t = r.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font;
            t.text = value;
            RuntimeUIFactory.FitText(t, size);
            t.color = color ?? Cream;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        // 핵심 분기: sprite == null 판정.
        // 상태 변경: im.sprite 갱신.
        // 다음 연결: Blacksmith.BlacksmithView.Rect(string, UnityEngine.Transform, UnityEngine.Vector2, UnityEngine.Vector2, Unity… 호출.
        public Button Button(string name, Transform parent, string label, Vector2 min, Vector2 max, Action action, string sprite = null)
        {
            var r = Rect(name, parent, min, max, new Vector2(3, 3));
            var im = r.gameObject.AddComponent<Image>();
            im.sprite = Art(sprite);
            bool framed = sprite == null && !string.IsNullOrEmpty(label) && !name.StartsWith("Equip_", StringComparison.Ordinal);
            im.color = sprite == null ? (framed ? new Color(.25f, .18f, .12f) : new Color(.22f, .21f, .18f)) : Color.white;
            im.preserveAspect = sprite != null;
            var b = r.gameObject.AddComponent<Button>();
            b.targetGraphic = im;
            var c = b.colors;
            c.normalColor = Color.white;
            c.highlightedColor = framed ? new Color(1.25f, 1.15f, .93f) : new Color(1, .85f, .50f);
            c.pressedColor = framed ? new Color(.72f, .67f, .59f) : new Color(.70f, .52f, .25f);
            c.disabledColor = framed ? new Color(.55f, .55f, .55f, .8f) : new Color(.3f, .3f, .3f, .5f);
            b.colors = c;
            if (sprite == null)
            {
                var o = r.gameObject.AddComponent<Outline>();
                o.effectColor = framed ? new Color(.69f, .51f, .30f) : Gold;
                o.effectDistance = framed ? new Vector2(2, -2) : new Vector2(1, -1);
            }

            if (!string.IsNullOrEmpty(label))
            {
                if (framed)
                {
                    var shadow = r.gameObject.AddComponent<Shadow>();
                    shadow.effectColor = new Color(.035f, .025f, .015f, .8f);
                    shadow.effectDistance = new Vector2(0, -4);
                    Image("TopEdge", r, null, new Color(.84f, .67f, .42f, .78f),
                        new Vector2(0, 1), Vector2.one).rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 2);
                    Image("BottomEdge", r, null, new Color(.075f, .045f, .025f, .9f),
                        Vector2.zero, new Vector2(1, 0)).rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 3);
                }
                var text = Text("Label", r, label, 21, Vector2.zero, Vector2.one,
                    framed ? Cream : (Color?)null, TextAlignmentOptions.Center);
                if (framed)
                {
                    text.fontStyle = FontStyles.Bold;
                    RuntimeUIFactory.FitText(text, 21);
                }
            }
            if (action != null)
                b.onClick.AddListener(() => action());
            return b;
        }

        public void Emphasize(Button button)
        {
            button.image.color = new Color(.68f, .43f, .19f);
            var outline = button.GetComponent<Outline>();
            if (outline != null)
                outline.effectColor = new Color(.98f, .77f, .40f);
            var label = button.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.color = Color.white;
        }

        public void HighlightChoice(Button button, bool selected)
        {
            button.image.color = selected ? Gold : new Color(.25f, .18f, .12f);
            var label = button.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.color = selected ? Ink : Cream;
        }

        public TMP_InputField Search(Transform parent, Action<string> changed, Vector2 min, Vector2 max)
        {
            var r = Panel("Search", parent, min, max);
            var field = r.gameObject.AddComponent<TMP_InputField>();
            var viewport = Rect("TextViewport", r, Vector2.zero, Vector2.one, new Vector2(10, 4));
            viewport.gameObject.AddComponent<RectMask2D>();
            var text = Text("Text", viewport, "", 20, Vector2.zero, Vector2.one);
            var placeholder = Text("Placeholder", viewport, "아이템 검색…", 20, Vector2.zero, Vector2.one, new Color(.6f, .6f, .55f));
            field.textViewport = viewport;
            field.textComponent = (TextMeshProUGUI)text;
            field.placeholder = placeholder;
            field.onValueChanged.AddListener(value => changed(value));
            return field;
        }

        // 핵심 분기: columns > 0 판정.
        // 상태 변경: sr.horizontal 갱신.
        // 다음 연결: Blacksmith.BlacksmithView.Rect(string, UnityEngine.Transform, UnityEngine.Vector2, UnityEngine.Vector2, Unity… 호출.
        public RectTransform Scroll(Transform parent, string name, Vector2 min, Vector2 max, int columns = 0, float cell = 90)
        {
            var root = Rect(name, parent, min, max);
            var sr = root.gameObject.AddComponent<ScrollRect>();
            sr.horizontal = false;
            sr.scrollSensitivity = 30;
            sr.movementType = ScrollRect.MovementType.Clamped;
            var viewport = Rect("Viewport", root, Vector2.zero, new Vector2(.96f, 1));
            viewport.gameObject.AddComponent<RectMask2D>();
            var bg = viewport.gameObject.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, .001f);
            var content = Rect("Content", viewport, new Vector2(0, 1), Vector2.one);
            content.pivot = new Vector2(.5f, 1);
            content.sizeDelta = new Vector2(0, 0);
            if (columns > 0)
            {
                var grid = content.gameObject.AddComponent<GridLayoutGroup>();
                grid.cellSize = new Vector2(cell, cell);
                grid.spacing = new Vector2(8, 8);
                grid.padding = new RectOffset(8, 8, 8, 8);
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = columns;
            }
            else
            {
                var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.spacing = 12;
                layout.padding = new RectOffset(6, 6, 6, 6);
                layout.childControlHeight = true;
                layout.childForceExpandHeight = false;
                layout.childControlWidth = true;
            }

            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.content = content;
            sr.viewport = viewport;
            var bar = Rect("Scrollbar", root, new Vector2(.975f, 0), Vector2.one);
            bar.gameObject.AddComponent<Image>().color = new Color(.25f, .23f, .18f);
            var handle = Full("Handle", bar);
            var hi = handle.gameObject.AddComponent<Image>();
            hi.color = Gold;
            var scroll = bar.gameObject.AddComponent<Scrollbar>();
            scroll.handleRect = handle;
            scroll.targetGraphic = hi;
            scroll.direction = Scrollbar.Direction.BottomToTop;
            sr.verticalScrollbar = scroll;
            return content;
        }

        // Keep empty inventory cells visible without making decorative cells intercept drops.
        public void FillEmptyGridSlots(RectTransform content, int occupied, int minimumRows = 4)
        {
            var grid = content.GetComponent<GridLayoutGroup>();
            if (grid == null || grid.constraintCount < 1) return;
            foreach (Transform child in content)
                if (child.name == "EmptyGridSlot")
                {
                    child.gameObject.SetActive(false);
                    Destroy(child.gameObject);
                }
            int columns = grid.constraintCount;
            int cellCount = Math.Max(columns * minimumRows,
                ((occupied + columns - 1) / columns) * columns);
            for (int i = occupied; i < cellCount; i++)
            {
                var empty = Image("EmptyGridSlot", content, "slot",
                    new Color(1f, 1f, 1f, .32f), Vector2.zero, Vector2.one);
                empty.raycastTarget = false;
            }
        }

        public void Build()
        {
            Clear(transform);
            stage = Full("StationStage", transform);
            overlay = Full("InteractivePanels", transform);
            hud = Full("HUD", transform);
            Panel("Header", hud, new Vector2(0, .93f), Vector2.one);
            status = Text("Status", hud, "", 21, new Vector2(.21f, .934f), new Vector2(.75f, .992f));
            notice = Text("Feedback", hud, "", 23, new Vector2(.12f, 0), new Vector2(.88f, .07f), Cream, TextAlignmentOptions.Center);
            tooltip = Panel("Tooltip", hud, new Vector2(.32f, .07f), new Vector2(.70f, .24f));
            tooltip.gameObject.SetActive(false);
        }

        public void ShowTooltip(string value)
        {
            Clear(tooltip);
            Text("Info", tooltip, value, 21, new Vector2(.02f, .03f), new Vector2(.98f, .97f));
            tooltip.gameObject.SetActive(true);
            tooltip.SetAsLastSibling();
        }

        public void HideTooltip()
        {
            tooltip.gameObject.SetActive(false);
        }

        public InventorySlotView Slot(Transform parent, Stack stack, ItemDefinition item, Action click, Action right, Action<InventorySlotView> drop, Action<bool> hover = null, bool bag = false)
        {
            var slot = Instantiate(slotPrefab, parent);
            slot.gameObject.SetActive(true);
            slot.name = item.displayName + "_Slot";
            slot.Bind(stack, item, ItemArt(item), bag ? Art("bag_slot") : Art("slot"), font, click, right, drop, hover);
            return slot;
        }
    }
}
