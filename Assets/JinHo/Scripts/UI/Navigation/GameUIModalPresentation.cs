using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Presentation uses the existing Activity list/order. No item, pause or game-rule ownership here.
public sealed partial class GameUIController
{
    [SerializeField,Range(0,1)] float modalShadeOpacity=.22f;
    static readonly List<Transform> tutorialTargets=new();
    static Component tutorialOwner;
    static Canvas shadeCanvas;
    static Image modalShade;
    static string presentationSignature;
    internal static long NextPresentationOrder()=>++activityOrder;
    internal static Activity BindPresentation(UIKeyboardSurface surface)
    {
        var a=activities.FirstOrDefault(x=>!x.disposed&&x.surface==surface);
        if(a==null){a=new Activity{owner=surface,surface=surface,order=surface.opened};activities.Add(a);}
        a.close=surface.back;a.priority=surface.priority;SynchronizePresentations();return a;
    }
    internal static UIKeyboardSurface TopPresentation=>activities.Where(a=>!a.disposed&&a.surface!=null&&a.surface.IsUsable)
        .OrderByDescending(a=>a.priority>=31500?1:0).ThenByDescending(a=>a.order).Select(a=>a.surface).FirstOrDefault();
    public static float ModalShadeAlpha=>Instance!=null?Instance.modalShadeOpacity:.22f;
    public static int PresentationOverlayOrder => TopPresentation!=null?TopPresentation.PresentationOrder+5:5000;
    public static void SetTutorialTargets(Component owner,IEnumerable<Transform> targets)
    {
        tutorialOwner=owner;tutorialTargets.Clear();if(targets!=null)tutorialTargets.AddRange(targets.Where(t=>t!=null).Distinct());
        if(modalShade!=null)modalShade.color=new Color(0,0,0,TutorialRestricts&&TopPresentation?.priority<23000?0:ModalShadeAlpha);
    }
    static bool TutorialRestricts=>tutorialOwner!=null&&(tutorialOwner is not Behaviour behaviour||behaviour.isActiveAndEnabled)&&StoryTutorial.Guiding&&tutorialTargets.Count>0;
    public static bool AllowsUI(Transform target)
    {
        if(target==null)return false;
        var top=TopPresentation;
        if(top!=null&&!top.Owns(target))return false;
        if(top!=null&&top.priority>=23000)return true;
        if(!TutorialRestricts)return true;
        return tutorialTargets.Any(t=>t!=null&&t.gameObject.activeInHierarchy&&(target==t||target.IsChildOf(t)));
    }
    internal static bool ShadeBlocks(Vector2 screen,Camera camera)
    {
        if(!TutorialRestricts||TopPresentation?.priority>=23000)return true;
        return !tutorialTargets.OfType<RectTransform>().Any(t=>t!=null&&t.gameObject.activeInHierarchy&&RectTransformUtility.RectangleContainsScreenPoint(t,screen,camera));
    }
    internal static void SynchronizePresentations()
    {
        if(!Application.isPlaying)return;
        var visible=activities.Where(a=>!a.disposed&&a.surface!=null&&a.surface.IsUsable).OrderBy(a=>a.priority>=31500?1:0).ThenBy(a=>a.order).ToArray();
        string signature=string.Join(",",visible.Select(a=>a.order+":"+a.surface.GetInstanceID()));
        if(signature==presentationSignature)return;presentationSignature=signature;
        for(int i=0;i<visible.Length;i++)visible[i].surface.SetPresentationOrder(visible[i].priority>=31500?31500:10000+i*20);
        if(modalShade==null&&visible.Length>0)
        {
            var go=new GameObject("Modal presentation shade",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));DontDestroyOnLoad(go);
            shadeCanvas=go.GetComponent<Canvas>();shadeCanvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var image=RuntimeUIFactory.CreateImage("ModalShade",go.transform);RuntimeUIFactory.StretchToParent(image.rectTransform);modalShade=image;image.raycastTarget=true;
            image.gameObject.AddComponent<ModalShadeFilter>();
        }
        if(modalShade!=null)
        {
            bool show=visible.Length>0;shadeCanvas.gameObject.SetActive(show);
            if(show){shadeCanvas.sortingOrder=visible[^1].surface.PresentationOrder-1;modalShade.color=new Color(0,0,0,TutorialRestricts&&visible[^1].priority<23000?0:ModalShadeAlpha);}
        }
        var selected=EventSystem.current?.currentSelectedGameObject;
        if(selected!=null&&!AllowsUI(selected.transform))EventSystem.current.SetSelectedGameObject(null);
    }
}
public sealed class ModalShadeFilter:MonoBehaviour,ICanvasRaycastFilter
{
    public bool IsRaycastLocationValid(Vector2 point,Camera camera)=>GameUIController.ShadeBlocks(point,camera);
}
public sealed class ModalGraphicFilter:MonoBehaviour,ICanvasRaycastFilter
{
    public bool IsRaycastLocationValid(Vector2 point,Camera camera){var top=GameUIController.TopPresentation;return top!=null&&top.transform.IsChildOf(transform)||GameUIController.AllowsUI(transform);}
}
