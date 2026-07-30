using UnityEngine;

namespace PlatformerGame.Enemies
{
    /// <summary>
    /// A flying enemy that moves between two points and can dash at a target.
    /// </summary>
    public sealed class FlyingEnemy : Enemy
    {
        [Header("Flying enemy")]
        [SerializeField] private Transform pointA;
        [SerializeField] private Transform pointB;
        [SerializeField] private Transform abilityTarget;
        [SerializeField, Min(1f)] private float dashMultiplier = 2f;
        [SerializeField, Min(0.05f)] private float arrivalDistance = 0.15f;

        private Transform currentDestination;

        protected override void Awake()
        {
            base.Awake();
            Body.gravityScale = 0f;
            currentDestination = pointB != null ? pointB : pointA;
        }

        protected override void Move()
        {
            if (IsAggroed && PlayerTarget != null)
            {
                MoveTowards(PlayerTarget.position);
                return;
            }

            if (currentDestination == null)
            {
                Body.linearVelocity = Vector2.zero;
                return;
            }

            MoveTowards(currentDestination.position);

            if (Vector2.Distance(Body.position, currentDestination.position) <= arrivalDistance)
            {
                currentDestination = currentDestination == pointA ? pointB : pointA;
            }
        }

        public override void UsePrimaryAbility()
        {
            Transform target = PlayerTarget != null ? PlayerTarget : abilityTarget;
            if (target == null)
            {
                return;
            }

            Vector2 dashDirection =
                ((Vector2)target.position - Body.position).normalized;
            Body.linearVelocity = dashDirection * MoveSpeed * dashMultiplier;
        }
    }
}
