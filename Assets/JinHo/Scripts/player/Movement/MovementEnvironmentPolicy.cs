// [코드 지도] MovementEnvironmentPolicy: 환경에 속해 있는 동안 일반 점프 차단, 낙하 피해 억제, 선택적 중력 배율을 표현한다. 이동 모드를 구르기나 넉백이 대신해도 환경 정책은 별도로 적용된다.
// 주요 함수: MovementEnvironmentPolicy, Floating
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/player/Movement/MovementEnvironmentPolicy.cs.md

/// <summary>Environment membership policies remain active during rolls/knockback.</summary>
public readonly struct MovementEnvironmentPolicy
{
    public readonly bool BlocksNormalJump;
    public readonly bool SuppressesFallDamage;
    public readonly float? GravityScale;

    public MovementEnvironmentPolicy(bool blocksNormalJump, bool suppressesFallDamage, float? gravityScale)
    {
        BlocksNormalJump = blocksNormalJump;
        SuppressesFallDamage = suppressesFallDamage;
        GravityScale = gravityScale;
    }

    public static MovementEnvironmentPolicy Floating => new MovementEnvironmentPolicy(true, true, 0f);
}
