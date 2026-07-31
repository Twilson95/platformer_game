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
        [SerializeField, Min(0f)] private float jumpForce = 7f;
        [SerializeField] private Transform groundCheck;
        [SerializeField, Min(0.01f)] private float groundCheckRadius = 0.15f;
        [SerializeField] private LayerMask groundLayers;
        [Tooltip("Prevents rapid left/right flickering when directly above the player.")]
        [SerializeField, Min(0f)] private float horizontalChaseDeadZone = 0.05f;

        private float chaseDirection;
        private bool leftGroundDuringAttack;
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
            }

            commandedMoveDirection = direction;
            SetHorizontalVelocity(direction);

            bool grounded = IsGrounded();

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
