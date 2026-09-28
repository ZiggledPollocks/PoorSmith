using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class UiHoverOutline : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler
{
    Outline outline;
    void Awake(){outline=GetComponent<Outline>()??gameObject.AddComponent<Outline>();outline.effectColor=Color.white;outline.effectDistance=new Vector2(2,-2);outline.enabled=false;}
    public void OnPointerEnter(PointerEventData e){outline.enabled=true;}
    public void OnPointerExit(PointerEventData e){outline.enabled=false;}
    void OnDisable(){if(outline!=null)outline.enabled=false;}
}
