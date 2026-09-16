#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MultiTechStudio.EscapeGame
{
    public class EscapeGameSetupWindow : EditorWindow
    {
        private enum TabType
        {
            Settings,
            Theme
        }

        private TabType currentTab = TabType.Theme;
        private List<ThemeData> themes = new List<ThemeData>();
        private ThemeTabDrawer themeTabDrawer;

        [MenuItem("Tools/MultiTech Studio/Escape Game Setup")]
        public static void ShowWindow()
        {
            EscapeGameSetupWindow window = GetWindow<EscapeGameSetupWindow>("Escape Game");
            window.minSize = new Vector2(600, 400);
            window.Show();
        }

        void OnEnable()
        {
            ThemeHelpers.EnsureThemesFolder();
            ReloadThemes();
            
            themeTabDrawer = new ThemeTabDrawer(
                onThemeSelected: (theme) => { },
                onThemesReloaded: ReloadThemes
            );
            
            ThemeData selectedTheme = ThemeHelpers.LoadSelectedTheme();
            if (selectedTheme != null)
            {
                themeTabDrawer.SetSelectedTheme(selectedTheme);
            }
        }

        void OnFocus()
        {
            ReloadThemes();
        }

        void OnGUI()
        {
            DrawTabs();
            GUILayout.Space(10);

            switch (currentTab)
            {
                case TabType.Settings:
                    SettingsTabDrawer.DrawSettingsTab();
                    break;
                case TabType.Theme:
                    if (themeTabDrawer != null)
                    {
                        themeTabDrawer.DrawThemeTab();
                    }
                    break;
            }
        }

        private void DrawTabs()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Toggle(currentTab == TabType.Settings, "Settings", EditorStyles.toolbarButton))
            {
                currentTab = TabType.Settings;
            }

            if (GUILayout.Toggle(currentTab == TabType.Theme, "Theme", EditorStyles.toolbarButton))
            {
                currentTab = TabType.Theme;
            }

            EditorGUILayout.EndHorizontal();
        }

        private void ReloadThemes()
        {
            themes = ThemeHelpers.LoadThemes();
            if (themeTabDrawer != null)
            {
                themeTabDrawer.SetThemes(themes);
                
                // Restore selected theme if it still exists
                ThemeData selectedTheme = ThemeHelpers.LoadSelectedTheme();
                if (selectedTheme != null && themes.Contains(selectedTheme))
                {
                    themeTabDrawer.SetSelectedTheme(selectedTheme);
                }
            }
        }
    }
}
#endif

