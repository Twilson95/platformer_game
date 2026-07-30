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
        [SerializeField, Min(0.1f)] private float jumpInterval = 2f;
        [SerializeField] private Transform groundCheck;
        [SerializeField, Min(0.01f)] private float groundCheckRadius = 0.15f;
        [SerializeField] private LayerMask groundLayers;

        private float nextJumpTime;

        protected override void Awake()
        {
            base.Awake();
            patrolDirection = Mathf.Sign(
                Mathf.Approximately(patrolDirection, 0f) ? 1f : patrolDirection);
        }

        protected override void Move()
        {
            SetHorizontalVelocity(patrolDirection);

            if (Time.time >= nextJumpTime && IsGrounded())
            {
                UsePrimaryAbility();
            }
        }

        public override void UsePrimaryAbility()
        {
            if (!IsGrounded())
            {
                return;
            }

            ApplyVerticalImpulse(jumpForce);
            nextJumpTime = Time.time + jumpInterval;
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

        private void OnDrawGizmosSelected()
        {
            if (groundCheck == null)
            {
                return;
            }

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
}
