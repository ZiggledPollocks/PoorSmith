using UnityEngine;

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
        private readonly PeMonsterController owner; private float direction; private float endTime;
        public PatrolState(PeMonsterController owner) => this.owner = owner;
        public void Enter() { direction = Random.value < 0.5f ? -1f : 1f; endTime = Time.time + Random.Range(owner.minimumPatrolTime, owner.maximumPatrolTime); owner.PlayAnimation(AnimationState.Walk); }
        public void Tick()
        {
            if (Time.time >= endTime) { owner.SelectRoamingState(); return; }
            if (!owner.CanMove(direction)) { direction *= -1f; if (!owner.CanMove(direction)) { owner.ChangeState(owner.idleState); return; } }
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
            direction = owner.GetFleeDirection();
            if (!owner.CanMove(direction)) { owner.ChangeState(owner.idleState); return; }
            owner.Move(direction, owner.fleeSpeed);
        }
        public void Exit() { }
    }

    private sealed class DeadState : IBehaviourState
    {
        private readonly PeMonsterController owner; private float destroyTime;
        public DeadState(PeMonsterController owner) => this.owner = owner;
        public void Enter() { owner.isDead = true; owner.StopMoving(); owner.rb.simulated = false; owner.bodyCollider.enabled = false; owner.PlayAnimation(AnimationState.Death); destroyTime = Time.time + owner.DeathAnimationDuration; }
        public void Tick() { if (Time.time < destroyTime) return; owner.SpawnDeathDrops(); Destroy(owner.gameObject); }
        public void Exit() { }
    }
}
