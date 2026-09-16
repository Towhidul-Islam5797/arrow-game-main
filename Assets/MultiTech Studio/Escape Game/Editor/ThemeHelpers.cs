#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MultiTechStudio.EscapeGame
{
    public static class ThemeHelpers
    {
        public const string THEMES_FOLDER = "Assets/MultiTech Studio/Escape Game/Themes";
        public const string SELECTED_THEME_KEY = "EscapeGame_SelectedTheme";
        public const string DEFAULT_ARROW_PREFAB = "Assets/MultiTech Studio/Escape Game/Prefabs/Arrow.prefab";
        public const string DEFAULT_SKYBOX = "Assets/MultiTech Studio/Escape Game/Materials/Skybox Regular.mat";
        public const string DEFAULT_GRID_ICON = "Assets/MultiTech Studio/Escape Game/Sprites/Circle.png";

        public static void EnsureThemesFolder()
        {
            if (!AssetDatabase.IsValidFolder(THEMES_FOLDER))
            {
                string parentFolder = "Assets/MultiTech Studio/Escape Game";
                if (!AssetDatabase.IsValidFolder(parentFolder))
                {
                    string[] folders = parentFolder.Split('/');
                    string currentPath = folders[0];
                    for (int i = 1; i < folders.Length; i++)
                    {
                        string newPath = $"{currentPath}/{folders[i]}";
                        if (!AssetDatabase.IsValidFolder(newPath))
                        {
                            AssetDatabase.CreateFolder(currentPath, folders[i]);
                        }
                        currentPath = newPath;
                    }
                }
                AssetDatabase.CreateFolder("Assets/MultiTech Studio/Escape Game", "Themes");
            }
        }

        public static List<ThemeData> LoadThemes()
        {
            List<ThemeData> themes = new List<ThemeData>();

            if (!AssetDatabase.IsValidFolder(THEMES_FOLDER))
            {
                EnsureThemesFolder();
                CreateDefaultThemes();
                return LoadThemes(); // Recursive call after creating defaults
            }

            string[] guids = AssetDatabase.FindAssets("t:ThemeData", new[] { THEMES_FOLDER });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                ThemeData theme = AssetDatabase.LoadAssetAtPath<ThemeData>(path);
                if (theme != null)
                {
                    themes.Add(theme);
                }
            }

            // Create default themes if folder is empty
            if (themes.Count == 0)
            {
                CreateDefaultThemes();
                return LoadThemes(); // Recursive call after creating defaults
            }

            return themes;
        }

        public static ThemeData LoadSelectedTheme()
        {
            string path = EditorPrefs.GetString(SELECTED_THEME_KEY, "");
            if (!string.IsNullOrEmpty(path))
            {
                ThemeData theme = AssetDatabase.LoadAssetAtPath<ThemeData>(path);
                return theme;
            }
            return null;
        }

        public static void SaveSelectedTheme(ThemeData theme)
        {
            if (theme != null)
            {
                EditorPrefs.SetString(SELECTED_THEME_KEY, AssetDatabase.GetAssetPath(theme));
            }
            else
            {
                EditorPrefs.DeleteKey(SELECTED_THEME_KEY);
            }
        }

        public static void SetDefaultArrowPrefab(ThemeData theme)
        {
            if (theme == null) return;

            GameObject defaultPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DEFAULT_ARROW_PREFAB);
            if (defaultPrefab != null)
            {
                theme.arrowPrefab = defaultPrefab;
                EditorUtility.SetDirty(theme);
            }
        }

        public static void SetDefaultSkybox(ThemeData theme)
        {
            if (theme == null) return;

            Material defaultSkybox = AssetDatabase.LoadAssetAtPath<Material>(DEFAULT_SKYBOX);
            if (defaultSkybox != null)
            {
                theme.skyboxMaterial = defaultSkybox;
                EditorUtility.SetDirty(theme);
            }
        }

        public static void SetDefaultGridIcon(ThemeData theme)
        {
            if (theme == null) return;

            Sprite defaultIcon = AssetDatabase.LoadAssetAtPath<Sprite>(DEFAULT_GRID_ICON);
            if (defaultIcon != null)
            {
                theme.gridIcon = defaultIcon;
                EditorUtility.SetDirty(theme);
            }
        }

        private static void CreateDefaultThemes()
        {
            // Prefab names and their corresponding icon sprites
            string[] prefabNames = { "Arrow Neon", "Snake 1", "Snake 2", "Snake 3", "Train", "Train 1", "Arrow" };
            string[] iconNames = { "Icon arrow colors", "Icon snake", "Icon snake", "Icon snake", "Icon Train", "Icon Train 1", "Icon Arrow Black" };

            for (int i = 0; i < prefabNames.Length; i++)
            {
                ThemeData theme = ScriptableObject.CreateInstance<ThemeData>();
                theme.themeName = prefabNames[i];

                // Load arrow prefab
                string prefabPath = $"Assets/MultiTech Studio/Escape Game/Prefabs/{prefabNames[i]}.prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab != null)
                {
                    theme.arrowPrefab = prefab;

                    // Extract colors from LineColors component using SerializedObject
                    LineColors lineColors = prefab.GetComponent<LineColors>();
                    if (lineColors != null)
                    {
                        SerializedObject serializedObject = new SerializedObject(lineColors);
                        SerializedProperty colorsProperty = serializedObject.FindProperty("colors");

                        if (colorsProperty != null && colorsProperty.isArray && colorsProperty.arraySize > 0)
                        {
                            theme.haveDifferentColors = true;
                            theme.colors = new List<Color>();

                            for (int j = 0; j < colorsProperty.arraySize; j++)
                            {
                                SerializedProperty colorElement = colorsProperty.GetArrayElementAtIndex(j);
                                Color color = colorElement.colorValue;
                                theme.colors.Add(color);
                            }
                        }
                    }
                }

                // Load theme icon
                string iconPath = $"Assets/MultiTech Studio/Escape Game/Sprites/{iconNames[i]}.png";
                Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
                if (icon == null)
                {
                    // Try with .meta extension removed
                    icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath.Replace(".png", ""));
                }
                if (icon != null)
                {
                    theme.themeIcon = icon;
                }

                // Set default skybox
                Material skybox = AssetDatabase.LoadAssetAtPath<Material>(DEFAULT_SKYBOX);
                if (skybox != null)
                {
                    theme.skyboxMaterial = skybox;
                }

                // Set default grid icon
                Sprite gridIcon = AssetDatabase.LoadAssetAtPath<Sprite>(DEFAULT_GRID_ICON);
                if (gridIcon != null)
                {
                    theme.gridIcon = gridIcon;
                }

                // If no arrow prefab found, use default
                if (theme.arrowPrefab == null)
                {
                    GameObject defaultPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DEFAULT_ARROW_PREFAB);
                    if (defaultPrefab != null)
                    {
                        theme.arrowPrefab = defaultPrefab;
                    }
                }

                string assetPath = $"{THEMES_FOLDER}/Theme_{i + 1}_{prefabNames[i]}.asset";
                AssetDatabase.CreateAsset(theme, assetPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
#endif

