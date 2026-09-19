using UnityEngine;

namespace MultiTechStudio.EscapeGame
{
    /// <summary>
    /// ScriptableObject that holds the currently active theme reference.
    /// Other scripts can reference this to get the active theme at runtime.
    /// </summary>
    [CreateAssetMenu(fileName = "CurrentTheme", menuName = "Tools/MultiTech Studio/Escape Game Current Theme")]
    public class CurrentTheme : ScriptableObject
    {
        [SerializeField] private ThemeData activeTheme;

        /// <summary>
        /// The currently active theme. Setting this will trigger OnThemeChanged event.
        /// </summary>
        public ThemeData ActiveTheme
        {
            get => activeTheme;
            set
            {
                if (activeTheme != value)
                {
                    activeTheme = value;
                    OnThemeChanged?.Invoke(activeTheme);
                }
            }
        }

        /// <summary>
        /// Event triggered when the active theme changes.
        /// </summary>
        public System.Action<ThemeData> OnThemeChanged;

        private void OnEnable()
        {
            // Reset event subscription when asset is loaded
            OnThemeChanged = null;
        }
    }
}

