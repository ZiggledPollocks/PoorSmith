using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Blacksmith
{
    public class InventorySlotView : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IPointerClickHandler,IPointerDownHandler,IBeginDragHandler,IDragHandler,IEndDragHandler,IDropHandler
    {
        public Image background,icon;public TMP_Text count;
        public Stack Stack {get;private set;} public ItemDefinition Item {get;private set;}
        public bool LongPress {get;private set;} public static InventorySlotView Dragging;
        Action click,right;Action<InventorySlotView> drop;Action<bool> hover;float down;
        public void Bind(Stack s,ItemDefinition item,Sprite art,Sprite frame,TMP_FontAsset font,Action left,Action rightClick,Action<InventorySlotView> onDrop,Action<bool> onHover)
        {Stack=s;Item=item;background.sprite=frame;icon.sprite=art;count.font=font;count.text=s.count.ToString();click=left;right=rightClick;drop=onDrop;hover=onHover;}
        public void OnPointerEnter(PointerEventData e){background.color=new Color(1,.84f,.45f);hover?.Invoke(true);}
        public void OnPointerExit(PointerEventData e){background.color=Color.white;hover?.Invoke(false);}
        public void OnPointerDown(PointerEventData e){down=Time.unscaledTime;}
        public void OnPointerClick(PointerEventData e){LongPress=Time.unscaledTime-down>=.45f;if(e.button==PointerEventData.InputButton.Right)right?.Invoke();else click?.Invoke();}
        public void OnBeginDrag(PointerEventData e){Dragging=this;icon.color=new Color(1,1,1,.45f);}
        public void OnDrag(PointerEventData e){}
        public void OnEndDrag(PointerEventData e){Dragging=null;icon.color=Color.white;}
        public void OnDrop(PointerEventData e){if(Dragging!=null&&Dragging!=this)drop?.Invoke(Dragging);}
    }
}
