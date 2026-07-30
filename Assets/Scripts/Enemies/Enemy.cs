using UnityEngine;

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

        protected Rigidbody2D Body { get; private set; }
        protected float MoveSpeed => moveSpeed;
        public float CurrentHealth { get; private set; }
        public bool IsAlive => CurrentHealth > 0f;

        protected virtual void Awake()
        {
            Body = GetComponent<Rigidbody2D>();
            CurrentHealth = maxHealth;
        }

        protected virtual void FixedUpdate()
        {
            if (IsAlive)
            {
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

        private void FaceDirection(float horizontalDirection)
        {
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * Mathf.Sign(horizontalDirection);
            transform.localScale = scale;
        }
    }
}
