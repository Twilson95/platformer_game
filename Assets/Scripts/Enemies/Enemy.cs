using System.Collections.Generic;
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
        [Tooltip("Terrain layers that must not block attacks from reaching the player.")]
        [SerializeField] private LayerMask terrainLayers;

        [Header("Attack")]
        [SerializeField, Min(0f)] private float attackDamage = 1f;
        [SerializeField, Min(0f)] private float attackCooldown = 2f;
        [SerializeField, Min(0f)] private float attackWindupDuration = 0.4f;
        [Tooltip("Assign a child containing the enemy sprite for a visual-only shake.")]
        [SerializeField] private Transform attackVisual;
        [SerializeField, Min(0f)] private float windupVibrationDistance = 0.06f;

        [Header("Runtime Debug (read only during play)")]
        [SerializeField] private bool aggroActive;
        [SerializeField] private bool attackWindingUp;
        [SerializeField] private bool attackActive;
        [SerializeField] private string currentAiState = "Searching for player";
        [SerializeField] private bool logAggroChanges;

        private float attackEndTime;
        private float windupEndTime;
        private float pendingAttackDuration;
        private float nextAttackTime;
        private float nextTargetSearchTime;
        private bool hasWarnedAboutMissingPlayer;
        private readonly HashSet<PlayerHealth> playersHitThisAttack = new();
        private Vector3 attackVisualRestPosition;

        protected Rigidbody2D Body { get; private set; }
        protected float MoveSpeed => moveSpeed;
        protected Transform PlayerTarget => playerTarget;
        protected bool IsAggroed => aggroActive;
        protected bool IsAttacking => attackActive;
        protected bool IsWindingUp => attackWindingUp;
        protected bool CanStartAttack =>
            IsAlive &&
            !attackActive &&
            !attackWindingUp &&
            Time.time >= nextAttackTime;
        public bool HasAggro => aggroActive;
        public bool AttackWindingUp => attackWindingUp;
        public bool AttackActive => attackActive;
        public Transform CurrentTarget => playerTarget;
        public float CurrentHealth { get; private set; }
        public bool IsAlive => CurrentHealth > 0f;

        protected virtual void Awake()
        {
            Body = GetComponent<Rigidbody2D>();
            CurrentHealth = maxHealth;
            FindAttackVisual();
            FindPlayer();
        }

        protected virtual void FixedUpdate()
        {
            if (IsAlive)
            {
                UpdateAttack();
                UpdateAggro();

                if (attackWindingUp)
                {
                    currentAiState = "Telegraphing attack";
                    Body.linearVelocity = Vector2.zero;
                    return;
                }

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

        protected void SetHorizontalVelocity(
            float direction,
            float speedMultiplier = 1f)
        {
            float normalizedDirection = Mathf.Clamp(direction, -1f, 1f);
            Body.linearVelocity = new Vector2(
                normalizedDirection * moveSpeed * Mathf.Max(0f, speedMultiplier),
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

        /// <summary>
        /// Starts the telegraph. The damaging attack window opens after the
        /// windup. A duration of zero keeps that window open until EndAttack.
        /// </summary>
        protected bool BeginAttack(float duration = 0f)
        {
            if (!CanStartAttack || !HasClearAttackPath())
            {
                return false;
            }

            attackWindingUp = attackWindupDuration > 0f;
            attackActive = !attackWindingUp;
            windupEndTime = Time.time + attackWindupDuration;
            pendingAttackDuration = duration;
            attackEndTime = attackActive && duration > 0f
                ? Time.time + duration
                : 0f;
            nextAttackTime = Time.time + attackCooldown;
            playersHitThisAttack.Clear();
            Body.linearVelocity = Vector2.zero;

            if (attackActive)
            {
                OnAttackStarted();
            }

            return true;
        }

        private bool HasClearAttackPath()
        {
            if (playerTarget == null)
            {
                return true;
            }

            RaycastHit2D terrainHit = Physics2D.Linecast(
                Body.position,
                playerTarget.position,
                terrainLayers);

            return terrainHit.collider == null;
        }

        /// <summary>
        /// Called when the telegraph finishes and the damaging window opens.
        /// </summary>
        protected virtual void OnAttackStarted()
        {
        }

        protected void EndAttack()
        {
            attackActive = false;
            attackEndTime = 0f;
        }

        protected virtual void Die()
        {
            CancelAttack();
            EndAttack();
            Body.linearVelocity = Vector2.zero;
            Destroy(gameObject);
        }

        private void UpdateAttack()
        {
            if (attackWindingUp)
            {
                Body.linearVelocity = Vector2.zero;
                ShakeAttackVisual();

                if (Time.time >= windupEndTime)
                {
                    attackWindingUp = false;
                    attackActive = true;
                    attackEndTime = pendingAttackDuration > 0f
                        ? Time.time + pendingAttackDuration
                        : 0f;
                    RestoreAttackVisual();
                    OnAttackStarted();
                }

                return;
            }

            if (attackActive && attackEndTime > 0f && Time.time >= attackEndTime)
            {
                EndAttack();
            }
        }

        private void CancelAttack()
        {
            attackWindingUp = false;
            attackActive = false;
            windupEndTime = 0f;
            attackEndTime = 0f;
            RestoreAttackVisual();
        }

        private void FindAttackVisual()
        {
            if (attackVisual == null)
            {
                SpriteRenderer sprite = GetComponentInChildren<SpriteRenderer>();
                if (sprite != null && sprite.transform != transform)
                {
                    attackVisual = sprite.transform;
                }
            }

            if (attackVisual != null)
            {
                attackVisualRestPosition = attackVisual.localPosition;
            }
        }

        private void ShakeAttackVisual()
        {
            if (attackVisual == null)
            {
                return;
            }

            Vector2 vibration =
                Random.insideUnitCircle * windupVibrationDistance;
            attackVisual.localPosition =
                attackVisualRestPosition + (Vector3)vibration;
        }

        private void RestoreAttackVisual()
        {
            if (attackVisual != null)
            {
                attackVisual.localPosition = attackVisualRestPosition;
            }
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

            PlayerHealth playerHealth =
                Object.FindAnyObjectByType<PlayerHealth>();
            if (playerHealth != null)
            {
                playerTarget = playerHealth.transform;
                hasWarnedAboutMissingPlayer = false;
                return;
            }

            Movement playerMovement =
                Object.FindAnyObjectByType<Movement>();
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

        private void OnDisable()
        {
            CancelAttack();
        }

        private void TryDamagePlayer(Collider2D other)
        {
            if (!IsAlive || !attackActive || attackDamage <= 0f)
            {
                return;
            }

            PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
            if (playerHealth == null || playersHitThisAttack.Contains(playerHealth))
            {
                return;
            }

            playerHealth.TakeDamage(attackDamage);
            playersHitThisAttack.Add(playerHealth);
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
