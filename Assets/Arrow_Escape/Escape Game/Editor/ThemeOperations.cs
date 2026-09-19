#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MultiTechStudio.EscapeGame
{
    public static class ThemeOperations
    {
        public static ThemeData CreateNewTheme(List<ThemeData> existingThemes)
        {
            ThemeData newTheme = ScriptableObject.CreateInstance<ThemeData>();
            newTheme.themeName = $"Theme {existingThemes.Count + 1}";

            // Set defaults
            ThemeHelpers.SetDefaultArrowPrefab(newTheme);
            ThemeHelpers.SetDefaultSkybox(newTheme);
            ThemeHelpers.SetDefaultGridIcon(newTheme);

            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{ThemeHelpers.THEMES_FOLDER}/Theme_{existingThemes.Count + 1}.asset");
            AssetDatabase.CreateAsset(newTheme, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return newTheme;
        }

        public static void DeleteTheme(ThemeData theme)
        {
            string path = AssetDatabase.GetAssetPath(theme);
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static bool ApplyTheme(ThemeData theme)
        {
            if (theme == null)
            {
                EditorUtility.DisplayDialog("Apply Theme", "No theme selected.", "OK");
                return false;
            }

            // Validate required fields
            if (theme.arrowPrefab == null)
            {
                EditorUtility.DisplayDialog("Apply Theme", "Arrow Prefab is required. Please assign an Arrow Prefab to the theme.", "OK");
                return false;
            }

            if (theme.skyboxMaterial == null)
            {
                EditorUtility.DisplayDialog("Apply Theme", "Skybox Material is required. Please assign a Skybox Material to the theme.", "OK");
                return false;
            }

            bool success = true;
            List<string> errors = new List<string>();

            // 1. Set Arrow Prefab in LevelManager
            if (!ApplyArrowPrefab(theme))
            {
                success = false;
                errors.Add("Failed to set Arrow Prefab in LevelManager");
            }

            // 2. Handle LineColors Script on Prefab
            if (!ApplyLineColors(theme))
            {
                success = false;
                errors.Add("Failed to apply LineColors settings to prefab");
            }

            // 3. Set Skybox Material
            if (!ApplySkybox(theme))
            {
                success = false;
                errors.Add("Failed to set Skybox Material");
            }

            // 4. Set Background Sprite
            if (!ApplyBackground(theme))
            {
                success = false;
                errors.Add("Failed to set Background Sprite");
            }

            // 5. Set Grid Icon
            if (!ApplyGridIcon(theme))
            {
                success = false;
                errors.Add("Failed to set Grid Icon");
            }

            // 6. Set Theme Icon
            if (!ApplyThemeIcon(theme))
            {
                success = false;
                errors.Add("Failed to set Theme Icon");
            }

            // 7. Update CurrentTheme asset (if it exists)
            UpdateCurrentTheme(theme);

            // Show result
            if (success)
            {
                EditorUtility.DisplayDialog("Apply Theme", $"Theme '{theme.themeName}' applied successfully!", "OK");
            }
            else
            {
                string errorMessage = $"Theme '{theme.themeName}' applied with errors:\n\n" + string.Join("\n", errors);
                EditorUtility.DisplayDialog("Apply Theme", errorMessage, "OK");
            }

            return success;
        }

        private static bool ApplyArrowPrefab(ThemeData theme)
        {
            LevelManager levelManager = Object.FindFirstObjectByType<LevelManager>();
            if (levelManager == null)
            {
                Debug.LogWarning("[ThemeOperations] LevelManager not found in scene.");
                return false;
            }

            ArrowLine arrowLine = theme.arrowPrefab.GetComponent<ArrowLine>();
            if (arrowLine == null)
            {
                Debug.LogWarning("[ThemeOperations] Arrow Prefab does not have ArrowLine component.");
                return false;
            }

            levelManager.arrowLinePrefab = arrowLine;
            EditorUtility.SetDirty(levelManager);
            return true;
        }

        private static bool ApplyLineColors(ThemeData theme)
        {
            if (theme.arrowPrefab == null)
            {
                return false;
            }

            string prefabPath = AssetDatabase.GetAssetPath(theme.arrowPrefab);
            if (string.IsNullOrEmpty(prefabPath))
            {
                Debug.LogWarning("[ThemeOperations] Could not get asset path for Arrow Prefab.");
                return false;
            }

            // Load the prefab asset
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabAsset == null)
            {
                Debug.LogWarning("[ThemeOperations] Could not load prefab asset.");
                return false;
            }

            LineColors lineColors = prefabAsset.GetComponent<LineColors>();
            if (lineColors == null)
            {
                // If LineColors doesn't exist and haveDifferentColors is false, that's fine
                if (!theme.haveDifferentColors)
                {
                    return true;
                }
                Debug.LogWarning("[ThemeOperations] LineColors component not found on prefab.");
                return false;
            }

            SerializedObject serializedObject = new SerializedObject(lineColors);

            // Enable/Disable LineColors component
            SerializedProperty enabledProperty = serializedObject.FindProperty("m_Enabled");
            if (enabledProperty != null)
            {
                enabledProperty.boolValue = theme.haveDifferentColors;
            }

            // If haveDifferentColors is true, copy colors
            if (theme.haveDifferentColors)
            {
                SerializedProperty colorsProperty = serializedObject.FindProperty("colors");
                if (colorsProperty != null && colorsProperty.isArray)
                {
                    colorsProperty.arraySize = theme.colors != null ? theme.colors.Count : 0;

                    if (theme.colors != null)
                    {
                        for (int i = 0; i < theme.colors.Count; i++)
                        {
                            SerializedProperty colorElement = colorsProperty.GetArrayElementAtIndex(i);
                            if (colorElement != null)
                            {
                                colorElement.colorValue = theme.colors[i];
                            }
                        }
                    }
                }
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(prefabAsset);
            AssetDatabase.SaveAssets();

            return true;
        }

        private static bool ApplySkybox(ThemeData theme)
        {
            if (theme.skyboxMaterial == null)
            {
                return false;
            }

            RenderSettings.skybox = theme.skyboxMaterial;
            // Mark the scene as dirty to save the skybox change
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
            return true;
        }

        private static bool ApplyBackground(ThemeData theme)
        {
            // Try to find Background GameObject
            GameObject backgroundObj = GameObject.Find("Background");
            if (backgroundObj == null)
            {
                // Try to find it under Main Camera
                Camera mainCamera = Camera.main;
                if (mainCamera != null)
                {
                    Transform backgroundTransform = mainCamera.transform.Find("Background");
                    if (backgroundTransform != null)
                    {
                        backgroundObj = backgroundTransform.gameObject;
                    }
                }
            }

            if (backgroundObj == null)
            {
                Debug.LogWarning("[ThemeOperations] Background GameObject not found in scene.");
                return false;
            }

            SpriteRenderer spriteRenderer = backgroundObj.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                Debug.LogWarning("[ThemeOperations] Background GameObject does not have SpriteRenderer component.");
                return false;
            }

            // Apply the sprite (even if it's null, we still want to set it to null)
            spriteRenderer.sprite = theme.background;
            EditorUtility.SetDirty(backgroundObj);
            return true;
        }

        private static bool ApplyGridIcon(ThemeData theme)
        {
            GridManager gridManager = Object.FindFirstObjectByType<GridManager>();
            if (gridManager == null)
            {
                Debug.LogWarning("[ThemeOperations] GridManager not found in scene.");
                return false;
            }

            if (gridManager.dotPrefab == null)
            {
                Debug.LogWarning("[ThemeOperations] Dot Prefab is not assigned in GridManager.");
                return false;
            }

            // Get the prefab asset path from the Dot component's GameObject
            string prefabPath = AssetDatabase.GetAssetPath(gridManager.dotPrefab.gameObject);
            if (string.IsNullOrEmpty(prefabPath))
            {
                Debug.LogWarning("[ThemeOperations] Could not get asset path for Dot Prefab.");
                return false;
            }

            // Load the prefab asset
            GameObject dotPrefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (dotPrefabAsset == null)
            {
                Debug.LogWarning("[ThemeOperations] Could not load Dot Prefab asset.");
                return false;
            }

            SpriteRenderer spriteRenderer = dotPrefabAsset.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                Debug.LogWarning("[ThemeOperations] Dot Prefab does not have SpriteRenderer component.");
                return false;
            }

            // Apply the sprite (even if it's null, we still want to set it to null)
            spriteRenderer.sprite = theme.gridIcon;
            EditorUtility.SetDirty(dotPrefabAsset);
            AssetDatabase.SaveAssets();

            // Rebuild the grid to reflect the sprite changes
            gridManager.DestroyGrid(true);
            gridManager.RebuildSmart();

            return true;
        }

        private static bool ApplyThemeIcon(ThemeData theme)
        {
            if (theme.themeIcon == null)
            {
                // Theme icon is optional, but we should still try to set it to null if needed
                // Return true to not fail the apply process
                return true;
            }

            // Try to find Icon GameObject - it's under Button_Battle > Main Screen > Canvas > Level Screen
            GameObject iconObj = GameObject.Find("Icon");
            if (iconObj == null)
            {
                // Try to find it by traversing the hierarchy
                GameObject buttonBattle = GameObject.Find("Button_Battle");
                if (buttonBattle != null)
                {
                    Transform iconTransform = buttonBattle.transform.Find("Icon");
                    if (iconTransform != null)
                    {
                        iconObj = iconTransform.gameObject;
                    }
                }
            }

            if (iconObj == null)
            {
                Debug.LogWarning("[ThemeOperations] Icon GameObject not found in scene.");
                return false;
            }

            // Get the Image component (Unity UI Image, not SpriteRenderer)
            Image image = iconObj.GetComponent<Image>();
            if (image == null)
            {
                Debug.LogWarning("[ThemeOperations] Icon GameObject does not have Image component.");
                return false;
            }

            // Apply the sprite (even if it's null, we still want to set it to null)
            image.sprite = theme.themeIcon;
            EditorUtility.SetDirty(iconObj);

            return true;
        }

        /// <summary>
        /// Updates the CurrentTheme asset with the given theme, or creates one if it doesn't exist.
        /// This allows runtime scripts to access the active theme.
        /// </summary>
        private static void UpdateCurrentTheme(ThemeData theme)
        {
            if (theme == null) return;

            // Try to find existing CurrentTheme asset
            string[] guids = AssetDatabase.FindAssets("t:CurrentTheme");
            CurrentTheme currentTheme = null;

            if (guids.Length > 0)
            {
                // Use the first found CurrentTheme asset
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                currentTheme = AssetDatabase.LoadAssetAtPath<CurrentTheme>(path);
            }

            // If no CurrentTheme exists, create one
            if (currentTheme == null)
            {
                currentTheme = ScriptableObject.CreateInstance<CurrentTheme>();
                string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{ThemeHelpers.THEMES_FOLDER}/CurrentTheme.asset");
                AssetDatabase.CreateAsset(currentTheme, assetPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[ThemeOperations] Created CurrentTheme asset at {assetPath}");
            }

            // Update the active theme
            if (currentTheme != null)
            {
                currentTheme.ActiveTheme = theme;
                EditorUtility.SetDirty(currentTheme);
                AssetDatabase.SaveAssets();
            }
        }
    }
}
#endif

