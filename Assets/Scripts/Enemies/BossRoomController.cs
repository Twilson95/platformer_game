using UnityEngine;

namespace PlatformerGame.Enemies
{
    /// <summary>
    /// Starts a boss encounter when the player enters its trigger and opens
    /// the room again when the assigned boss is defeated.
    /// </summary>
    public sealed class BossRoomController : MonoBehaviour
    {
        [SerializeField] private BossEnemy boss;
        [SerializeField] private GameObject[] entranceBlockers;
        [SerializeField] private bool startWithBlockersClosed;

        private bool encounterStarted;

        private void Awake()
        {
            SetBlockers(startWithBlockersClosed);
        }

        private void OnEnable()
        {
            if (boss != null)
            {
                boss.Defeated += OnBossDefeated;
            }
        }

        private void OnDisable()
        {
            if (boss != null)
            {
                boss.Defeated -= OnBossDefeated;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (encounterStarted || boss == null ||
                other.GetComponentInParent<Player.Movement>() == null)
            {
                return;
            }

            encounterStarted = true;
            SetBlockers(true);
            boss.BeginEncounter();
        }

        public void OnBossDefeated()
        {
            SetBlockers(false);
        }

        private void SetBlockers(bool closed)
        {
            if (entranceBlockers == null)
            {
                return;
            }

            foreach (GameObject blocker in entranceBlockers)
            {
                if (blocker != null)
                {
                    blocker.SetActive(closed);
                }
            }
        }
    }
}
