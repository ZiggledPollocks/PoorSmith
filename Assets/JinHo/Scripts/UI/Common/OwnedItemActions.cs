using System;
using System.Linq;
using Blacksmith;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Presentation of the existing split callback. Selecting or inspecting never mutates inventory.
public sealed class OwnedItemActions:MonoBehaviour
{
    public BlacksmithView view;
    public Func<int> count;
    public Func<int,bool> commit;
    public Func<string> Description;
    public RectTransform DetailAnchor;
    public Action PreparingDetails;
    public void Preview()
    {
        if(view==null||view.tooltip==null||count==null)return;
        var slot=GetComponent<InventorySlotView>();if(slot==null||!slot.HasItem)return;
        PreparingDetails?.Invoke();
        if(view.TooltipUpdatedFrame!=Time.frameCount||!view.tooltip.gameObject.activeSelf)view.ShowTooltip(Description?.Invoke()??$"{slot.Item.displayName} ×{count()}\n{slot.Item.description}");
        view.DetailsSource=this;
        if(count()>1){
        var scroller=view.tooltip.Find("TooltipScroll") as RectTransform;
        if(scroller!=null){scroller.anchorMin=new(.045f,.33f);scroller.anchorMax=new(.955f,.62f);scroller.offsetMin=new(8,4);scroller.offsetMax=new(-8,-4);}
        var split=view.Button("SplitStack",view.tooltip,"수량 분할",new(.14f,.055f),new(.86f,.30f),()=>OwnedItemControls.Split(view,transform,count,commit));
        var size=split.gameObject.AddComponent<LayoutElement>();size.minHeight=size.preferredHeight=60;size.preferredWidth=240;
        var rule=split.GetComponent<UIElementTemplate>()??split.gameObject.AddComponent<UIElementTemplate>();rule.minimumHeight=0;rule.appearance=UIElementTemplate.Appearance.PrefabOverrides;rule.image=true;rule.imageColor=false;split.image.pixelsPerUnitMultiplier=.5f;((RectTransform)split.transform).offsetMin=((RectTransform)split.transform).offsetMax=Vector2.zero;
        // Clear uses deferred Destroy: a path search can still find the previous inactive SplitStack.
        // Apply presentation to the newly created button reference, never to that stale sibling.
        string detailsGuide=DetailAnchor!=null?"OwnedItemDetails":
            view.GetComponent<BlacksmithController>()?.State==ScreenState.Selecting?"SmithySelectionHUD":null;
        if(detailsGuide!=null){
            var template=Resources.Load<GameObject>("UI/Screens/Layouts/"+detailsGuide);
            var guide=template!=null?template.transform.Find("Tooltip/SplitStack"):null;
            if(guide!=null){
                guide.GetComponent<UIElementTemplate>()?.ApplyTo(split.transform);
                var labelGuide=guide.Find("Label");var label=split.transform.Find("Label");
                if(labelGuide!=null&&label!=null)labelGuide.GetComponent<UIElementTemplate>()?.ApplyTo(label);
            }
        }
        }
        // Campaign UI borrows this view's art helpers but renders on a different existing Canvas.
        var surface=GetComponentsInParent<UIKeyboardSurface>(true).FirstOrDefault(x=>x.IsUsable);
        var canvas=GetComponentsInParent<Canvas>(true).FirstOrDefault(x=>x.isActiveAndEnabled);
        if(surface!=null&&canvas!=null){
            bool smith=surface.screenKey.StartsWith("Smithy/",StringComparison.Ordinal);
            view.tooltip.SetParent(smith?view.hud:canvas.transform,false);
            if(!smith)ScreenLayoutTemplate.Apply("OwnedItemDetails",view.tooltip.parent);
            UIKeyboardNavigation.AttachRoot(surface,view.tooltip);
            // Reuse a screen's existing details region instead of covering its grid with a second panel.
            if(DetailAnchor!=null&&canvas.transform is RectTransform canvasRect){
                // Keep the shared tooltip on the persistent Canvas: rebuilding a screen must not destroy it.
                var corners=new Vector3[4];DetailAnchor.GetWorldCorners(corners);
                Vector2 lo=canvasRect.InverseTransformPoint(corners[0]),hi=canvasRect.InverseTransformPoint(corners[2]);
                view.tooltip.SetParent(canvasRect,false);
                view.tooltip.anchorMin=(lo-canvasRect.rect.min)/canvasRect.rect.size;
                view.tooltip.anchorMax=(hi-canvasRect.rect.min)/canvasRect.rect.size;
                view.tooltip.offsetMin=view.tooltip.offsetMax=Vector2.zero;DetailAnchor.gameObject.SetActive(false);
            }
        }
        // Apply guides after creating dynamic actions, so edited child sizing also survives refreshes.
        UIKeyboardNavigation.MarkDirty(view.tooltip);
    }
    void OnDisable(){if(DetailAnchor!=null)DetailAnchor.gameObject.SetActive(true);if(view!=null&&view.DetailsSource==this){view.DetailsSource=null;view.tooltip.gameObject.SetActive(false);}}
}
