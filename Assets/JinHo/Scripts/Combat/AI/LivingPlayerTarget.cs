// [코드 지도] LivingPlayerTarget: 몬스터가 사용할 플레이어 Transform과 IDamageable 참조를 찾거나 보충한다. 체력 참조가 없으면 살아 있는 것으로 허용하는 반환 정책을 갖는다.
// 주요 함수: Resolve
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/Combat/AI/LivingPlayerTarget.cs.md

using UnityEngine;

/// <summary>Resolves a living player target for monster AI.</summary>
public static class LivingPlayerTarget
{
    public static bool Resolve(ref Transform playerTarget, ref IDamageable playerDamageable)
    {
        if (playerTarget == null)
        {
            PlayerAssimilate player = Object.FindFirstObjectByType<PlayerAssimilate>();
            if (player == null) return false;
            playerTarget = player.transform;
            playerDamageable = player;
        }
        else
        {
            playerDamageable ??= playerTarget.GetComponent<IDamageable>();
        }
        return playerDamageable == null || !playerDamageable.IsDead;
    }
}
