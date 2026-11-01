// [코드 지도] SpriteFrameClock: 누적 프레임 시간으로 스프라이트 인덱스를 계산하는 공통 함수다. 실제 SpriteRenderer 적용과 종별 공격 프레임 효과는 각 컨트롤러가 담당한다.
// 주요 함수: Advance
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/Combat/Animation/SpriteFrameClock.cs.md

using UnityEngine;

/// <summary>Shared frame timing; species-specific frame effects remain in their controllers.</summary>
public static class SpriteFrameClock
{
    public static int Advance(ref float time, float deltaTime, float framesPerSecond, int count, bool loops)
    {
        time += deltaTime * framesPerSecond;
        int index = Mathf.FloorToInt(time);
        return loops ? index % count : Mathf.Min(index, count - 1);
    }
}
