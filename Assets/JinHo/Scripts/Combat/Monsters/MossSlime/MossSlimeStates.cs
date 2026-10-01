// [코드 지도] MossSlimeStates: MossSlimeController의 partial 구현이다. 배회 점프·추적 점프·사망 상태의 Enter/Tick/Exit를 담으며 별도 부착하는 컴포넌트가 아니다.
// 주요 함수: Enter, Tick, ChooseDirection
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/Combat/Monsters/MossSlime/MossSlimeStates.cs.md

using UnityEngine;

/// <summary>Contains the moss slime's behaviour state implementations.</summary>
public sealed partial class MossSlimeController
{
    private sealed class WanderHopState : IBehaviourState
    {
        private readonly MossSlimeController owner;
        private float direction;
        private float nextDirectionTime;
        public WanderHopState(MossSlimeController owner) => this.owner = owner;
        public void Enter() { owner.PlayAnimation(AnimationState.Hop); ChooseDirection(); }
        public void Tick()
        {
            if (owner.PlayerIsDetected()) { owner.ChangeState(owner.chaseState); return; }
            if (Time.time >= nextDirectionTime) ChooseDirection();
            owner.Jump(direction);
        }
        public void Exit() { }
        private void ChooseDirection()
        {
            direction = Random.value < 0.5f ? -1f : 1f;
            nextDirectionTime = Time.time + owner.wanderDirectionTime;
        }
    }

    private sealed class ChaseHopState : IBehaviourState
    {
        private readonly MossSlimeController owner;
        public ChaseHopState(MossSlimeController owner) => this.owner = owner;
        public void Enter() => owner.PlayAnimation(AnimationState.Hop);
        public void Tick()
        {
            if (!owner.PlayerIsDetected()) { owner.ChangeState(owner.wanderState); return; }
            float direction = owner.playerTarget.position.x - owner.transform.position.x;
            owner.Jump(direction);
        }
        public void Exit() { }
    }

    private sealed class DeadState : IBehaviourState
    {
        private readonly MossSlimeController owner;
        private float destroyTime;
        public DeadState(MossSlimeController owner) => this.owner = owner;
        public void Enter()
        {
            owner.isDead = true;
            owner.rb.linearVelocity = Vector2.zero;
            owner.rb.simulated = false;
            owner.bodyCollider.enabled = false;
            owner.PlayAnimation(AnimationState.Death);
            destroyTime = Time.time + owner.DeathDuration;
        }
        public void Tick()
        {
            if (Time.time < destroyTime) return;
            owner.SpawnMucus();
            Destroy(owner.gameObject);
        }
        public void Exit() { }
    }
}
