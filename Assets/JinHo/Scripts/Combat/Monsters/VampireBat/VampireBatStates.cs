using UnityEngine;

/// <summary>Contains the vampire bat's behaviour state implementations.</summary>
public sealed partial class VampireBatController
{
    private sealed class CeilingIdleState : IBehaviourState
    {
        private readonly VampireBatController owner;
        public CeilingIdleState(VampireBatController owner) => this.owner = owner;
        public void Enter()
        {
            owner.StopMoving();
            owner.PlayAnimation(AnimationState.Idle);
            owner.SnapToCeiling();
        }
        public void Tick()
        {
            owner.StopMoving();
            if (!owner.TryGetCeilingAnchor(out Vector2 anchor) ||
                Vector2.Distance(owner.rb.position, anchor) > owner.ceilingArrivalDistance)
            {
                owner.ChangeState(owner.returnToCeilingState);
                return;
            }

            owner.rb.position = anchor;
            if (Time.time >= owner.nextAttackTime && owner.PlayerIsDetected()) owner.ChangeState(owner.attackState);
        }
        public void Exit() { }
    }

    private sealed class DiveAttackState : IBehaviourState
    {
        private readonly VampireBatController owner;
        private float endTime;
        public DiveAttackState(VampireBatController owner) => this.owner = owner;
        public void Enter() { endTime = Time.time + owner.attackDuration; owner.PlayAnimation(AnimationState.Attack); }
        public void Tick()
        {
            if (!owner.FindLivingPlayer() || Time.time >= endTime) { owner.TryDamagePlayer(); owner.BeginRetreat(); return; }
            Vector2 direction = (Vector2)owner.playerTarget.position - owner.rb.position;
            owner.Move(direction);
            if (direction.magnitude <= owner.contactDistance) { owner.TryDamagePlayer(); owner.BeginRetreat(); }
        }
        public void Exit() { }
    }

    private sealed class DiagonalRetreatState : IBehaviourState
    {
        private readonly VampireBatController owner;
        private Vector2 direction;
        private float endTime;
        public DiagonalRetreatState(VampireBatController owner) => this.owner = owner;
        public void Enter()
        {
            Vector2 away = owner.FindLivingPlayer() ? owner.rb.position - (Vector2)owner.playerTarget.position : Vector2.left;
            float horizontal = Mathf.Abs(away.x) > 0.01f ? Mathf.Sign(away.x) : (Random.value < 0.5f ? -1f : 1f);
            direction = new Vector2(horizontal, Mathf.Max(0.45f, away.normalized.y + owner.retreatUpwardBias)).normalized;
            endTime = Time.time + owner.retreatDuration;
            owner.PlayAnimation(AnimationState.Fly);
        }
        public void Tick()
        {
            if (Time.time >= endTime) { owner.ChangeState(owner.returnToCeilingState); return; }
            owner.Move(direction);
        }
        public void Exit() { }
    }

    private sealed class ReturnToCeilingState : IBehaviourState
    {
        private readonly VampireBatController owner;
        public ReturnToCeilingState(VampireBatController owner) => this.owner = owner;

        public void Enter() => owner.PlayAnimation(AnimationState.Fly);

        public void Tick()
        {
            if (!owner.TryGetCeilingAnchor(out Vector2 anchor))
            {
                owner.Move(Vector2.up);
                return;
            }

            Vector2 direction = anchor - owner.rb.position;
            if (direction.magnitude > owner.ceilingArrivalDistance)
            {
                owner.Move(direction);
                return;
            }

            owner.rb.position = anchor;
            owner.ChangeState(owner.idleState);
        }

        public void Exit() => owner.StopMoving();
    }

    private sealed class DeadState : IBehaviourState
    {
        private readonly VampireBatController owner;
        private float destroyTime;
        public DeadState(VampireBatController owner) => this.owner = owner;
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
            owner.SpawnSharpFangs();
            Destroy(owner.gameObject);
        }
        public void Exit() { }
    }
}
