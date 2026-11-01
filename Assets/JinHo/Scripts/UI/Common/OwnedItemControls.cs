using System;
using System.Linq;
using Blacksmith;
using TMPro;
using UnityEngine;

// Only owned-inventory controls and factual journal display; no recipe lookup, hint or mutation on browse.
public static class OwnedItemControls
{
    static readonly string[] SortNames = { "원래순", "이름순", "수량순", "무게순" };
    static readonly string[] GroupNames = { "전체", "채집물", "제작물", "장비" };
    public static void Toolbar(BlacksmithView view, Transform parent, string name, OwnedItemQuery query,
        Action refresh, Vector2 min, Vector2 max)
    {
        var root = view.Rect(name, parent, min, max);
        var search = view.Search(root, _ => {}, new(0,0), new(.46f,1));
        search.onValueChanged.RemoveAllListeners(); search.SetTextWithoutNotify(query.text);
        if (search.placeholder is TMP_Text hint) hint.text = "이름 검색 · Enter";
        search.onEndEdit.AddListener(value => { query.text=value;UIKeyboardNavigation.ResetList(parent);refresh(); });
        view.Button("Sort", root, SortNames[query.sort], new(.48f,0), new(.73f,1),
            () => { query.sort=(query.sort+1)%4;refresh();UIKeyboardNavigation.MarkDirty(parent); });
        view.Button("Filter", root, GroupNames[query.group+1], new(.75f,0), Vector2.one,
            () => { query.group=query.group==2?-1:query.group+1;UIKeyboardNavigation.ResetList(parent);refresh(); });
        ScreenLayoutTemplate.Apply("OwnedItemTools",root);
        // Button captions are always fully visible; their existing container owns sizing.
        foreach(var button in root.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            foreach(var label in button.GetComponentsInChildren<TMP_Text>(true)){label.overflowMode=TextOverflowModes.Overflow;label.textWrappingMode=TextWrappingModes.Normal;}
    }
    static Transform CanvasParent(Transform parent) => parent.GetComponentInParent<Canvas>()?.transform ?? parent;
    static RectTransform ModalRoot(BlacksmithView view, Transform parent, string name)
    {
        var root=view.Panel(name,CanvasParent(parent),Vector2.zero,Vector2.one);
        root.GetComponent<UnityEngine.UI.Image>().color=new Color(0,0,0,.72f);
        RuntimeUIFactory.RaiseModal(root);return root;
    }
    public static void SplitButton(BlacksmithView view, Transform slot, Func<int> count, Func<int,bool> commit,Func<string> description=null)
    {
        // Store the existing split transaction on the slot; render its action in the details, never over item art.
        var action=slot.GetComponent<OwnedItemActions>()??slot.gameObject.AddComponent<OwnedItemActions>();
        action.view=view;action.count=count;action.commit=commit;action.Description=description;
    }
    public static void Split(BlacksmithView view,Transform parent,Func<int> count,Func<int,bool> commit)
        => Quantity(view,parent,"수량 분할",count,1,commit,true);
    // The same dialog can stage a keyboard quantity without changing any inventory until its owner commits.
    public static void Quantity(BlacksmithView view,Transform parent,string title,Func<int> count,int initial,Func<int,bool> commit,bool split=false)
    {
        var root=ModalRoot(view,parent,"OwnedQuantityDialog");var modal=OwnedItemModal.Attach(root.gameObject);modal.source=parent;modal.owner=view;
        var panel=view.Panel("Panel",root,new(.28f,.28f),new(.72f,.74f));
        view.Text("Title",panel,title,32,new(.06f,.80f),new(.94f,.96f));
        var info=view.Text("Info",panel,$"보유 {count()}개 · {(split?"분할할":"이동할")} 수량을 입력하세요.",26,new(.06f,.58f),new(.94f,.78f));
        var amount=view.Search(panel,_=>{},new(.15f,.35f),new(.85f,.55f));amount.contentType=TMP_InputField.ContentType.IntegerNumber;amount.SetTextWithoutNotify(Mathf.Max(1,initial).ToString());
        if(amount.placeholder is TMP_Text hint)hint.text=split?"1 이상, 보유 수량 미만":"1 이상, 보유 수량 이하";
        view.Button("Cancel",panel,"취소",new(.08f,.07f),new(.46f,.25f),modal.Close);
        view.Button("Confirm",panel,split?"분할":"확인",new(.54f,.07f),new(.92f,.25f),()=>{
            if(!int.TryParse(amount.text,out int n)||n<=0||n>(split?count()-1:count())){info.text=split?"보유 수량보다 작은 정수를 입력하세요.":"보유 수량 이하의 정수를 입력하세요.";return;}
            if(!commit(n)){info.text="물품 상태가 바뀌었습니다. 다시 선택하세요.";return;}
            modal.Close();
        });
        ScreenLayoutTemplate.Apply("OwnedQuantityDialog",root);
        UIKeyboardSurface.Bind(modal,root,"OwnedQuantityDialog",modal.Close,24000);
    }
    static string Items(System.Collections.Generic.IEnumerable<Blacksmith.Stack> stacks, BlacksmithCatalog catalog) =>
        string.Join(", ",(stacks??System.Array.Empty<Blacksmith.Stack>()).Where(x=>x!=null).Select(x=>(catalog?.Item(x.itemId)?.displayName??x.itemId)+" ×"+x.count+
            (QualityRules.AppliesTo(catalog?.Item(x.itemId))?" ("+QualityRules.Name(x.quality)+")":"")));
    public static void Journal(BlacksmithView view,Transform parent,SaveData data,BlacksmithCatalog catalog)
    {
        var root=ModalRoot(view,parent,"ExperimentJournal");var modal=OwnedItemModal.Attach(root.gameObject);modal.source=parent;modal.owner=view;
        var panel=view.Panel("Panel",root,new(.12f,.10f),new(.88f,.90f));
        view.Text("Title",panel,"실험 기록 · 최근 200건",29,new(.04f,.88f),new(.80f,.97f));
        view.Button("Close",panel,"닫기",new(.83f,.88f),new(.97f,.97f),modal.Close);
        var content=view.Scroll(panel,"Records",new(.035f,.04f),new(.965f,.86f));
        UIKeyboardPanRegion.TextScroll(content.parent.parent.GetComponent<UnityEngine.UI.ScrollRect>());
        var records=data.experiments;
        if(records==null||records.Count==0)view.Text("Empty",panel,"기록된 실험이 없습니다.",24,new(.10f,.4f),new(.9f,.6f));
        if(records!=null)foreach(var entry in records.Where(x=>x!=null))
        {
            string stamp=entry.lastUtcTicks>0&&entry.lastUtcTicks<=DateTime.MaxValue.Ticks?new DateTime(entry.lastUtcTicks,DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"):"시각 미확인";
            string station=new[]{"작업대","도구 걸이","모루","담금질","용광로"}[Mathf.Clamp((int)entry.station,0,4)];
            string tool=entry.station==Station.Tools?" · "+new[]{"칼","대패","톱","숫돌","망치"}[Mathf.Clamp((int)entry.tool,0,4)]:"";
            string text=$"{stamp} · {entry.repetitions}회 · {entry.outcome}\n{station}{tool} · 투입: {Items(entry.inputs,catalog)}\n조작: {string.Join(" → ",entry.actions??new System.Collections.Generic.List<string>())}\n결과물: {Items(entry.outputs,catalog)}\n반환: {Items(entry.returned,catalog)}";
            if(entry.fuelSpent>0)text+=$"\n사용 연료: {entry.fuelSpent}";
            var row=view.Panel("Record",content,Vector2.zero,Vector2.one);
            row.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight=Mathf.Max(190,100+(entry.actions?.Count??0)*14);
            var label=view.Text("Body",row,text,21,new(.035f,.04f),new(.965f,.96f));
            label.overflowMode=TextOverflowModes.Overflow;
            var fit=row.gameObject.AddComponent<OwnedRecordHeight>();fit.label=label;
        }
        ScreenLayoutTemplate.Apply("ExperimentJournal",root);
    }
}
public sealed class OwnedRecordHeight : MonoBehaviour
{
    public TMP_Text label;
    public float minimumHeight=170,verticalPadding=32,heightFraction=.92f;
    float measuredWidth=-1,measuredFont=-1;
    string measuredText;
    void LateUpdate()
    {
        if(label==null)return;
        float width=label.rectTransform.rect.width;
        if(width<=0 || Mathf.Approximately(width,measuredWidth)&&Mathf.Approximately(label.fontSize,measuredFont)&&measuredText==label.text)return;
        measuredWidth=width;measuredText=label.text;measuredFont=label.fontSize;
        // Account for the label's vertical inset; long records remain scrollable with no clipped last line.
        GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight=
            Mathf.Max(minimumHeight,(label.GetPreferredValues(label.text,width,0).y+verticalPadding)/Mathf.Max(.01f,heightFraction));
    }
}
