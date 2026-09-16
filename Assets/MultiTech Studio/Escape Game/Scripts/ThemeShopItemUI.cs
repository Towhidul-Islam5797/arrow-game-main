using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MultiTechStudio.EscapeGame
{
    /// <summary>
    /// UI component for individual theme shop items. Handles display and interaction for a single theme.
    /// </summary>
    public class ThemeShopItemUI : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("Image component to display theme icon")]
        [SerializeField] private Image themeIconImage;

        [Tooltip("Text component to display theme name")]
        [SerializeField] private TextMeshProUGUI themeNameText;

        [Tooltip("Text component to display price")]
        [SerializeField] private TextMeshProUGUI buttonText;

        [Tooltip("Button to purchase/use theme")]
        [SerializeField] private Button purchaseButton;

        [Tooltip("Optional: GameObject to show when theme is active")]
        [SerializeField] private GameObject activeIndicator;
        [SerializeField] private GameObject coinIcon;

        private ThemeData currentTheme;
        private ThemeShopManager shopManager;
        private bool isPurchased;

        /// <summary>
        /// Initializes the theme item UI with theme data.
        /// </summary>
        public void Initialize(ThemeData theme, bool purchased, ThemeShopManager manager)
        {
            currentTheme = theme;
            shopManager = manager;
            isPurchased = purchased;

            UpdateUI();
        }

        /// <summary>
        /// Updates all UI elements based on current state.
        /// </summary>
        private void UpdateUI()
        {
            if (currentTheme == null) return;

            // Set theme icon
            if (themeIconImage != null && currentTheme.themeIcon != null)
            {
                themeIconImage.sprite = currentTheme.themeIcon;
            }

            // Set theme name
            if (themeNameText != null)
            {
                themeNameText.text = currentTheme.themeName;
            }

            // Update purchase button
            if (purchaseButton != null)
            {
                // Remove existing listeners
                purchaseButton.onClick.RemoveAllListeners();

                if (isPurchased)
                {
                    // Show "Use" button
                    if (buttonText != null)
                        buttonText.text = "USE";
                    coinIcon.SetActive(false);
                    purchaseButton.onClick.AddListener(OnUseButtonClicked);
                }
                else
                {
                    if (buttonText != null)
                        buttonText.text = $"{currentTheme.price}";
                    coinIcon.SetActive(true);
                    purchaseButton.onClick.AddListener(OnBuyButtonClicked);
                }
            }
        }

        /// <summary>
        /// Updates the purchase status and refreshes UI.
        /// </summary>
        public void UpdatePurchaseStatus(bool purchased)
        {
            isPurchased = purchased;
            UpdateUI();
        }

        /// <summary>
        /// Sets the active indicator visibility.
        /// </summary>
        public void SetActiveIndicator(bool isActive)
        {
            if (activeIndicator != null)
            {
                activeIndicator.SetActive(isActive);
            }
        }

        /// <summary>
        /// Called when Buy button is clicked.
        /// </summary>
        private void OnBuyButtonClicked()
        {
            if (shopManager == null || currentTheme == null) return;

            bool success = shopManager.PurchaseTheme(currentTheme);

            if (success)
            {
                // If purchase successful, update UI to show "Use" button
                UpdatePurchaseStatus(true);

                // Automatically use the theme after purchase
                OnUseButtonClicked();
            }
        }

        /// <summary>
        /// Called when Use button is clicked.
        /// </summary>
        private void OnUseButtonClicked()
        {
            if (shopManager == null || currentTheme == null) return;

            if (isPurchased)
            {
                shopManager.UseTheme(currentTheme);
            }
            else
            {
                // If not purchased, try to purchase first
                OnBuyButtonClicked();
            }
        }
    }
}

