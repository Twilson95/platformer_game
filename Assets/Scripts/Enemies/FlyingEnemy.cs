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
        [SerializeField, Min(0f)] private float chargeRange = 3f;
        [SerializeField, Min(0.05f)] private float chargeDuration = 0.5f;
        [SerializeField, Min(0.05f)] private float arrivalDistance = 0.15f;
        [Tooltip("Minimum height kept above ground outside of a charge attack.")]
        [SerializeField, Min(0f)] private float groundClearance = 0.75f;
        [SerializeField] private LayerMask groundLayers;

        private Transform currentDestination;
        private Vector2 chargeDirection;

        protected override void Awake()
        {
            base.Awake();
            Body.gravityScale = 0f;
            currentDestination = pointB != null ? pointB : pointA;
        }

        protected override void Move()
        {
            if (IsAttacking)
            {
                Body.linearVelocity = chargeDirection * MoveSpeed * dashMultiplier;
                return;
            }

            if (IsAggroed && PlayerTarget != null)
            {
                if (CanStartAttack &&
                    Vector2.Distance(Body.position, PlayerTarget.position) <=
                    chargeRange)
                {
                    UsePrimaryAbility();
                    return;
                }

                MoveWithGroundClearance(PlayerTarget.position);
                return;
            }

            if (currentDestination == null)
            {
                Body.linearVelocity = Vector2.zero;
                return;
            }

            MoveWithGroundClearance(currentDestination.position);

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

            chargeDirection =
                ((Vector2)target.position - Body.position).normalized;

            BeginAttack(chargeDuration);
        }

        protected override void OnAttackStarted()
        {
            Body.linearVelocity =
                chargeDirection * MoveSpeed * dashMultiplier;
        }

        private void MoveWithGroundClearance(Vector2 destination)
        {
            RaycastHit2D groundHit = Physics2D.Raycast(
                Body.position,
                Vector2.down,
                Mathf.Infinity,
                groundLayers);

            if (groundHit.collider != null)
            {
                float minimumHeight = groundHit.point.y + groundClearance;
                destination.y = Mathf.Max(destination.y, minimumHeight);
            }

            MoveTowards(destination);

            if (groundHit.collider == null || Body.linearVelocity.y >= 0f)
            {
                return;
            }

            float distanceAboveMinimum =
                Body.position.y - (groundHit.point.y + groundClearance);
            float maximumDownwardSpeed =
                Mathf.Max(0f, distanceAboveMinimum) / Time.fixedDeltaTime;

            Body.linearVelocity = new Vector2(
                Body.linearVelocity.x,
                Mathf.Max(Body.linearVelocity.y, -maximumDownwardSpeed));
        }
    }
}
