using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MultiTechStudio.EscapeGame
{
    /// <summary>
    /// Manages the theme shop UI. Handles theme purchasing, display, and application.
    /// </summary>
    public class ThemeShopManager : MonoBehaviour
    {
        [Header("Theme Configuration")]
        [Tooltip("List of all available themes for purchase")]
        [SerializeField] private List<ThemeData> availableThemes = new List<ThemeData>();

        [Header("UI References")]
        [Tooltip("Prefab for individual theme shop item UI")]
        [SerializeField] private GameObject themeItemPrefab;

        [Tooltip("Parent transform where theme items will be instantiated (e.g., ScrollView Content)")]
        [SerializeField] private Transform themeItemsParent;

        [Header("References")]
        [Tooltip("Reference to UiManager for coin management")]
        [SerializeField] private UiManager uiManager;

        [Tooltip("Reference to ThemeManager for applying themes")]
        [SerializeField] private ThemeManager themeManager;

        private Dictionary<ThemeData, GameObject> themeItemUIs = new Dictionary<ThemeData, GameObject>();

        private void Start()
        {
            // Auto-find references if not assigned
            if (uiManager == null)
                uiManager = FindFirstObjectByType<UiManager>();

            if (ThemeManager.Instance == null)
                themeManager = FindFirstObjectByType<ThemeManager>();

            // Initialize themes that are free (price = 0) as purchased
            InitializeFreeThemes();

            // Create UI for all themes
            CreateThemeItems();
        }

        /// <summary>
        /// Initializes free themes (price = 0) as already purchased.
        /// </summary>
        private void InitializeFreeThemes()
        {
            foreach (var theme in availableThemes)
            {
                if (theme != null && theme.price == 0)
                {
                    MarkThemeAsPurchased(theme);
                }
            }
        }

        /// <summary>
        /// Creates UI items for all available themes.
        /// </summary>
        public void CreateThemeItems()
        {
            if (themeItemPrefab == null)
            {
                Debug.LogError("[ThemeShopManager] Theme Item Prefab is not assigned!");
                return;
            }

            if (themeItemsParent == null)
            {
                Debug.LogError("[ThemeShopManager] Theme Items Parent is not assigned!");
                return;
            }

            // Clear existing items
            foreach (var kvp in themeItemUIs)
            {
                if (kvp.Value != null)
                    Destroy(kvp.Value);
            }
            themeItemUIs.Clear();

            // Create UI for each theme
            foreach (var theme in availableThemes)
            {
                if (theme == null) continue;

                GameObject itemUI = Instantiate(themeItemPrefab, themeItemsParent);
                ThemeShopItemUI itemComponent = itemUI.GetComponent<ThemeShopItemUI>();

                if (itemComponent == null)
                {
                    Debug.LogWarning($"[ThemeShopManager] Theme Item Prefab does not have ThemeShopItemUI component. Adding it...");
                    itemComponent = itemUI.AddComponent<ThemeShopItemUI>();
                }

                // Initialize the item UI
                bool isPurchased = IsThemePurchased(theme);
                itemComponent.Initialize(theme, isPurchased, this);

                themeItemUIs[theme] = itemUI;
                if (ThemeManager.Instance.currentThemeAsset.ActiveTheme == theme)
                    itemComponent.SetActiveIndicator(true);
            }
        }

        /// <summary>
        /// Checks if a theme is already purchased.
        /// </summary>
        public bool IsThemePurchased(ThemeData theme)
        {
            if (theme == null) return false;
            string key = GetPurchaseKey(theme);
            return PlayerPrefs.GetInt(key, 0) == 1;
        }

        /// <summary>
        /// Marks a theme as purchased.
        /// </summary>
        public void MarkThemeAsPurchased(ThemeData theme)
        {
            if (theme == null) return;
            string key = GetPurchaseKey(theme);
            PlayerPrefs.SetInt(key, 1);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Gets the PlayerPrefs key for a theme's purchase status.
        /// </summary>
        private string GetPurchaseKey(ThemeData theme)
        {
            return $"Theme_Purchased_{theme.name}";
        }

        /// <summary>
        /// Attempts to purchase a theme with coins.
        /// </summary>
        public bool PurchaseTheme(ThemeData theme)
        {
            if (theme == null)
            {
                Debug.LogWarning("[ThemeShopManager] Cannot purchase null theme.");
                return false;
            }

            if (IsThemePurchased(theme))
            {
                Debug.Log($"[ThemeShopManager] Theme '{theme.themeName}' is already purchased.");
                return true;
            }

            if (uiManager == null)
            {
                Debug.LogError("[ThemeShopManager] UiManager is not assigned!");
                return false;
            }

            // Check if player has enough coins
            if (uiManager.coins < theme.price)
            {
                Debug.Log($"[ThemeShopManager] Not enough coins to purchase '{theme.themeName}'. Required: {theme.price}, Have: {uiManager.coins}");
                return false;
            }

            // Spend coins
            uiManager.SpendCoins(theme.price);

            // Mark as purchased
            MarkThemeAsPurchased(theme);

            // Update UI
            if (themeItemUIs.TryGetValue(theme, out GameObject itemUI))
            {
                ThemeShopItemUI itemComponent = itemUI.GetComponent<ThemeShopItemUI>();
                if (itemComponent != null)
                {
                    itemComponent.UpdatePurchaseStatus(true);
                }
            }

            Debug.Log($"[ThemeShopManager] Successfully purchased theme '{theme.themeName}' for {theme.price} coins.");
            return true;
        }

        /// <summary>
        /// Uses/applies a theme. Called when player clicks "Use" button on a purchased theme.
        /// </summary>
        public void UseTheme(ThemeData theme)
        {
            if (theme == null)
            {
                Debug.LogWarning("[ThemeShopManager] Cannot use null theme.");
                return;
            }

            if (!IsThemePurchased(theme))
            {
                Debug.LogWarning($"[ThemeShopManager] Theme '{theme.themeName}' is not purchased yet.");
                return;
            }

            // Apply theme using ThemeManager
            if (themeManager != null)
            {
                themeManager.ApplyTheme(theme);
            }
            else
            {
                Debug.LogWarning("[ThemeShopManager] ThemeManager is not assigned. Cannot apply theme.");
            }

            // Update all UI items to show which theme is currently active
            UpdateActiveThemeIndicator(theme);
        }

        /// <summary>
        /// Updates the UI to show which theme is currently active.
        /// </summary>
        private void UpdateActiveThemeIndicator(ThemeData activeTheme)
        {
            foreach (var kvp in themeItemUIs)
            {
                ThemeShopItemUI itemComponent = kvp.Value.GetComponent<ThemeShopItemUI>();
                if (itemComponent != null)
                {
                    bool isActive = kvp.Key == activeTheme;
                    itemComponent.SetActiveIndicator(isActive);
                }
            }
        }

        /// <summary>
        /// Refreshes all theme item UIs (useful when coins change).
        /// </summary>
        public void RefreshThemeItems()
        {
            foreach (var kvp in themeItemUIs)
            {
                ThemeShopItemUI itemComponent = kvp.Value.GetComponent<ThemeShopItemUI>();
                if (itemComponent != null)
                {
                    bool isPurchased = IsThemePurchased(kvp.Key);
                    itemComponent.UpdatePurchaseStatus(isPurchased);
                }
            }
        }
    }
}

