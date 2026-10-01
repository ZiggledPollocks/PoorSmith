// [코드 지도] StoneGolemStates: StoneGolemController의 partial 구현이다. 순찰·추적·공격·사망 상태를 구현하며 공격 프레임 인덱스 4에서 피해를 적용하고 잠시 프레임을 유지한다.
// 주요 함수: Tick, Enter, DormantState
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/Combat/Monsters/StoneGolem/StoneGolemStates.cs.md

using UnityEngine;

/// <summary>Stone golem: dormant rock, wake-up, five steps, selected attack, or home return.</summary>
public sealed partial class StoneGolemController
{
    private sealed class DormantState : IBehaviourState
    {
        private readonly StoneGolemController owner;
        public DormantState(StoneGolemController owner) => this.owner = owner;
        public void Enter()
        {
            owner.isProvoked = false;
            owner.hasAttacked = false;
            owner.StopMoving();
            owner.outboundPath.Clear();
            owner.outboundPath.Add(owner.spawnPosition);
            owner.PlayAnimation(AnimationState.Idle);
        }
        public void Tick() => owner.StopMoving();
        public void Exit() { }
    }

    private sealed class AwakingState : IBehaviourState
    {
        private readonly StoneGolemController owner;
        private float finish;
        public AwakingState(StoneGolemController owner) => this.owner = owner;
        public void Enter()
        {
            owner.StopMoving();
            owner.PlayAnimation(AnimationState.Awake);
            owner.EmitAwakeningDebris();
            finish = Time.time + owner.Duration(owner.awakeFrames, owner.awakeFramesPerSecond);
        }
        public void Tick()
        {
            owner.StopMoving();
            if (Time.time >= finish) owner.ChangeState(owner.fiveStepState);
        }
        public void Exit() { }
    }

    private sealed class FiveStepState : IBehaviourState
    {
        private readonly StoneGolemController owner;
        private float finish;
        public FiveStepState(StoneGolemController owner) => this.owner = owner;
        public void Enter()
        {
            float duration = owner.hasAttacked ? owner.repeatWalkDuration : owner.fiveStepDuration;
            owner.PlayAnimation(AnimationState.Walk, duration);
            finish = Time.time + duration;
        }
        // 핵심 분기: float.IsPositiveInfinity(distance) 판정.
        // 다음 연결: StoneGolemController.ChangeState(IBehaviourState) 호출.
        public void Tick()
        {
            float distance = owner.PlayerDistance;
            if (float.IsPositiveInfinity(distance))
            {
                owner.ChangeState(owner.returnHomeState);
                return;
            }
            if (distance <= owner.meleeRange && Time.time >= owner.nextAttackTime)
            {
                owner.ChangeState(owner.slamState);
                return;
            }
            if (Time.time >= finish)
            {
                owner.StopMoving();
                if (distance > owner.returnDistance)
                    owner.ChangeState(owner.returnHomeState);
                else if (Time.time < owner.nextAttackTime)
                    return;
                else if (distance <= owner.meleeRange)
                    owner.ChangeState(owner.slamState);
                else if (distance <= owner.throwRange)
                    owner.ChangeState(owner.throwState);
                else
                    owner.ChangeState(owner.fiveStepState);
                return;
            }
            float direction = owner.playerTarget.position.x - owner.rb.position.x;
            if (owner.CanMove(direction)) owner.Move(direction);
            else owner.StopMoving();
        }
        public void Exit() => owner.StopMoving();
    }

    private sealed class SlamState : IBehaviourState
    {
        private readonly StoneGolemController owner;
        private float impactAt;
        private float finish;
        private bool impacted;
        public SlamState(StoneGolemController owner) => this.owner = owner;
        public void Enter()
        {
            owner.StopMoving();
            owner.FacePlayer();
            owner.PlayAnimation(AnimationState.Slam);
            impacted = false;
            impactAt = Time.time + owner.slamImpactFrame / owner.slamFramesPerSecond;
            finish = Time.time + owner.Duration(owner.slamFrames, owner.slamFramesPerSecond);
        }
        public void Tick()
        {
            owner.StopMoving();
            if (!impacted && Time.time >= impactAt)
            {
                impacted = true;
                owner.SlamImpact();
            }
            if (Time.time < finish) return;
            owner.nextAttackTime = Time.time + owner.attackCooldown;
            owner.hasAttacked = true;
            owner.ChangeState(owner.fiveStepState);
        }
        public void Exit() { }
    }

    private sealed class ThrowState : IBehaviourState
    {
        private readonly StoneGolemController owner;
        private float releaseAt;
        private float finish;
        private bool released;
        public ThrowState(StoneGolemController owner) => this.owner = owner;
        public void Enter()
        {
            owner.StopMoving();
            owner.FacePlayer();
            owner.PlayAnimation(AnimationState.Throw);
            released = false;
            releaseAt = Time.time + owner.throwReleaseFrame / owner.throwFramesPerSecond;
            finish = Time.time + owner.Duration(owner.throwFrames, owner.throwFramesPerSecond);
        }
        public void Tick()
        {
            owner.StopMoving();
            if (!released && Time.time >= releaseAt)
            {
                released = true;
                owner.ThrowRock();
            }
            if (Time.time < finish) return;
            owner.nextAttackTime = Time.time + owner.attackCooldown;
            owner.hasAttacked = true;
            owner.ChangeState(owner.fiveStepState);
        }
        public void Exit() { }
    }

    private sealed class ReturnHomeState : IBehaviourState
    {
        private readonly StoneGolemController owner;
        public ReturnHomeState(StoneGolemController owner) => this.owner = owner;
        public void Enter()
        {
            owner.StopMoving();
            owner.RecordPath();
            owner.PlayAnimation(AnimationState.Walk);
        }
        public void Tick()
        {
            if (owner.ReturnAlongPath())
            {
                owner.ChangeState(owner.dormantState); // Health is deliberately unchanged.
            }
        }
        public void Exit() => owner.StopMoving();
    }

    private sealed class DeadState : IBehaviourState
    {
        private readonly StoneGolemController owner;
        private float destroyTime;
        public DeadState(StoneGolemController owner) => this.owner = owner;
        public void Enter()
        {
            owner.isDead = true;
            owner.StopMoving();
            owner.CaptureHeadDropPosition();
            owner.rb.simulated = false;
            owner.bodyCollider.enabled = false;
            owner.PlayAnimation(AnimationState.Death);
            destroyTime = Time.time + owner.DeathDuration;
        }
        public void Tick()
        {
            if (Time.time < destroyTime) return;
            owner.SpawnCore();
            Destroy(owner.gameObject);
        }
        public void Exit() { }
    }
}
