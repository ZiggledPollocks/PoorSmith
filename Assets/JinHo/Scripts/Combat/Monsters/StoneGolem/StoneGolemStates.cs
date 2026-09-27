using UnityEngine;

public sealed partial class StoneGolemController
{
    private sealed class IdlePatrolState : IBehaviourState
    {
        private readonly StoneGolemController owner;
        private float direction;
        private float nextTurnTime;
        public IdlePatrolState(StoneGolemController owner) => this.owner = owner;
        public void Enter()
        {
            direction = Random.value < 0.5f ? -1f : 1f;
            nextTurnTime = Time.time + Random.Range(0.8f, 1.8f);
            owner.PlayAnimation(AnimationState.Walk);
        }
        public void Tick()
        {
            if (owner.PlayerIsDetected())
            {
                owner.isProvoked = true;
                owner.ChangeState(owner.attackState);
                return;
            }

            if (owner.isProvoked) { owner.ChangeState(owner.chaseState); return; }
            float offset = owner.rb.position.x - owner.spawnPosition.x;
            if (Mathf.Abs(offset) >= owner.idlePatrolRadius) direction = -Mathf.Sign(offset);
            else if (Time.time >= nextTurnTime)
            {
                direction *= -1f;
                nextTurnTime = Time.time + Random.Range(0.8f, 1.8f);
            }
            if (!owner.CanMove(direction)) { owner.StopMoving(); return; }
            owner.Move(direction);
        }
        public void Exit() { }
    }

    private sealed class ChaseState : IBehaviourState
    {
        private readonly StoneGolemController owner;
        public ChaseState(StoneGolemController owner) => this.owner = owner;
        public void Enter() => owner.PlayAnimation(AnimationState.Walk);
        public void Tick()
        {
            if (!owner.FindLivingPlayer()) { owner.StopMoving(); return; }
            float delta = owner.playerTarget.position.x - owner.transform.position.x;
            float distance = Vector2.Distance(owner.rb.position, owner.playerTarget.position);
            if (distance <= owner.attackRange)
            {
                owner.StopMoving();
                if (Time.time >= owner.nextAttackTime)
                    owner.ChangeState(owner.attackState);
                return;
            }
            if (!owner.CanMove(delta)) { owner.StopMoving(); return; }
            owner.Move(delta);
        }
        public void Exit() { }
    }

    private sealed class AttackState : IBehaviourState
    {
        private const int CriticalFrameIndex = 4;
        private readonly StoneGolemController owner;
        private int frameIndex;
        private float nextFrameTime;
        private float holdEndTime;
        private bool holdingCriticalFrame;
        public AttackState(StoneGolemController owner) => this.owner = owner;
        public void Enter()
        {
            owner.StopMoving();
            frameIndex = 0;
            holdingCriticalFrame = false;
            owner.ApplyAttackFrame(frameIndex);
            nextFrameTime = Time.time + 1f / owner.attackFramesPerSecond;
        }
        public void Tick()
        {
            owner.StopMoving();
            if (holdingCriticalFrame)
            {
                if (Time.time < holdEndTime) return;
                holdingCriticalFrame = false;
                frameIndex = CriticalFrameIndex + 1;
                owner.ApplyAttackFrame(frameIndex);
                nextFrameTime = Time.time + 1f / owner.attackFramesPerSecond;
                return;
            }
            if (Time.time < nextFrameTime) return;
            frameIndex++;
            if (owner.attackFrames == null || frameIndex >= owner.attackFrames.Length)
            {
                owner.nextAttackTime = Time.time + owner.attackCooldown;
                owner.ChangeState(owner.chaseState);
                return;
            }
            owner.ApplyAttackFrame(frameIndex);
            if (frameIndex == CriticalFrameIndex)
            {
                owner.DealCriticalFrameDamage();
                holdingCriticalFrame = true;
                holdEndTime = Time.time + owner.criticalFrameHold;
            }
            else
            {
                nextFrameTime = Time.time + 1f / owner.attackFramesPerSecond;
            }
        }
        public void Exit() => owner.manualAttackAnimation = false;
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
