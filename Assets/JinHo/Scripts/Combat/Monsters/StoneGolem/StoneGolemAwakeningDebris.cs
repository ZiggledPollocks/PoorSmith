// [코드 지도] StoneGolemAwakeningDebris: 돌 골렘 각성 때 생기는 파편의 이동과 소멸을 처리한다.
// 주요 함수: Emit, Update
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/Combat/Monsters/StoneGolem/StoneGolemAwakeningDebris.cs.md

using UnityEngine;

/// <summary>Small, visual-only stones shed when the dormant golem rises.</summary>
public sealed class StoneGolemAwakeningDebris : MonoBehaviour
{
    private SpriteRenderer visual;
    private Vector2 velocity;
    private float born;
    private float spin;

    // 핵심 분기: stoneSprite == null || golemVisual == null 판정.
    // 상태 변경: chip.transform.position 갱신.
    public static void Emit(Bounds body, Sprite stoneSprite, SpriteRenderer golemVisual)
    {
        if (stoneSprite == null || golemVisual == null) return;
        for (int i = 0; i < 3; i++)
        {
            var chip = new GameObject("Stone Golem Awakening Chip");
            chip.transform.position = new Vector3(
                body.center.x + (i - 1) * body.extents.x * .65f,
                body.max.y + .12f + (i % 2) * .18f,
                golemVisual.transform.position.z);
            chip.transform.localScale = Vector3.one * (i == 1 ? .027f : .021f);
            var renderer = chip.AddComponent<SpriteRenderer>();
            renderer.sprite = stoneSprite;
            renderer.sortingLayerID = golemVisual.sortingLayerID;
            renderer.sortingOrder = golemVisual.sortingOrder + 1;
            var motion = chip.AddComponent<StoneGolemAwakeningDebris>();
            motion.visual = renderer;
            motion.velocity = new Vector2((i - 1) * .55f, -.4f - i * .18f);
            motion.spin = (i - 1) * 150f + 65f;
            motion.born = Time.time;
        }
    }

    private void Update()
    {
        float elapsed = Time.time - born;
        if (elapsed >= .8f) { Destroy(gameObject); return; }
        velocity += Vector2.down * (7f * Time.deltaTime);
        transform.position += (Vector3)(velocity * Time.deltaTime);
        transform.Rotate(0f, 0f, spin * Time.deltaTime);
        Color color = visual.color;
        color.a = Mathf.Clamp01((.8f - elapsed) / .35f);
        visual.color = color;
    }
}
