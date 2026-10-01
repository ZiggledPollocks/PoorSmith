// [코드 지도] CombatHitFeedback2D: 확인된 검 타격에 짧은 시간 감속과 카메라 흔들림을 더합니다. 같은 파일의 MonsterHitFlash2D는 피격 색 변화를, SwordHitSpark2D는 네 방향 불꽃을, SwordReachArc2D는 무기 리치의 곡선 표시를 담당합니다. 효과 시간은 unscaledTime을 사용하여 게임 시간이 느려져도 진행됩니다. 피해량 계산이나 명중 판정 자체를 수행하는 코드는 아닙니다.
// 주요 함수: Build, Update, PlaySound
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/Combat/Feedback/CombatHitFeedback2D.cs.md

using System.Collections;
using UnityEngine;

/// <summary>
/// Coordinates short hit-stop and camera shake for confirmed player hits.
/// Uses unscaled time so the feedback continues during hit-stop.
/// </summary>
[DefaultExecutionOrder(10000)]
[DisallowMultipleComponent]
public sealed class CombatHitFeedback2D : MonoBehaviour
{
    private const float HitStopDuration = 0.055f;
    private const float HitStopScale = 0.06f;
    private const float ShakeDuration = 0.12f;
    private const float ShakeStrength = 0.09f;

    private static CombatHitFeedback2D instance;

    private Vector3 appliedShakeOffset;
    private float shakeEndsAt;
    private float shakeStrength;
    private float hitStopEndsAt;
    private float timeScaleBeforeHitStop = 1f;
    private float appliedHitStopScale = 1f;
    private bool hitStopActive;
    private AudioSource impactAudio;
    private static AudioClip woodClip, stoneClip, monsterClip;

    public static void Play(GameObject target, Vector2 impactPoint)
    {
        if (target == null)
            return;

        MonsterHitFlash2D.PlayOn(target);
        SwordHitSpark2D.Spawn(impactPoint, target);

        CombatHitFeedback2D feedback = GetOrCreate();
        if (feedback == null)
            return;

        feedback.PlaySound(2);
        feedback.TriggerHitStop();
        feedback.TriggerShake();
    }

    public static void PlayResource(GameObject target,bool wood,bool effective)
    {
        if(target==null)return;
        ResourceHitFlash2D.PlayOn(target,effective);
        var renderer=target.GetComponentInChildren<SpriteRenderer>();
        Vector2 point=renderer!=null?renderer.bounds.center:target.transform.position;
        SwordHitSpark2D.Spawn(point,target,wood?new Color(.77f,.52f,.27f):new Color(.74f,.81f,.84f),effective?1f:.55f);
        if(effective)GetOrCreate()?.PlaySound(wood?0:1);
    }

    void PlaySound(int kind)
    {
        if(impactAudio==null)
        {
            var audioObject=new GameObject("Field Impact Audio",typeof(AudioSource));
            audioObject.transform.SetParent(transform,false);
            impactAudio=audioObject.GetComponent<AudioSource>();
            impactAudio.playOnAwake=false;
            impactAudio.spatialBlend=0f;
            impactAudio.volume=.28f;
            audioObject.AddComponent<GameAudioChannel>().Bind(GameUIController.Instance?.Sound);
        }
        AudioClip clip=kind==0?woodClip??=CreateClip("Wood Impact",175f):
            kind==1?stoneClip??=CreateClip("Stone Impact",310f):
            monsterClip??=CreateClip("Monster Impact",235f);
        impactAudio.PlayOneShot(clip);
    }

    static AudioClip CreateClip(string name,float frequency)
    {
        const int samples=3600;
        var data=new float[samples];
        for(int i=0;i<samples;i++)
        {
            float t=i/44100f;
            float envelope=Mathf.Pow(1f-i/(float)samples,2f);
            float noise=Mathf.Sin(i*2.17f)*Mathf.Sin(i*.71f);
            data[i]=(Mathf.Sin(t*frequency*Mathf.PI*2f)*.55f+noise*.24f)*envelope;
        }
        AudioClip clip=AudioClip.Create(name,samples,1,44100,false);
        clip.SetData(data,0);
        return clip;
    }

    public static void FinishHitStopBeforePause()
    {
        if (instance == null || !instance.hitStopActive)
            return;

        if (Mathf.Approximately(Time.timeScale, instance.appliedHitStopScale))
            Time.timeScale = instance.timeScaleBeforeHitStop;

        instance.hitStopActive = false;
    }

    private static CombatHitFeedback2D GetOrCreate()
    {
        if (instance != null)
            return instance;

        Camera camera = Camera.main;
        if (camera == null)
            return null;

        instance = camera.GetComponent<CombatHitFeedback2D>();
        if (instance == null)
            instance = camera.gameObject.AddComponent<CombatHitFeedback2D>();

        return instance;
    }

    private void Awake()
    {
        if (instance == null)
            instance = this;
    }

    private void Update()
    {
        // Remove the previous render offset before camera-follow systems run
        // their LateUpdate. This prevents cumulative drift with Cinemachine.
        transform.position -= appliedShakeOffset;
        appliedShakeOffset = Vector3.zero;

        if (!hitStopActive || Time.unscaledTime < hitStopEndsAt)
            return;

        if (Mathf.Approximately(Time.timeScale, appliedHitStopScale))
            Time.timeScale = timeScaleBeforeHitStop;

        hitStopActive = false;
    }

    private void LateUpdate()
    {
        if (Time.unscaledTime >= shakeEndsAt)
        {
            shakeStrength = 0f;
            return;
        }

        Vector2 randomOffset = Random.insideUnitCircle * shakeStrength;
        appliedShakeOffset = new Vector3(randomOffset.x, randomOffset.y, 0f);
        transform.position += appliedShakeOffset;
    }

    private void TriggerHitStop()
    {
        if (Time.timeScale <= 0f)
            return;

        if (!hitStopActive)
        {
            timeScaleBeforeHitStop = Time.timeScale;
            appliedHitStopScale = timeScaleBeforeHitStop * HitStopScale;
            Time.timeScale = appliedHitStopScale;
            hitStopActive = true;
        }

        hitStopEndsAt = Mathf.Max(
            hitStopEndsAt,
            Time.unscaledTime + HitStopDuration);
    }

    private void TriggerShake()
    {
        shakeEndsAt = Mathf.Max(shakeEndsAt, Time.unscaledTime + ShakeDuration);
        shakeStrength = Mathf.Clamp(shakeStrength + ShakeStrength, ShakeStrength, 0.14f);
    }

    private void OnDisable()
    {
        transform.position -= appliedShakeOffset;
        appliedShakeOffset = Vector3.zero;

        if (hitStopActive && Mathf.Approximately(Time.timeScale, appliedHitStopScale))
            Time.timeScale = timeScaleBeforeHitStop;

        hitStopActive = false;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }
}

[DisallowMultipleComponent]
public sealed class MonsterHitFlash2D : MonoBehaviour
{
    private const float FlashDuration = 0.16f;
    private static readonly Color HitColor = new(1f, 0.22f, 0.08f, 1f);

    private SpriteRenderer targetRenderer;
    private Color restingColor = Color.white;
    private Coroutine flashRoutine;

    public static void PlayOn(GameObject target)
    {
        MonsterHitFlash2D feedback = target.GetComponent<MonsterHitFlash2D>();
        if (feedback == null)
            feedback = target.AddComponent<MonsterHitFlash2D>();

        feedback.Play();
    }

    private void Awake()
    {
        ResolveRenderer();
    }

    private void ResolveRenderer()
    {
        if (targetRenderer != null)
            return;

        targetRenderer = GetComponent<SpriteRenderer>();
        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<SpriteRenderer>();

        if (targetRenderer != null)
            restingColor = targetRenderer.color;
    }

    private void Play()
    {
        ResolveRenderer();
        if (targetRenderer == null)
            return;

        if (flashRoutine != null)
            StopCoroutine(flashRoutine);
        else
            restingColor = targetRenderer.color;

        flashRoutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        float startedAt = Time.unscaledTime;
        while (Time.unscaledTime - startedAt < FlashDuration)
        {
            float progress = (Time.unscaledTime - startedAt) / FlashDuration;
            float intensity = Mathf.Sin(progress * Mathf.PI);
            targetRenderer.color = Color.Lerp(restingColor, HitColor, intensity);
            yield return null;
        }

        targetRenderer.color = restingColor;
        flashRoutine = null;
    }

    private void OnDisable()
    {
        if (targetRenderer != null)
            targetRenderer.color = restingColor;
        flashRoutine = null;
    }
}

[DisallowMultipleComponent]
public sealed class ResourceHitFlash2D : MonoBehaviour
{
    SpriteRenderer target;
    Color baseColor;
    Coroutine pulse;
    public static void PlayOn(GameObject owner,bool effective)
    {
        var effect=owner.GetComponent<ResourceHitFlash2D>()??owner.AddComponent<ResourceHitFlash2D>();
        effect.Play(effective);
    }
    void Play(bool effective)
    {
        target??=GetComponentInChildren<SpriteRenderer>();
        if(target==null)return;
        if(pulse!=null)StopCoroutine(pulse);
        else baseColor=target.color;
        pulse=StartCoroutine(Flash(effective));
    }
    IEnumerator Flash(bool effective)
    {
        float start=Time.unscaledTime;
        Color flash=effective?new Color(1f,.91f,.58f):new Color(.55f,.56f,.56f);
        while(Time.unscaledTime-start<.18f)
        {
            float p=(Time.unscaledTime-start)/.18f;
            target.color=Color.Lerp(baseColor,flash,Mathf.Sin(p*Mathf.PI));
            yield return null;
        }
        target.color=baseColor;pulse=null;
    }
    void OnDisable(){if(target!=null)target.color=baseColor;pulse=null;}
}

/// <summary>A silent, short visual rebound for a resource struck with the wrong tool type.</summary>
public sealed class ResourceToolFeedback2D : MonoBehaviour
{
    const float Duration=.2f;
    static Sprite markSprite;
    readonly SpriteRenderer[] marks=new SpriteRenderer[2];
    Vector2 origin,away;
    float startedAt;

    public static void PlayWrongTool(GameObject player,GameObject resource)
    {
        if(player==null||resource==null)return;
        var target=resource.GetComponent<IResourceProvider>();
        if(target==null)return;
        var collider=resource.GetComponentInChildren<Collider2D>();
        var renderer=resource.GetComponentInChildren<SpriteRenderer>();
        Vector2 point=collider!=null?collider.ClosestPoint(player.transform.position):
            renderer!=null?(Vector2)renderer.bounds.ClosestPoint(player.transform.position):resource.transform.position;
        Vector2 direction=(Vector2)player.transform.position-point;
        if(direction.sqrMagnitude<.01f)direction=Vector2.up;
        var effect=new GameObject("Wrong Tool Rebound",typeof(ResourceToolFeedback2D)).GetComponent<ResourceToolFeedback2D>();
        effect.Build(point,direction.normalized,renderer);
        string required=target is TreeInteractable or ThicketInteractable?"도끼":"곡괭이";
        player.GetComponent<FieldHud>()?.ShowToolHint($"{required}가 필요합니다",point);
    }

    void Build(Vector2 point,Vector2 direction,SpriteRenderer target)
    {
        if(markSprite==null)
            markSprite=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,1,1),new Vector2(.5f,.5f),1f);
        origin=point;away=direction;startedAt=Time.unscaledTime;
        transform.position=point;
        for(int i=0;i<marks.Length;i++)
        {
            var part=new GameObject("Rebound Stroke",typeof(SpriteRenderer));
            part.transform.SetParent(transform,false);
            part.transform.localRotation=Quaternion.Euler(0,0,i==0?45f:-45f);
            part.transform.localScale=new Vector3(.075f,.29f,1f);
            var mark=part.GetComponent<SpriteRenderer>();
            mark.sprite=markSprite;
            mark.color=new Color(.95f,.79f,.58f);
            mark.sortingLayerID=target!=null?target.sortingLayerID:0;
            mark.sortingOrder=target!=null?target.sortingOrder+50:50;
            marks[i]=mark;
        }
    }

    void Update()
    {
        float progress=(Time.unscaledTime-startedAt)/Duration;
        if(progress>=1f){Destroy(gameObject);return;}
        transform.position=origin+away*(.38f*Mathf.Sin(progress*Mathf.PI*.5f));
        float scale=1f-progress*.4f;
        foreach(var mark in marks)
        {
            mark.transform.localScale=new Vector3(.075f*scale,.29f*scale,1f);
            var color=mark.color;color.a=1f-progress;mark.color=color;
        }
    }
}

public sealed class SwordHitSpark2D : MonoBehaviour
{
    private const float Lifetime = 0.16f;
    private static Sprite sparkSprite;

    private SpriteRenderer[] renderers;
    private Transform[] sparkTransforms;
    private float startedAt;
    private float sizeScale=1f;

    public static void Spawn(Vector2 position, GameObject target)
        => Spawn(position,target,new Color(1f,.9f,.35f));

    public static void Spawn(Vector2 position, GameObject target,Color tint)
        => Spawn(position,target,tint,1f);

    public static void Spawn(Vector2 position, GameObject target,Color tint,float scale)
    {
        GameObject root = new("Sword Hit Spark");
        root.layer = target.layer;
        root.transform.position = new Vector3(position.x, position.y, target.transform.position.z);

        SwordHitSpark2D effect = root.AddComponent<SwordHitSpark2D>();
        effect.sizeScale=scale;
        effect.Build(target,tint);
    }

    // 핵심 분기: sparkSprite == null 판정.
    // 상태 변경: sparkSprite 갱신.
    private void Build(GameObject target,Color tint)
    {
        if (sparkSprite == null)
        {
            sparkSprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            sparkSprite.name = "Runtime Sword Hit Spark Sprite";
        }

        SpriteRenderer targetRenderer = target.GetComponent<SpriteRenderer>();
        if (targetRenderer == null)
            targetRenderer = target.GetComponentInChildren<SpriteRenderer>();

        int sortingLayerId = targetRenderer != null ? targetRenderer.sortingLayerID : 0;
        int sortingOrder = targetRenderer != null ? targetRenderer.sortingOrder + 100 : 100;

        renderers = new SpriteRenderer[4];
        sparkTransforms = new Transform[4];
        for (int index = 0; index < sparkTransforms.Length; index++)
        {
            GameObject ray = new($"Ray {index + 1}");
            ray.layer = target.layer;
            ray.transform.SetParent(transform, false);
            ray.transform.localRotation = Quaternion.Euler(0f, 0f, 45f + index * 90f);

            SpriteRenderer rayRenderer = ray.AddComponent<SpriteRenderer>();
            rayRenderer.sprite = sparkSprite;
            rayRenderer.color = tint;
            rayRenderer.sortingLayerID = sortingLayerId;
            rayRenderer.sortingOrder = sortingOrder;

            renderers[index] = rayRenderer;
            sparkTransforms[index] = ray.transform;
        }

        startedAt = Time.unscaledTime;
    }

    // 핵심 분기: progress >= 1f 판정.
    // 상태 변경: ray.localPosition 갱신.
    private void Update()
    {
        float progress = (Time.unscaledTime - startedAt) / Lifetime;
        if (progress >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        float length = Mathf.Lerp(0.18f, 0.52f, progress)*sizeScale;
        float thickness = Mathf.Lerp(0.055f, 0.012f, progress)*sizeScale;
        float alpha = 1f - progress;

        for (int index = 0; index < sparkTransforms.Length; index++)
        {
            Transform ray = sparkTransforms[index];
            ray.localPosition = ray.up * Mathf.Lerp(0.02f, 0.16f, progress);
            ray.localScale = new Vector3(thickness, length, 1f);

            Color color = renderers[index].color;
            color.a = alpha;
            renderers[index].color = color;
        }
    }
}

/// <summary>Brief visual trace along the outer edge of a weapon's actual reach.</summary>
public sealed class SwordReachArc2D : MonoBehaviour
{
    private const int Segments = 24;
    private const float Lifetime = .18f;
    private static Material lineMaterial;

    private LineRenderer line;
    private float radius;
    private float startAngle;
    private float spanAngle;
    private float startedAt;

    public static void Spawn(Vector2 origin, Vector2 direction, ToolData weapon, GameObject owner)
    {
        if (weapon == null || owner == null) return;

        var trace = new GameObject("Sword Reach Arc", typeof(LineRenderer), typeof(SwordReachArc2D));
        trace.layer = owner.layer;
        trace.transform.position = new Vector3(origin.x, origin.y, owner.transform.position.z);
        trace.GetComponent<SwordReachArc2D>().Configure(direction, weapon, owner);
    }

    private void Configure(Vector2 direction, ToolData weapon, GameObject owner)
    {
        radius = weapon.Reach;
        float aimAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        // A thrust's narrow tip traces only its capsule width; a swing traces its full hit sector.
        spanAngle = weapon.SwordAttackStyle == SwordAttackStyle.Thrust
            ? 2f * Mathf.Atan2(weapon.ThrustWidth * .5f, radius) * Mathf.Rad2Deg
            : weapon.SwingAngle;
        startAngle = aimAngle - spanAngle * .5f;
        startedAt = Time.unscaledTime;

        line = GetComponent<LineRenderer>();
        if (lineMaterial == null) lineMaterial = new Material(Shader.Find("Sprites/Default"));
        line.sharedMaterial = lineMaterial;
        line.useWorldSpace = false;
        line.loop = false;
        line.positionCount = Segments + 1;
        line.widthMultiplier = .055f;
        line.numCapVertices = 2;
        var sprite = owner.GetComponent<SpriteRenderer>() ?? owner.GetComponentInChildren<SpriteRenderer>();
        line.sortingLayerID = sprite != null ? sprite.sortingLayerID : 0;
        line.sortingOrder = sprite != null ? sprite.sortingOrder + 100 : 100;
        Draw(0f);
    }

    private void Update()
    {
        float progress = (Time.unscaledTime - startedAt) / Lifetime;
        if (progress >= 1f)
        {
            Destroy(gameObject);
            return;
        }
        Draw(progress);
    }

    private void Draw(float progress)
    {
        // Reveal the curve with the swing, then fade without changing its reach radius.
        float revealed = Mathf.Clamp01(progress * 2.5f);
        for (int index = 0; index <= Segments; index++)
        {
            float angle = (startAngle + spanAngle * revealed * index / Segments) * Mathf.Deg2Rad;
            line.SetPosition(index, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius));
        }
        float alpha = .8f * (1f - Mathf.Clamp01((progress - .4f) / .6f));
        var color = new Color(1f, .82f, .38f, alpha);
        line.startColor = color;
        line.endColor = color;
    }
}
