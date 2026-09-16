#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

#if MT_IAP
using UnityEngine.Purchasing;
#endif

namespace MultiTechStudio.EscapeGame
{
    public static class SettingsTabDrawer
    {
        private static ProjectSetupData currentProjectSetupData;

        public static void DrawSettingsTab()
        {
            // Project Setup Data Section
            DrawProjectSetupDataSection();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("SDK Setup & Management", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // SDK Status Section
            DrawSDKStatusSection();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("SDK Management", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // SDK Toggle Section
            DrawSDKToggleSection();

            EditorGUILayout.Space();

            // AdMob Configuration Section (only show if AdMob is enabled)
            if (SDKDefinesHelper.HasDefine("MT_ADMOB"))
            {
                DrawAdMobConfigSection();
            }

            EditorGUILayout.Space();

            // IAP Configuration Section (only show if IAP is enabled)
            if (SDKDefinesHelper.HasDefine("MT_IAP"))
            {
                DrawIapConfigSection();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Action Buttons
            DrawActionButtons();
        }

        private static void DrawProjectSetupDataSection()
        {
            EditorGUILayout.LabelField("Project Setup Data", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Try to find existing ProjectSetupData in project
            if (currentProjectSetupData == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:ProjectSetupData");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    currentProjectSetupData = AssetDatabase.LoadAssetAtPath<ProjectSetupData>(path);
                }
            }

            EditorGUI.BeginChangeCheck();
            currentProjectSetupData = EditorGUILayout.ObjectField(
                new GUIContent("Project Setup Data", "Centralized configuration for IAP and Ads"),
                currentProjectSetupData,
                typeof(ProjectSetupData),
                false
            ) as ProjectSetupData;
            EditorGUI.EndChangeCheck();

            if (currentProjectSetupData != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox($"Editing: {AssetDatabase.GetAssetPath(currentProjectSetupData)}", MessageType.Info);
            }
            else
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox("Create or assign a ProjectSetupData asset to manage IAP and Ads configuration centrally.", MessageType.Info);
            }
        }

        private static void DrawSDKStatusSection()
        {
            EditorGUILayout.LabelField("Current SDK Status:", EditorStyles.boldLabel);

            bool hasAdMob = SDKDefinesHelper.HasDefine("MT_ADMOB");
            bool hasFirebase = SDKDefinesHelper.HasDefine("MT_FIREBASE");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Google Mobile Ads (AdMob):", GUILayout.Width(200));
            EditorGUILayout.LabelField(hasAdMob ? "✓ Available" : "✗ Not Available",
                hasAdMob ? EditorStyles.boldLabel : EditorStyles.label);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Firebase:", GUILayout.Width(200));
            EditorGUILayout.LabelField(hasFirebase ? "✓ Available" : "✗ Not Available",
                hasFirebase ? EditorStyles.boldLabel : EditorStyles.label);
            EditorGUILayout.EndHorizontal();

            bool hasIap = SDKDefinesHelper.HasDefine("MT_IAP");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Unity IAP:", GUILayout.Width(200));
            EditorGUILayout.LabelField(hasIap ? "✓ Available" : "✗ Not Available",
                hasIap ? EditorStyles.boldLabel : EditorStyles.label);
            EditorGUILayout.EndHorizontal();

            bool hasDotween = SDKDefinesHelper.HasDefine("MT_DOTWEEN");
            bool dotweenInstalled = AutoDefines.IsDotweenInstalled();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("DOTween:", GUILayout.Width(200));
            EditorGUILayout.LabelField(hasDotween ? "✓ Available" : "✗ Not Available",
                hasDotween ? EditorStyles.boldLabel : EditorStyles.label);
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawSDKToggleSection()
        {
            EditorGUILayout.LabelField("Enable/Disable SDKs:", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            bool hasAdMob = SDKDefinesHelper.HasDefine("MT_ADMOB");
            bool hasFirebase = SDKDefinesHelper.HasDefine("MT_FIREBASE");
            bool hasIap = SDKDefinesHelper.HasDefine("MT_IAP");
            bool hasDotween = SDKDefinesHelper.HasDefine("MT_DOTWEEN");
            bool dotweenInstalled = AutoDefines.IsDotweenInstalled();

            hasAdMob = EditorGUILayout.ToggleLeft("Enable Google Mobile Ads (AdMob)", hasAdMob);
            hasFirebase = EditorGUILayout.ToggleLeft("Enable Firebase", hasFirebase);
            hasIap = EditorGUILayout.ToggleLeft("Enable Unity IAP", hasIap);
            
            using (new EditorGUI.DisabledScope(!dotweenInstalled && !hasDotween))
            {
                hasDotween = EditorGUILayout.ToggleLeft("Enable DOTween (MT_DOTWEEN)", hasDotween);
            }

            if (EditorGUI.EndChangeCheck())
            {
                SDKDefinesHelper.SetDefine("MT_ADMOB", hasAdMob);
                SDKDefinesHelper.SetDefine("MT_FIREBASE", hasFirebase);
                SDKDefinesHelper.SetDefine("MT_IAP", hasIap);
                SDKDefinesHelper.SetDefine("MT_DOTWEEN", hasDotween);
            }

            // Help boxes for SDK status
            if (!hasAdMob)
            {
                EditorGUILayout.HelpBox("AdMob SDK not detected. Import Google Mobile Ads Unity plugin to enable.", MessageType.Info);
            }
            if (!hasIap)
            {
                EditorGUILayout.HelpBox("Unity IAP not detected. Add 'In-app Purchasing' via Package Manager.", MessageType.Info);
            }
            if (!dotweenInstalled)
            {
                EditorGUILayout.HelpBox("DOTween is not installed. Tween-based UI and animations will fall back to simple state changes. Import DOTween from the Asset Store to restore full effects.", MessageType.Warning);
            }
            else if (!hasDotween)
            {
                EditorGUILayout.HelpBox("DOTween is installed but disabled. Re-enable it to restore tween-driven animations.", MessageType.Info);
            }
        }

        private static void DrawAdMobConfigSection()
        {
            EditorGUILayout.LabelField("AdMob Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Use currentProjectSetupData if available, otherwise try to get from AdsManager
            ProjectSetupData setupDataToUse = currentProjectSetupData;
            
            if (setupDataToUse == null)
            {
                // Find AdsManager in the scene and check if it has ProjectSetupData assigned
                AdsManager adsManager = Object.FindFirstObjectByType<AdsManager>();
                if (adsManager != null)
                {
                    SerializedObject serializedAdsManagerCheck = new SerializedObject(adsManager);
                    SerializedProperty projectSetupDataProp = serializedAdsManagerCheck.FindProperty("projectSetupData");
                    if (projectSetupDataProp != null && projectSetupDataProp.objectReferenceValue != null)
                    {
                        setupDataToUse = projectSetupDataProp.objectReferenceValue as ProjectSetupData;
                    }
                }
            }

            if (setupDataToUse == null)
            {
                EditorGUILayout.HelpBox("No ProjectSetupData found. Please create or assign a ProjectSetupData asset above to configure AdMob settings.", MessageType.Warning);
                return;
            }

            // Edit only ProjectSetupData
            SerializedObject serializedSetupData = new SerializedObject(setupDataToUse);
            serializedSetupData.Update();

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.LabelField("Ad Unit IDs:", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedSetupData.FindProperty("bannerId"), new GUIContent("Banner ID"));
            EditorGUILayout.PropertyField(serializedSetupData.FindProperty("interId"), new GUIContent("Interstitial ID"));
            EditorGUILayout.PropertyField(serializedSetupData.FindProperty("rewardedInterId"), new GUIContent("Rewarded Interstitial ID"));
            EditorGUILayout.PropertyField(serializedSetupData.FindProperty("rewardedId"), new GUIContent("Rewarded ID"));

            EditorGUILayout.Space();

            // Interstitial Settings (now in ProjectSetupData)
            EditorGUILayout.LabelField("Interstitial Settings:", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedSetupData.FindProperty("interstitialLevelThreshold"), new GUIContent("Interstitial Level Threshold"));
            EditorGUILayout.PropertyField(serializedSetupData.FindProperty("canShowInterstitialAfterLevel"), new GUIContent("Can Show Interstitial After Level"));

            if (EditorGUI.EndChangeCheck())
            {
                serializedSetupData.ApplyModifiedProperties();
                EditorUtility.SetDirty(setupDataToUse);
            }
        }

        private static void DrawIapConfigSection()
        {
            EditorGUILayout.LabelField("IAP Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Use currentProjectSetupData if available, otherwise try to get from IapManager
            ProjectSetupData setupDataToUse = currentProjectSetupData;
            
            if (setupDataToUse == null)
            {
                // Find IapManager in the scene and check if it has ProjectSetupData assigned
                IapManager iapManager = Object.FindFirstObjectByType<IapManager>();
                if (iapManager != null)
                {
                    SerializedObject serializedIapManagerCheck = new SerializedObject(iapManager);
                    SerializedProperty projectSetupDataProp = serializedIapManagerCheck.FindProperty("projectSetupData");
                    if (projectSetupDataProp != null && projectSetupDataProp.objectReferenceValue != null)
                    {
                        setupDataToUse = projectSetupDataProp.objectReferenceValue as ProjectSetupData;
                    }
                }
            }

            if (setupDataToUse == null)
            {
                EditorGUILayout.HelpBox("No ProjectSetupData found. Please create or assign a ProjectSetupData asset above to configure IAP settings.", MessageType.Warning);
                return;
            }

            // Edit only ProjectSetupData
            SerializedObject serializedSetupData = new SerializedObject(setupDataToUse);
            serializedSetupData.Update();

            EditorGUI.BeginChangeCheck();

            SerializedProperty iapProductsProp = serializedSetupData.FindProperty("iapProducts");
            if (iapProductsProp != null)
            {
                EditorGUILayout.PropertyField(iapProductsProp, new GUIContent("IAP Products"), true);
            }

            if (EditorGUI.EndChangeCheck())
            {
                serializedSetupData.ApplyModifiedProperties();
                EditorUtility.SetDirty(setupDataToUse);
            }
        }

        private static void DrawActionButtons()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Update Defines", GUILayout.Height(30)))
            {
                AutoDefines.ForceUpdateDefines();
                CheckAndDisableDotweenIfNotInstalled();
            }

            if (GUILayout.Button("Clear All SDK Defines", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("Clear All SDK Defines",
                    "This will disable all SDKs and test compilation without external dependencies. Continue?",
                    "Yes", "Cancel"))
                {
                    AutoDefines.ClearAllSDKDefines();
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Test Compilation", GUILayout.Height(30)))
            {
                TestCompilation();
            }

            if (GUILayout.Button("Refresh Project", GUILayout.Height(30)))
            {
                AssetDatabase.Refresh();
                CheckAndDisableDotweenIfNotInstalled();
            }

            EditorGUILayout.EndHorizontal();
        }

        private static void CheckAndDisableDotweenIfNotInstalled()
        {
            bool dotweenInstalled = AutoDefines.IsDotweenInstalled();
            bool hasDotween = SDKDefinesHelper.HasDefine("MT_DOTWEEN");
            
            if (!dotweenInstalled && hasDotween)
            {
                SDKDefinesHelper.SetDefine("MT_DOTWEEN", false);
                Debug.Log("[SettingsTabDrawer] DOTween is not installed. Disabled MT_DOTWEEN define.");
            }
        }

        private static void TestCompilation()
        {
            EditorUtility.DisplayDialog("Test Compilation",
                "Compilation test initiated. Check the Console for any errors.", "OK");

            // Force a recompilation by refreshing assets
            AssetDatabase.Refresh();

            // Unity will automatically recompile scripts when assets are refreshed
            Debug.Log("[SettingsTabDrawer] Compilation test completed - check Console for errors");
        }
    }
}
#endif

