// [코드 지도] BlacksmithView: 대장간의 패널·이미지·버튼·텍스트·슬롯 UI를 만든다.
// 주요 함수: Button, Scroll, Art
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Crafting/Scripts/UI/BlacksmithView.cs.md

using System;
using System.Collections.Generic;
using System.Linq;
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
        public static readonly Color Ink = new Color(.11f, .09f, .07f), Cream = new Color(.95f, .89f, .77f), Gold = new Color(.79f, .63f, .37f), Dark = new Color(.12f, .11f, .09f, .97f);
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
            // General workshop art has its own key; merchandise and weapon icons keep their existing lookup.
            if (name == "crafting_hammer") return GameArtSkin.Current != null && GameArtSkin.Current.WorkshopHammer != null ? GameArtSkin.Current.WorkshopHammer : Art("hammer");
            if (name == "item_wind_stone" && GameArtSkin.Current != null) return GameArtSkin.Current.windCrystal;
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
            if (name == "tools" && GameArtSkin.Current != null && GameArtSkin.Current.workshopTools != null) return GameArtSkin.Current.workshopTools;
            var sprites = Resources.LoadAll<Sprite>("SharedUi/" + name);
            return sprites.Length > 0 ? sprites[0] : null;
        }

        public static void Clear(Transform parent)
        {
            UIKeyboardNavigation.MarkDirty(parent);
            var memory = parent.GetComponentInParent<Canvas>()?.GetComponent<UIScrollPositionMemory>();
            if (memory == null && parent.GetComponentInParent<Canvas>() is Canvas canvas)
                memory = canvas.gameObject.AddComponent<UIScrollPositionMemory>();
            memory?.Capture(parent);
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

            GameArtSkin.Panel(im);
            return r;
        }

        public TMP_Text Text(string name, Transform parent, string value, float size, Vector2 min, Vector2 max, Color? color = null, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
        {
            var r = Rect(name, parent, min, max, new Vector2(6, 3));
            var t = r.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = GameTextFonts.Primary ?? font;
            t.text = value;
            RuntimeUIFactory.FitText(t, size);
            t.color = color ?? Cream;
            GameArtSkin.Text(t);
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
            im.color = sprite == null ? (framed ? new Color(.28f, .21f, .15f) : new Color(.18f, .16f, .13f)) : Color.white;
            im.preserveAspect = sprite != null;
            var b = r.gameObject.AddComponent<Button>();
            b.targetGraphic = im;
            var c = b.colors;
            c.normalColor = Color.white;
            c.highlightedColor = framed ? new Color(1.15f, 1.08f, .90f) : new Color(1f, .88f, .60f);
            c.pressedColor = framed ? new Color(.75f, .66f, .52f) : new Color(.72f, .56f, .32f);
            c.disabledColor = framed ? new Color(.48f, .44f, .38f, .75f) : new Color(.35f, .32f, .28f, .5f);
            b.colors = c;
            if (!string.IsNullOrEmpty(label) || !string.IsNullOrEmpty(sprite))
            {
                var o = r.gameObject.AddComponent<Outline>();
                o.effectColor = framed ? new Color(.73f, .56f, .33f) : Gold;
                o.effectDistance = framed ? new Vector2(2, -2) : new Vector2(1, -1);
                o.useGraphicAlpha = true;
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
                var text = Text("Label", r, label, 24, Vector2.zero, Vector2.one,
                    framed ? Cream : (Color?)null, TextAlignmentOptions.Center);
                if (framed)
                {
                    text.fontStyle = FontStyles.Bold;
                    RuntimeUIFactory.FitText(text, 24);
                }
            }
            if (action != null)
                b.onClick.AddListener(() =>
                {
                    if (GetComponent<StoryTutorialView>() != null || StoryTutorial.AllowButton(name)) action();
                });
            GameArtSkin.Button(b);
            return b;
        }

        public void Emphasize(Button button)
        {
            button.image.color = new Color(.63f, .42f, .22f);
            var outline = button.GetComponent<Outline>();
            if (outline != null)
                outline.effectColor = Gold;
            var label = button.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.color = Color.white;
        }

        public void HighlightChoice(Button button, bool selected)
        {
            button.image.color = selected ? Gold : new Color(.28f, .21f, .15f);
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
            var placeholder = Text("Placeholder", viewport, "아이템 검색", 20, Vector2.zero, Vector2.one, new Color(.6f, .6f, .55f));
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
            var viewport = Rect("Viewport", root, Vector2.zero, new Vector2(.97f, 1));
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
            var positionMemory = root.GetComponentInParent<Canvas>()?.GetComponent<UIScrollPositionMemory>();
            positionMemory?.RestoreWhenLaidOut(sr);
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
            sr.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
            var skin=GameArtSkin.Current;
            if(skin!=null){bar.GetComponent<Image>().sprite=skin.scrollTrack;bar.GetComponent<Image>().type=UnityEngine.UI.Image.Type.Sliced;bar.GetComponent<Image>().color=Color.white;hi.sprite=skin.grabber;hi.type=UnityEngine.UI.Image.Type.Sliced;hi.color=Color.white;}
            bar.gameObject.AddComponent<UIKeyboardScrollbar>().bar=scroll;
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
            // Lists do not imply fixed capacity. Existing functional destination slots are built by their owners.

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
            TownFieldUiStyle.FrameBehind(notice,new Color(.14f,.08f,.05f,.96f));
            tooltip = Panel("Tooltip", hud, new Vector2(.32f, .07f), new Vector2(.70f, .24f));
            tooltip.gameObject.SetActive(false);
        }

        public int TooltipUpdatedFrame {get;private set;}=-1;
        public OwnedItemActions DetailsSource {get;set;}
        Coroutine tooltipHide;
        bool tooltipHovered;
        UIKeyboardSurface tooltipScope;
        void LateUpdate(){if(tooltip!=null&&tooltip.gameObject.activeSelf&&tooltipScope!=GameUIController.TopPresentation){tooltip.gameObject.SetActive(false);DetailsSource=null;}}
        public void ShowTooltip(string value)
        {
            tooltipScope=GameUIController.TopPresentation;
            if(tooltipScope!=null)UIKeyboardNavigation.AttachRoot(tooltipScope,tooltip);
            var tooltipCanvas=tooltip.GetComponent<Canvas>();if(tooltipCanvas==null)tooltipCanvas=tooltip.gameObject.AddComponent<Canvas>();tooltipCanvas.overrideSorting=true;tooltipCanvas.sortingOrder=GameUIController.PresentationOverlayOrder+1;
            if(tooltipHide!=null){StopCoroutine(tooltipHide);tooltipHide=null;}
            TooltipUpdatedFrame=Time.frameCount;DetailsSource=null;tooltipHovered=false;Clear(tooltip);tooltip.gameObject.SetActive(true);
            var lines=(value??string.Empty).Split('\n');
            int fixedLines=lines.Length>1&&lines[1].StartsWith("판매",StringComparison.Ordinal)?2:1;
            var summary=Text("Summary",tooltip,string.Join("\n",lines.Take(fixedLines)),24,new(.055f,.65f),new(.945f,.94f));
            summary.enableAutoSizing=false;summary.fontSize=24*(ReleasePreferences.LargeText?1.2f:1f);
            summary.alignment=TextAlignmentOptions.TopLeft;summary.margin=new Vector4(0,4,0,4);
            // The text fills the content width; only the split action keeps a compact right alignment.
            summary.gameObject.AddComponent<LayoutElement>().flexibleWidth=1;
            var summaryHeight=summary.gameObject.AddComponent<OwnedRecordHeight>();summaryHeight.label=summary;summaryHeight.minimumHeight=0;summaryHeight.verticalPadding=8;summaryHeight.heightFraction=1;
            value=string.Join("\n",lines.Skip(fixedLines));
            // Full descriptions remain readable: overflow grows the scroll content, not smaller glyphs.
            var content=Scroll(tooltip,"TooltipScroll",new Vector2(.045f,.10f),new Vector2(.955f,.62f));
            var scrollRoot=(RectTransform)content.parent.parent;
            UIKeyboardPanRegion.TextScroll(scrollRoot.GetComponent<ScrollRect>());
            var bodyLayout=scrollRoot.gameObject.AddComponent<LayoutElement>();bodyLayout.minHeight=32;bodyLayout.flexibleHeight=1;bodyLayout.flexibleWidth=1;
            scrollRoot.offsetMin=new Vector2(8,4);scrollRoot.offsetMax=new Vector2(-8,-4);
            var layout=content.GetComponent<VerticalLayoutGroup>();layout.childControlHeight=true;
            var label=Text("Info",content,value,24,Vector2.zero,Vector2.one);
            label.enableAutoSizing=false;label.fontSize=24*(ReleasePreferences.LargeText?1.2f:1f);
            label.fontSizeMin=24;label.overflowMode=TextOverflowModes.Overflow;
            label.textWrappingMode=TextWrappingModes.Normal;label.margin=new Vector4(4,4,12,4);
            var trigger=tooltip.GetComponent<UnityEngine.EventSystems.EventTrigger>()??tooltip.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            trigger.triggers.Clear();
            var enter=new UnityEngine.EventSystems.EventTrigger.Entry{eventID=UnityEngine.EventSystems.EventTriggerType.PointerEnter};
            enter.callback.AddListener(_=>{tooltipHovered=true;if(tooltipHide!=null){StopCoroutine(tooltipHide);tooltipHide=null;}});
            var exit=new UnityEngine.EventSystems.EventTrigger.Entry{eventID=UnityEngine.EventSystems.EventTriggerType.PointerExit};
            exit.callback.AddListener(_=>{tooltipHovered=false;HideTooltip();});trigger.triggers.Add(enter);trigger.triggers.Add(exit);
            GameArtSkin.Apply(tooltip);tooltip.SetAsLastSibling();
        }
        public void HideTooltip()
        {
            if(tooltip==null||DetailsSource!=null&&DetailsSource.isActiveAndEnabled)return;
            if(tooltipHide!=null)StopCoroutine(tooltipHide);
            if(!isActiveAndEnabled){tooltip.gameObject.SetActive(false);return;}
            tooltipHide=StartCoroutine(HideTooltipAfterPointerTransfer());
        }
        System.Collections.IEnumerator HideTooltipAfterPointerTransfer()
        {
            // Let the pointer reach the scrollable tooltip without closing it on the source slot's exit.
            yield return new WaitForSecondsRealtime(.12f);
            if(!tooltipHovered&&tooltip!=null)tooltip.gameObject.SetActive(false);
            tooltipHide=null;
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
