// [코드 지도] StoneGolemImpactPulse: 돌 골렘 타격 효과의 판정과 시각 연출을 처리한다.
// 주요 함수: Configure, Draw, Update
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/Combat/Monsters/StoneGolem/StoneGolemImpactPulse.cs.md

using UnityEngine;

/// <summary>Short visual marker for the actual impact radius; collision and damage stay in the attack owners.</summary>
public sealed class StoneGolemImpactPulse : MonoBehaviour
{
    private const int Segments = 32;
    private static Material lineMaterial;
    private LineRenderer line;
    private float radius;
    private float started;

    public static void Spawn(Vector2 position, float impactRadius)
    {
        var marker = new GameObject("Stone Golem Impact Pulse", typeof(LineRenderer), typeof(StoneGolemImpactPulse));
        marker.transform.position = position;
        marker.GetComponent<StoneGolemImpactPulse>().Configure(impactRadius);
    }

    private void Configure(float impactRadius)
    {
        radius = impactRadius;
        started = Time.time;
        line = GetComponent<LineRenderer>();
        if (lineMaterial == null) lineMaterial = new Material(Shader.Find("Sprites/Default"));
        line.sharedMaterial = lineMaterial;
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = Segments;
        line.widthMultiplier = .1f;
        line.sortingOrder = 22;
        Draw(0f);
    }

    private void Update()
    {
        if (line == null) return;
        float progress = (Time.time - started) / .3f;
        if (progress >= 1f) { Destroy(gameObject); return; }
        Draw(progress);
    }

    private void Draw(float progress)
    {
        float currentRadius = radius * Mathf.Lerp(.3f, 1f, progress);
        for (int i = 0; i < Segments; i++)
        {
            float angle = i * Mathf.PI * 2f / Segments;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * currentRadius);
        }
        var color = new Color(.75f, .68f, .53f, .8f * (1f - progress));
        line.startColor = color;
        line.endColor = color;
    }
}
