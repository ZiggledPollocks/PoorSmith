using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Shows the shared non-crafting campaign status while gathering.</summary>
public sealed class FieldHud : MonoBehaviour
{
    [SerializeField] TMP_FontAsset font;
    [SerializeField] CaveEntranceBackgroundTransition entrance;
    Canvas canvas;
    TMP_Text status, region, mapTitle, mapLegend, notice, confirmationText;
    RectTransform mapModal, mapArea, mapButton, mapPlayerMarker, confirmation;
    Bounds mapBounds;
    float previousTimeScale;
    bool previousExternalActivity, mapOpen, warpMapOpen;
    Coroutine noticeRoutine;
    CampaignWorldObject selectedWarp;

    void Awake()
    {
        var root = new GameObject("FieldStatusCanvas", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920,1080);
        status = Label("FieldStatus", root.transform, new Vector2(.015f,.91f), new Vector2(.55f,.985f),
            TextAlignmentOptions.Left);
        region = Label("FieldRegion", root.transform, new Vector2(.76f,.91f), new Vector2(.985f,.985f),
            TextAlignmentOptions.Right);
        mapButton = ExplorationMapLayout.Rect("FieldMapButton",root.transform,Vector2.zero,Vector2.zero);
        mapButton.anchorMin = mapButton.anchorMax = Vector2.zero;
        mapButton.pivot = Vector2.zero;
        mapButton.anchoredPosition = new Vector2(228f,26f);
        mapButton.sizeDelta = new Vector2(84f,60f);
        var mapButtonImage=mapButton.gameObject.AddComponent<Image>();
        mapButtonImage.color=new Color(.18f,.25f,.23f,.95f);
        mapButton.gameObject.AddComponent<Button>().onClick.AddListener(OpenMap);
        Label("MapButtonText",mapButton,Vector2.zero,Vector2.one,TextAlignmentOptions.Center).text="지도";

        mapModal=ExplorationMapLayout.Rect("FieldMapModal",root.transform,Vector2.zero,Vector2.one);
        var shade=mapModal.gameObject.AddComponent<Image>();
        shade.color=new Color(0f,0f,0f,.84f);
        shade.raycastTarget=true;
        var panel=ExplorationMapLayout.Rect("MapPaper",mapModal,new Vector2(.10f,.08f),new Vector2(.90f,.90f));
        panel.gameObject.AddComponent<Image>().color=new Color(.18f,.17f,.13f,.98f);
        mapTitle=Label("MapTitle",panel,new Vector2(.05f,.87f),new Vector2(.55f,.97f),
            TextAlignmentOptions.Left);
        mapTitle.text="채집 필드 지도";
        var back=ExplorationMapLayout.Rect("MapBack",panel,new Vector2(.85f,.88f),new Vector2(.96f,.97f));
        back.gameObject.AddComponent<Image>().color=new Color(.35f,.31f,.25f);
        back.gameObject.AddComponent<Button>().onClick.AddListener(CloseMap);
        Label("BackText",back,Vector2.zero,Vector2.one,TextAlignmentOptions.Center).text="뒤로";
        mapArea=ExplorationMapLayout.Rect("MapArea",panel,new Vector2(.05f,.17f),new Vector2(.95f,.85f));
        mapArea.gameObject.AddComponent<Image>().color=new Color(.10f,.13f,.14f,.98f);
        mapLegend=Label("MapLegend",panel,new Vector2(.05f,.03f),new Vector2(.95f,.13f),
            TextAlignmentOptions.Center);
        mapLegend.text="현재 위치 · 동화율 석상 · 출입구  |  방문한 구역만 표시";
        confirmation=ExplorationMapLayout.Rect("WarpConfirmation",panel,new Vector2(.24f,.31f),new Vector2(.76f,.69f));
        confirmation.gameObject.AddComponent<Image>().color=new Color(.12f,.14f,.16f,.98f);
        confirmationText=Label("ConfirmationText",confirmation,new Vector2(.08f,.48f),new Vector2(.92f,.90f),TextAlignmentOptions.Center);
        var yes=CreateButton("WarpYes",confirmation,"예",new Vector2(.10f,.11f),new Vector2(.43f,.37f));
        yes.onClick.AddListener(ConfirmWarp);
        var no=CreateButton("WarpNo",confirmation,"아니오",new Vector2(.57f,.11f),new Vector2(.90f,.37f));
        no.onClick.AddListener(()=>confirmation.gameObject.SetActive(false));
        confirmation.gameObject.SetActive(false);
        mapModal.gameObject.SetActive(false);
        notice=Label("FieldNotice",root.transform,new Vector2(.23f,.83f),new Vector2(.77f,.90f),TextAlignmentOptions.Center);
        notice.gameObject.SetActive(false);
    }

    void Update()
    {
        var loop = SmithingLoop.Instance;
        if (canvas == null || loop == null || !loop.Initialized) return;
        if(mapOpen)
        {
            canvas.enabled=true;
            ExplorationMapLayout.MoveMarker(mapPlayerMarker,transform.position,mapBounds);
            if(Keyboard.current?.escapeKey.wasPressedThisFrame==true ||
                Gamepad.current?.buttonEast.wasPressedThisFrame==true)
            {
                if(confirmation.gameObject.activeSelf)confirmation.gameObject.SetActive(false);
                else CloseMap();
            }
            return;
        }
        bool hudVisible=!GameUIController.BlocksGameplayInput&&!loop.InShop;
        canvas.enabled=hudVisible;
        if (!hudVisible) return;
        var campaign = loop.Campaign;
        status.text = $"Day {loop.SmithData.day} · {campaign.gold} G · 화살 {GetComponent<CampaignCombat>()?.ArrowCount ?? 0}" +
            (GetComponent<CampaignCombat>()?.fieldRules?.provisional == true ? " · 임시 밸런스" : "");
        region.text = entrance != null && entrance.IsInsideCave ? "동굴" : "숲";
    }

    void OpenMap() => OpenMap(false);

    public void OpenWarpMap(CampaignWorldObject source)
    {
        if(source==null||source.kind!=CampaignObjectKind.Warp)return;
        OpenMap(true);
    }

    public void ShowNotice(string message)
    {
        if(notice==null)return;
        if(noticeRoutine!=null)StopCoroutine(noticeRoutine);
        noticeRoutine=StartCoroutine(NoticeRoutine(message));
    }

    IEnumerator NoticeRoutine(string message)
    {
        notice.text=message;
        notice.gameObject.SetActive(true);
        yield return new WaitForSecondsRealtime(3f);
        notice.gameObject.SetActive(false);
        noticeRoutine=null;
    }

    void OpenMap(bool warp)
    {
        if(mapOpen||GameUIController.BlocksGameplayInput||SmithingLoop.Instance?.Initialized!=true||
            GetComponent<FieldSceneState>()?.Ready!=true)return;
        CombatHitFeedback2D.FinishHitStopBeforePause();
        previousTimeScale=Time.timeScale;
        previousExternalActivity=GameUIController.ExternalActivity;
        Time.timeScale=0f;
        GameUIController.ExternalActivity=true;
        GetComponent<PlayerInputHandler>()?.ClearGameplayInput();
        status.gameObject.SetActive(false);
        region.gameObject.SetActive(false);
        mapButton.gameObject.SetActive(false);
        mapModal.gameObject.SetActive(true);
        confirmation.gameObject.SetActive(false);
        warpMapOpen=warp;
        mapTitle.text=warp?"워프석 지도":"채집 필드 지도";
        mapLegend.text=warp?"방문해 활성화한 워프석을 선택하세요. 집은 마을로 돌아갑니다.":
            "현재 위치 · 동화율 석상 · 출입구  |  방문한 구역만 표시";
        mapOpen=true;
        BuildMap();
    }

    void BuildMap()
    {
        for(int i=mapArea.childCount-1;i>=0;i--)Destroy(mapArea.GetChild(i).gameObject);
        var exploration=GetComponent<CampaignExploration>();
        var boundary=FindFirstObjectByType<FieldRegionCameraBounds>();
        mapBounds=boundary!=null?boundary.MapBounds:
            new Bounds(new Vector3(25f,-21f,0f),new Vector3(184f,86f,1f));
        float caveX=entrance!=null?entrance.transform.position.x:float.PositiveInfinity;
        var state=SmithingLoop.Instance.Campaign;
        if(!warpMapOpen)
        {
            ExplorationMapLayout.DrawVisited(mapArea,state.visitedCells,
                exploration!=null?exploration.cellSize:8f,mapBounds,
                new Color(.43f,.62f,.47f,.86f),new Color(.61f,.57f,.48f,.90f),caveX,-10f);
            ExplorationMapLayout.DrawTerrain(mapArea,exploration,mapBounds,
                new Color(.27f,.26f,.24f,.87f));
        }
        if(exploration!=null)
        {
            foreach(var altar in FindObjectsByType<AssissZone>(FindObjectsSortMode.None))
            {
                if(warpMapOpen)continue;
                if(exploration.IsExplored(altar.transform.position))
                    ExplorationMapLayout.Marker(mapArea,"Altar",altar.transform.position,mapBounds,
                        "석",new Color(.72f,.91f,.84f),font);
            }
            if(!warpMapOpen&&entrance!=null&&exploration.IsExplored(entrance.transform.position))
                ExplorationMapLayout.Marker(mapArea,"CaveEntrance",entrance.transform.position,
                    mapBounds,"굴",new Color(.81f,.70f,.43f),font);
            foreach(var gate in FindObjectsByType<FieldSceneReturnGate>(FindObjectsSortMode.None))
            {
                if(warpMapOpen)continue;
                if(exploration.IsExplored(gate.transform.position))
                    ExplorationMapLayout.Marker(mapArea,"TownExit",gate.transform.position,
                        mapBounds,"마",new Color(.87f,.68f,.41f),font);
            }
            foreach(var landmark in FindObjectsByType<CampaignWorldObject>(FindObjectsSortMode.None))
            {
                if(warpMapOpen)
                {
                    if(landmark.kind!=CampaignObjectKind.Warp||state.unlockedWarps==null||
                        !state.unlockedWarps.Contains(landmark.stableId))continue;
                    var marker=ExplorationMapLayout.Marker(mapArea,"Warp_"+landmark.stableId,
                        landmark.transform.position,mapBounds,"워",new Color(.88f,.76f,.44f),font);
                    if(marker==null)continue;
                    marker.GetComponent<Image>().raycastTarget=true;
                    var captured=landmark;
                    marker.gameObject.AddComponent<Button>().onClick.AddListener(()=>SelectWarp(captured));
                    marker.gameObject.AddComponent<FieldWarpMarkerHover>();
                }
                else if(exploration.IsExplored(landmark.transform.position)&&
                    (landmark.kind==CampaignObjectKind.Warp ||
                     landmark.kind==CampaignObjectKind.WindEntrance ||
                     landmark.kind==CampaignObjectKind.WindExit))
                    ExplorationMapLayout.Marker(mapArea,"Marker_"+landmark.stableId,
                        landmark.transform.position,mapBounds,
                        landmark.kind==CampaignObjectKind.Warp?"워":"입",
                        new Color(.88f,.76f,.44f),font);
            }
        }
        if(warpMapOpen)
        {
            var home=CreateButton("TownWarp",mapArea,"집",new Vector2(.04f,.08f),new Vector2(.13f,.20f));
            home.onClick.AddListener(()=>SelectWarp(null));
            home.gameObject.AddComponent<FieldWarpMarkerHover>();
        }
        mapPlayerMarker=ExplorationMapLayout.Marker(mapArea,"PlayerPosition",transform.position,
            mapBounds,"나",new Color(.99f,.82f,.30f),font);
        if(mapPlayerMarker!=null)mapPlayerMarker.SetAsLastSibling();
    }

    void SelectWarp(CampaignWorldObject destination)
    {
        selectedWarp=destination;
        confirmationText.text=destination==null?"마을로 돌아가시겠습니까?":
            destination.displayName+"으로 이동하시겠습니까?";
        confirmation.gameObject.SetActive(true);
        confirmation.SetAsLastSibling();
    }

    void ConfirmWarp()
    {
        var destination=selectedWarp;
        CloseMap();
        bool started=destination==null?FieldSceneTravel.BeginToTown():
            FieldSceneTravel.BeginFieldWarp((Vector2)destination.transform.position+Vector2.up*1.6f);
        if(!started)ShowNotice("지금은 이동할 수 없습니다. 잠시 후 다시 시도하세요.");
    }

    Button CreateButton(string name,Transform parent,string text,Vector2 min,Vector2 max)
    {
        var rect=ExplorationMapLayout.Rect(name,parent,min,max);
        rect.gameObject.AddComponent<Image>().color=new Color(.52f,.45f,.28f,.98f);
        var button=rect.gameObject.AddComponent<Button>();
        Label(name+"Text",rect,Vector2.zero,Vector2.one,TextAlignmentOptions.Center).text=text;
        return button;
    }

    void CloseMap()
    {
        if(!mapOpen)return;
        mapOpen=false;
        warpMapOpen=false;
        confirmation.gameObject.SetActive(false);
        mapModal.gameObject.SetActive(false);
        status.gameObject.SetActive(true);
        region.gameObject.SetActive(true);
        mapButton.gameObject.SetActive(true);
        Time.timeScale=previousTimeScale;
        GameUIController.ExternalActivity=previousExternalActivity;
        GetComponent<PlayerInputHandler>()?.ClearGameplayInput();
    }

    void OnDestroy()
    {
        if(!mapOpen)return;
        Time.timeScale=previousTimeScale;
        GameUIController.ExternalActivity=previousExternalActivity;
    }

    TMP_Text Label(string name, Transform parent, Vector2 min, Vector2 max, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var text = go.GetComponent<TextMeshProUGUI>();
        text.font = font ?? TMP_Settings.defaultFontAsset;
        text.fontSize = 24;
        text.alignment = alignment;
        text.color = new Color(.97f,.91f,.76f);
        text.raycastTarget = false;
        return text;
    }
}
