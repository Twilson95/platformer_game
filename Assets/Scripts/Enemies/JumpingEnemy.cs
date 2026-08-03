using UnityEngine;

namespace PlatformerGame.Enemies
{
    /// <summary>
    /// A ground enemy that patrols horizontally and jumps at an interval.
    /// </summary>
    public sealed class JumpingEnemy : Enemy
    {
        [Header("Jumping enemy")]
        [SerializeField] private float patrolDirection = 1f;
        [Tooltip("Maximum distance this enemy can patrol from its starting position while idle.")]
        [SerializeField, Min(0f)] private float patrolRange = 2.5f;
        [SerializeField, Min(0f)] private float jumpForce = 7f;
        [SerializeField] private Transform groundCheck;
        [SerializeField, Min(0.01f)] private float groundCheckRadius = 0.15f;
        [SerializeField] private LayerMask groundLayers;
        [Tooltip("Prevents rapid left/right flickering when directly above the player.")]
        [SerializeField, Min(0f)] private float horizontalChaseDeadZone = 0.05f;

        private float chaseDirection;
        private float movementDirection;
        private bool leftGroundDuringAttack;
        private float patrolOriginX;
        [Header("Runtime Debug (read only during play)")]
        [SerializeField] private float commandedMoveDirection;

        protected override void Awake()
        {
            base.Awake();
            patrolDirection = Mathf.Sign(
                Mathf.Approximately(patrolDirection, 0f) ? 1f : patrolDirection);
            chaseDirection = patrolDirection;
            movementDirection = patrolDirection;
            patrolOriginX = Body.position.x;
        }

        protected override void Move()
        {
            bool grounded = IsGrounded();

            if (grounded && !IsAttacking)
            {
                movementDirection = patrolDirection;

                if (patrolRange > 0f)
                {
                    float offsetFromOrigin = Body.position.x - patrolOriginX;
                    if ((patrolDirection > 0f && offsetFromOrigin >= patrolRange) ||
                        (patrolDirection < 0f && offsetFromOrigin <= -patrolRange))
                    {
                        patrolDirection *= -1f;
                        movementDirection = patrolDirection;
                    }
                }

                if (IsAggroed && PlayerTarget != null)
                {
                    float horizontalOffset =
                        PlayerTarget.position.x - Body.position.x;

                    if (Mathf.Abs(horizontalOffset) > horizontalChaseDeadZone)
                    {
                        chaseDirection = Mathf.Sign(horizontalOffset);
                    }

                    movementDirection = chaseDirection;
                }
            }

            commandedMoveDirection = movementDirection;
            SetHorizontalVelocity(movementDirection);

            if (IsAttacking)
            {
                if (!grounded)
                {
                    leftGroundDuringAttack = true;
                }
                else if (leftGroundDuringAttack)
                {
                    EndAttack();
                    leftGroundDuringAttack = false;
                }
            }
            else if (IsAggroed && CanStartAttack && grounded)
            {
                UsePrimaryAbility();
            }
        }

        public override void UsePrimaryAbility()
        {
            if (!IsGrounded() || !BeginAttack())
            {
                return;
            }

            leftGroundDuringAttack = false;
        }

        protected override void OnAttackStarted()
        {
            ApplyVerticalImpulse(jumpForce);
        }

        public void ReversePatrolDirection()
        {
            patrolDirection *= -1f;
        }

        protected override bool IsGroundedForAnimation => IsGrounded();

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
