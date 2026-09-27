using UnityEngine;

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
