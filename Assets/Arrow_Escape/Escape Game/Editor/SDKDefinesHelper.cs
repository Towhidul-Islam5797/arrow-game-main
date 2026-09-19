#if UNITY_EDITOR
using UnityEditor;

namespace MultiTechStudio.EscapeGame
{
    public static class SDKDefinesHelper
    {
        public static bool HasDefine(string define)
        {
            var buildTarget = EditorUserBuildSettings.selectedBuildTargetGroup;
            var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(buildTarget);
            return defines.Contains(define);
        }

        public static void SetDefine(string define, bool enabled)
        {
            var buildTarget = EditorUserBuildSettings.selectedBuildTargetGroup;
            var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(buildTarget);
            var defineList = new System.Collections.Generic.List<string>(defines.Split(';'));

            if (enabled && !defineList.Contains(define))
            {
                defineList.Add(define);
            }
            else if (!enabled && defineList.Contains(define))
            {
                defineList.Remove(define);
            }

            PlayerSettings.SetScriptingDefineSymbolsForGroup(buildTarget, string.Join(";", defineList));
            UnityEngine.Debug.Log($"[SDKDefinesHelper] Set {define} to {enabled}");
        }
    }
}
#endif

