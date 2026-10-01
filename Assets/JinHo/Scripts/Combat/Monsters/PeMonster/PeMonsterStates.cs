// [코드 지도] PeMonsterStates: PeMonsterController의 partial 구현이다. 대기·순찰·피격 후 도주·사망 상태를 담는다. 감지해서 추적하거나 공격하는 상태는 이 구현에 없다.
// 주요 함수: Tick, IdleState, Enter
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/Combat/Monsters/PeMonster/PeMonsterStates.cs.md

using UnityEngine;

/// <summary>Contains the deer monster's behaviour state implementations.</summary>
public sealed partial class PeMonsterController
{
    private sealed class IdleState : IBehaviourState
    {
        private readonly PeMonsterController owner; private float endTime;
        public IdleState(PeMonsterController owner) => this.owner = owner;
        public void Enter() { owner.StopMoving(); owner.PlayAnimation(AnimationState.Idle); endTime = Time.time + Random.Range(owner.minimumIdleTime, owner.maximumIdleTime); }
        public void Tick() { owner.StopMoving(); if (Time.time >= endTime) owner.ChangeState(owner.patrolState); }
        public void Exit() { }
    }

    private sealed class PatrolState : IBehaviourState
    {
        private readonly PeMonsterController owner; private float direction; private float endTime; private float resumeAt;
        public PatrolState(PeMonsterController owner) => this.owner = owner;
        public void Enter() { direction = Random.value < 0.5f ? -1f : 1f; endTime = Time.time + Random.Range(owner.minimumPatrolTime, owner.maximumPatrolTime); resumeAt = 0f; owner.PlayAnimation(AnimationState.Walk); }
        public void Tick()
        {
            if (Time.time >= endTime) { owner.SelectRoamingState(); return; }
            if (Time.time < resumeAt) { owner.StopMoving(); return; }
            if (!owner.CanMove(direction))
            {
                direction *= -1f;
                if (!owner.CanMove(direction)) { owner.ChangeState(owner.idleState); return; }
                resumeAt = Time.time + Mathf.Max(owner.turnPause,
                    Mathf.Abs(owner.rb.linearVelocity.x) / owner.braking);
                owner.StopMoving();
                owner.PlayAnimation(AnimationState.Idle);
                return;
            }
            owner.PlayAnimation(AnimationState.Walk);
            owner.Move(direction, owner.walkSpeed);
        }
        public void Exit() { }
    }

    private sealed class FleeState : IBehaviourState
    {
        private readonly PeMonsterController owner; private float direction; private float endTime;
        public FleeState(PeMonsterController owner) => this.owner = owner;
        public void Enter() { direction = owner.GetFleeDirection(); endTime = Time.time + owner.fleeDuration; owner.PlayAnimation(AnimationState.Run); }
        public void Tick()
        {
            if (Time.time >= endTime) { owner.SelectRoamingState(); return; }
            if (!owner.CanMove(direction)) { owner.ChangeState(owner.idleState); return; }
            owner.Move(direction, owner.fleeSpeed);
        }
        public void Exit() { }
    }

    private sealed class DeadState : IBehaviourState
    {
        private readonly PeMonsterController owner; private float destroyTime;
        public DeadState(PeMonsterController owner) => this.owner = owner;
        public void Enter() { owner.isDead = true; owner.StopMoving(true); owner.rb.simulated = false; owner.bodyCollider.enabled = false; owner.PlayAnimation(AnimationState.Death); destroyTime = Time.time + owner.DeathAnimationDuration; }
        public void Tick() { if (Time.time < destroyTime) return; owner.SpawnDeathDrops(); Destroy(owner.gameObject); }
        public void Exit() { }
    }
}
