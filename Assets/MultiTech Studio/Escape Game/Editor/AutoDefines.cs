#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace MultiTechStudio.EscapeGame
{
    /// <summary>
    /// Automatically manages compile-time defines based on detected SDKs
    /// This ensures the project compiles safely with or without external SDKs
    /// </summary>
    public static class AutoDefines
    {
        // Define constants for each SDK
        private const string AdMobDefine = "MT_ADMOB";
        private const string FirebaseDefine = "MT_FIREBASE";
        private const string IapDefine = "MT_IAP";
        private const string DotweenDefine = "MT_DOTWEEN";

        /// <summary>
        /// Initialize on Unity load - automatically update defines
        /// </summary>
        [InitializeOnLoadMethod]
        static void UpdateDefines()
        {
            var buildTarget = EditorUserBuildSettings.selectedBuildTargetGroup;
            var currentDefines = PlayerSettings.GetScriptingDefineSymbolsForGroup(buildTarget);
            var defineList = new List<string>(currentDefines.Split(';'));

            // Check for AdMob SDK
            bool hasAdMob = HasType("GoogleMobileAds.Api.MobileAds");
            ToggleDefine(defineList, AdMobDefine, hasAdMob);

            // Check for Firebase SDK
            bool hasFirebase = HasType("Firebase.FirebaseApp");
            ToggleDefine(defineList, FirebaseDefine, hasFirebase);

            // Check for IAP SDK
            bool hasIap = HasType("UnityEngine.Purchasing.UnityPurchasing");
            ToggleDefine(defineList, IapDefine, hasIap);

            // Check for DOTween SDK (only remove if not installed, don't auto-add)
            if (!HasType("DG.Tweening.DOTween"))
            {
                defineList.RemoveAll(d => d == DotweenDefine);
            }

            // Update defines if changed
            string newDefines = string.Join(";", defineList);
            if (newDefines != currentDefines)
            {
                PlayerSettings.SetScriptingDefineSymbolsForGroup(buildTarget, newDefines);
                Debug.Log($"[AutoDefines] Updated defines: {newDefines}");
            }
        }

        /// <summary>
        /// Toggle a specific define based on SDK availability
        /// </summary>
        static void ToggleDefine(List<string> defineList, string define, bool shouldHave)
        {
            bool hasDefine = defineList.Contains(define);

            if (shouldHave && !hasDefine)
            {
                defineList.Add(define);
                Debug.Log($"[AutoDefines] Added define: {define}");
            }
            else if (!shouldHave && hasDefine)
            {
                defineList.Remove(define);
                Debug.Log($"[AutoDefines] Removed define: {define}");
            }
        }

        /// <summary>
        /// Check if a specific type exists in loaded assemblies
        /// </summary>
        static bool HasType(string typeName)
        {
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (assembly.GetType(typeName) != null)
                    {
                        return true;
                    }
                }
                catch (System.Exception)
                {
                    // Ignore assembly loading errors
                }
            }
            return false;
        }

        /// <summary>
        /// Manual method to force update defines
        /// </summary>
        public static void ForceUpdateDefines()
        {
            UpdateDefines();
            Debug.Log("[AutoDefines] Manually updated defines");
        }

        /// <summary>
        /// Clear all SDK defines (for testing compilation without SDKs)
        /// </summary>
        public static void ClearAllSDKDefines()
        {
            var buildTarget = EditorUserBuildSettings.selectedBuildTargetGroup;
            var currentDefines = PlayerSettings.GetScriptingDefineSymbolsForGroup(buildTarget);
            var defineList = new List<string>(currentDefines.Split(';'));

            // Remove all SDK defines
            defineList.RemoveAll(define =>
                define == AdMobDefine ||
                define == FirebaseDefine ||
                define == IapDefine ||
                define == DotweenDefine
            );

            string newDefines = string.Join(";", defineList);
            PlayerSettings.SetScriptingDefineSymbolsForGroup(buildTarget, newDefines);
            Debug.Log($"[AutoDefines] Cleared all SDK defines: {newDefines}");
        }

        /// <summary>
        /// Check if DOTween is installed
        /// </summary>
        public static bool IsDotweenInstalled()
        {
            return HasType("DG.Tweening.DOTween");
        }
    }
}
#endif

