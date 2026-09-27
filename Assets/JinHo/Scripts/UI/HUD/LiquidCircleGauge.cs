using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class LiquidCircleGauge : MonoBehaviour
{
    private const float MinimumRangeSize = 0.0001f;

    private static readonly int FillAmountId = Shader.PropertyToID("_FillAmount");
    private static readonly int WaveBoostId = Shader.PropertyToID("_WaveBoost");
    private static readonly int CircleRadiusId = Shader.PropertyToID("_CircleRadius");

    [Header("UI References")]
    [SerializeField] private Image waterImage;
    [SerializeField] private TMP_Text percentageText;
    [SerializeField] private Shader liquidShader;

    [Header("Value")]
    [SerializeField] private float minValue;
    [SerializeField] private float maxValue = 100f;
    [SerializeField] private float currentValue = 70f;

    [Header("Circle Mask")]
    [SerializeField, Range(0.1f, 0.5f)] private float liquidRadius = 0.445f;

    [Header("Animation")]
    [SerializeField, Min(0.01f)] private float fillSpeed = 0.65f;
    [SerializeField, Min(0f)] private float valueChangeWaveStrength = 1.5f;
    [SerializeField, Min(0.01f)] private float waveRecoverySpeed = 2.5f;

    private PlayerAssimilate assimilationSource;
    private Material runtimeMaterial;
    private float displayedFill;
    private float targetFill;
    private float waveBoost = 1f;
    private bool hasInitialValue;

    public float CurrentValue => currentValue;
    public float NormalizedValue => targetFill;

    private void Awake()
    {
        EnsureMaterial();
        SetValueInternal(currentValue, true);
    }

    private void OnEnable()
    {
        SubscribeToSource();
    }

    private void Update()
    {
        if (!EnsureMaterial())
            return;

        displayedFill = Mathf.MoveTowards(
            displayedFill,
            targetFill,
            fillSpeed * Time.unscaledDeltaTime);

        waveBoost = Mathf.MoveTowards(
            waveBoost,
            1f,
            waveRecoverySpeed * Time.unscaledDeltaTime);

        ApplyVisuals();
    }

    private void OnDisable()
    {
        UnsubscribeFromSource();
    }

    private void OnDestroy()
    {
        if (waterImage != null && waterImage.material == runtimeMaterial)
            waterImage.material = null;

        if (runtimeMaterial == null)
            return;

        if (Application.isPlaying)
            Destroy(runtimeMaterial);
        else
            DestroyImmediate(runtimeMaterial);
    }

    public void Configure(
        Image water,
        TMP_Text label,
        Shader shader,
        PlayerAssimilate source)
    {
        waterImage = water;
        percentageText = label;
        liquidShader = shader;

        EnsureMaterial();
        SetSource(source);

        if (source == null)
            SetValueInternal(currentValue, true);
    }

    public void SetValue(float value)
    {
        SetValueInternal(value, false);
    }

    public void SetNormalizedValue(float normalizedValue)
    {
        float clampedNormalizedValue = Mathf.Clamp01(normalizedValue);
        SetValue(Mathf.Lerp(minValue, maxValue, clampedNormalizedValue));
    }

    public void SetRange(float minimum, float maximum)
    {
        minValue = minimum;
        maxValue = Mathf.Max(minimum + MinimumRangeSize, maximum);
        SetValueInternal(currentValue, !hasInitialValue);
    }

    private void SetSource(PlayerAssimilate source)
    {
        UnsubscribeFromSource();
        assimilationSource = source;
        SubscribeToSource();

        if (assimilationSource != null)
            SyncFromSource(true);
    }

    private void SubscribeToSource()
    {
        if (!isActiveAndEnabled || assimilationSource == null)
            return;

        assimilationSource.AssimilationChanged -= HandleAssimilationChanged;
        assimilationSource.AssimilationChanged += HandleAssimilationChanged;
    }

    private void UnsubscribeFromSource()
    {
        if (assimilationSource != null)
            assimilationSource.AssimilationChanged -= HandleAssimilationChanged;
    }

    private void SyncFromSource(bool immediate)
    {
        minValue = 0f;
        maxValue = Mathf.Max(1, assimilationSource.MaxAssimilation);
        SetValueInternal(assimilationSource.CurrentAssimilation, immediate);
    }

    private void HandleAssimilationChanged(int value, int maximum)
    {
        minValue = 0f;
        maxValue = Mathf.Max(1, maximum);
        SetValueInternal(value, false);
    }

    private void SetValueInternal(float value, bool immediate)
    {
        maxValue = Mathf.Max(minValue + MinimumRangeSize, maxValue);
        currentValue = Mathf.Clamp(value, minValue, maxValue);

        float nextFill = Mathf.InverseLerp(minValue, maxValue, currentValue);
        float change = Mathf.Abs(nextFill - targetFill);
        targetFill = nextFill;

        if (immediate || !hasInitialValue)
        {
            displayedFill = targetFill;
            hasInitialValue = true;
        }
        else if (change > 0f)
        {
            waveBoost = Mathf.Max(waveBoost, 1f + change * valueChangeWaveStrength);
        }

        ApplyVisuals();
    }

    private bool EnsureMaterial()
    {
        if (runtimeMaterial != null)
            return true;

        if (waterImage == null || liquidShader == null)
            return false;

        runtimeMaterial = new Material(liquidShader)
        {
            name = $"{name} Liquid Gauge (Instance)",
            hideFlags = HideFlags.HideAndDontSave
        };

        waterImage.material = runtimeMaterial;
        return true;
    }

    private void ApplyVisuals()
    {
        if (runtimeMaterial != null)
        {
            runtimeMaterial.SetFloat(FillAmountId, displayedFill);
            runtimeMaterial.SetFloat(WaveBoostId, waveBoost);
            runtimeMaterial.SetFloat(CircleRadiusId, liquidRadius);
        }

        if (percentageText != null)
            percentageText.text = $"{Mathf.RoundToInt(displayedFill * 100f)}%";
    }

    private void OnValidate()
    {
        maxValue = Mathf.Max(minValue + MinimumRangeSize, maxValue);
        currentValue = Mathf.Clamp(currentValue, minValue, maxValue);
        fillSpeed = Mathf.Max(0.01f, fillSpeed);
        waveRecoverySpeed = Mathf.Max(0.01f, waveRecoverySpeed);
        liquidRadius = Mathf.Clamp(liquidRadius, 0.1f, 0.5f);

        if (Application.isPlaying)
            SetValueInternal(currentValue, false);
    }
}
