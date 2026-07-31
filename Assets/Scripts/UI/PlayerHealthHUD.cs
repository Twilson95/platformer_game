using PlatformerGame.Player;
using UnityEngine;
using UnityEngine.UI;

namespace PlatformerGame.UI
{
    /// <summary>
    /// Creates and maintains a top-left player health bar.
    /// Add this component to an empty GameObject in the scene.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasScaler))]
    public sealed class PlayerHealthHUD : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private PlayerHealth playerHealth;

        [Header("Layout")]
        [SerializeField] private Vector2 screenOffset = new Vector2(24f, -24f);
        [SerializeField] private Vector2 barSize = new Vector2(260f, 34f);

        [Header("Appearance")]
        [SerializeField] private Color backgroundColor =
            new Color(0.08f, 0.08f, 0.08f, 0.9f);
        [SerializeField] private Color healthyColor =
            new Color(0.15f, 0.8f, 0.25f);
        [SerializeField] private Color lowHealthColor =
            new Color(0.9f, 0.12f, 0.1f);

        private Image healthFill;
        private RectTransform healthFillRect;
        private Text healthText;

        private void Awake()
        {
            ConfigureCanvas();
            BuildHUD();
        }

        private void Start()
        {
            FindAndBindPlayer();
        }

        private void OnDestroy()
        {
            UnbindPlayer();
        }

        private void ConfigureCanvas()
        {
            Canvas canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        private void BuildHUD()
        {
            RectTransform panel = CreateRect("Health Panel", transform);
            panel.anchorMin = new Vector2(0f, 1f);
            panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 1f);
            panel.anchoredPosition = screenOffset;
            panel.sizeDelta = barSize;

            Image background = panel.gameObject.AddComponent<Image>();
            background.color = backgroundColor;

            healthFillRect = CreateRect("Health Fill", panel);
            healthFillRect.anchorMin = new Vector2(0f, 0.5f);
            healthFillRect.anchorMax = new Vector2(0f, 0.5f);
            healthFillRect.pivot = new Vector2(0f, 0.5f);
            healthFillRect.anchoredPosition = new Vector2(4f, 0f);
            healthFillRect.sizeDelta =
                new Vector2(barSize.x - 8f, barSize.y - 8f);

            healthFill = healthFillRect.gameObject.AddComponent<Image>();
            healthFill.color = healthyColor;

            RectTransform textRect = CreateRect("Health Text", panel);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            healthText = textRect.gameObject.AddComponent<Text>();
            healthText.font =
                Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            healthText.fontSize = 20;
            healthText.fontStyle = FontStyle.Bold;
            healthText.alignment = TextAnchor.MiddleCenter;
            healthText.color = Color.white;
            healthText.text = "Health";
        }

        private void FindAndBindPlayer()
        {
            if (playerHealth == null)
            {
                playerHealth = Object.FindAnyObjectByType<PlayerHealth>();
            }

            if (playerHealth == null)
            {
                healthText.text = "Player not found";
                Debug.LogWarning(
                    $"{name}: PlayerHealthHUD could not find PlayerHealth.",
                    this);
                return;
            }

            playerHealth.HealthChanged += UpdateDisplay;
            UpdateDisplay(playerHealth.CurrentHealth, playerHealth.MaxHealth);
        }

        private void UnbindPlayer()
        {
            if (playerHealth != null)
            {
                playerHealth.HealthChanged -= UpdateDisplay;
            }
        }

        private void UpdateDisplay(float currentHealth, float maxHealth)
        {
            float normalized = maxHealth > 0f
                ? Mathf.Clamp01(currentHealth / maxHealth)
                : 0f;

            healthFillRect.sizeDelta = new Vector2(
                Mathf.Max(0f, barSize.x - 8f) * normalized,
                Mathf.Max(0f, barSize.y - 8f));
            healthFill.color =
                Color.Lerp(lowHealthColor, healthyColor, normalized);
            healthText.text =
                $"Health: {Mathf.CeilToInt(currentHealth)} / " +
                $"{Mathf.CeilToInt(maxHealth)}";
        }

        private static RectTransform CreateRect(
            string objectName,
            Transform parent)
        {
            GameObject child = new GameObject(
                objectName,
                typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return child.GetComponent<RectTransform>();
        }
    }
}
