using System;
using UnityEngine;
using UnityEngine.Events;

namespace PlatformerGame.Player
{
    /// <summary>
    /// Attach to the player root object. Enemies find this component on contact
    /// and call TakeDamage.
    /// </summary>
    public sealed class PlayerHealth : MonoBehaviour
    {
        [Header("Health")]
        [SerializeField, Min(1f)] private float maxHealth = 5f;
        [SerializeField, Min(0f)] private float invulnerabilityDuration = 0.5f;

        [Header("Events")]
        [SerializeField] private UnityEvent onDamaged;
        [SerializeField] private UnityEvent onDied;

        private float nextDamageTime;

        public float MaxHealth => maxHealth;
        public float CurrentHealth { get; private set; }
        public bool IsAlive => CurrentHealth > 0f;
        public float NormalizedHealth =>
            maxHealth > 0f ? CurrentHealth / maxHealth : 0f;
        public event Action<float, float> HealthChanged;

        private void Awake()
        {
            CurrentHealth = maxHealth;
        }

        public void TakeDamage(float amount)
        {
            if (!IsAlive || amount <= 0f || Time.time < nextDamageTime)
            {
                return;
            }

            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            nextDamageTime = Time.time + invulnerabilityDuration;
            NotifyHealthChanged();
            onDamaged?.Invoke();

            if (!IsAlive)
            {
                Die();
            }
        }

        public void Heal(float amount)
        {
            if (!IsAlive || amount <= 0f)
            {
                return;
            }

            CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
            NotifyHealthChanged();
        }

        public void RestoreToFullHealth()
        {
            CurrentHealth = maxHealth;
            nextDamageTime = 0f;
            NotifyHealthChanged();
        }

        private void NotifyHealthChanged()
        {
            HealthChanged?.Invoke(CurrentHealth, maxHealth);
        }

        private void Die()
        {
            Rigidbody2D body = GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }

            Movement movement = GetComponent<Movement>();
            if (movement != null)
            {
                movement.enabled = false;
            }

            onDied?.Invoke();
        }
    }
}
