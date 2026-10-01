// [코드 지도] LiquidCircleGaugeHUD: 화면 좌하단에 원형 동화율 HUD의 UI 계층을 런타임 생성한다. 수치 보간과 셰이더 변경은 LiquidCircleGauge에 맡긴다. Canvas와 필요 시 만든 폰트의 수명도 관리한다.
// 주요 함수: BuildGauge, CreatePercentageText, CreateImage
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/UI/HUD/LiquidCircleGaugeHUD.cs.md

using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
/// <summary>Connects the player's value to the circular HUD gauge.</summary>
public sealed class LiquidCircleGaugeHUD : MonoBehaviour
{
    [Header("Gauge Assets")]
    [SerializeField] private PlayerAssimilate assimilationSource;
    [SerializeField] private Sprite outlineSprite;
    [SerializeField] private Shader liquidShader;

    [Header("Bottom-left Layout")]
    [SerializeField] private Vector2 gaugeSize = new(180f, 180f);
    [SerializeField] private Vector2 screenMargin = new(24f, 24f);
    [SerializeField] private int canvasSortingOrder = 100;

    private GameObject canvasObject;
    private LiquidCircleGauge gauge;
    private TMP_FontAsset runtimeFontAsset;

    public void AttachToUIRoot(Transform root, int sortingOrder)
    {
        if (canvasObject == null) return;
        canvasObject.transform.SetParent(root, false);
        canvasObject.GetComponent<Canvas>().sortingOrder = sortingOrder;
    }

    public void SetVisible(bool visible)
    {
        if (canvasObject != null) canvasObject.SetActive(visible);
    }

    /// <summary>Refresh the newly loaded value before the scene fade reveals this HUD.</summary>
    public void SyncRestoredValue() => gauge?.SnapToSource();

    private void Awake()
    {
        BuildGauge();
    }

    private void OnDestroy()
    {
        if (canvasObject != null)
            Destroy(canvasObject);

        if (runtimeFontAsset != null)
            Destroy(runtimeFontAsset);
    }

    // 핵심 분기: liquidShader == null 판정.
    // 상태 변경: assimilationSource 갱신.
    // 다음 연결: LiquidCircleGaugeHUD.CreateRectTransform(string, UnityEngine.Transform) 호출.
    private void BuildGauge()
    {
        if (liquidShader == null)
        {
            Debug.LogError("LiquidCircleGaugeHUD에 Liquid Gauge 셰이더가 연결되지 않았습니다.", this);
            return;
        }

        if (assimilationSource == null)
            assimilationSource = GetComponent<PlayerAssimilate>();

        canvasObject = new GameObject(
            "HUDCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = canvasSortingOrder;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform gaugeRect = CreateRectTransform("CircleGauge", canvasObject.transform);
        gaugeRect.anchorMin = Vector2.zero;
        gaugeRect.anchorMax = Vector2.zero;
        gaugeRect.pivot = Vector2.zero;
        gaugeRect.anchoredPosition = screenMargin;
        gaugeRect.sizeDelta = gaugeSize;

        Image water = CreateImage("Water", gaugeRect);
        StretchToParent(water.rectTransform);
        water.color = Color.white;

        TextMeshProUGUI percentageText = CreatePercentageText(gaugeRect);
        StretchToParent(percentageText.rectTransform, 18f);

        Image outline = CreateImage("Outline", gaugeRect);
        StretchToParent(outline.rectTransform);
        outline.sprite = outlineSprite;
        outline.preserveAspect = true;
        outline.color = Color.white;

        gauge = gaugeRect.gameObject.AddComponent<LiquidCircleGauge>();
        gauge.Configure(water, percentageText, liquidShader, assimilationSource);
    }

    // 핵심 분기: defaultFont != null 판정.
    // 상태 변경: text.raycastTarget 갱신.
    private TextMeshProUGUI CreatePercentageText(Transform parent)
    {
        GameObject textObject = new("PercentageText", typeof(RectTransform));
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.Center;
        RuntimeUIFactory.FitText(text, 36f);
        text.fontStyle = FontStyles.Bold;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.NoWrap;

        TMP_FontAsset defaultFont = TMP_Settings.defaultFontAsset;
        if (defaultFont != null)
        {
            text.font = defaultFont;
            return text;
        }

        Font builtInFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (builtInFont == null)
        {
            Debug.LogWarning("TMP 기본 글꼴을 찾지 못해 게이지 수치가 표시되지 않을 수 있습니다.", this);
            return text;
        }

        runtimeFontAsset = TMP_FontAsset.CreateFontAsset(builtInFont);
        runtimeFontAsset.name = "Liquid Gauge Runtime Font";
        runtimeFontAsset.hideFlags = HideFlags.HideAndDontSave;
        text.font = runtimeFontAsset;
        return text;
    }

    private static Image CreateImage(string objectName, Transform parent)
    {
        GameObject imageObject = new(objectName, typeof(RectTransform));
        imageObject.transform.SetParent(parent, false);

        Image image = imageObject.AddComponent<Image>();
        image.raycastTarget = false;
        image.type = Image.Type.Simple;
        image.useSpriteMesh = false;
        return image;
    }

    private static RectTransform CreateRectTransform(string objectName, Transform parent)
    {
        GameObject uiObject = new(objectName, typeof(RectTransform));
        uiObject.transform.SetParent(parent, false);
        return uiObject.GetComponent<RectTransform>();
    }

    private static void StretchToParent(RectTransform rectTransform, float inset = 0f)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.offsetMin = new Vector2(inset, inset);
        rectTransform.offsetMax = new Vector2(-inset, -inset);
    }
}
