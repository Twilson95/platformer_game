using UnityEngine;

namespace PlatformerGame.Enemies
{
    /// <summary>
    /// A simple ground enemy that patrols and walks after an aggroed player.
    /// It has no jumping or flying behavior.
    /// </summary>
    public sealed class WalkingEnemy : Enemy
    {
        [Header("Walking enemy")]
        [SerializeField] private float patrolDirection = 1f;
        [Tooltip("Prevents rapid left/right flickering when overlapping the player.")]
        [SerializeField, Min(0f)] private float horizontalChaseDeadZone = 0.05f;
        [SerializeField, Min(0f)] private float chargeRange = 2.5f;
        [SerializeField, Min(0.05f)] private float chargeDuration = 0.5f;
        [SerializeField, Min(1f)] private float chargeSpeedMultiplier = 2.5f;
        [SerializeField] private Transform groundCheck;
        [SerializeField, Min(0.01f)] private float groundCheckRadius = 0.15f;
        [SerializeField] private LayerMask groundLayers;

        private float chaseDirection;
        private float chargeDirection;
        [Header("Runtime Debug (read only during play)")]
        [SerializeField] private float commandedMoveDirection;

        protected override void Awake()
        {
            base.Awake();
            patrolDirection = Mathf.Sign(
                Mathf.Approximately(patrolDirection, 0f) ? 1f : patrolDirection);
            chaseDirection = patrolDirection;
        }

        protected override void Move()
        {
            if (IsAttacking)
            {
                commandedMoveDirection = chargeDirection;
                SetHorizontalVelocity(chargeDirection, chargeSpeedMultiplier);
                return;
            }

            float direction = patrolDirection;

            if (IsAggroed && PlayerTarget != null)
            {
                float horizontalOffset =
                    PlayerTarget.position.x - Body.position.x;

                if (Mathf.Abs(horizontalOffset) > horizontalChaseDeadZone)
                {
                    chaseDirection = Mathf.Sign(horizontalOffset);
                }

                direction = chaseDirection;

                float distanceToPlayer = Mathf.Abs(
                    PlayerTarget.position.x - Body.position.x);
                if (CanStartAttack &&
                    distanceToPlayer <= chargeRange &&
                    IsGrounded())
                {
                    UsePrimaryAbility();
                    return;
                }
            }

            commandedMoveDirection = direction;
            SetHorizontalVelocity(direction);
        }

        public override void UsePrimaryAbility()
        {
            if (PlayerTarget == null || !IsGrounded())
            {
                return;
            }

            float horizontalOffset =
                PlayerTarget.position.x - Body.position.x;
            chargeDirection = Mathf.Approximately(horizontalOffset, 0f)
                ? chaseDirection
                : Mathf.Sign(horizontalOffset);

            BeginAttack(chargeDuration);
        }

        protected override void OnAttackStarted()
        {
            commandedMoveDirection = chargeDirection;
            SetHorizontalVelocity(chargeDirection, chargeSpeedMultiplier);
        }

        public void ReversePatrolDirection()
        {
            patrolDirection *= -1f;
        }

        private bool IsGrounded()
        {
            return groundCheck != null &&
                   Physics2D.OverlapCircle(
                       groundCheck.position,
                       groundCheckRadius,
                       groundLayers) != null;
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            if (groundCheck == null)
            {
                return;
            }

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
}
