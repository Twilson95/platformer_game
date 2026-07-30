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

        private float chaseDirection;
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
        }

        public override void UsePrimaryAbility()
        {
            // Walking enemies deliberately have no special movement ability.
        }

        public void ReversePatrolDirection()
        {
            patrolDirection *= -1f;
        }
    }
}
