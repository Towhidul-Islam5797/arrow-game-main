#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace MultiTechStudio.EscapeGame
{
    public class ThemeTabDrawer
    {
        private Vector2 themeScrollPosition;
        private Vector2 detailsScrollPosition;
        private ThemeData selectedTheme;
        private List<ThemeData> themes;
        private ReorderableList colorList;
        private System.Action<ThemeData> onThemeSelected;
        private System.Action onThemesReloaded;

        public ThemeTabDrawer(System.Action<ThemeData> onThemeSelected, System.Action onThemesReloaded)
        {
            this.onThemeSelected = onThemeSelected;
            this.onThemesReloaded = onThemesReloaded;
        }

        public void SetThemes(List<ThemeData> themes)
        {
            this.themes = themes;
        }

        public void SetSelectedTheme(ThemeData theme)
        {
            if (selectedTheme != theme)
            {
                selectedTheme = theme;
                InitializeColorList();
            }
        }

        public ThemeData GetSelectedTheme()
        {
            return selectedTheme;
        }

        public void DrawThemeTab()
        {
            EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            // Left panel: Theme list - narrow width to bring details panel closer
            EditorGUILayout.BeginVertical(GUILayout.Width(100), GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(false));
            DrawThemeList();
            EditorGUILayout.EndVertical();

            // Right panel: Theme details - positioned close to scroll bar with no gap
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            DrawThemeDetails();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawThemeList()
        {
            EditorGUILayout.LabelField("Themes", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh", GUILayout.Width(60)))
            {
                onThemesReloaded?.Invoke();
            }
            if (GUILayout.Button("Create New", GUILayout.Width(100)))
            {
                CreateNewTheme();
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(5);

            // Debug info
            if (themes == null || themes.Count == 0)
            {
                EditorGUILayout.HelpBox("No themes found. Click 'Create' to create one.", MessageType.Info);
            }

            // Vertical scroll view for theme slots - use full available width
            Rect scrollRect = GUILayoutUtility.GetRect(0, 0, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            float scrollbarWidth = 15f; // Approximate scrollbar width
            float slotWidth = scrollRect.width - scrollbarWidth; // Account for scrollbar
            float slotHeight = 100;
            float spacing = 2; // Reduced spacing
            // Calculate content height based on actual theme count
            float contentHeight = (themes != null ? themes.Count : 0) * (slotHeight + spacing);
            Rect viewRect = new Rect(0, 0, slotWidth, Mathf.Max(scrollRect.height, contentHeight));

            themeScrollPosition = GUI.BeginScrollView(
                scrollRect,
                themeScrollPosition,
                viewRect,
                false, // Don't show horizontal scrollbar
                true);  // Always show vertical scrollbar

            // Draw all themes using explicit rects in vertical layout
            if (themes != null)
            {
                for (int i = 0; i < themes.Count; i++)
                {
                    Rect slotRect = new Rect(0, i * (slotHeight + spacing), slotWidth, slotHeight);
                    DrawThemeSlotRect(themes[i], slotRect, i);
                }
            }

            GUI.EndScrollView();
        }

        private void DrawThemeSlotRect(ThemeData theme, Rect rect, int index)
        {
            bool isSelected = selectedTheme == theme;
            Color originalColor = GUI.backgroundColor;
            Color originalContentColor = GUI.contentColor;

            if (isSelected)
            {
                GUI.backgroundColor = new Color(0.3f, 0.5f, 0.9f);
            }

            // Button for selection - use full width
            Rect buttonRect = new Rect(rect.x, rect.y, rect.width, 80);
            if (GUI.Button(buttonRect, ""))
            {
                SelectTheme(theme);
            }

            GUI.backgroundColor = originalColor;

            // Draw theme icon
            if (theme.themeIcon != null)
            {
                Rect iconRect = new Rect(buttonRect.x + 10, buttonRect.y + 10, buttonRect.width - 20, buttonRect.height - 20);
                if (theme.themeIcon.texture != null)
                {
                    GUI.DrawTexture(iconRect, theme.themeIcon.texture, ScaleMode.ScaleToFit);
                }
            }
            else
            {
                GUI.Label(buttonRect, "No Icon", EditorStyles.centeredGreyMiniLabel);
            }

            // Theme name - centered and use full width
            Rect nameRect = new Rect(rect.x, rect.y + 82, rect.width, 18);
            GUI.Label(nameRect, theme.themeName, EditorStyles.centeredGreyMiniLabel);

            GUI.contentColor = originalContentColor;
        }

        private void DrawThemeDetails()
        {
            if (selectedTheme == null)
            {
                EditorGUILayout.HelpBox("Select a theme to view details", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Theme Details", EditorStyles.boldLabel);
            GUILayout.Space(5);

            detailsScrollPosition = EditorGUILayout.BeginScrollView(detailsScrollPosition);

            bool hasChanges = false;

            // Theme Name
            EditorGUI.BeginChangeCheck();
            selectedTheme.themeName = EditorGUILayout.TextField("Theme Name", selectedTheme.themeName);
            if (EditorGUI.EndChangeCheck()) hasChanges = true;

            GUILayout.Space(5);

            // Theme Icon
            EditorGUI.BeginChangeCheck();
            selectedTheme.themeIcon = (Sprite)EditorGUILayout.ObjectField("Theme Icon", selectedTheme.themeIcon, typeof(Sprite), false);
            if (EditorGUI.EndChangeCheck()) hasChanges = true;

            GUILayout.Space(5);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            // Arrow Prefab
            EditorGUI.BeginChangeCheck();
            selectedTheme.arrowPrefab = (GameObject)EditorGUILayout.ObjectField("Arrow Prefab", selectedTheme.arrowPrefab, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck()) hasChanges = true;

            if (selectedTheme.arrowPrefab == null)
            {
                EditorGUILayout.HelpBox("Arrow Prefab is required", MessageType.Warning);
                if (GUILayout.Button("Set Default Arrow Prefab"))
                {
                    ThemeHelpers.SetDefaultArrowPrefab(selectedTheme);
                    hasChanges = true;
                }
            }

            GUILayout.Space(5);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            // Have Different Colors
            EditorGUI.BeginChangeCheck();
            selectedTheme.haveDifferentColors = EditorGUILayout.Toggle("Have Different Colors", selectedTheme.haveDifferentColors);
            if (EditorGUI.EndChangeCheck()) hasChanges = true;

            if (selectedTheme.haveDifferentColors)
            {
                GUILayout.Space(5);
                EditorGUILayout.LabelField("Colors", EditorStyles.boldLabel);

                if (colorList != null)
                {
                    colorList.DoLayoutList();
                }
            }

            GUILayout.Space(5);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

            // Skybox Material
            EditorGUI.BeginChangeCheck();
            selectedTheme.skyboxMaterial = (Material)EditorGUILayout.ObjectField("Skybox Material", selectedTheme.skyboxMaterial, typeof(Material), false);
            if (EditorGUI.EndChangeCheck()) hasChanges = true;

            if (selectedTheme.skyboxMaterial == null)
            {
                EditorGUILayout.HelpBox("Skybox Material is required", MessageType.Warning);
                if (GUILayout.Button("Set Default Skybox"))
                {
                    ThemeHelpers.SetDefaultSkybox(selectedTheme);
                    hasChanges = true;
                }
            }

            GUILayout.Space(5);

            // Grid Icon
            EditorGUI.BeginChangeCheck();
            selectedTheme.gridIcon = (Sprite)EditorGUILayout.ObjectField("Grid Icon", selectedTheme.gridIcon, typeof(Sprite), false);
            if (EditorGUI.EndChangeCheck()) hasChanges = true;

            if (selectedTheme.gridIcon == null)
            {
                if (GUILayout.Button("No Grid Icon - Set Default Icon"))
                {
                    ThemeHelpers.SetDefaultGridIcon(selectedTheme);
                    hasChanges = true;
                }
            }

            GUILayout.Space(5);

            // Background Sprite
            EditorGUI.BeginChangeCheck();
            selectedTheme.background = (Sprite)EditorGUILayout.ObjectField("Background", selectedTheme.background, typeof(Sprite), false);
            if (EditorGUI.EndChangeCheck()) hasChanges = true;

            // Mark as dirty when changes occur, but don't save immediately
            // Unity will handle saving on focus loss or manual save
            if (hasChanges)
            {
                EditorUtility.SetDirty(selectedTheme);
            }

            EditorGUILayout.EndScrollView();

            GUILayout.Space(10);

            // Apply button
            EditorGUILayout.BeginHorizontal();
            GUI.enabled = selectedTheme != null;
            if (GUILayout.Button("APPLY", GUILayout.Height(30)))
            {
                ThemeOperations.ApplyTheme(selectedTheme);
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(5);

            // Delete button
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Delete Theme", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("Delete Theme", $"Are you sure you want to delete '{selectedTheme.themeName}'?", "Yes", "No"))
                {
                    DeleteTheme(selectedTheme);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void InitializeColorList()
        {
            if (selectedTheme != null)
            {
                if (selectedTheme.colors == null)
                {
                    selectedTheme.colors = new List<Color>();
                }

                colorList = new ReorderableList(selectedTheme.colors, typeof(Color), true, true, true, true);

                colorList.drawHeaderCallback = (Rect rect) =>
                {
                    EditorGUI.LabelField(rect, "Colors");
                };

                colorList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
                {
                    if (index < selectedTheme.colors.Count)
                    {
                        rect.y += 2;
                        rect.height = EditorGUIUtility.singleLineHeight;

                        // Color field with color bar
                        Rect colorBarRect = new Rect(rect.x, rect.y, rect.width - 60, rect.height);
                        Rect colorFieldRect = new Rect(rect.x + rect.width - 55, rect.y, 50, rect.height);

                        // Draw color bar
                        EditorGUI.DrawRect(colorBarRect, selectedTheme.colors[index]);
                        EditorGUI.DrawRect(new Rect(colorBarRect.x, colorBarRect.y, colorBarRect.width, 1), Color.black);
                        EditorGUI.DrawRect(new Rect(colorBarRect.x, colorBarRect.y + colorBarRect.height - 1, colorBarRect.width, 1), Color.black);
                        EditorGUI.DrawRect(new Rect(colorBarRect.x, colorBarRect.y, 1, colorBarRect.height), Color.black);
                        EditorGUI.DrawRect(new Rect(colorBarRect.x + colorBarRect.width - 1, colorBarRect.y, 1, colorBarRect.height), Color.black);

                        // Color field - mark dirty on change, but don't save immediately
                        // This prevents saving on every color picker drag
                        EditorGUI.BeginChangeCheck();
                        selectedTheme.colors[index] = EditorGUI.ColorField(colorFieldRect, selectedTheme.colors[index]);
                        if (EditorGUI.EndChangeCheck())
                        {
                            // Only mark dirty, don't save assets immediately
                            // Unity will auto-save on focus loss or when user manually saves
                            EditorUtility.SetDirty(selectedTheme);
                        }
                    }
                };

                colorList.onAddCallback = (ReorderableList list) =>
                {
                    selectedTheme.colors.Add(Color.white);
                    EditorUtility.SetDirty(selectedTheme);
                };

                colorList.onRemoveCallback = (ReorderableList list) =>
                {
                    if (list.index >= 0 && list.index < selectedTheme.colors.Count)
                    {
                        selectedTheme.colors.RemoveAt(list.index);
                        EditorUtility.SetDirty(selectedTheme);
                    }
                };

                colorList.elementHeight = EditorGUIUtility.singleLineHeight + 4;
            }
        }

        private void SelectTheme(ThemeData theme)
        {
            if (selectedTheme != theme)
            {
                selectedTheme = theme;
                ThemeHelpers.SaveSelectedTheme(theme);
                InitializeColorList();
                onThemeSelected?.Invoke(theme);
            }
        }

        private void CreateNewTheme()
        {
            ThemeData newTheme = ThemeOperations.CreateNewTheme(themes);
            onThemesReloaded?.Invoke();
            SelectTheme(newTheme);
        }

        private void DeleteTheme(ThemeData theme)
        {
            ThemeOperations.DeleteTheme(theme);

            if (selectedTheme == theme)
            {
                selectedTheme = null;
                ThemeHelpers.SaveSelectedTheme(null);
            }

            onThemesReloaded?.Invoke();
        }
    }
}
#endif

