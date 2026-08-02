using UnityEngine;
using UnityEngine.Events;
using System;

namespace PlatformerGame.Enemies
{
    /// <summary>
    /// A large enemy with a repeating set of attacks. The boss remains idle
    /// until BeginEncounter is called by a BossRoomController.
    /// </summary>
    public sealed class BossEnemy : Enemy
    {
        private enum MoveType
        {
            Dash,
            Leap,
            CloseAttack
        }

        [Header("Boss")]
        [SerializeField, Min(0f)] private float attackRange = 1.8f;
        [SerializeField, Min(0.1f)] private float moveInterval = 2f;
        [SerializeField, Min(0.1f)] private float dashSpeedMultiplier = 3f;
        [SerializeField, Min(0.05f)] private float dashDuration = 0.45f;
        [SerializeField, Min(0.1f)] private float leapForce = 9f;
        [SerializeField, Min(0.05f)] private float closeAttackDuration = 0.35f;
        [SerializeField] private Transform groundCheck;
        [SerializeField, Min(0.01f)] private float groundCheckRadius = 0.2f;
        [SerializeField] private LayerMask groundLayers;
        [SerializeField] private UnityEvent onDefeated;

        [Header("Runtime Debug")]
        [SerializeField] private bool encounterActive;
        [SerializeField] private string currentMove = "Waiting";

        private MoveType nextMove;
        private float nextMoveTime;
        private float attackDirection;
        private bool leftGroundDuringLeap;

        public bool EncounterActive => encounterActive;
        public event Action Defeated;

        protected override bool AlwaysAggro => true;

        protected override void Awake()
        {
            base.Awake();
            BeginEncounter();
        }

        public void BeginEncounter()
        {
            if (!IsAlive || encounterActive)
            {
                return;
            }

            encounterActive = true;
            nextMove = MoveType.Dash;
            nextMoveTime = Time.time + moveInterval;
            currentMove = "Ready";
        }

        protected override void Move()
        {
            if (!encounterActive || PlayerTarget == null)
            {
                Body.linearVelocity = new Vector2(0f, Body.linearVelocity.y);
                return;
            }

            if (IsAttacking)
            {
                if (currentMove == "Dashing")
                {
                    SetHorizontalVelocity(attackDirection, dashSpeedMultiplier);
                }

                if (currentMove == "Leaping" && IsGrounded() && leftGroundDuringLeap)
                {
                    EndAttack();
                    leftGroundDuringLeap = false;
                    currentMove = "Ready";
                }

                if (currentMove == "Leaping" && !IsGrounded())
                {
                    leftGroundDuringLeap = true;
                }

                return;
            }

            float horizontalOffset = PlayerTarget.position.x - Body.position.x;
            float direction = Mathf.Sign(horizontalOffset);
            if (!Mathf.Approximately(direction, 0f))
            {
                SetHorizontalVelocity(direction);
            }

            currentMove = "Following";

            if (Time.time >= nextMoveTime)
            {
                UsePrimaryAbility();
            }
        }

        public override void UsePrimaryAbility()
        {
            if (!encounterActive || PlayerTarget == null || !CanStartAttack)
            {
                return;
            }

            attackDirection = Mathf.Sign(PlayerTarget.position.x - Body.position.x);
            if (Mathf.Approximately(attackDirection, 0f))
            {
                attackDirection = 1f;
            }

            bool started = false;
            switch (nextMove)
            {
                case MoveType.Dash:
                    currentMove = "Dashing";
                    started = BeginAttack(dashDuration);
                    break;
                case MoveType.Leap:
                    currentMove = "Leaping";
                    leftGroundDuringLeap = false;
                    started = BeginAttack();
                    break;
                case MoveType.CloseAttack:
                    if (Mathf.Abs(PlayerTarget.position.x - Body.position.x) <= attackRange)
                    {
                        currentMove = "Close attack";
                        started = BeginAttack(closeAttackDuration);
                    }
                    break;
            }

            if (!started)
            {
                currentMove = "Ready";
                nextMoveTime = Time.time + 0.25f;
            }
        }

        protected override void OnAttackStarted()
        {
            if (currentMove == "Dashing")
            {
                SetHorizontalVelocity(attackDirection, dashSpeedMultiplier);
            }
            else if (currentMove == "Leaping")
            {
                ApplyVerticalImpulse(leapForce);
            }
        }

        protected override void FixedUpdate()
        {
            base.FixedUpdate();

            if (!IsAlive || !encounterActive || IsAttacking || Time.time < nextMoveTime)
            {
                return;
            }

            nextMove = (MoveType)(((int)nextMove + 1) % 3);
            nextMoveTime = Time.time + moveInterval;
        }

        protected override void Die()
        {
            encounterActive = false;
            Defeated?.Invoke();
            onDefeated?.Invoke();
            base.Die();
        }

        private bool IsGrounded()
        {
            return groundCheck != null && Physics2D.OverlapCircle(
                groundCheck.position, groundCheckRadius, groundLayers) != null;
        }
    }
}
