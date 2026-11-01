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
        search.onEndEdit.AddListener(value => { query.text=value;refresh(); });
        view.Button("Sort", root, SortNames[query.sort], new(.48f,0), new(.73f,1),
            () => { query.sort=(query.sort+1)%4;refresh(); });
        view.Button("Filter", root, GroupNames[query.group+1], new(.75f,0), Vector2.one,
            () => { query.group=query.group==2?-1:query.group+1;refresh(); });
        ScreenLayoutTemplate.Apply("OwnedItemTools",root);
    }
    static Transform CanvasParent(Transform parent) => parent.GetComponentInParent<Canvas>()?.transform ?? parent;
    static RectTransform ModalRoot(BlacksmithView view, Transform parent, string name)
    {
        var root=view.Panel(name,CanvasParent(parent),Vector2.zero,Vector2.one);
        root.GetComponent<UnityEngine.UI.Image>().color=new Color(0,0,0,.72f);
        root.SetAsLastSibling();return root;
    }
    public static void SplitButton(BlacksmithView view, Transform slot, Func<int> count, Func<int,bool> commit)
    {
        if(count()<=1)return;
        var button=view.Button("SplitStack",slot,"분할",new(.48f,.74f),new(.98f,.98f),
            ()=>Split(view,slot,count,commit));
        // Compact action inside a slot: do not inherit the full-sized screen button minimum height.
        var rect=(RectTransform)button.transform;rect.anchorMin=new(.43f,.70f);rect.anchorMax=new(.98f,.98f);
        rect.offsetMin=rect.offsetMax=Vector2.zero;
        var style=button.GetComponent<UIElementTemplate>()??button.gameObject.AddComponent<UIElementTemplate>();style.minimumHeight=0;
        foreach(var text in button.GetComponentsInChildren<TMP_Text>())RuntimeUIFactory.FitText(text,13);
    }
    public static void Split(BlacksmithView view, Transform parent, Func<int> count, Func<int,bool> commit)
    {
        var root=ModalRoot(view,parent,"OwnedQuantityDialog");var modal=OwnedItemModal.Attach(root.gameObject);modal.source=view;
        var panel=view.Panel("Panel",root,new(.30f,.30f),new(.70f,.70f));
        view.Text("Title",panel,"수량 분할",28,new(.06f,.78f),new(.94f,.96f));
        var info=view.Text("Info",panel,$"현재 {count()}개 · 분할할 수량을 입력하세요.",21,new(.06f,.57f),new(.94f,.76f));
        var amount=view.Search(panel,_=>{},new(.15f,.32f),new(.85f,.54f));
        amount.contentType=TMP_InputField.ContentType.IntegerNumber;
        if(amount.placeholder is TMP_Text hint)hint.text="1 이상, 보유 수량 미만";
        view.Button("Cancel",panel,"취소",new(.08f,.06f),new(.46f,.25f),modal.Close);
        view.Button("Confirm",panel,"분할",new(.54f,.06f),new(.92f,.25f),()=>{
            if(!int.TryParse(amount.text,out int n)||n<=0||n>=count()){info.text="보유 수량보다 작은 정수를 입력하세요.";return;}
            // Commit validates the original reference again; stale dialogs cannot split another item.
            if(!commit(n)){info.text="물품 상태가 바뀌었습니다. 다시 선택하세요.";return;}
            modal.Close();
        });
        ScreenLayoutTemplate.Apply("OwnedQuantityDialog",root);
    }
    static string Items(System.Collections.Generic.IEnumerable<Blacksmith.Stack> stacks, BlacksmithCatalog catalog) =>
        string.Join(", ",(stacks??System.Array.Empty<Blacksmith.Stack>()).Where(x=>x!=null).Select(x=>(catalog?.Item(x.itemId)?.displayName??x.itemId)+" ×"+x.count+
            (QualityRules.AppliesTo(catalog?.Item(x.itemId))?" ("+QualityRules.Name(x.quality)+")":"")));
    public static void Journal(BlacksmithView view,Transform parent,SaveData data,BlacksmithCatalog catalog)
    {
        var root=ModalRoot(view,parent,"ExperimentJournal");var modal=OwnedItemModal.Attach(root.gameObject);modal.source=view;
        var panel=view.Panel("Panel",root,new(.12f,.10f),new(.88f,.90f));
        view.Text("Title",panel,"실험 기록 · 최근 200건",29,new(.04f,.88f),new(.80f,.97f));
        view.Button("Close",panel,"닫기",new(.83f,.88f),new(.97f,.97f),modal.Close);
        var content=view.Scroll(panel,"Records",new(.035f,.04f),new(.965f,.86f));
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
    float measuredWidth=-1;
    string measuredText;
    void LateUpdate()
    {
        if(label==null)return;
        float width=label.rectTransform.rect.width;
        if(width<=0 || Mathf.Approximately(width,measuredWidth)&&measuredText==label.text)return;
        measuredWidth=width;measuredText=label.text;
        // Account for the label's vertical inset; long records remain scrollable with no clipped last line.
        GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight=
            Mathf.Max(170,(label.GetPreferredValues(label.text,width,0).y+32)/.92f);
    }
}
