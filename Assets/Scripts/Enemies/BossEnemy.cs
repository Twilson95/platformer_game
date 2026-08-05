using UnityEngine;
using UnityEngine.Events;
using System;
using System.Collections;

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
            Burrow
        }

        [Header("Boss")]
        [SerializeField, Min(0f)] private float attackRange = 1.8f;
        [SerializeField, Min(0.1f)] private float moveInterval = 2f;
        [SerializeField, Min(0.1f)] private float dashSpeedMultiplier = 3f;
        [SerializeField, Min(0.05f)] private float dashDuration = 0.45f;
        [SerializeField, Min(0.1f)] private float leapForce = 9f;
        [SerializeField, Min(0.05f)] private float burrowDuration = 0.35f;
        [SerializeField, Min(0.1f)] private float burrowDepth = 3.5f;
        [SerializeField, Min(0.5f)] private float teleportHeight = 5f;
        [SerializeField, Min(0.1f)] private float enragedHealthPercent = 0.5f;
        [SerializeField, Min(1f)] private float enragedMovementSpeedMultiplier = 1.35f;
        [SerializeField, Min(1f)] private float enragedAttackSpeedMultiplier = 1.5f;
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
        protected override bool IsGroundedForAnimation => IsGrounded();
        protected override float AttackCooldownMultiplier => IsEnraged
            ? 1f / enragedAttackSpeedMultiplier
            : 1f;

        private bool IsEnraged => CurrentHealth <= maxHealth * enragedHealthPercent;
        private float AttackInterval => moveInterval / (IsEnraged
            ? enragedAttackSpeedMultiplier
            : 1f);
        private float SpeedMultiplier => IsEnraged
            ? enragedMovementSpeedMultiplier
            : 1f;

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
            nextMoveTime = Time.time + AttackInterval;
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
                if (currentMove == "Burrowing")
                {
                    Body.linearVelocity = Vector2.zero;
                    return;
                }

                if (currentMove == "Dashing")
                {
                    SetHorizontalVelocity(
                        attackDirection,
                        dashSpeedMultiplier * SpeedMultiplier);
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
                SetHorizontalVelocity(direction, SpeedMultiplier);
            }

            currentMove = "Following";

            if (Time.time >= nextMoveTime)
            {
                nextMove = (MoveType)UnityEngine.Random.Range(0, 3);
                UsePrimaryAbility();
            }
        }

        public override void UsePrimaryAbility()
        {
            if (!encounterActive ||
                PlayerTarget == null ||
                !CanStartAttack ||
                !IsGrounded())
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
                case MoveType.Burrow:
                    currentMove = "Burrowing";
                    started = BeginAttack();
                    if (started)
                    {
                        StartCoroutine(BurrowAttack());
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
                ApplyVerticalImpulse(leapForce * SpeedMultiplier);
            }
        }

        private IEnumerator BurrowAttack()
        {
            SuppressStateCollider(true);
            Body.linearVelocity = Vector2.zero;

            Vector2 burrowStartPosition = Body.position;
            float elapsed = 0f;
            while (elapsed < burrowDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / burrowDuration);
                Body.position = burrowStartPosition + Vector2.down *
                    (burrowDepth * progress);
                yield return null;
            }

            if (!IsAlive || PlayerTarget == null)
            {
                SuppressStateCollider(false);
                EndAttack();
                currentMove = "Ready";
                yield break;
            }

            Body.position = new Vector2(
                PlayerTarget.position.x,
                PlayerTarget.position.y + teleportHeight);
            Body.linearVelocity = Vector2.down * leapForce * SpeedMultiplier;
            SuppressStateCollider(false);
            currentMove = "Leaping";
        }

        protected override void FixedUpdate()
        {
            base.FixedUpdate();

            if (!IsAlive || !encounterActive || IsAttacking || Time.time < nextMoveTime)
            {
                return;
            }

            nextMove = (MoveType)(((int)nextMove + 1) % 3);
            nextMoveTime = Time.time + AttackInterval;
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
            return IsGroundedAtActiveColliderBottom(groundCheckRadius, groundLayers);
        }
    }
}
