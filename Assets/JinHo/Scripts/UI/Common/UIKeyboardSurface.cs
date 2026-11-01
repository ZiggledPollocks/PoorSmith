using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Screen-local input boundary, registered by the existing screen owner. It owns no game state.
[DefaultExecutionOrder(-500)]
[DisallowMultipleComponent]
public sealed class UIKeyboardSurface : MonoBehaviour
{
    internal Component owner;
    internal string screenKey;
    internal int priority;
    internal long opened;
    internal Action back;
    internal bool dirty=true;
    internal bool hideToolHud;
    GameUIController.Activity presentation;
    Canvas presentationCanvas;bool priorOverride;int priorOrder;
    internal int PresentationOrder {get;private set;}
    internal void SetPresentationOrder(int order)
    {
        if(presentationCanvas==null){presentationCanvas=GetComponent<Canvas>();if(presentationCanvas==null)presentationCanvas=gameObject.AddComponent<Canvas>();priorOverride=presentationCanvas.overrideSorting;priorOrder=presentationCanvas.sortingOrder;if(GetComponent<UnityEngine.UI.GraphicRaycaster>()==null)gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();}
        presentationCanvas.overrideSorting=true;presentationCanvas.sortingOrder=PresentationOrder=order;
    }
    void BindScope(){if(IsUsable)presentation=GameUIController.BindPresentation(this);}
    void ReleaseScope(){presentation?.Dispose();presentation=null;if(presentationCanvas!=null){presentationCanvas.overrideSorting=priorOverride;presentationCanvas.sortingOrder=priorOrder;}}
    void GuardGraphics(){foreach(var graphic in GetComponentsInChildren<UnityEngine.UI.Graphic>(true)){
        if(graphic.GetComponent<ModalGraphicFilter>()==null)graphic.gameObject.AddComponent<ModalGraphicFilter>();
        if(graphic is UnityEngine.UI.Image image&&(image.transform==transform||image.name=="InputShade"||image.name=="Dim")&&image.color.r<.10f&&image.color.g<.10f&&image.color.b<.10f){var rect=image.rectTransform;if(rect.anchorMax.x-rect.anchorMin.x>.95f&&rect.anchorMax.y-rect.anchorMin.y>.95f)image.color=Color.clear;}
    }}
    internal readonly List<Transform> attachedRoots=new();
    public bool Owns(Transform child)=>child!=null&&(child==transform||child.IsChildOf(transform)||attachedRoots.Any(root=>root!=null&&(child==root||child.IsChildOf(root))));
    public bool IsUsable => isActiveAndEnabled&&gameObject.activeInHierarchy&&owner!=null&&
        GetComponentsInParent<Canvas>(true).All(canvas=>canvas.isActiveAndEnabled);
    public static UIKeyboardSurface Bind(Component owner,Transform root,string key,Action back=null,int priority=500,bool hideToolHud=true,long openingOrder=0)
    {
        if(root==null)return null;
        var surface=root.GetComponent<UIKeyboardSurface>()??root.gameObject.AddComponent<UIKeyboardSurface>();
        bool changed=surface.screenKey!=key||surface.owner!=owner;
        surface.owner=owner;surface.back=back;surface.priority=priority;surface.hideToolHud=hideToolHud;
        if(changed){surface.screenKey=key;surface.dirty=true;}
        if(openingOrder>0)surface.opened=openingOrder;
        UIKeyboardNavigation.Register(surface);surface.BindScope();return surface;
    }
    public void Refresh(){dirty=true;GuardGraphics();}
    void OnEnable(){opened=GameUIController.NextPresentationOrder();UIKeyboardNavigation.Register(this);BindScope();GuardGraphics();}
    void OnDisable(){ReleaseScope();var footer=GetComponent<UIKeyboardFooter>();if(footer!=null)footer.Show(false,null);UIKeyboardNavigation.Unregister(this);}
    void OnDestroy(){ReleaseScope();UIKeyboardNavigation.Unregister(this);}
    void OnTransformChildrenChanged(){dirty=true;GuardGraphics();}
    void Update()=>UIKeyboardNavigation.Tick();
}
