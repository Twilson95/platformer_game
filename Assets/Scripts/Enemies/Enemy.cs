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
        private enum EnemyAnimationState
        {
            Idle = 0,
            Jump = 1,
            Fall = 2
        }

        [Header("Shared attributes")]
        [SerializeField, Min(1f)] protected float maxHealth = 1f;
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
        [Header("Hit Reaction")]
        [Tooltip("Impulse applied away from the player when this enemy is hit.")]
        [SerializeField, Min(0f)] private float hitRecoilForce = 2f;
        [Tooltip("How long normal enemy movement is disabled after being hit.")]
        [SerializeField, Min(0f)] private float hitRecoilRecovery = 0.2f;
        [Tooltip("How quickly grounded recoil slows down, in world units per second.")]
        [SerializeField, Min(0f)] private float groundedRecoilDeceleration = 8f;
        [Tooltip("Assign a child containing the enemy sprite for a visual-only shake.")]
        [SerializeField] private Transform attackVisual;
        [SerializeField, Min(0f)] private float windupVibrationDistance = 0.06f;

        [Header("Animation")]
        [Tooltip("Animator on this enemy. Its controller should use the AnimationState integer parameter: 0 Idle, 1 Jump, 2 Fall.")]
        [SerializeField] private Animator enemyAnimator;

        [Header("State colliders")]
        [Tooltip("Collider used while the enemy is grounded.")]
        [SerializeField] private Collider2D idleCollider;
        [Tooltip("Collider used while the enemy is rising.")]
        [SerializeField] private Collider2D jumpCollider;
        [Tooltip("Collider used while the enemy is falling.")]
        [SerializeField] private Collider2D fallCollider;

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
        private EnemyAnimationState currentAnimationState;
        private Collider2D previouslyActiveCollider;
        private Vector2 pendingRecoilVelocity;
        private float recoilRecoveryRemaining;
        private bool stateColliderSuppressed;

        protected Rigidbody2D Body { get; private set; }
        protected float MoveSpeed => moveSpeed;
        protected Transform PlayerTarget => playerTarget;
        protected bool IsAggroed => aggroActive;
        protected bool IsAttacking => attackActive;
        protected bool IsWindingUp => attackWindingUp;
        protected virtual bool AlwaysAggro => false;
        protected virtual bool IsGroundedForAnimation => false;
        protected virtual float AttackCooldownMultiplier => 1f;
        protected Vector2 GroundCheckPosition
        {
            get
            {
                Collider2D activeCollider = GetActiveStateCollider();
                if (activeCollider != null)
                {
                    Bounds bounds = activeCollider.bounds;
                    return new Vector2(bounds.center.x, bounds.min.y);
                }

                return transform.position;
            }
        }
        protected bool CanStartAttack =>
            IsAlive &&
            !attackActive &&
            !attackWindingUp &&
            Time.time >= nextAttackTime;

        protected static float UpdateSteeringDirection(
            float currentDirection,
            float horizontalOffset,
            float deadZone)
        {
            if (Mathf.Approximately(currentDirection, 0f))
            {
                return Mathf.Sign(horizontalOffset);
            }

            // Keep moving in the current direction until the target has
            // crossed past it by the dead-zone distance. This prevents the
            // enemy from repeatedly reversing while passing directly over
            // the target.
            if (horizontalOffset * currentDirection < -deadZone)
            {
                return -currentDirection;
            }

            return currentDirection;
        }
        protected bool IsGroundedAtActiveColliderBottom(
            float checkRadius,
            LayerMask layers)
        {
            Collider2D activeCollider = GetActiveStateCollider();
            if (activeCollider == null)
            {
                return false;
            }

            Bounds bounds = activeCollider.bounds;
            Vector2 checkSize = new(
                Mathf.Max(bounds.size.x * 0.8f, checkRadius * 2f),
                checkRadius * 2f);
            Vector2 checkCenter = new(bounds.center.x, bounds.min.y);

            return Physics2D.OverlapBox(
                       checkCenter,
                       checkSize,
                       0f,
                       layers) != null;
        }

        protected void SuppressStateCollider(bool suppress)
        {
            stateColliderSuppressed = suppress;
            if (suppress)
            {
                SetColliderEnabled(idleCollider, false);
                SetColliderEnabled(jumpCollider, false);
                SetColliderEnabled(fallCollider, false);
                previouslyActiveCollider = null;
            }
        }
        public bool HasAggro => aggroActive;
        public bool AttackWindingUp => attackWindingUp;
        public bool AttackActive => attackActive;
        public Transform CurrentTarget => playerTarget;
        public float CurrentHealth { get; private set; }
        public bool IsAlive => CurrentHealth > 0f;

        protected virtual void Awake()
        {
            Body = GetComponent<Rigidbody2D>();
            if (enemyAnimator == null)
            {
                enemyAnimator = GetComponentInChildren<Animator>();
            }

            FindStateColliders();

            CurrentHealth = maxHealth;
            FindAttackVisual();
            FindPlayer();
        }

        protected virtual void FixedUpdate()
        {
            UpdateAnimationState();
            UpdateAnimationParameters();
            UpdateStateCollider();

            if (recoilRecoveryRemaining > 0f)
            {
                Body.linearVelocity += pendingRecoilVelocity;
                pendingRecoilVelocity = Vector2.zero;

                // Terrain uses low/no friction so enemies do not snag on
                // walls. Slow grounded recoil explicitly instead.
                if (Body.IsTouchingLayers(terrainLayers))
                {
                    float slowedX = Mathf.MoveTowards(
                        Body.linearVelocity.x,
                        0f,
                        groundedRecoilDeceleration * Time.fixedDeltaTime);
                    Body.linearVelocity = new Vector2(slowedX, Body.linearVelocity.y);
                }

                recoilRecoveryRemaining -= Time.fixedDeltaTime;
                return;
            }

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

        private void UpdateAnimationParameters()
        {
            if (enemyAnimator == null)
            {
                return;
            }

            enemyAnimator.SetInteger("AnimationState", (int)currentAnimationState);
            // Evaluate the animator immediately so the visual state changes in
            // the same physics tick as the matching state collider.
            enemyAnimator.Update(0f);
        }

        private void UpdateAnimationState()
        {
            if (IsGroundedForAnimation)
            {
                currentAnimationState = EnemyAnimationState.Idle;
            }
            else
            {
                currentAnimationState = Body.linearVelocity.y >= 0f
                    ? EnemyAnimationState.Jump
                    : EnemyAnimationState.Fall;
            }
        }

        private void FindStateColliders()
        {
            idleCollider ??= FindChildCollider("collider_idle");
            jumpCollider ??= FindChildCollider("collider_jump");
            fallCollider ??= FindChildCollider("collider_fall");
        }

        private Collider2D FindChildCollider(string childName)
        {
            Transform child = transform.Find(childName);
            return child != null ? child.GetComponent<Collider2D>() : null;
        }

        private void UpdateStateCollider()
        {
            if (stateColliderSuppressed)
            {
                return;
            }

            Collider2D activeCollider = GetActiveStateCollider();

            if (activeCollider == previouslyActiveCollider)
            {
                return;
            }

            bool hadPreviousCollider = previouslyActiveCollider != null;
            float previousBottom = hadPreviousCollider
                ? previouslyActiveCollider.bounds.min.y
                : 0f;

            SetColliderEnabled(idleCollider, activeCollider == idleCollider);
            SetColliderEnabled(jumpCollider, activeCollider == jumpCollider);
            SetColliderEnabled(fallCollider, activeCollider == fallCollider);

            if (activeCollider != null && hadPreviousCollider)
            {
                Physics2D.SyncTransforms();
                Body.position += Vector2.up * (previousBottom - activeCollider.bounds.min.y);
            }

            previouslyActiveCollider = activeCollider;
        }

        private Collider2D GetActiveStateCollider()
        {
            return currentAnimationState switch
            {
                EnemyAnimationState.Jump => jumpCollider,
                EnemyAnimationState.Fall => fallCollider,
                _ => idleCollider
            };
        }

        private static void SetColliderEnabled(Collider2D collider, bool enabled)
        {
            if (collider != null)
            {
                collider.enabled = enabled;
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
            TakeDamage(amount, Vector2.zero);
        }

        public virtual void TakeDamage(float amount, Vector2 hitDirection)
        {
            if (!IsAlive || amount <= 0f)
            {
                return;
            }

            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            ApplyHitRecoil(hitDirection);

            if (!IsAlive)
            {
                Die();
            }
        }

        private void ApplyHitRecoil(Vector2 hitDirection)
        {
            if (Body == null || hitRecoilForce <= 0f)
            {
                return;
            }

            if (hitDirection.sqrMagnitude < 0.001f)
            {
                hitDirection = playerTarget != null
                    ? Body.position - (Vector2)playerTarget.position
                    : Vector2.right * (transform.localScale.x >= 0f ? 1f : -1f);
            }

            Vector2 recoilVelocity = hitDirection.normalized * hitRecoilForce;
            if (hitRecoilRecovery > 0f)
            {
                // A hit interrupts any attack windup so a jumping enemy
                // cannot launch after being knocked airborne.
                CancelAttack();
                pendingRecoilVelocity += recoilVelocity;
                recoilRecoveryRemaining = hitRecoilRecovery;
            }
            else
            {
                Body.linearVelocity += recoilVelocity;
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
            nextAttackTime = Time.time + attackCooldown * AttackCooldownMultiplier;
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

        protected void CancelAttack()
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

            if (AlwaysAggro)
            {
                SetAggro(true);
                currentAiState = "Always aggroed";
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

        protected void FaceDirection(float horizontalDirection)
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
