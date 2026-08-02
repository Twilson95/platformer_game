using System.Collections.Generic;
using PlatformerGame.Enemies;
using UnityEngine;

namespace PlatformerGame.Player
{
    /// <summary>
    /// Damages each enemy at most once during a single sword swing.
    /// Attach this to the sword object alongside a trigger collider.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class SwordHitbox : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float damage = 1f;

        private readonly HashSet<Enemy> enemiesHitThisSwing = new();

        private void Awake()
        {
            Collider2D hitbox = GetComponent<Collider2D>();
            hitbox.isTrigger = true;
        }

        private void OnEnable()
        {
            enemiesHitThisSwing.Clear();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            Enemy enemy = other.GetComponentInParent<Enemy>();
            if (enemy == null || !enemy.IsAlive || enemiesHitThisSwing.Contains(enemy))
            {
                return;
            }

            enemiesHitThisSwing.Add(enemy);
            enemy.TakeDamage(damage);
        }
    }
}
