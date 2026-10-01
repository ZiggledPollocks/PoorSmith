// [코드 지도] VampireBatStates: VampireBatController의 partial 구현이다. 천장 대기·급강하·대각선 후퇴·천장 복귀·사망 상태를 구현한다.
// 주요 함수: Tick, Enter, CeilingIdleState
// 함수별 조건·상태 변경·호출 관계: Obsidian/batterground/코드해체분석기/Assets/JinHo/Scripts/Combat/Monsters/VampireBat/VampireBatStates.cs.md

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
            if (!owner.FindLivingPlayer() || Time.time >= endTime)
            {
                if (owner.TryDamagePlayer()) owner.BeginCounterattackWindow();
                else owner.BeginRetreat();
                return;
            }
            Vector2 direction = (Vector2)owner.playerTarget.position - owner.rb.position;
            owner.Move(direction);
            if (direction.magnitude <= owner.contactDistance)
            {
                if (owner.TryDamagePlayer()) owner.BeginCounterattackWindow();
                else owner.BeginRetreat();
            }
        }
        public void Exit() { }
    }

    private sealed class CounterattackWindowState : IBehaviourState
    {
        private readonly VampireBatController owner;
        private float endTime;
        public CounterattackWindowState(VampireBatController owner) => this.owner = owner;
        public void Enter()
        {
            owner.StopMoving();
            owner.PlayAnimation(AnimationState.Fly);
            endTime = Time.time + owner.counterattackWindow;
        }
        public void Tick()
        {
            owner.StopMoving();
            if (Time.time >= endTime) owner.ChangeState(owner.retreatState);
        }
        public void Exit() { }
    }

    private sealed class DiagonalRetreatState : IBehaviourState
    {
        private readonly VampireBatController owner;
        private Vector2 direction;
        public DiagonalRetreatState(VampireBatController owner) => this.owner = owner;
        public void Enter()
        {
            Vector2 away = owner.FindLivingPlayer() ? owner.rb.position - (Vector2)owner.playerTarget.position : Vector2.left;
            float horizontal = Mathf.Abs(away.x) > 0.01f ? Mathf.Sign(away.x) : (Random.value < 0.5f ? -1f : 1f);
            direction = new Vector2(horizontal, Mathf.Max(0.45f, away.normalized.y + owner.retreatUpwardBias)).normalized;
            owner.PlayAnimation(AnimationState.Fly);
        }
        public void Tick()
        {
            if (Time.time >= owner.nextAttackTime)
            {
                owner.ChangeState(owner.PlayerIsDetected() ? owner.attackState : owner.returnToCeilingState);
                return;
            }
            owner.MoveDuringRetreat(direction);
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
            owner.StopAttackEffect();
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
