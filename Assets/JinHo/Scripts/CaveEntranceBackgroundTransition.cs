using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class CaveEntranceBackgroundTransition : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private SpriteRenderer entranceRenderer;
    [SerializeField] private GameObject outsideBackground;
    [SerializeField] private GameObject caveBackground;

    [Header("Crossing")]
    [SerializeField] private bool caveIsToRight = true;
    [SerializeField] private bool allowReturnToOutside = true;
    [SerializeField, Min(0f)] private float returnHysteresis = 0.15f;
    [SerializeField] private float crossingOffsetX = -1f;

    [Header("Forest Background Boundary")]
    [SerializeField, Min(1f)] private float forestMaskWidth = 1000f;
    [SerializeField, Min(1f)] private float forestMaskHeight = 1000f;

    private float crossingX;
    private bool isInsideCave;
    private bool isInitialized;
    private GameObject forestMaskObject;
    private Sprite forestMaskSprite;
    private SpriteRenderer[] forestRenderers;

    private void Reset()
    {
        entranceRenderer = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        Initialize();
    }

    private void Update()
    {
        if (!isInitialized)
        {
            Initialize();
        }

        if (!isInitialized)
            return;

        float signedDistance = GetSignedDistanceFromEntrance();

        if (!isInsideCave && signedDistance >= 0f)
        {
            ApplyBackgroundState(true);
        }
        else if (isInsideCave && allowReturnToOutside && signedDistance <= -returnHysteresis)
        {
            ApplyBackgroundState(false);
        }
    }

    private void Initialize()
    {
        if (entranceRenderer == null)
        {
            entranceRenderer = GetComponent<SpriteRenderer>();
        }

        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                player = playerObject.transform;
            }
        }

        if (player == null || entranceRenderer == null ||
            outsideBackground == null || caveBackground == null)
        {
            return;
        }

        crossingX = entranceRenderer.bounds.center.x + crossingOffsetX;
        CreateForestBackgroundMask();
        ApplyBackgroundState(GetSignedDistanceFromEntrance() >= 0f);
        isInitialized = true;
    }

    private float GetSignedDistanceFromEntrance()
    {
        float direction = caveIsToRight ? 1f : -1f;
        return (player.position.x - crossingX) * direction;
    }

    private void ApplyBackgroundState(bool insideCave)
    {
        isInsideCave = insideCave;
        outsideBackground.SetActive(true);
        caveBackground.SetActive(insideCave);
    }

    private void CreateForestBackgroundMask()
    {
        if (forestMaskObject != null)
            return;

        forestRenderers = outsideBackground.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer spriteRenderer in forestRenderers)
        {
            spriteRenderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        }

        Texture2D whiteTexture = Texture2D.whiteTexture;
        forestMaskSprite = Sprite.Create(
            whiteTexture,
            new Rect(0f, 0f, whiteTexture.width, whiteTexture.height),
            new Vector2(0.5f, 0.5f),
            whiteTexture.width);
        forestMaskSprite.name = "ForestBackgroundBoundaryMaskSprite";

        forestMaskObject = new GameObject("ForestBackgroundBoundaryMask");
        SpriteMask spriteMask = forestMaskObject.AddComponent<SpriteMask>();
        spriteMask.sprite = forestMaskSprite;
        spriteMask.isCustomRangeActive = true;
        spriteMask.frontSortingLayerID = 0;
        spriteMask.frontSortingOrder = 100;
        spriteMask.backSortingLayerID = 0;
        spriteMask.backSortingOrder = -100;

        float maskCenterX = caveIsToRight
            ? crossingX - forestMaskWidth * 0.5f
            : crossingX + forestMaskWidth * 0.5f;

        forestMaskObject.transform.position = new Vector3(
            maskCenterX,
            entranceRenderer.bounds.center.y,
            0f);
        forestMaskObject.transform.localScale = new Vector3(
            forestMaskWidth,
            forestMaskHeight,
            1f);
    }

    private void OnDestroy()
    {
        if (forestRenderers != null)
        {
            foreach (SpriteRenderer spriteRenderer in forestRenderers)
            {
                if (spriteRenderer != null)
                    spriteRenderer.maskInteraction = SpriteMaskInteraction.None;
            }
        }

        if (forestMaskObject != null)
            Destroy(forestMaskObject);

        if (forestMaskSprite != null)
            Destroy(forestMaskSprite);
    }
}
