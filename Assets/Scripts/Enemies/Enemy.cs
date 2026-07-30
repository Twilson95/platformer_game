using UnityEngine;
using PlatformerGame.Player;

namespace PlatformerGame.Enemies
{
    /// <summary>
    /// Shared foundation for every enemy. Concrete enemies must provide their
    /// own movement and primary ability.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public abstract class Enemy : MonoBehaviour
    {
        [Header("Shared attributes")]
        [SerializeField, Min(1f)] private float maxHealth = 1f;
        [SerializeField, Min(0f)] private float moveSpeed = 3f;

        [Header("Aggro")]
        [SerializeField] private Transform playerTarget;
        [SerializeField, Min(0f)] private float aggroRange = 6f;
        [Tooltip("Shows this enemy's aggro ranges in the Scene view even when it is not selected.")]
        [SerializeField] private bool alwaysShowAggroRange;

        [Header("Contact damage")]
        [SerializeField, Min(0f)] private float contactDamage = 1f;
        [SerializeField, Min(0.05f)] private float damageCooldown = 0.75f;

        [Header("Runtime Debug (read only during play)")]
        [SerializeField] private bool aggroActive;
        [SerializeField] private string currentAiState = "Searching for player";
        [SerializeField] private bool logAggroChanges;

        private float nextDamageTime;
        private float nextTargetSearchTime;
        private bool hasWarnedAboutMissingPlayer;

        protected Rigidbody2D Body { get; private set; }
        protected float MoveSpeed => moveSpeed;
        protected Transform PlayerTarget => playerTarget;
        protected bool IsAggroed => aggroActive;
        public bool HasAggro => aggroActive;
        public Transform CurrentTarget => playerTarget;
        public float CurrentHealth { get; private set; }
        public bool IsAlive => CurrentHealth > 0f;

        protected virtual void Awake()
        {
            Body = GetComponent<Rigidbody2D>();
            CurrentHealth = maxHealth;
            FindPlayer();
        }

        protected virtual void FixedUpdate()
        {
            if (IsAlive)
            {
                UpdateAggro();
                Move();
            }
        }

        /// <summary>
        /// Called every physics tick. Each enemy type must implement its
        /// characteristic movement here.
        /// </summary>
        protected abstract void Move();

        /// <summary>
        /// Each enemy type must expose its characteristic ability.
        /// An AI/state machine can call this when the ability should be used.
        /// </summary>
        public abstract void UsePrimaryAbility();

        public virtual void TakeDamage(float amount)
        {
            if (!IsAlive || amount <= 0f)
            {
                return;
            }

            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);

            if (!IsAlive)
            {
                Die();
            }
        }

        protected void SetHorizontalVelocity(float direction)
        {
            float normalizedDirection = Mathf.Clamp(direction, -1f, 1f);
            Body.linearVelocity = new Vector2(
                normalizedDirection * moveSpeed,
                Body.linearVelocity.y);

            if (!Mathf.Approximately(normalizedDirection, 0f))
            {
                FaceDirection(normalizedDirection);
            }
        }

        protected void MoveTowards(Vector2 destination)
        {
            Vector2 direction = (destination - Body.position).normalized;
            Body.linearVelocity = direction * moveSpeed;

            if (!Mathf.Approximately(direction.x, 0f))
            {
                FaceDirection(direction.x);
            }
        }

        protected void ApplyVerticalImpulse(float force)
        {
            Body.AddForce(Vector2.up * force, ForceMode2D.Impulse);
        }

        protected virtual void Die()
        {
            Body.linearVelocity = Vector2.zero;
            Destroy(gameObject);
        }

        private void UpdateAggro()
        {
            if (playerTarget == null)
            {
                if (Time.time >= nextTargetSearchTime)
                {
                    FindPlayer();
                    nextTargetSearchTime = Time.time + 1f;
                }

                SetAggro(false);
                currentAiState = "Searching for player";
                return;
            }

            // Aggro remains latched until ClearAggro is called. This prevents
            // an enemy forgetting the player when either character jumps.
            if (aggroActive)
            {
                currentAiState = "Chasing player";
                return;
            }

            float distanceSquared =
                ((Vector2)playerTarget.position - Body.position).sqrMagnitude;
            SetAggro(distanceSquared <= aggroRange * aggroRange);
            currentAiState = aggroActive
                ? "Chasing player"
                : "Player outside aggro range";
        }

        public void ClearAggro()
        {
            SetAggro(false);
            currentAiState = "Aggro cleared";
        }

        private void SetAggro(bool active)
        {
            if (aggroActive == active)
            {
                return;
            }

            aggroActive = active;

            if (logAggroChanges)
            {
                Debug.Log(
                    $"{name}: aggro {(active ? "activated" : "cleared")}. " +
                    $"Target: {(playerTarget != null ? playerTarget.name : "none")}",
                    this);
            }
        }

        private void FindPlayer()
        {
            // Preserve a target explicitly assigned in the Inspector.
            if (playerTarget != null)
            {
                hasWarnedAboutMissingPlayer = false;
                return;
            }

            PlayerHealth playerHealth = FindFirstObjectByType<PlayerHealth>();
            if (playerHealth != null)
            {
                playerTarget = playerHealth.transform;
                hasWarnedAboutMissingPlayer = false;
                return;
            }

            Movement playerMovement = FindFirstObjectByType<Movement>();
            if (playerMovement != null)
            {
                playerTarget = playerMovement.transform;
                hasWarnedAboutMissingPlayer = false;
                return;
            }

            GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
            playerTarget = taggedPlayer != null ? taggedPlayer.transform : null;

            if (playerTarget == null && !hasWarnedAboutMissingPlayer)
            {
                Debug.LogWarning(
                    $"{name}: no player target was found. Assign Player Target, " +
                    "add PlayerHealth to the player, or tag the player as 'Player'.",
                    this);
                hasWarnedAboutMissingPlayer = true;
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            TryDamagePlayer(collision.collider);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            TryDamagePlayer(collision.collider);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryDamagePlayer(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            TryDamagePlayer(other);
        }

        private void TryDamagePlayer(Collider2D other)
        {
            if (!IsAlive || contactDamage <= 0f || Time.time < nextDamageTime)
            {
                return;
            }

            PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
            if (playerHealth == null)
            {
                return;
            }

            playerHealth.TakeDamage(contactDamage);
            nextDamageTime = Time.time + damageCooldown;
        }

        private void FaceDirection(float horizontalDirection)
        {
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * Mathf.Sign(horizontalDirection);
            transform.localScale = scale;
        }

        protected virtual void OnDrawGizmosSelected()
        {
            DrawAggroRanges();
        }

        protected virtual void OnDrawGizmos()
        {
            if (alwaysShowAggroRange)
            {
                DrawAggroRanges();
            }
        }

        private void DrawAggroRanges()
        {
            Gizmos.color = Application.isPlaying && aggroActive
                ? Color.green
                : Color.red;
            Gizmos.DrawWireSphere(transform.position, aggroRange);
        }
    }
}
