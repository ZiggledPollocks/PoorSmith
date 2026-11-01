using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Blacksmith;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Routes input only; all commits remain in existing button/drop/service callbacks.
public static class UIKeyboardNavigation
{
    internal sealed class Node
    {
        internal Component control;internal RectTransform rect;internal Selectable selectable;
        internal InventorySlotView slot;internal ItemDropTarget target;internal UIKeyboardPanRegion pan;
        internal string identity;internal Vector2 Center=>rect.TransformPoint(rect.rect.center);
        internal bool Usable=>control!=null&&GameUIController.AllowsUI(control.transform)&&control.gameObject.activeInHierarchy&&(selectable==null||selectable.isActiveAndEnabled&&selectable.IsInteractable())&&
            control.GetComponentsInParent<CanvasGroup>().All(x=>x.alpha>.02f&&x.interactable)&&
            (control.GetComponent<UIKeyboardButtonPreview>()?.CanFocus?.Invoke()!=false)&&(slot==null||slot.HasItem)&&(pan==null||pan.CanPan)&&(target==null||slot!=null||UIKeyboardMove.Active||target.ClickAvailable?.Invoke()==true);
    }
    sealed class Memory { internal string identity;internal Vector2 position;internal string group; }
    static readonly List<UIKeyboardSurface> surfaces=new();
    static readonly Dictionary<int,List<Node>> nodes=new();
    static readonly Dictionary<string,Memory> memory=new();
    static UIKeyboardSurface active;
    static Node selected;
    static EventSystem system;
    static bool priorNavigation;
    static int processed=-1, consumed=-1, editingEnded=-1;
    static long order;
    static TMP_InputField editing;
    static TMP_Dropdown dropdown;
    static UIKeyboardSurface dropdownOwner;
    static int dropdownOriginal,dropdownIndex;
    static bool keyboardMode,escapeHeldConsumed;
    public static bool EscapeConsumedWhileHeld=>escapeHeldConsumed&&Keyboard.current?.escapeKey.isPressed==true;
    public static void ConsumeEscape(){escapeHeldConsumed=Keyboard.current?.escapeKey.isPressed==true;Consume();}
    static void RefreshEscape(){if(Keyboard.current?.escapeKey.isPressed!=true)escapeHeldConsumed=false;}
    public static bool ConsumedThisFrame=>consumed==Time.frameCount;
    static TMP_InputField SelectedInput(){var events=EventSystem.current;if(events==null)return null;var go=events.currentSelectedGameObject;return go!=null?go.GetComponentInParent<TMP_InputField>():null;}
    public static bool TextOwnsInput=>editing!=null&&editing.isFocused||editingEnded==Time.frameCount||SelectedInput()?.isFocused==true;
    public static bool HideToolHud=>surfaces.Any(s=>s!=null&&s.IsUsable&&s.hideToolHud);
    public static UIKeyboardSurface Active=>active;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset(){surfaces.Clear();nodes.Clear();memory.Clear();active=null;selected=null;system=null;editing=null;dropdown=null;processed=consumed=editingEnded=-1;order=0;keyboardMode=false;escapeHeldConsumed=false;UIKeyboardMove.Reset();}
    internal static long NextOrder()=>GameUIController.NextPresentationOrder();
    internal static void Register(UIKeyboardSurface s){if(s!=null&&!surfaces.Contains(s))surfaces.Add(s);}
    internal static void Unregister(UIKeyboardSurface s){if(s==null)return;surfaces.Remove(s);nodes.Remove(s.GetInstanceID());if(active==s){Remember();UIKeyboardMove.CancelIfOwned(s);active=null;selected=null;}if(surfaces.Count==0)RestoreNavigation();}
    public static void MarkDirty(Transform t){if(t==null)return;foreach(var s in surfaces)if(s!=null&&s.Owns(t))s.dirty=true;}
    static void OwnNavigation(){var next=EventSystem.current;if(next==null)return;if(system!=next){RestoreNavigation();system=next;priorNavigation=system.sendNavigationEvents;}system.sendNavigationEvents=false;}
    static void RestoreNavigation(){if(system!=null)system.sendNavigationEvents=priorNavigation;system=null;}
    internal static void AttachRoot(UIKeyboardSurface owner,Transform root){if(owner.attachedRoots.Contains(root))return;foreach(var old in surfaces)if(old!=null&&old!=owner&&old.attachedRoots.Contains(root)){old.attachedRoots.Remove(root);old.Refresh();}if(!owner.attachedRoots.Contains(root))owner.attachedRoots.Add(root);owner.Refresh();}
    static bool Visible(Component c)=>c.gameObject.activeInHierarchy&&c.GetComponentsInParent<CanvasGroup>().All(x=>x.alpha>.02f);
    static string Path(Transform t,Transform root){string p=t.name;while(t.parent!=null&&t.parent!=root){t=t.parent;p=t.name+"/"+p;}return p;}
    static string Identity(Component c,InventorySlotView slot,Transform root)=>Path(c.transform.parent??root,root)+"/"+(slot?.Stack!=null?slot.Stack.Key+"@"+RuntimeHelpers.GetHashCode(slot.Stack):c.name);
    static List<Node> Build(UIKeyboardSurface s)
    {
        var list=new List<Node>();var seen=new HashSet<int>();var roots=new[]{s.transform}.Concat(s.attachedRoots).Where(x=>x!=null).Distinct().ToArray();
        void Add(Component c,Selectable selectable=null,InventorySlotView slot=null,ItemDropTarget target=null,UIKeyboardPanRegion pan=null){if(c==null||c.transform is not RectTransform r||!seen.Add(c.gameObject.GetInstanceID()))return;var n=new Node{control=c,rect=r,selectable=selectable,slot=slot,target=target,pan=pan,identity=Identity(c,slot,s.transform)};list.Add(n);var pointer=c.GetComponent<UIKeyboardPointer>()??c.gameObject.AddComponent<UIKeyboardPointer>();pointer.control=c;
            if(c is TMP_InputField input){var owner=input.GetComponent<UIKeyboardTextOwner>()??input.gameObject.AddComponent<UIKeyboardTextOwner>();owner.Bind(input);}}
        foreach(var slot in roots.SelectMany(root=>root.GetComponentsInChildren<InventorySlotView>(true)))Add(slot,slot.GetComponent<Selectable>(),slot,slot.GetComponent<ItemDropTarget>());
        foreach(var control in roots.SelectMany(root=>root.GetComponentsInChildren<Selectable>(true)))if(control is not Scrollbar)Add(control,control,target:control.GetComponent<ItemDropTarget>());
        foreach(var pan in roots.SelectMany(root=>root.GetComponentsInChildren<UIKeyboardPanRegion>(true)))Add(pan,pan:pan);
        foreach(var target in roots.SelectMany(root=>root.GetComponentsInChildren<ItemDropTarget>(true)))Add(target,target:target);
        nodes[s.GetInstanceID()]=list;s.dirty=false;return list;
    }
    static UIKeyboardSurface Top()=>GameUIController.TopPresentation;
    static List<Node> Items()=>active==null?new List<Node>():!nodes.TryGetValue(active.GetInstanceID(),out var found)||active.dirty?Build(active):found;
    static void Remember(){if(active==null||selected==null||selected.control==null)return;memory[active.screenKey]=new Memory{identity=selected.identity,position=selected.Center,group=Path(selected.rect.parent,active.transform)};}
    static void Select(Node node)
    {
        if(node==null||!node.Usable)return;
        if(selected?.control!=null&&selected!=node){selected.slot?.PreviewKeyboard(false);UIKeyboardBorder.Show(selected.rect,false);}
        bool changed=selected?.control!=node.control;selected=node;
        if(EventSystem.current?.currentSelectedGameObject!=node.control.gameObject)EventSystem.current?.SetSelectedGameObject(node.control.gameObject);
        if(changed){node.slot?.PreviewKeyboard(true);node.control.GetComponent<UIKeyboardButtonPreview>()?.Focused?.Invoke();}UIKeyboardBorder.Show(node.rect,true,UIKeyboardMove.Active&&UIKeyboardMove.Owner==active&&(node.target!=null||node.slot!=null)&&node.slot!=UIKeyboardMove.Source?(CanReceive(node)?"✓":"×"):"◆");
        EnsureVisible(node.rect);Remember();UIKeyboardMove.Preview(node.target,node.slot);UpdateFooter();
    }
    static bool CanReceive(Node n)=>!UIKeyboardMove.Active||n.target!=null&&n.target.CanPreview(UIKeyboardMove.Source)||n.slot!=null&&n.slot.CanKeyboardDrop(UIKeyboardMove.Source);
    static void RestoreSelection(List<Node> list)
    {
        var usable=list.Where(n=>n.Usable).ToList();if(usable.Count==0){selected=null;return;}
        Node next=null;
        if(memory.TryGetValue(active.screenKey,out var state))next=usable.FirstOrDefault(n=>n.identity==state.identity)??usable.Where(n=>Path(n.rect.parent,active.transform)==state.group).OrderBy(n=>(n.Center-state.position).sqrMagnitude).FirstOrDefault();
        if(active.priority>=23000||active.screenKey.IndexOf("Confirm",StringComparison.OrdinalIgnoreCase)>=0)next??=usable.FirstOrDefault(n=>n.control.name.IndexOf("Cancel",StringComparison.OrdinalIgnoreCase)>=0||n.control.name is "No" or "noButton");
        next??=usable.FirstOrDefault(n=>n.slot!=null)??usable.FirstOrDefault(n=>n.selectable is Button)??usable[0];Select(next);
    }
    public static void Tick()
    {
        RefreshEscape();if(processed==Time.frameCount)return;
        // The authored title keeps its own screen transition/focus restoration, using TickTitle below.
        if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="TitleScene")return;
        surfaces.RemoveAll(s=>s==null);var top=Top();
        if(top==null){if(active!=null){Remember();active.GetComponent<UIKeyboardFooter>()?.Show(false,null);UIKeyboardMove.Cancel();active=null;selected=null;}RestoreNavigation();return;}
        Process(top,false);
    }
    public static void TickTitle(Transform scope)
    {
        RefreshEscape();if(scope==null||processed==Time.frameCount)return;
        var component=scope.GetComponent<UIKeyboardSurface>()??scope.gameObject.AddComponent<UIKeyboardSurface>();component.owner=scope.GetComponentInParent<TitleSceneController>();component.screenKey="Title/"+scope.name;component.priority=2000;
        Process(component,true);
    }
    static void Process(UIKeyboardSurface surface,bool allowTab)
    {
        processed=Time.frameCount;OwnNavigation();
        if(active!=surface){Remember();if(active!=null)active.GetComponent<UIKeyboardFooter>()?.Show(false,null);active=surface;selected=null;surface.dirty=true;}
        if(Time.frameCount%15==0)active.dirty=true;
        var list=Items();UIKeyboardMove.Validate();
        var earlyKeys=Keyboard.current;
        if(dropdown!=null&&dropdown.IsExpanded&&dropdownOwner==active&&earlyKeys!=null){HandleDropdown(earlyKeys);return;}
        var current=EventSystem.current?.currentSelectedGameObject;
        var actual=current!=null?list.FirstOrDefault(n=>n.control!=null&&n.control.gameObject==current&&n.Usable):null;
        if(actual!=null&&selected!=actual)Select(actual);
        if(selected==null||!selected.Usable||!list.Contains(selected))RestoreSelection(list);
        var k=Keyboard.current;if(k==null)return;
        if(active.owner is SettingsMenuUI.UIManager manager&&manager.CapturesKeyInput){if(k.escapeKey.wasPressedThisFrame&&!EscapeConsumedWhileHeld){ConsumeEscape();active.back?.Invoke();}return;}
        if(editing!=null&&!editing.isFocused)editing=null;
        if(TextOwnsInput&&(editing==null||active.Owns(editing.transform))){if(k.escapeKey.wasPressedThisFrame)TryHandleEscape();return;}
        if(ConsumedThisFrame)return;
        if(k.escapeKey.wasPressedThisFrame){if(TryHandleEscape())return;GameUIController.HandleEscape();return;}
        if(selected?.selectable is TMP_InputField input&&(k.enterKey.wasPressedThisFrame||k.numpadEnterKey.wasPressedThisFrame)){editing=input;input.ActivateInputField();Consume();return;}
        if(dropdown!=null&&dropdown.IsExpanded){HandleDropdown(k);return;}
        if(selected?.selectable is TMP_Dropdown expanded&&expanded.IsExpanded){dropdown=expanded;dropdownOwner=active;dropdownOriginal=dropdownIndex=expanded.value;HandleDropdown(k);return;}
        if(selected!=null&&(k.enterKey.wasPressedThisFrame||k.numpadEnterKey.wasPressedThisFrame)){keyboardMode=true;Submit(selected);Consume();return;}
        var dir=k.leftArrowKey.wasPressedThisFrame?Vector2.left:k.rightArrowKey.wasPressedThisFrame?Vector2.right:k.upArrowKey.wasPressedThisFrame?Vector2.up:k.downArrowKey.wasPressedThisFrame?Vector2.down:Vector2.zero;
        if(dir!=Vector2.zero){keyboardMode=true;if(selected?.pan!=null&&selected.pan.OwnsPan){selected.pan.Pan(dir);Consume();return;}
            if(selected?.selectable is Slider slider&&Mathf.Abs(dir.x)>0){slider.value+=dir.x*(slider.wholeNumbers?1f:(slider.maxValue-slider.minValue)/20f);Consume();return;}
            Move(dir,list);Consume();return;}
        if(k.pageDownKey.wasPressedThisFrame||k.pageUpKey.wasPressedThisFrame){var sr=selected?.control?.GetComponentInParent<ScrollRect>()??active.GetComponentInChildren<ScrollRect>();if(sr!=null)sr.verticalNormalizedPosition=Mathf.Clamp01(sr.verticalNormalizedPosition+(k.pageUpKey.wasPressedThisFrame?.5f:-.5f));Consume();return;}
        if(allowTab&&k.tabKey.wasPressedThisFrame){var available=list.Where(n=>n.Usable).OrderByDescending(n=>n.Center.y).ThenBy(n=>n.Center.x).ToList();int at=available.IndexOf(selected),step=k.shiftKey.isPressed?-1:1;if(available.Count>0)Select(available[(at+step+available.Count)%available.Count]);Consume();}
    }
    static void HandleDropdown(Keyboard k)
    {
        if(dropdown==null)return;
        if(k.escapeKey.wasPressedThisFrame){TryHandleEscape();return;}
        if(k.upArrowKey.wasPressedThisFrame)dropdownIndex=Mathf.Max(0,dropdownIndex-1);
        if(k.downArrowKey.wasPressedThisFrame)dropdownIndex=Mathf.Min(dropdown.options.Count-1,dropdownIndex+1);
        var list=dropdown.transform.Find("Dropdown List");if(list!=null){var options=list.GetComponentsInChildren<Toggle>();if(dropdownIndex<options.Length)EventSystem.current?.SetSelectedGameObject(options[dropdownIndex].gameObject);}
        if(k.upArrowKey.wasPressedThisFrame||k.downArrowKey.wasPressedThisFrame)Consume();
        if(k.enterKey.wasPressedThisFrame||k.numpadEnterKey.wasPressedThisFrame){var confirmed=dropdown;dropdown=null;confirmed.value=dropdownIndex;confirmed.Hide();Consume();}
    }
    static void Move(Vector2 direction,List<Node> list)
    {
        if(selected==null){RestoreSelection(list);return;}var origin=selected.Center;
        var next=list.Where(n=>n!=selected&&n.Usable).Select(n=>new{node=n,delta=n.Center-origin}).Where(x=>Vector2.Dot(x.delta,direction)>2f)
            .OrderBy(x=>Vector2.Dot(x.delta,direction)+Mathf.Abs(direction.x*x.delta.y-direction.y*x.delta.x)*3.5f).FirstOrDefault();
        if(next!=null)Select(next.node); // No opposite-edge wrapping.
    }
    static void Submit(Node n)
    {
        if(!n.Usable)return;
        if(UIKeyboardMove.Active&&UIKeyboardMove.Owner==active){if(n.control.name=="Quantity"){UIKeyboardMove.ChooseQuantity();return;}if(n.target!=null||n.slot!=null){if(!UIKeyboardMove.Commit(n.target,n.slot)&&active!=null)UIKeyboardFooter.For(active).Rejected();return;}UIKeyboardMove.Cancel();}
        if(n.slot!=null){if(n.slot.HasDragAction)UIKeyboardMove.Begin(active,n.slot);else n.slot.KeyboardClick();return;}
        if(n.pan!=null){n.pan.OwnsPan=!n.pan.OwnsPan;return;}
        if(n.selectable is Button b){if(b.GetComponentInParent<StoryTutorialView>()==null&&!StoryTutorial.CanUseButton(b.name)){StoryTutorial.Reject();return;}ExecuteEvents.Execute(b.gameObject,new BaseEventData(EventSystem.current),ExecuteEvents.submitHandler);return;}
        if(n.selectable!=null&&!StoryTutorial.CanUseButton(n.control.name)){StoryTutorial.Reject();return;}
        if(n.selectable is Toggle toggle){toggle.isOn=!toggle.isOn;return;}
        if(n.selectable is TMP_Dropdown drop){dropdown=drop;dropdownOwner=active;dropdownOriginal=dropdownIndex=drop.value;drop.Show();return;}
        if(n.selectable!=null)ExecuteEvents.Execute(n.control.gameObject,new BaseEventData(EventSystem.current),ExecuteEvents.submitHandler);
    }
    public static bool TryHandleEscape()
    {
        if(ConsumedThisFrame||EscapeConsumedWhileHeld)return true;
        var top=UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="TitleScene"?active:Top();if(top!=null&&top!=active){Remember();active=top;selected=null;}
        var field=editing!=null?editing:SelectedInput();
        if(field!=null&&field.isFocused&&(active==null||active.Owns(field.transform))){field.DeactivateInputField();editing=null;editingEnded=Time.frameCount;ConsumeEscape();return true;}
        if(dropdown!=null&&dropdown.IsExpanded&&dropdownOwner==active){dropdown.SetValueWithoutNotify(dropdownOriginal);dropdown.Hide();dropdown=null;ConsumeEscape();return true;}
        if(selected?.pan?.OwnsPan==true){selected.pan.OwnsPan=false;ConsumeEscape();return true;}
        // Covered parent moves never intercept a child's Esc.
        if(UIKeyboardMove.Active&&UIKeyboardMove.Owner==active){UIKeyboardMove.Cancel();ConsumeEscape();return true;}
        return false;
    }
    internal static void EditingStarted(TMP_InputField input){editing=input;}
    internal static void EditingFinished(TMP_InputField input){if(editing==input)editing=null;editingEnded=Time.frameCount;if(Keyboard.current?.escapeKey.isPressed==true)ConsumeEscape();else Consume();}
    internal static void Consume(){consumed=Time.frameCount;}
    internal static void Pointer(Component control,PointerEventData e,bool down)
    {
        if(control==null||active==null||!active.Owns(control.transform))return;
        if(!down&&e.delta.sqrMagnitude<16f)return;
        if(down&&!(control.name=="Quantity"&&UIKeyboardMove.Owner==active))UIKeyboardMove.Cancel();keyboardMode=false;
        var n=Items().FirstOrDefault(x=>x.control!=null&&x.control.gameObject==control.gameObject);if(n!=null&&n.Usable)Select(n);
    }
    public static void ResetList(Transform root){UIKeyboardMove.Cancel();if(active==null)return;memory.Remove(active.screenKey);active.dirty=true;selected=null;if(!TextOwnsInput)EventSystem.current?.SetSelectedGameObject(null);}
    public static void EnsureVisible(RectTransform target)
    {
        foreach(var scroll in target.GetComponentsInParent<ScrollRect>()){
            if(scroll.content==null||scroll.viewport==null||!target.IsChildOf(scroll.content))continue;Canvas.ForceUpdateCanvases();var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport,target);var view=scroll.viewport.rect;Vector2 correction=Vector2.zero;
            if(scroll.vertical){if(bounds.max.y>view.yMax)correction.y=view.yMax-bounds.max.y;else if(bounds.min.y<view.yMin)correction.y=view.yMin-bounds.min.y;}
            if(scroll.horizontal){if(bounds.min.x<view.xMin)correction.x=view.xMin-bounds.min.x;else if(bounds.max.x>view.xMax)correction.x=view.xMax-bounds.max.x;}
            if(correction!=Vector2.zero){var world=scroll.viewport.TransformVector(correction);var local=scroll.content.parent.InverseTransformVector(world);scroll.content.anchoredPosition+=(Vector2)local;scroll.StopMovement();}
        }
    }
    static void UpdateFooter(){if(active!=null)UIKeyboardFooter.For(active).Show(keyboardMode,selected?.slot,selected?.pan,UIKeyboardMove.Active&&UIKeyboardMove.Owner==active&&selected!=null&&(selected.target!=null||selected.slot!=null)&&selected.slot!=UIKeyboardMove.Source?CanReceive(selected):(bool?)null);}
}
public sealed class UIKeyboardPointer:MonoBehaviour,IPointerEnterHandler,IPointerDownHandler
{
    internal Component control;
    public void OnPointerEnter(PointerEventData e)=>UIKeyboardNavigation.Pointer(control,e,false);
    public void OnPointerDown(PointerEventData e)=>UIKeyboardNavigation.Pointer(control,e,true);
}
public sealed class UIKeyboardTextOwner:MonoBehaviour
{
    TMP_InputField field;
    public void Bind(TMP_InputField next){if(field==next)return;if(field!=null){field.onSelect.RemoveListener(Selected);field.onEndEdit.RemoveListener(Ended);}field=next;field.onSelect.AddListener(Selected);field.onEndEdit.AddListener(Ended);}
    void Selected(string _) => UIKeyboardNavigation.EditingStarted(field);
    void Ended(string _) => UIKeyboardNavigation.EditingFinished(field);
    void OnDestroy(){if(field!=null){field.onSelect.RemoveListener(Selected);field.onEndEdit.RemoveListener(Ended);}}
}

public sealed class UIKeyboardButtonPreview:MonoBehaviour { public Action Focused;public Func<bool> CanFocus; }
