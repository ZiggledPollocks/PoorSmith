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
        public InventorySlotView slotPrefab;
        public RecipeEntryView recipePrefab;
        public RectTransform stage, overlay, hud, tooltip;
        public TMP_Text status, notice;
        public static readonly Color Ink = new Color(.14f, .12f, .10f), Cream = new Color(.96f, .88f, .70f), Gold = new Color(.76f, .56f, .28f), Dark = new Color(.10f, .12f, .12f, .97f);
        Dictionary<string, Sprite> cache;
        public Sprite Art(string name)
        {
            if (cache == null)
            {
                cache = new Dictionary<string, Sprite>();
                foreach (var s in sprites)
                    if (s)
                        cache[s.name] = s;
            }

            return name != null && cache.TryGetValue(name, out var value) ? value : SmithingLoop.Instance?.ToolIcon(name);
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
            t.fontSize = size;
            t.color = color ?? Cream;
            t.alignment = align;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        public Button Button(string name, Transform parent, string label, Vector2 min, Vector2 max, Action action, string sprite = null)
        {
            var r = Rect(name, parent, min, max, new Vector2(3, 3));
            var im = r.gameObject.AddComponent<Image>();
            im.sprite = Art(sprite);
            im.color = sprite == null ? new Color(.22f, .21f, .18f) : Color.white;
            im.preserveAspect = sprite != null;
            var b = r.gameObject.AddComponent<Button>();
            b.targetGraphic = im;
            var c = b.colors;
            c.normalColor = Color.white;
            c.highlightedColor = new Color(1, .85f, .50f);
            c.pressedColor = new Color(.70f, .52f, .25f);
            c.disabledColor = new Color(.3f, .3f, .3f, .5f);
            b.colors = c;
            if (sprite == null)
            {
                var o = r.gameObject.AddComponent<Outline>();
                o.effectColor = Gold;
                o.effectDistance = new Vector2(1, -1);
            }

            if (!string.IsNullOrEmpty(label))
                Text("Label", r, label, 22, Vector2.zero, Vector2.one, null, TextAlignmentOptions.Center);
            if (action != null)
                b.onClick.AddListener(() => action());
            return b;
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
                grid.spacing = new Vector2(7, 7);
                grid.padding = new RectOffset(4, 4, 4, 4);
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
            slot.Bind(stack, item, Art(item.sprite), bag ? Art("bag_slot") : Art("slot"), font, click, right, drop, hover);
            return slot;
        }
    }
}