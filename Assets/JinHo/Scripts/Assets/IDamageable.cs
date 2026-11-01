// [코드 지도] IDamageable: 피해를 받을 수 있는 객체와 체력 정보를 읽을 수 있는 객체의 계약을 정의한다. IDamageable은 행동과 사망 여부, IHealthSource는 읽기용 현재/최대 체력을 제공한다. PlayerInteraction, 전투 객체, MonsterHealthBar2D처럼 서로 다른 소비자가 구체 몬스터 클래스에 덜 의존하게 한다. 필드와 실행 본문은 없다.
// 주요 함수: TakeDamage
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/Assets/IDamageable.cs.md

public interface IDamageable
{
    bool IsDead { get; }
    void TakeDamage(float amount);
}

/// <summary>
/// Read-only health data used by world-space UI such as monster health bars.
/// </summary>
public interface IHealthSource : IDamageable
{
    float CurrentHealth { get; }
    int MaxHealth { get; }
}
