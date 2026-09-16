using UnityEngine;
using UnityEngine.UI;

namespace MultiTechStudio.EscapeGame
{
    /// <summary>
    /// Manages theme application at runtime. Applies theme settings from CurrentTheme asset.
    /// </summary>
    public class ThemeManager : MonoBehaviour
    {
        public static ThemeManager Instance { get; private set; }
        [Header("Theme Reference")]
        [Tooltip("The CurrentTheme ScriptableObject that holds the active theme")]
        [SerializeField] public CurrentTheme currentThemeAsset;

        [Header("Scene References")]
        [Tooltip("LevelManager to apply arrow prefab")]
        [SerializeField] private LevelManager levelManager;

        [Tooltip("GridManager to apply grid icon")]
        [SerializeField] private GridManager gridManager;

        [Tooltip("Background SpriteRenderer (optional)")]
        [SerializeField] private SpriteRenderer backgroundRenderer;

        [Tooltip("Theme Icon Image component (optional)")]
        [SerializeField] private Image themeIconImage;

        [Header("Auto Apply")]
        [Tooltip("Automatically apply theme on Start")]
        [SerializeField] private bool applyOnStart = true;

        private void Awake()
        {
            Instance = this;
        }

        // private void OnEnable()
        // {
        //     if (currentThemeAsset != null)
        //     {
        //         currentThemeAsset.OnThemeChanged += ApplyTheme;
        //     }
        // }

        // private void OnDisable()
        // {
        //     if (currentThemeAsset != null)
        //     {
        //         currentThemeAsset.OnThemeChanged -= ApplyTheme;
        //     }
        // }

        private void Start()
        {
            if (levelManager == null)
                levelManager = LevelManager.Instance;
            if (applyOnStart && currentThemeAsset != null && currentThemeAsset.ActiveTheme != null)
            {
                ApplyTheme(currentThemeAsset.ActiveTheme);
            }
        }

        /// <summary>
        /// Applies the given theme to all relevant scene objects.
        /// </summary>
        public void ApplyTheme(ThemeData theme)
        {
            if (theme == null)
            {
                Debug.LogWarning("[ThemeManager] Cannot apply null theme.");
                return;
            }
            currentThemeAsset.ActiveTheme = theme;

            bool anyApplied = false;

            // Apply arrow prefab
            if (theme.arrowPrefab != null && levelManager != null)
            {
                ArrowLine arrowLine = theme.arrowPrefab.GetComponent<ArrowLine>();
                if (arrowLine != null)
                {
                    levelManager.arrowLinePrefab = arrowLine;
                    anyApplied = true;
                }
                else
                {
                    Debug.LogWarning("[ThemeManager] Arrow Prefab does not have ArrowLine component.");
                }
            }

            // Note: LineColors settings are applied to prefab instances when they're created.
            // The prefab asset itself cannot be modified at runtime.
            // If you need to apply LineColors at runtime, you'll need to modify the LineColors
            // component on arrow instances after they're instantiated.

            // Apply skybox
            if (theme.skyboxMaterial != null)
            {
                RenderSettings.skybox = theme.skyboxMaterial;
                anyApplied = true;
            }

            // Apply background sprite
            if (backgroundRenderer != null)
            {
                backgroundRenderer.sprite = theme.background;
                anyApplied = true;
            }

            // Apply grid icon (runtime - requires grid rebuild)
            if (theme.gridIcon != null && gridManager != null && gridManager.dotPrefab != null)
            {
                // At runtime, we need to update existing dots or the prefab reference
                // For runtime, we'll update existing dots' sprites
                UpdateGridDotsSprite(theme.gridIcon);
                anyApplied = true;
            }

            // Apply theme icon
            if (theme.themeIcon != null && themeIconImage != null)
            {
                themeIconImage.sprite = theme.themeIcon;
                anyApplied = true;
            }

            if (anyApplied)
            {
                Debug.Log($"[ThemeManager] Theme '{theme.themeName}' applied successfully.");
            }
            levelManager.ReloadLevel();
        }

        /// <summary>
        /// Updates all existing grid dots with the new sprite at runtime.
        /// </summary>
        private void UpdateGridDotsSprite(Sprite newSprite)
        {
            if (gridManager == null || gridManager.dotsParent == null) return;

            foreach (Transform child in gridManager.dotsParent)
            {
                SpriteRenderer sr = child.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.sprite = newSprite;
                }
            }
        }

        /// <summary>
        /// Sets the active theme in the CurrentTheme asset.
        /// </summary>
        public void SetActiveTheme(ThemeData theme)
        {
            if (currentThemeAsset != null)
            {
                currentThemeAsset.ActiveTheme = theme;
            }
            else
            {
                Debug.LogWarning("[ThemeManager] CurrentTheme asset is not assigned.");
            }
        }

        /// <summary>
        /// Gets the currently active theme.
        /// </summary>
        public ThemeData GetActiveTheme()
        {
            return currentThemeAsset != null ? currentThemeAsset.ActiveTheme : null;
        }

        // Auto-find references if not assigned (for convenience)
        private void Reset()
        {
            if (levelManager == null)
                levelManager = FindFirstObjectByType<LevelManager>();
            if (gridManager == null)
                gridManager = FindFirstObjectByType<GridManager>();
            if (backgroundRenderer == null)
            {
                GameObject bg = GameObject.Find("Background");
                if (bg != null)
                    backgroundRenderer = bg.GetComponent<SpriteRenderer>();
            }
        }
    }
}

